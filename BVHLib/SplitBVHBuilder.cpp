#include "SplitBVHBuilder.h"
#include <cassert>

namespace BVHLib
{
	BVHBuildNode* SplitBVHBuilder::Build(Bounds3f* bounds, int boundsNum, int _maxPrimsInNode)
	{
		totalNodes = 0;
		NodeSpec root; //new NodeSpec();
		m_refStack.clear();
		_orderedPrimitives.clear();
		maxPrimsInNode = _maxPrimsInNode;
		for (int i = 0; i < boundsNum; ++i)
		{
			//primitiveInfos.Add(new BVHPrimitiveInfo(i, prims[i].worldBound));
			Reference reference = Reference
			{
				i,  //reference's triIdx is the index in _primitives
				bounds[i]
			};
			m_refStack.emplace_back(reference);
		}
		for (int i = 0; i < boundsNum; ++i)
		{
			//root.bounds = GPUBounds.Union(root.bounds, primitiveInfos[i].worldBound);
			//root.centroidBounds = GPUBounds.Union(root.centroidBounds, primitiveInfos[i].worldBound.centroid);
			root.bounds.Union(m_refStack[i].bounds);
			root.centroidBounds.Union(m_refStack[i].bounds.Centroid());
		}
		root.numRef = boundsNum;

		m_num_nodes_for_regular = (2 * boundsNum - 1);
		m_num_nodes_required = (int)(m_num_nodes_for_regular * (1.f + m_extra_refs_budget));

		InitNodeAllocator(m_num_nodes_required);

		// Remove degenerates.
		//把无效的boundingbox去掉，例如线和带负数的
		int firstRef = m_refStack.size() - root.numRef;
		for (int i = m_refStack.size() - 1; i >= firstRef; i--)
		{
			if (i >= m_refStack.size() || i < 0)
			{
				//Debug.LogError("Remove degenerates error!");
			}
			Vector3f size = m_refStack[i].bounds.Diagonal();
			//removes the negetive size and the line bounding
			if (m_refStack[i].bounds.MinSize() < 0.0f || (size.x + size.y + size.z) == m_refStack[i].bounds.MaxSize())
			{
				m_refStack[i] = m_refStack[m_refStack.size() - 1];
				m_refStack.erase(m_refStack.end() - 1);
			}
		}
		root.numRef = m_refStack.size() - firstRef;


		//m_rightBounds = new GPUBounds[Mathf.Max(root.numRef, NumSpatialBins) - 1];
		//m_minOverlap = root.bounds.SurfaceArea() * m_splitAlpha;
		innerNodes = 0;
		leafNodes = 0;
		totalNodes = 0;

		m_root = RecursiveBuild(root, 0, 0, 1.0f);

		return m_root;
	}

	BVHBuildNode* SplitBVHBuilder::RecursiveBuild(NodeSpec& spec, int level, float progressStart, float progressEnd)
	{
		totalNodes++;

		if (spec.numRef <= maxPrimsInNode || level >= MaxDepth)
			return CreateLeaf(spec);

		//find the split candidate
		//判断split space和split object的依据是？
		float area = spec.bounds.SurfaceArea();
		//float leafSAH = GetTriangleCost(spec.numRef);
		//这里是因为2个子节点？
		//float nodeSAH = area * GetNodeCost(2);

		// Choose the maximum extent
		int axis = spec.centroidBounds.MaximumExtent();
		float border = spec.centroidBounds.Centroid()[axis];

		SplitType split_type = SplitType::kObject;
		//Profiler.BeginSample("BVH Find Object Splits");
		SahSplit objectSplit = FindObjectSplit(spec);
		//Profiler.EndSample();

		SahSplit spatialSplit;
		if (level < MaxSpatialDepth && objectSplit.overlap >= m_minOverlap)
		{
			//由于object划分会产生overlap的区域，当overlap的区域＞minOverlap的时候，需要划分spatial split
			//Profiler.BeginSample("BVH FindSpatialSplit");
			spatialSplit = FindSpatialSplit(spec);
			//Profiler.EndSample();
		}


		//BVHBuildNode* node = AllocateNode();
		//float minSAH = std::min(objectSplit.sah, spatialSplit.sah);


		if (objectSplit.sah < spatialSplit.sah)
		{
			axis = objectSplit.dim;
		}
		else
		{
			split_type = SplitType::kSpatial;
			axis = spatialSplit.dim;
		}

		if (split_type == SplitType::kSpatial)
		{
			int elems = spec.startIdx + spec.numRef * 2;
			if (m_refStack.size() < elems)
			{
				//primrefs.resize(elems);
				std::vector<Reference> extras;
				int refCount = m_refStack.size();
				for (int i = 0; i < elems - refCount; ++i)
					m_refStack.push_back(Reference());
					//extras.Add(Reference.DefaultReference());
				//m_refStack.AddRange(extras);
			}

			// Split prim refs and add extra refs to request
			int extra_refs = 0;
			//Profiler.BeginSample("SplitPrimRefs");
			SplitPrimRefs(spatialSplit, spec, m_refStack, extra_refs);
			//Profiler.EndSample();
			spec.numRef += extra_refs;
			border = spatialSplit.pos;
			axis = spatialSplit.dim;
		}
		else
		{
			border = !std::isnan(objectSplit.pos) ? objectSplit.pos : border;
			axis = !std::isnan(objectSplit.pos) ? objectSplit.dim : axis;
		}

		//分组，把原来ref队列进行分组
		// Start partitioning and updating extents for children at the same time
		Bounds3f leftbounds;
		Bounds3f rightbounds;
		Bounds3f leftcentroid_bounds;
		Bounds3f rightcentroid_bounds;
		int splitidx = spec.startIdx;

		bool near2far = ((spec.numRef + spec.startIdx) & 0x1) != 0;



		//Cmp cmp1 = compl;//near2far ? compl : compge;
		//if (!near2far)
		//	cmp1 = compge;
		//Cmp cmp2 = compge;
		//if (!near2far)
		//	cmp2 = compl;
		bool(*cmpl)(float, float) = [](float a, float b) -> bool { return a < b; };
		bool(*cmpge)(float, float) = [](float a, float b) -> bool { return a >= b; };
		auto cmp1 = near2far ? cmpl : cmpge;
		auto cmp2 = near2far ? cmpge : cmpl;

		if (spec.centroidBounds.Diagonal()[axis] > 0.0f)
		{
			int first = spec.startIdx;
			int last = spec.startIdx + spec.numRef;

			while (true)
			{
				while ((first != last) && cmp1(m_refStack[first].bounds.Centroid()[axis], border))
				{
					//leftbounds = GPUBounds.Union(m_refStack[first].bounds, leftbounds);
					//leftcentroid_bounds = GPUBounds.Union(leftcentroid_bounds, m_refStack[first].bounds.centroid);
					leftbounds.Union(m_refStack[first].bounds);
					leftcentroid_bounds.Union(m_refStack[first].bounds.Centroid());
					++first;
				}

				if (first == last--)
					break;

				//rightbounds = GPUBounds.Union(m_refStack[first].bounds, rightbounds);
				//rightcentroid_bounds = GPUBounds.Union(rightcentroid_bounds, m_refStack[first].bounds.centroid);
				rightbounds.Union(m_refStack[first].bounds);
				rightcentroid_bounds.Union(m_refStack[first].bounds.Centroid());

				while ((first != last) && cmp2(m_refStack[last].bounds.Centroid()[axis], border))
				{
					//rightbounds = GPUBounds.Union(m_refStack[last].bounds, rightbounds);
					//rightcentroid_bounds = GPUBounds.Union(rightcentroid_bounds, m_refStack[last].bounds.centroid);
					rightbounds.Union(m_refStack[last].bounds);
					rightcentroid_bounds.Union(m_refStack[last].bounds.Centroid());
					--last;
				}

				if (first == last)
					break;

				//leftbounds = GPUBounds.Union(m_refStack[last].bounds, leftbounds);
				//leftcentroid_bounds = GPUBounds.Union(leftcentroid_bounds, m_refStack[last].bounds.centroid);
				leftbounds.Union(m_refStack[last].bounds);
				leftcentroid_bounds.Union(m_refStack[last].bounds.Centroid());

				//std::swap(primrefs[first++], primrefs[last]);
				//SortSwap(m_refStack, first, last);
				std::swap(m_refStack[first++], m_refStack[last]);
				//first++;
			}


			splitidx = first;
		}


		if (splitidx == spec.startIdx || splitidx == spec.startIdx + spec.numRef)
		{
			splitidx = spec.startIdx + (spec.numRef >> 1);

			for (int i = spec.startIdx; i < splitidx; ++i)
			{
				//leftbounds = GPUBounds.Union(m_refStack[i].bounds, leftbounds);
				//leftcentroid_bounds = GPUBounds.Union(leftcentroid_bounds, m_refStack[i].bounds.centroid);
				leftbounds.Union(m_refStack[i].bounds);
				leftcentroid_bounds.Union(m_refStack[i].bounds.Centroid());
			}

			for (int i = splitidx; i < spec.startIdx + spec.numRef; ++i)
			{
				//rightbounds = GPUBounds.Union(m_refStack[i].bounds, rightbounds);
				//rightcentroid_bounds = GPUBounds.Union(rightcentroid_bounds, m_refStack[i].bounds.centroid);
				rightbounds.Union(m_refStack[i].bounds);
				rightcentroid_bounds.Union(m_refStack[i].bounds.Centroid());
			}
		}
		//分组结束

		NodeSpec left
		{
			spec.startIdx,
			splitidx - spec.startIdx,
			leftbounds,
			leftcentroid_bounds
		};
		NodeSpec right
		{
			splitidx,
			spec.numRef - (splitidx - spec.startIdx),
			rightbounds,
			rightcentroid_bounds
		};


		//if (minSAH == spatialSplit.sah)
		//    PerformSpatialSplit(left, right, spec, spatialSplit);
		//if (left.numRef == 0 || right.numRef == 0)
		//    PerformObjectSplit(left, right, spec, objectSplit);

		m_numDuplicates += left.numRef + right.numRef - spec.numRef;
		float progressMid = BVHLib::Lerp(progressStart, progressEnd, (float)right.numRef / (float)(left.numRef + right.numRef));
		BVHBuildNode* rightNode = RecursiveBuild(right, level + 1, progressStart, progressMid);
		BVHBuildNode* leftNode = RecursiveBuild(left, level + 1, progressMid, progressEnd);
		BVHBuildNode* innerNode = CreateInnerNode(spec.bounds, leftNode, rightNode);
		return innerNode;
	}

	bool SplitBVHBuilder::SplitPrimRef(const Reference& refPrim, int axis, float split, Reference& leftref, Reference& rightref)
	{
		// Start with left and right refs equal to original ref
		leftref.triIdx = rightref.triIdx = refPrim.triIdx;
		leftref.bounds = rightref.bounds = refPrim.bounds;

		// Only split if split value is within our bounds range
		if (split > refPrim.bounds.pMin[axis] && split < refPrim.bounds.pMax[axis])
		{
			// Trim left box on the right
			leftref.bounds.pMax[axis] = split;
			// Trim right box on the left
			rightref.bounds.pMin[axis] = split;
			return true;
		}

		return false;
	}

	void SplitBVHBuilder::SplitPrimRefs(const SahSplit& split, const NodeSpec& req, std::vector<Reference>& refs, int& extra_refs)
	{
		int appendprims = req.numRef;

		// Split refs if any of them require to be split
		for (int i = req.startIdx; i < req.startIdx + req.numRef; ++i)
		{
			assert(static_cast<size_t>(req.startIdx + appendprims) < refs.size());

			Reference leftref; 
			Reference rightref;
			if (SplitPrimRef(refs[i], split.dim, split.pos, leftref, rightref))
			{
				// Copy left ref instead of original
				refs[i] = leftref;
				// Append right one at the end
				refs[req.startIdx + appendprims++] = rightref;
			}
		}

		// Return number of primitives after this operation
		extra_refs = appendprims - req.numRef;
	}

	BVHBuildNode* SplitBVHBuilder::CreateLeaf(const NodeSpec& spec)
	{
		leafNodes++;
		//List<int> tris = m_bvh.getTriIndices();
		for (int i = spec.startIdx; i < spec.startIdx + spec.numRef; i++)
		{

			Reference primRef = m_refStack[i];

			//_orderedPrimitives.Add(_primitives[primRef.triIdx]);
			_orderedPrimitives.push_back(primRef.triIdx);
		}
		BVHBuildNode* leafNode = AllocateNode();
		leafNode->InitLeaf(_orderedPrimitives.size() - spec.numRef, spec.numRef, spec.bounds);
		return leafNode;
	}

	BVHBuildNode* SplitBVHBuilder::CreateInnerNode(const Bounds3f& bounds, BVHBuildNode* left, BVHBuildNode* right)
	{
		innerNodes++;
		BVHBuildNode* node = AllocateNode();
		node->bounds = bounds;
		node->childrenLeft = left;
		node->childrenRight = right;
		node->nPrimitives = 0;
		return node;
	}

	SahSplit SplitBVHBuilder::FindObjectSplit(const NodeSpec& spec)
	{
		SahSplit split;

		Vector3f origin = spec.bounds.pMin;
		Vector3f binSize = (spec.bounds.pMax - origin) * (1.0f / (float)NumSpatialBins);
		int splitidx = -1;

		int start = spec.startIdx;
		int end = start + spec.numRef;
		float sah = MaxFloat;//float.MaxValue;
		float thisNodeSurfaceArea = spec.bounds.SurfaceArea();
		float invSurfaceArea = 1.0f / thisNodeSurfaceArea;

		Vector3f centroid_extents = spec.centroidBounds.Diagonal();
		if (centroid_extents.LengthSquared() == 0.f)
		{
			return split;
		}

		//std::vector<BucketInfo> buckets[3];
		//buckets[0].resize(NumSpatialBins);
		//buckets[1].resize(NumSpatialBins);
		//buckets[2].resize(NumSpatialBins);

		for (int axis = 0; axis < 3; axis++)
		{
			float rootminc = spec.centroidBounds.pMin[axis];
			float centroid_rng = spec.centroidBounds.Diagonal()[axis];

			if (centroid_rng == 0.0f)
				continue;
			auto invcentroid_rng = 1.f / centroid_rng;

			int nBuckets = NumSpatialBins;

			//for (int i = 0; i < nBuckets; ++i)
			//{
			//	bins[axis][i].bounds = Bounds3f();
			//	bins[axis][i].count = 0;
			//}
			BucketInfo buckets[NumSpatialBins];

			// Initialize _BucketInfo_ for SAH partition buckets
			for (int i = start; i < end; ++i)
			{
				//计算当前的Primitive属于哪个bucket
				//int b = (int)(nBuckets *
				//	spec.centroidBounds.Offset(m_refStack[i].bounds.Centroid())[axis]);
				//if (b == nBuckets)
				//	b = nBuckets - 1;
				int b = (int)std::min<float>(static_cast<float>(NumSpatialBins) * ((m_refStack[i].bounds.Centroid()[axis] - rootminc) * invcentroid_rng), static_cast<float>(NumSpatialBins - 1));
				//if (binidx != b)
				//{
				//	int a = 0;
				//}
				//CHECK_GE(b, 0);
				//CHECK_LT(b, nBuckets);
				buckets[b].count++;
				//计算bucket的bounds
				//buckets[b].bounds =
				//    GPUBounds.Union(buckets[b].bounds, m_refStack[i].bounds);
				buckets[b].bounds.Union(m_refStack[i].bounds);
			}

			//用pbrt的方法没必要sort了
			//BVHSort.Sort<Reference>(start, end, m_refStack, ReferenceCompare, SortSwap);
			// Sweep right to left and determine bounds.
			Bounds3f rightBounds[NumSpatialBins - 1];
			Bounds3f rightBox;
			for (int i = nBuckets - 1; i > 0; i--)
			{
				//rightBox = GPUBounds.Union(buckets[i].bounds, rightBox);
				rightBox.Union(buckets[i].bounds);
				rightBounds[i - 1] = rightBox;
			}

			// Sweep left to right and select lowest SAH.

			Bounds3f leftBounds;
			int leftcount = 0;
			int rightcount = spec.numRef;

			//分组，计算每组的cost
			//cost(A,B) = t_trav + pA∑t_isect(ai) + pB∑t_isect(ai)
			//t_trav = 0.125; t_isect = 1
			//float[] cost = new float[nBuckets - 1];

			for (int i = 0; i < nBuckets - 1; ++i)
			{
				//leftBounds = GPUBounds.Union(buckets[i].bounds, leftBounds);
				leftBounds.Union(buckets[i].bounds);
				leftcount += buckets[i].count;
				rightcount -= buckets[i].count;
				float sahTemp = m_traversalCost +
					(GetTriangleCost(leftcount) * leftBounds.SurfaceArea() + GetTriangleCost(rightcount) * rightBounds[i].SurfaceArea()) * invSurfaceArea;

				if (sahTemp < sah)
				{
					sah = sahTemp;
					split.sah = sah;
					split.dim = axis;
					splitidx = i;
					//split.numLeft = i;
					//split.leftBounds = leftBounds;
					//split.rightBounds = m_rightBounds[i];
					split.overlap = Bounds3f::Intersect(leftBounds, rightBounds[i]).SurfaceArea() * invSurfaceArea;
				}
			}
		}

		if (splitidx != -1)
		{
			split.pos = spec.centroidBounds.pMin[split.dim] + (splitidx + 1) * (spec.centroidBounds.Diagonal()[split.dim] / NumSpatialBins);
		}

		//split.overlap = GPUBounds.Intersection(split.leftBounds, split.rightBounds).SurfaceArea() / spec.bounds.SurfaceArea();
		return split;
	}

	SahSplit SplitBVHBuilder::FindSpatialSplit(const NodeSpec& spec)
	{
		// Initialize bins.
		Vector3f origin = spec.bounds.pMin;
		Vector3f binSize = (spec.bounds.pMax - origin) * (1.0f / (float)NumSpatialBins);
		Vector3f invBinSize(1.0f / binSize.x, 1.0f / binSize.y, 1.0f / binSize.z);
		float invSurfaceArea = 1.0f / spec.bounds.SurfaceArea();


		for (int dim = 0; dim < 3; dim++)
		{
			for (int i = 0; i < NumSpatialBins; i++)
			{
				int index = dim * NumSpatialBins + i;
				m_bins[index] = SpatialBin(); //new SpatialBin();
			}
		}

		// Chop references into bins.

		for (int refIdx = spec.startIdx; refIdx < spec.startIdx + spec.numRef; refIdx++)
		{
			Reference reference = m_refStack[refIdx];
			Vector3f minMinusOrig = reference.bounds.pMin - origin;
			Vector3f maxMinusOrig = reference.bounds.pMax - origin;

			//Vector3Int firstBin = ClampVector3Int(Vector3Int.FloorToInt(new Vector3(minMinusOrig.x * invBinSize.x, minMinusOrig.y * invBinSize.y, minMinusOrig.z * invBinSize.z)),
			//    0, new Vector3Int(NumSpatialBins - 1, NumSpatialBins - 1, NumSpatialBins - 1));
			//Vector3Int lastBin = ClampVector3Int(Vector3Int.FloorToInt(new Vector3(maxMinusOrig.x * invBinSize.x, maxMinusOrig.y * invBinSize.y, maxMinusOrig.z * invBinSize.z)), 
			//    firstBin, new Vector3Int(NumSpatialBins - 1, NumSpatialBins - 1, NumSpatialBins - 1));
			Vector3f firstBin = Vector3f::Clamp(minMinusOrig * invBinSize, 0, NumSpatialBins - 1);
			Vector3f lastBin = Vector3f::Clamp(maxMinusOrig * invBinSize, firstBin, Vector3f(NumSpatialBins - 1, NumSpatialBins - 1, NumSpatialBins - 1));

			for (int dim = 0; dim < 3; dim++)
			{
				if (spec.bounds.Diagonal()[dim] == 0.0f)
					continue;
				Reference currRef = reference;
				for (int i = (int)firstBin[dim]; i < (int)lastBin[dim]; i++)
				{
					Reference leftRef;
					Reference rightRef;
					float splitPos = origin[dim] + binSize[dim] * (float)(i + 1);
					//SplitReference(leftRef, rightRef, currRef, dim, splitPos);
					//Profiler.BeginSample("SplitPrimRef");
					if (SplitPrimRef(currRef, dim, splitPos, leftRef, rightRef))
					{
						//m_bins[dim, i].bounds = GPUBounds.Union(m_bins[dim, i].bounds, leftRef.bounds);
						int index = dim * NumSpatialBins + i;
						m_bins[index].bounds.Union(leftRef.bounds);
						currRef = rightRef;
					}
					//Profiler.EndSample();
				}

				//m_bins[dim, (int)lastBin[dim]].bounds = GPUBounds.Union(m_bins[dim, (int)lastBin[dim]].bounds, currRef.bounds);
				m_bins[dim * NumSpatialBins + (int)lastBin[dim]].bounds.Union(currRef.bounds);
				m_bins[dim * NumSpatialBins + (int)firstBin[dim]].enter++;
				m_bins[dim * NumSpatialBins + (int)lastBin[dim]].exit++;
			}
		}

		// Select best split plane.
		SahSplit split;
		for (int dim = 0; dim < 3; dim++)
		{
			if (spec.bounds.Diagonal()[dim] == 0.0f)
				continue;
			// Sweep right to left and determine bounds.
			//GPUBounds[] rightBounds = new GPUBounds[NumSpatialBins - 1];
			Bounds3f rightBounds[NumSpatialBins - 1];
			Bounds3f rightBox;
			for (int i = NumSpatialBins - 1; i > 0; i--)
			{
				//rightBox = GPUBounds.Union(rightBox, m_bins[dim, i].bounds);
				int index = dim * NumSpatialBins + i;
				rightBox.Union(m_bins[index].bounds);
				rightBounds[i - 1] = rightBox;
			}

			// Sweep left to right and select lowest SAH.

			Bounds3f leftBox;
			int leftNum = 0;
			int rightNum = spec.numRef;

			for (int i = 1; i < NumSpatialBins; i++)
			{
				//leftBounds = GPUBounds.Union(leftBounds, m_bins[dim, i - 1].bounds);
				int index = dim * NumSpatialBins + i - 1;
				leftBox.Union(m_bins[index].bounds);
				leftNum += m_bins[index].enter;
				rightNum -= m_bins[index].exit;

				float sah = m_traversalCost + (leftBox.SurfaceArea() * GetTriangleCost(leftNum) + rightBounds[i - 1].SurfaceArea() * GetTriangleCost(rightNum)) * invSurfaceArea;
				if (sah < split.sah)
				{
					split.sah = sah;
					split.dim = dim;
					split.pos = origin[dim] + binSize[dim] * (float)i;
					split.overlap = 0;
				}
			}
		}
		return split;
	}

	BVHBuildNode* SplitBVHBuilder::AllocateNode()
	{
		if (m_nodecnt - m_num_nodes_archived >= m_num_nodes_for_regular)
		{
			m_node_archive.push_back(std::move(m_nodesPool));
			m_num_nodes_archived += m_num_nodes_for_regular;
			m_nodesPool = std::vector<BVHBuildNode>(m_num_nodes_for_regular);
		}

		return &m_nodesPool[m_nodecnt++ - m_num_nodes_archived];
	}

	void SplitBVHBuilder::InitNodeAllocator(size_t maxnum)
	{
		m_node_archive.clear();
		m_nodecnt = 0;
		m_nodesPool.resize(maxnum);

		// Set root_ pointer
		m_root = &m_nodesPool[0];
	}
}

