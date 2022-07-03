#pragma once

#include "math/geometry.h"
#include <vector>
#include "BVHAccel.h"

namespace BVHLib
{

	struct BVHBuildNode
	{
		// BVHBuildNode Public Methods
		//调用该方法表面BVHBuildNode就是一个叶子节点
		void InitLeaf(int first, int n, const Bounds3f& b)
		{
			firstPrimOffset = first;
			nPrimitives = n;
			bounds = b;
			children[0] = children[1] = nullptr;
			//++leafNodes;
			//++totalLeafNodes;
			//totalPrimitives += n;
		}
		void InitInterior(int axis, BVHBuildNode* c0, BVHBuildNode* c1)
		{
			children[0] = c0;
			children[1] = c1;
			bounds = Union(c0->bounds, c1->bounds);
			splitAxis = axis;
			nPrimitives = 0;
			//++interiorNodes;
		}
		Bounds3f bounds;
		BVHBuildNode* children[2];
		int splitAxis;
		//在BVHAccel::primivites中的索引
		int firstPrimOffset;
		//leaf中挂了多少个primitive
		int nPrimitives;
	};

	struct LinearBVHNode {
		Bounds3f bounds;
		union {
			//在primtives数组中的索引
			int primitivesOffset;    // leaf

			int secondChildOffset;   // interior
		};
		//如果是叶子节点，拥有的primitves的数量
		//因为在build tree node时，primitive已经根据放置的node做了排序
		//所以primitve是和node紧凑的，在同一个树叶下primitve的顺序是连续放在primitives数组下的
		uint16_t nPrimitives;  // 0 -> interior node
		uint8_t axis;          // interior node: xyz
		uint8_t pad[1];        // ensure 32 byte total size
	};

	struct BucketInfo
	{
		//拥有的primitive的数量
		int count = 0;
		//bucket的bounds
		Bounds3f bounds;
	};

	struct Reference
	{
		int triIdx = -1;
		Bounds3f bounds;
	};

	struct NodeSpec
	{
		int startIdx = 0;   //primitive在总的primitve中的位置
		int numRef = 0;     //该节点包含的所有子节点的三角形数量
		Bounds3f bounds;  //节点的AABB盒子
		Bounds3f centroidBounds;
	};

	struct SahSplit
	{
		int   dim = 0;     //按哪个轴
		float pos = 0;   //划分的位置
		float sah = MaxFloat;     //消耗的sah
		float overlap = 0; //overlap的比例，spatial是0
	};
	struct SpatialBin
	{
		Bounds3f bounds;
		int enter = 0;
		int exit = 0;
	};

	class BVHBuilder
	{
	public:
		float m_minOverlap = 0;   //划分空间的最小面积，意思是大于该面积，空间才可继续划分
		float m_splitAlpha = 1.0e-5f;   //what's this mean?
		float m_traversalCost = 0.125f;
		int m_numDuplicates = 0;   //重复在多个节点上的三角形数量
		std::vector<Reference> m_refStack;
		//GPUBounds[] m_rightBounds = null;
		int m_sortDim;
		const int MaxDepth = 64;
		int MaxSpatialDepth = 48;
	    static const int NumSpatialBins = 64;
	private:
		int innerNodes = 0;
		int leafNodes = 0;

		SpatialBin m_bins[3 * NumSpatialBins];
		BucketInfo buckets[NumSpatialBins];
		Bounds3f rightBounds[NumSpatialBins - 1];

	public:
		BVHBuildNode* Build(Bounds3f* bounds, int boundsNum, GPUVertex* vertices, int verticesNum, int _maxPrimsInNode = 1);
	};
}
