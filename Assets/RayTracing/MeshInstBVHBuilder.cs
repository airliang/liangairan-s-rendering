using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
public class MeshInstBVHBuilder
{
	int maxPrimsInNode = 1;
	protected int totalNodes = 0;
	List<Transform> primitives;
	public BVHBuildNode Build(List<Transform> prims, List<Primitive> orderedPrims, List<GPUVertex> vertices, List<int> triangles)
	{
		totalNodes = 0;
		primitives = prims;
		
		List<BVHPrimitiveInfo> primitiveInfos = new List<BVHPrimitiveInfo>();
		for (int i = 0; i < prims.Count; ++i)
		{
			MeshRenderer meshRenderer = prims[i].GetComponent<MeshRenderer>();
			primitiveInfos.Add(new BVHPrimitiveInfo(i, GPUBounds.ConvertUnityBounds(meshRenderer.bounds)));
		}
		return RecursiveBuild(primitiveInfos, 0, prims.Count, orderedPrims);

	}

	BVHBuildNode RecursiveBuild(List<BVHPrimitiveInfo> primitiveInfo,
		int start, int end,
		List<Primitive> orderedPrims)
	{
		Debug.Log("RecursiveBuild start = " + start + " end = " + end);
		if (start == end)
			return null;
		BVHBuildNode node = new BVHBuildNode();
		totalNodes++;

		GPUBounds bounds = GPUBounds.DefaultBounds();

		Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		//bounds.SetMinMax(min, max);
		for (int i = start; i < end; ++i)
		{
			//bounds.SetMinMax(Vector3.Min(bounds.min, primitiveInfo[i].worldBound.min), 
			//	Vector3.Max(bounds.max, primitiveInfo[i].worldBound.max));
			bounds = GPUBounds.Union(bounds, primitiveInfo[i].worldBound);
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
		GPUBounds centroidBounds = GPUBounds.DefaultBounds();

		for (int i = start; i < end; ++i)
		{
			centroidBounds = GPUBounds.Union(centroidBounds, primitiveInfo[i].worldBound.centroid); //SetMinMax(Vector3.Min(centroidBounds.min, primitiveInfo[i].worldBound.center),
																									//Vector3.Max(centroidBounds.max, primitiveInfo[i].worldBound.center)); //Union(centroidBounds, primitiveInfo[i].centroid);
		}
		int dim = centroidBounds.MaximunExtent();

		//假如centroidBounds是一个点
		//即上面的primitiveInfo的中心点在同一个位置
		int mid = (start + end) / 2;
		if (Mathf.Abs(centroidBounds.max[dim] - centroidBounds.min[dim]) < 0.01f)
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
					mid, end - 1,
					(a, b) => (a.worldBound.centroid[dim] < b.worldBound.centroid[dim]));
			}
			else
			{
				int nBuckets = 12;
				BucketInfo[] buckets = new BucketInfo[nBuckets];
				for (int i = 0; i < nBuckets; ++i)
				{
					buckets[i] = new BucketInfo();
				}

				// Initialize _BucketInfo_ for SAH partition buckets
				for (int i = start; i < end; ++i)
				{
					//计算当前的Primitive属于哪个bucket
					int b = (int)(nBuckets *
						centroidBounds.Offset(primitiveInfo[i].worldBound.centroid)[dim]);
					if (b == nBuckets)
						b = nBuckets - 1;
					//CHECK_GE(b, 0);
					//CHECK_LT(b, nBuckets);
					buckets[b].count++;
					//计算bucket的bounds
					buckets[b].bounds =
						GPUBounds.Union(buckets[b].bounds, primitiveInfo[i].worldBound);
				}

				//分组，计算每组的cost
				//cost(A,B) = t_trav + pA∑t_isect(ai) + pB∑t_isect(ai)
				//t_trav = 0.125; t_isect = 1
				float[] cost = new float[nBuckets - 1];
				for (int i = 0; i < nBuckets - 1; ++i)
				{
					GPUBounds bA = GPUBounds.DefaultBounds();
					//bA.SetMinMax(min, max);
					GPUBounds bB = GPUBounds.DefaultBounds();
					//bB.SetMinMax(min, max);
					int count0 = 0, count1 = 0;
					for (int j = 0; j <= i; ++j)
					{
						bA = GPUBounds.Union(bA, buckets[j].bounds);
						count0 += buckets[j].count;
					}
					for (int j = i + 1; j < nBuckets; ++j)
					{
						bB = GPUBounds.Union(bB, buckets[j].bounds);
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

					mid = std.partition<BVHPrimitiveInfo>(ref primitiveInfo, start, end,
						(pi) =>
						{
							int bNum = (int)(nBuckets * centroidBounds.Offset(pi.worldBound.centroid)[dim]);
							if (bNum == nBuckets) bNum = nBuckets - 1;
							return bNum <= minCostSplitBucket;
						});

					if (start == mid)
					{
						Debug.Log("error generate!");
					}
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

			node.InitInterior(dim, RecursiveBuild(primitiveInfo, start, mid, orderedPrims),
				RecursiveBuild(primitiveInfo, mid, end, orderedPrims));
		}
		return node;
	}
}
*/