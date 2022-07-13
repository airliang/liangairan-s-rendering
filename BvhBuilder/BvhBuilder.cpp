#include "BvhBuilder.h"
#include "RadeonRays/accelerator/bvh.h"
#include "RadeonRays/accelerator/split_bvh.h"

#include <cassert>
#include <stack>
#include <iostream>

typedef intptr_t Handle;

#if defined(BUILD_AS_DLL)
#define BVH_API __declspec(dllexport)
#else
#define BVH_API	__declspec(dllimport)
#endif

using namespace RadeonRays;

int ProcessASNodes(Node* nodes, const Bvh::Node* node, const std::vector<int>& packedIndices, bool isTLAS, int& currNodeIndex)
{
	RadeonRays::bbox bbox = node->bounds;

	int index = currNodeIndex;

	nodes[index].bboxmin = bbox.pmin;
	nodes[index].bboxmax = bbox.pmax;
	nodes[index].left = -1;
	if(node->type == RadeonRays::Bvh::NodeType::kLeaf)
	{
		if(!isTLAS)
		{
			nodes[index].left = node->startidx; // Add Triangle Offset
			nodes[index].right = node->numprims;
		}
		else
		{
			int instanceIndex = packedIndices[node->startidx];
			nodes[index].left = instanceIndex; // Change To the BLAS root index
			nodes[index].right = instanceIndex;
		}
	}
	else
	{
		currNodeIndex++;
		ProcessASNodes(nodes, node->lc, packedIndices, isTLAS, currNodeIndex);
		currNodeIndex++;
		nodes[index].right = ProcessASNodes(nodes, node->rc, packedIndices, isTLAS, currNodeIndex);
	}
	return index;
}

RRAPI BVHHandle CreateBVH(const RadeonRays::bbox* bounds, int count, bool useSah, bool useSplit, float traversalCost, int numBins, int splitDepth, float miniOverlap)
{
	Bvh* bvh = useSplit ? new SplitBvh(traversalCost, numBins, splitDepth, miniOverlap, 0.5f) : new Bvh(traversalCost, numBins, useSah);
	bvh->Build((const RadeonRays::bbox*)bounds, count);
	BVHHandle sbvh;
	sbvh.bvh = bvh;
	sbvh.numNodes = bvh->GetNodeCount();
	sbvh.numIndices = (int)bvh->GetNumIndices();
	sbvh.sortedIndices = (int*)bvh->GetIndices();
	sbvh.bounds = bvh->Bounds();
	return sbvh;
}

RRAPI void DestroyBVH(const BVHHandle* handle)
{
	if(handle != nullptr && handle->bvh != nullptr)
		delete handle->bvh;
}

RRAPI void TransferToFlat(Node* nodes, const BVHHandle* as, bool isTLAS)
{
	Bvh* bvh = (Bvh*)as->bvh;
	int currNodeIndex = 0;
	ProcessASNodes(nodes, bvh->GetRoot(), bvh->GetPackedIndices(), isTLAS, currNodeIndex);
}

int FlattenBVHTree(const Bvh::Node* node, int& offset, LinearBVHNode* linearNodes)
{
	int curOffset = offset;
	linearNodes[curOffset].bounds = node->bounds;
	int myOffset = offset++;
	if (node->type == Bvh::kLeaf)
	{
		//是一个叶子节点
		linearNodes[curOffset].nPrimitives = node->numprims;
		linearNodes[curOffset].firstPrimOffset = node->startidx;
		linearNodes[curOffset].leftChildIdx = -1;
		linearNodes[curOffset].rightChildIdx = -1;
	}
	else
	{
		//linearNodes[curOffset].axis = (ushort)node.splitAxis;
		linearNodes[curOffset].nPrimitives = 0;
		//这里返回了offset
		linearNodes[curOffset].leftChildIdx = FlattenBVHTree(node->lc, offset, linearNodes);
		linearNodes[curOffset].rightChildIdx = FlattenBVHTree(node->rc, offset, linearNodes);
	}

	return myOffset;
}

RRAPI void FlattenBVHTree(const BVHHandle* handle, LinearBVHNode* linearNodes)
{
	Bvh* bvh = (Bvh*)handle->bvh;
	int offset = 0;
	FlattenBVHTree(bvh->GetRoot(), offset, linearNodes);
}
