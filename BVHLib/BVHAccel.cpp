#include "BVHAccel.h"
#include "BVHBuilder.h"
#include <list>

namespace BVHLib
{
	

	
}

BVHAPI int Add(int a, int b)
{
	return a + b;
}


BVHAPI BVHHandle BuildBVH(BVHLib::Bounds3f* bounds, int boundsNum, int _maxPrimsInNode)
{
	BVHHandle handle;
	memset(&handle, 0, sizeof(handle));
	BVHLib::BVHBuilder* builder = new BVHLib::BVHBuilder();
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
		builder->FlattenBVHTree(builder->GetRoot(), offset, linearNodes);
	}
}
