#include "BVHAccel.h"

namespace BVHLib
{

	BVHAPI int Add(int a, int b)
	{
		return a + b;
	}


	BVHAPI BVHHandle BuildBVH(BVHLib::Bounds3f* bounds, int boundsNum, GPUVertex* vertices, int verticesNum)
	{
		BVHHandle handle;
		return handle;
	}
}
