#include "BVHAccel.h"
#include "SplitBVHBuilder.h"
#include <list>


BVHAPI int Add(int a, int b)
{
	return a + b;
}

int _FlattenBVHTree(BVHLib::BVHBuildNode* node, int& offset, LinearBVHNode* linearNodes)
{
	int curOffset = offset;
	linearNodes[curOffset].bounds = node->bounds;
	int myOffset = offset++;
	if (node->nPrimitives > 0)
	{
		//是一个叶子节点
		linearNodes[curOffset].nPrimitives = node->nPrimitives;
		linearNodes[curOffset].firstPrimOffset = node->firstPrimOffset;
		linearNodes[curOffset].leftChildIdx = -1;
		linearNodes[curOffset].rightChildIdx = -1;
	}
	else
	{
		//linearNodes[curOffset].axis = (ushort)node.splitAxis;
		linearNodes[curOffset].nPrimitives = 0;
		//这里返回了offset
		linearNodes[curOffset].leftChildIdx = _FlattenBVHTree(node->childrenLeft, offset, linearNodes);
		linearNodes[curOffset].rightChildIdx = _FlattenBVHTree(node->childrenRight, offset, linearNodes);
	}

	return myOffset;
}


BVHAPI BVHHandle BuildBVH(BVHLib::Bounds3f* bounds, int boundsNum, int _maxPrimsInNode, bool useSplit)
{
	BVHHandle handle;
	memset(&handle, 0, sizeof(handle));
	BVHLib::BVHBuilder* builder = useSplit ? new BVHLib::SplitBVHBuilder() : new BVHLib::BVHBuilder();
	builder->Build(bounds, boundsNum, _maxPrimsInNode);
	handle.numNodes = builder->GetTotalNodes();
	handle.sortIndices = (int*)builder->GetSortedIndices(handle.numIndices);
	handle.bvhBuilder = builder;
	return handle;
}

BVHAPI void      ReleaseBVH(const BVHHandle* handle)
{
	BVHLib::BVHBuilder* builder = (BVHLib::BVHBuilder*)handle->bvhBuilder;
	if (builder != nullptr)
		delete builder;
}

BVHAPI void		 FlattenBVHTree(const BVHHandle* handle, LinearBVHNode* linearNodes)
{
	BVHLib::BVHBuilder* builder = (BVHLib::BVHBuilder*)handle->bvhBuilder;
	if (builder != nullptr)
	{
		int offset = 0;
		_FlattenBVHTree(builder->GetRoot(), offset, linearNodes);
	}
}
