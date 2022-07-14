#pragma once

#include "BVHBuilder.h"
#include <list>

namespace BVHLib
{
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

	class SplitBVHBuilder : public BVHBuilder
	{
	public:
		float m_minOverlap = 0.05f;   //���ֿռ����С�������˼�Ǵ��ڸ�������ռ�ſɼ�������
		float m_splitAlpha = 1.0e-5f;   //what's this mean?
		float m_traversalCost = 0.125f;
		int m_numDuplicates = 0;   //�ظ��ڶ���ڵ��ϵ�����������
		std::vector<Reference> m_refStack;
		//GPUBounds[] m_rightBounds = null;
		//int m_sortDim;
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
		
		
		SpatialBin m_bins[3 * NumSpatialBins];
		//Bounds3f rightBounds[NumSpatialBins - 1];
		BVHBuildNode* RecursiveBuild(NodeSpec& spec, int level, float progressStart, float progressEnd);
		bool SplitPrimRef(const Reference& refPrim, int axis, float split, Reference& leftref, Reference& rightref);
		void SplitPrimRefs(const SahSplit& split, const NodeSpec& req, std::vector<Reference>& refs, int& extra_refs);
		BVHBuildNode* CreateLeaf(const NodeSpec& spec);
		BVHBuildNode* CreateInnerNode(const Bounds3f& bounds, BVHBuildNode* left, BVHBuildNode* right);
		SahSplit FindObjectSplit(const NodeSpec& spec);
		SahSplit FindSpatialSplit(const NodeSpec& spec);
		
		

		float m_extra_refs_budget = 0.5f;
		int m_num_nodes_required = 0;
		int m_num_nodes_for_regular = 0;
		// Node archive for memory management
		// As m_nodes fills up we archive it into m_node_archive
		// allocate new chunk and work there.

		// How many nodes have been archived so far
		int m_num_nodes_archived = 0;
		// Container for archived chunks
		std::list<std::vector<BVHBuildNode>> m_node_archive;
		

	protected:
		BVHBuildNode* AllocateNode() override;
		void  InitNodeAllocator(size_t maxnum) override;
	public:
		BVHBuildNode* Build(Bounds3f* bounds, int boundsNum, int _maxPrimsInNode = 1) override;
		

		
	};
}
