#pragma once

#include "math/geometry.h"
#include <vector>
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
		//��BVHAccel::primivites�е�����
		int firstPrimOffset;
		//leaf�й��˶��ٸ�primitive
		int nPrimitives;
	};

	struct LinearBVHNode {
		Bounds3f bounds;
		union {
			//��primtives�����е�����
			int primitivesOffset;    // leaf

			int secondChildOffset;   // interior
		};
		//�����Ҷ�ӽڵ㣬ӵ�е�primitves������
		//��Ϊ��build tree nodeʱ��primitive�Ѿ����ݷ��õ�node��������
		//����primitve�Ǻ�node���յģ���ͬһ����Ҷ��primitve��˳������������primitives�����µ�
		uint16_t nPrimitives;  // 0 -> interior node
		uint8_t axis;          // interior node: xyz
		uint8_t pad[1];        // ensure 32 byte total size
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
		float pos = 0;   //���ֵ�λ��
		float sah = MaxFloat;     //���ĵ�sah
		float overlap = 0; //overlap�ı�����spatial��0
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
		float m_minOverlap = 0;   //���ֿռ����С�������˼�Ǵ��ڸ�������ռ�ſɼ�������
		float m_splitAlpha = 1.0e-5f;   //what's this mean?
		float m_traversalCost = 0.125f;
		int m_numDuplicates = 0;   //�ظ��ڶ���ڵ��ϵ�����������
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
