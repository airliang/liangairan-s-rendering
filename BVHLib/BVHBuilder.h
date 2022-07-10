#pragma once

#include "math/geometry.h"
#include <vector>
#include <list>
#include <atomic>
#include "BVHAccel.h"

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

	class StackEntry
	{
	public:
		BVHBuildNode* node;
		int idx;

		StackEntry(BVHBuildNode* n, int i) : node(n), idx(i)
		{
		}

		int EncodeIdx() { return node->IsLeaf() ? ~idx : idx; }
	};

	struct BucketInfo
	{
		//ӵ�е�primitive������
		int count = 0;
		//bucket��bounds
		Bounds3f bounds;
	};

	struct Reference
	{
		int triIdx = -1;
		Bounds3f bounds;
	};

	struct NodeSpec
	{
		int startIdx = 0;   //primitive���ܵ�primitve�е�λ��
		int numRef = 0;     //�ýڵ�����������ӽڵ������������
		Bounds3f bounds;  //�ڵ��AABB����
		Bounds3f centroidBounds;
	};

	struct SahSplit
	{
		int   dim = 0;     //���ĸ���
		float pos = std::numeric_limits<float>::quiet_NaN();   //���ֵ�λ��
		float sah = MaxFloat;     //���ĵ�sah
		float overlap = 0; //overlap�ı�����spatial��0
	};
	struct SpatialBin
	{
		Bounds3f bounds;
		int enter = 0;
		int exit = 0;
	};

	enum SplitType
	{
		kObject,
		kSpatial
	};

	class BVHBuilder
	{
	public:
		float m_minOverlap = 0;   //���ֿռ����С�������˼�Ǵ��ڸ�������ռ�ſɼ�������
		float m_splitAlpha = 1.0e-5f;   //what's this mean?
		float m_traversalCost = 0.125f;
		int m_numDuplicates = 0;   //�ظ��ڶ���ڵ��ϵ�����������
		std::vector<Reference> m_refStack;
		//GPUBounds[] m_rightBounds = null;
		int m_sortDim;
		const int MaxDepth = 64;
		const int MaxSpatialDepth = 48;
	    static const int NumSpatialBins = 64;

		float GetTriangleCost(int triangles) const
		{
			//1.0��ʾһ���󽻵�����
			return triangles * 1.0f;
		}

		//return the SAH ray node intersect cost
		float GetNodeCost(int nodes) const
		{
			//1.0��ʾһ���󽻵�����
			return nodes * 1.0f;
		}
	private:
		int innerNodes = 0;
		int leafNodes = 0;
		int totalNodes = 0;
		int maxPrimsInNode = 4;
		SpatialBin m_bins[3 * NumSpatialBins];
		//Bounds3f rightBounds[NumSpatialBins - 1];
		BVHBuildNode* RecursiveBuild(NodeSpec& spec, int level, float progressStart, float progressEnd);
		bool SplitPrimRef(const Reference& refPrim, int axis, float split, Reference& leftref, Reference& rightref);
		void SplitPrimRefs(const SahSplit& split, const NodeSpec& req, std::vector<Reference>& refs, int& extra_refs);
		BVHBuildNode* CreateLeaf(const NodeSpec& spec);
		BVHBuildNode* CreateInnerNode(const Bounds3f& bounds, BVHBuildNode* left, BVHBuildNode* right);
		SahSplit FindObjectSplit(const NodeSpec& spec);
		SahSplit FindSpatialSplit(const NodeSpec& spec);
		std::vector<int> _orderedPrimitives;
		BVHBuildNode* m_root = nullptr;

		float m_extra_refs_budget = 0.5f;
		int m_num_nodes_required = 0;
		int m_num_nodes_for_regular = 0;
		// Node archive for memory management
		// As m_nodes fills up we archive it into m_node_archive
		// allocate new chunk and work there.

		// How many nodes have been archived so far
		int m_num_nodes_archived = 0;
		// Node allocator counter, atomic for thread safety
		std::atomic<int> m_nodecnt = 0;
		// Container for archived chunks
		std::list<std::vector<BVHBuildNode>> m_node_archive;
		std::vector<BVHBuildNode> m_nodesPool;

		BVHBuildNode* AllocateNode();
		void  InitNodeAllocator(size_t maxnum);
	public:
		BVHBuildNode* Build(Bounds3f* bounds, int boundsNum, int _maxPrimsInNode = 1);
		const int* GetSortedIndices(int& numSortIndices) const;
		int GetTotalNodes() const
		{
			return totalNodes;
		}

		BVHBuildNode* GetRoot()
		{
			return m_root;
		}

		int FlattenBVHTree(BVHBuildNode* node, int& offset, LinearBVHNode* linearNodes);
	};
}
