#include "BVHBuilder.h"


namespace BVHLib
{
	BVHBuildNode* BVHBuilder::Build(Bounds3f* bounds, int boundsNum, int _maxPrimsInNode)
	{
		totalNodes = 0;

		maxPrimsInNode = _maxPrimsInNode;

		for (int i = 0; i < boundsNum; ++i)
			m_primitiveInfos.emplace_back(BVHPrimitiveInfo{ i, bounds[i] });

		InitNodeAllocator(2 * boundsNum - 1);

		m_root = RecursiveBuild(0, boundsNum);

		return m_root;
	}

	const int* BVHBuilder::GetSortedIndices(int& numSortIndices) const
	{
		if (!_orderedPrimitives.empty())
		{
			numSortIndices = _orderedPrimitives.size();
			return &_orderedPrimitives[0];
		}
		return nullptr;
	}

	void  BVHBuilder::InitNodeAllocator(size_t maxnum)
	{
		m_nodecnt = 0;
		m_nodesPool.resize(maxnum);
	}

	BVHBuildNode* BVHBuilder::AllocateNode()
	{
		return &m_nodesPool[m_nodecnt++];
	}

	BVHBuildNode* BVHBuilder::RecursiveBuild(int start, int end)
	{
		if (start == end)
			return nullptr;
		BVHBuildNode* node = AllocateNode();
		totalNodes++;

		Bounds3f bounds;

		//Vector3f min = Vector3f(float.MaxValue, float.MaxValue, float.MaxValue);
		//Vector3f max = Vector3f(float.MinValue, float.MinValue, float.MinValue);
		//bounds.SetMinMax(min, max);
		for (int i = start; i < end; ++i)
		{
			//bounds.SetMinMax(Vector3.Min(bounds.min, primitiveInfo[i].worldBound.min), 
			//	Vector3.Max(bounds.max, primitiveInfo[i].worldBound.max));
			bounds.Union(m_primitiveInfos[i].worldBound);
		}
		node->bounds = bounds;

		//判断数组长度
		int nPrimitives = end - start;
		if (nPrimitives == 1)
		{
			//数组是1的时候不能再往下划分，创建leaf
			int firstPrimOffset = _orderedPrimitives.size();
			int primIndex = m_primitiveInfos[start].primitiveIndex;
			//orderedPrims.Add(primitives[primIndex]);
			_orderedPrimitives.push_back(primIndex);
			node->InitLeaf(firstPrimOffset, nPrimitives, bounds);
			return node;
		}

		//开始划分子节点
		//首先计算出primitive的中心点构成的Bounds
		Bounds3f centroidBounds;

		for (int i = start; i < end; ++i)
		{
			centroidBounds.Union(m_primitiveInfos[i].worldBound.Centroid()); //SetMinMax(Vector3.Min(centroidBounds.min, primitiveInfo[i].worldBound.center),
																									//Vector3.Max(centroidBounds.max, primitiveInfo[i].worldBound.center)); //Union(centroidBounds, primitiveInfo[i].centroid);
		}
		int dim = centroidBounds.MaximumExtent();

		//假如centroidBounds是一个点
		//即上面的primitiveInfo的中心点在同一个位置
		int mid = (start + end) / 2;
		if (std::abs(centroidBounds.pMax[dim] - centroidBounds.pMin[dim]) < 0.01f)
		{
			//build the leaf BVHBuildNode
			int firstPrimOffset = _orderedPrimitives.size();
			for (int i = start; i < end; ++i)
			{
				int primNum = m_primitiveInfos[i].primitiveIndex;
				//orderedPrims.Add(primitives[primNum]);
				_orderedPrimitives.push_back(primNum);
			}
			node->InitLeaf(firstPrimOffset, nPrimitives, bounds);
			return node;
		}
		else
		{
			if (nPrimitives <= 2)
			{
				// Partition primitives into equally-sized subsets
				mid = (start + end) / 2;
				std::nth_element(&m_primitiveInfos[start],
					&m_primitiveInfos[mid], &m_primitiveInfos[end - 1] + 1,
					[dim](const BVHPrimitiveInfo& a,
						const BVHPrimitiveInfo& b) {
							return a.worldBound.Centroid()[dim] <
								b.worldBound.Centroid()[dim];
					});
			}
			else
			{
				const int nBuckets = 12;
				BucketInfo buckets[nBuckets];

				// Initialize _BucketInfo_ for SAH partition buckets
				for (int i = start; i < end; ++i)
				{
					//计算当前的Primitive属于哪个bucket
					int b = (int)(nBuckets *
						centroidBounds.Offset(m_primitiveInfos[i].worldBound.Centroid())[dim]);
					if (b == nBuckets)
						b = nBuckets - 1;
					//CHECK_GE(b, 0);
					//CHECK_LT(b, nBuckets);
					buckets[b].count++;
					//计算bucket的bounds
					//buckets[b].bounds =
					//	GPUBounds.Union(buckets[b].bounds, primitiveInfo[i].worldBound);
					buckets[b].bounds.Union(m_primitiveInfos[i].worldBound);
				}

				//分组，计算每组的cost
				//cost(A,B) = t_trav + pA∑t_isect(ai) + pB∑t_isect(ai)
				//t_trav = 0.125; t_isect = 1
				float cost[nBuckets - 1];
				for (int i = 0; i < nBuckets - 1; ++i)
				{
					Bounds3f bA;
					//bA.SetMinMax(min, max);
					Bounds3f bB;
					//bB.SetMinMax(min, max);
					int count0 = 0, count1 = 0;
					for (int j = 0; j <= i; ++j)
					{
						bA.Union(buckets[j].bounds);
						count0 += buckets[j].count;
					}
					for (int j = i + 1; j < nBuckets; ++j)
					{
						bB.Union(buckets[j].bounds);
						count1 += buckets[j].count;
					}
					//t_trav = 0.125f
					cost[i] = 0.125f +
						(count0 * bA.SurfaceArea() +
							count1 * bB.SurfaceArea()) /
						bounds.SurfaceArea();
				}

				// Find bucket to split at that minimizes SAH metric
				float minCost = cost[0];
				int minCostSplitBucket = 0;
				for (int i = 1; i < nBuckets - 1; ++i)
				{
					if (cost[i] < minCost)
					{
						minCost = cost[i];
						minCostSplitBucket = i;
					}
				}

				//生成叶子节点或子树
				float leafCost = nPrimitives;
				if (nPrimitives > maxPrimsInNode || minCost < leafCost)
				{

					BVHPrimitiveInfo* pmid = std::partition(&m_primitiveInfos[start], &m_primitiveInfos[end - 1] + 1,
						[=](const BVHPrimitiveInfo& pi)
					{
						int bNum = (int)(nBuckets * centroidBounds.Offset(pi.worldBound.Centroid())[dim]);
						if (bNum == nBuckets) bNum = nBuckets - 1;
						return bNum <= minCostSplitBucket;
					});

					if (start == mid)
					{
						//Debug.Log("error generate!");
					}
				}
				else
				{
					int firstPrimOffset = _orderedPrimitives.size();
					for (int i = start; i < end; ++i)
					{
						int primNum = m_primitiveInfos[i].primitiveIndex;
						//orderedPrims.Add(primitives[primNum]);
						_orderedPrimitives.push_back(primNum);
					}
					node->InitLeaf(firstPrimOffset, nPrimitives, bounds);
					return node;
				}
			}

			node->InitInterior(dim, RecursiveBuild(start, mid),
				RecursiveBuild(mid, end));
		}
		return node;
	}
}
