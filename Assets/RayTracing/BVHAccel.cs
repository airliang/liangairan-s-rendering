using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct BVHNode
{
    public bool isLeaf;
    public int index;
    public int primitiveStart;   //primitive offset in primtive array
    public int primitiveNum;     //primitive's count
    public Bounds bounds;
}

struct BVHPrimitiveInfo
{
	public BVHPrimitiveInfo(int priIndex, Bounds bounds)	
	{
		primitiveIndex = priIndex;
		worldBound = bounds;
	}
	//primitive's index in primitive array
	public int primitiveIndex;
	
	public Bounds worldBound;
	//worldbound's central point
	//Vector3 centroid;
};
public class BVHBuildNode
{
	// BVHBuildNode Public Methods
	//
	public void InitLeaf(int first, int n, Bounds b) 
	{
		firstPrimOffset = first;
		nPrimitives = n;
		bounds = b;
		//children[0] = children[1] = nullptr;
		//++leafNodes;
		//++totalLeafNodes;
		//totalPrimitives += n;
	}
	public void InitInterior(int axis, BVHBuildNode c0, BVHBuildNode c1)
	{
		childrenLeft = c0;
		childrenRight = c1;
		bounds.SetMinMax(Vector3.Min(c0.bounds.min, c1.bounds.min), Vector3.Max(c0.bounds.max, c1.bounds.max));
		splitAxis = axis;
		nPrimitives = 0;
		//++interiorNodes;
	}
	public Bounds bounds;
	public BVHBuildNode childrenLeft;
	public BVHBuildNode childrenRight;
	public int splitAxis;
	
	public int firstPrimOffset;
	//the number of primtives in leaf. 0 is a interior node
	public int nPrimitives;
};

struct BucketInfo
{
	//拥有的primitive的数量
	public int count;
	//bucket的bounds
	public Bounds bounds;
};

//use as an array can transfer to the compute shader
public struct LinearBVHNode
{
	public Bounds bounds;  //64bytes
	
	public int primitivesOffset;    // leaf
	public int secondChildOffset;   // interior

	public ushort nPrimitives;  // 0 -> interior node
	public ushort axis;          // interior node: xyz
	//public int vertexOffset;     //vertexbuffer offset
	//public int faceOffset;       //indexbuffer offset
    //ushort pad;        // ensure 32 byte total size
};

public class BVHAccel
{
	int maxPrimsInNode;
	int totalNodes = 0;
	BVHBuildNode root;
	List<Primitive> primitives;
	public LinearBVHNode[] linearNodes;
	public void Build(List<Primitive> prims, int maxPrims, ref List<Primitive> orderedPrims)
    {
		primitives = prims;
		maxPrimsInNode = maxPrims;
		List<BVHPrimitiveInfo> primitiveInfos = new List<BVHPrimitiveInfo>();
		for (int i = 0; i < primitives.Count; ++i)
			primitiveInfos.Add(new BVHPrimitiveInfo(i, primitives[i].worldBound));
		//List<Primitive> orderedPrims = new List<Primitive>();
		root = RecursiveBuild(ref primitiveInfos, 0, prims.Count, ref orderedPrims);
		primitives = orderedPrims;

		int offset = 0;
		linearNodes = new LinearBVHNode[totalNodes];
		FlattenBVHTree(root, ref offset);
	}

	public void Clear()
    {
		root = null;
		primitives.Clear();
		linearNodes = null;
		totalNodes = 0;
	}
	int MaximunExtent(Vector3 extent)
	{
		if (extent.x > extent.y && extent.x > extent.z)
			return 0;
		else if (extent.y > extent.z)
			return 1;
		else
			return 2;
	}

	Bounds Union(Bounds a, Bounds b)
    {
		Bounds bounds = a;
		bounds.SetMinMax(Vector3.Min(bounds.min, b.min),
				Vector3.Max(bounds.max, b.max));

		return bounds;
    }

	float SurfaceArea(Bounds bounds)
    {
		return 2 * (bounds.size.x * bounds.size.y + bounds.size.x * bounds.size.z + bounds.size.y * bounds.size.z);

	}

	Vector3 Offset(Bounds b, Vector3 p)
	{
		Vector3 o = p - b.min;
		if (b.max.x > b.min.x) o.x /= b.max.x - b.min.x;
		if (b.max.y > b.min.y) o.y /= b.max.y - b.min.y;
		if (b.max.z > b.min.z) o.z /= b.max.z - b.min.z;
		return o;
	}
	BVHBuildNode RecursiveBuild(ref List<BVHPrimitiveInfo> primitiveInfo,
		int start, int end,
		ref List<Primitive> orderedPrims)
    {
		BVHBuildNode node = new BVHBuildNode();
		totalNodes++;

		Bounds bounds = new Bounds();
		
		Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		bounds.SetMinMax(min, max);
		for (int i = start; i < end; ++i)
		{
			bounds.SetMinMax(Vector3.Min(bounds.min, primitiveInfo[i].worldBound.min), 
				Vector3.Max(bounds.max, primitiveInfo[i].worldBound.max));
		}
		node.bounds = bounds;

		//判断数组长度
		int nPrimitives = end - start;
		if (nPrimitives == 1)
		{
			//数组是1的时候不能再往下划分，创建leaf
			int firstPrimOffset = orderedPrims.Count;
			int primIndex = primitiveInfo[start].primitiveIndex;
			orderedPrims.Add(primitives[primIndex]);
			node.InitLeaf(firstPrimOffset, nPrimitives, bounds);
			return node;
		}

		//开始划分子节点
		//首先计算出primitive的中心点构成的Bounds
		Bounds centroidBounds = new Bounds();
		centroidBounds.min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		centroidBounds.max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		for (int i = start; i < end; ++i)
		{
			centroidBounds.SetMinMax(Vector3.Min(centroidBounds.min, primitiveInfo[i].worldBound.center),
				Vector3.Max(centroidBounds.max, primitiveInfo[i].worldBound.center)); //Union(centroidBounds, primitiveInfo[i].centroid);
		}
		int dim = MaximunExtent(centroidBounds.size);

		//假如centroidBounds是一个点
		//即上面的primitiveInfo的中心点在同一个位置
		int mid = (start + end) / 2;
		if (centroidBounds.max[dim] == centroidBounds.min[dim])
		{
			//build the leaf BVHBuildNode
			int firstPrimOffset = orderedPrims.Count;
			for (int i = start; i < end; ++i)
			{
				int primNum = primitiveInfo[i].primitiveIndex;
				orderedPrims.Add(primitives[primNum]);
			}
			node.InitLeaf(firstPrimOffset, nPrimitives, bounds);
			return node;
		}
		else
		{
			if (nPrimitives <= 2)
			{
				// Partition primitives into equally-sized subsets
				mid = (start + end) / 2;
				std.nth_element<BVHPrimitiveInfo>(ref primitiveInfo, start,
					mid, end,
					(a, b) => (a.worldBound.center[dim] < b.worldBound.center[dim]));
			}
			else
			{
				int nBuckets = 12;
				BucketInfo[] buckets = new BucketInfo[nBuckets];

				// Initialize _BucketInfo_ for SAH partition buckets
				for (int i = start; i < end; ++i)
				{
					//计算当前的Primitive属于哪个bucket
					int b = (int)(nBuckets *
						Offset(centroidBounds, primitiveInfo[i].worldBound.center)[dim]);
					if (b == nBuckets)
						b = nBuckets - 1;
					//CHECK_GE(b, 0);
					//CHECK_LT(b, nBuckets);
					buckets[b].count++;
					//计算bucket的bounds
					buckets[b].bounds =
						Union(buckets[b].bounds, primitiveInfo[i].worldBound);
				}

				//分组，计算每组的cost
				//cost(A,B) = t_trav + pA∑t_isect(ai) + pB∑t_isect(ai)
				//t_trav = 0.125; t_isect = 1
				float[] cost = new float[nBuckets - 1];
				for (int i = 0; i < nBuckets - 1; ++i)
				{
					Bounds bA = new Bounds();
					Bounds bB = new Bounds();
					int count0 = 0, count1 = 0;
					for (int j = 0; j <= i; ++j)
					{
						bA = Union(bA, buckets[j].bounds);
						count0 += buckets[j].count;
					}
					for (int j = i + 1; j < nBuckets; ++j)
					{
						bB = Union(bB, buckets[j].bounds);
						count1 += buckets[j].count;
					}
					//t_trav = 0.125f
					cost[i] = 0.125f +
						(count0 * SurfaceArea(bA) +
							count1 * SurfaceArea(bB)) /
						SurfaceArea(bounds);
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
					mid = std.partition<BVHPrimitiveInfo>(ref primitiveInfo, start, end, 
						(pi) =>
						{
							int bNum = (int)(nBuckets * Offset(centroidBounds, pi.worldBound.center)[dim]);
							if (bNum == nBuckets) bNum = nBuckets - 1;
							return bNum <= minCostSplitBucket;
						});
					/*
					BVHPrimitiveInfo* pmid = std::partition(&primitiveInfo[start],
						&primitiveInfo[end - 1] + 1,
						[=](const BVHPrimitiveInfo&pi) {
						int b = nBuckets * centroidBounds.Offset(pi.centroid)[dim];
						if (b == nBuckets) b = nBuckets - 1;
						return b <= minCostSplitBucket;
					});
					mid = pmid - &primitiveInfo[0];
					*/
				}
				else
				{
					int firstPrimOffset = orderedPrims.Count;
					for (int i = start; i < end; ++i)
					{
						int primNum = primitiveInfo[i].primitiveIndex;
						orderedPrims.Add(primitives[primNum]);
					}
					node.InitLeaf(firstPrimOffset, nPrimitives, bounds);
					return node;
				}
			}

			node.InitInterior(dim, RecursiveBuild(ref primitiveInfo, start, mid, ref orderedPrims),
				RecursiveBuild(ref primitiveInfo, mid, end, ref orderedPrims));
		}
		return node;
	}

	int FlattenBVHTree(BVHBuildNode node, ref int offset)
	{
		//LinearBVHNode linearNode = linearNodes[offset];
		int curOffset = offset;
		linearNodes[curOffset].bounds = node.bounds;
		int myOffset = offset++;
		if (node.nPrimitives > 0)
		{
			//是一个叶子节点
			linearNodes[curOffset].nPrimitives = (ushort)node.nPrimitives;
			linearNodes[curOffset].primitivesOffset = node.firstPrimOffset;
		}
		else
		{
			linearNodes[curOffset].axis = (ushort)node.splitAxis;
			linearNodes[curOffset].nPrimitives = 0;
			//这里返回了offset
			FlattenBVHTree(node.childrenLeft, ref offset);
			linearNodes[curOffset].secondChildOffset = FlattenBVHTree(node.childrenRight, ref offset);
		}

		return myOffset;
	}

	public void DrawDebug()
    {
		if (linearNodes != null)
        {
			for (int i = 0; i < linearNodes.Length; ++i)
			{
				Bounds bound = linearNodes[i].bounds;
				RenderDebug.DrawDebugBound(bound.min.x, bound.max.x, bound.min.y, bound.max.y, bound.min.z, bound.max.z);
			}
		}
		
    }
}
