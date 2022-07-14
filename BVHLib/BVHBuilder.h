#pragma once
#include "math/geometry.h"
#include <vector>
#include <atomic>

namespace BVHLib
{
	struct BVHBuildNode
	{
		// BVHBuildNode Public Methods
		//���ø÷�������BVHBuildNode����һ��Ҷ�ӽڵ�
		void InitLeaf(int first, int n, const Bounds3f& b)
		{
			firstPrimOffset = first;
			nPrimitives = n;
			bounds = b;
			childrenLeft = childrenRight = nullptr;
			//++leafNodes;
			//++totalLeafNodes;
			//totalPrimitives += n;
		}
		void InitInterior(int axis, BVHBuildNode* c0, BVHBuildNode* c1)
		{
			childrenLeft = c0;
			childrenRight = c1;
			bounds = Union(c0->bounds, c1->bounds);
			splitAxis = axis;
			nPrimitives = 0;
			//++interiorNodes;
		}
		Bounds3f bounds;
		BVHBuildNode* childrenLeft = nullptr;
		BVHBuildNode* childrenRight = nullptr;
		int splitAxis = 0;
		//��BVHAccel::primivites�е�����
		int firstPrimOffset = 0;
		//leaf�й��˶��ٸ�primitive
		int nPrimitives = 0;

		bool IsLeaf()
		{
			return nPrimitives > 0;
		}
	};

	struct BucketInfo
	{
		//ӵ�е�primitive������
		int count = 0;
		//bucket��bounds
		Bounds3f bounds;
	};

	struct BVHPrimitiveInfo
	{
		int primitiveIndex = 0;
		Bounds3f worldBound;
	};

	class BVHBuilder
	{
	public:
		virtual BVHBuildNode* Build(Bounds3f* bounds, int boundsNum, int _maxPrimsInNode = 1);

		const int* GetSortedIndices(int& numSortIndices) const;
		int GetTotalNodes() const
		{
			return totalNodes;
		}

		BVHBuildNode* GetRoot()
		{
			return m_root;
		}
	private:
		BVHBuildNode* RecursiveBuild(int start, int end);
		std::vector<BVHPrimitiveInfo> m_primitiveInfos;
	protected:
		std::vector<int> _orderedPrimitives;
		BVHBuildNode* m_root = nullptr;
		int totalNodes = 0;
		int maxPrimsInNode = 4;
		std::atomic<int> m_nodecnt;
		std::vector<BVHBuildNode> m_nodesPool;

		virtual BVHBuildNode* AllocateNode();
		virtual void  InitNodeAllocator(size_t maxnum);
	};
}
