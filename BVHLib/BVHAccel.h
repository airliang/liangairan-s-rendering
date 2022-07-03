#pragma once
#include "math/geometry.h"

#ifdef _WIN32
#ifdef EXPORT_API
#define BVHAPI __declspec(dllexport)
#else
#define BVHAPI __declspec(dllimport)
#endif
#elif defined(__GNUC__)
#ifdef EXPORT_API
#define BVHAPI __attribute__((visibility ("default")))
#else
#define BVHAPI
#endif
#endif

#ifdef __cplusplus
extern "C"
{
#endif

	struct BVHHandle
	{
		int numNodes;
		int numWoodTriangles;
	};

	struct GPUVertex
	{
		BVHLib::Vector3f position;
		BVHLib::Vector2f uv;
		BVHLib::Vector3f normal;
	};

	BVHAPI int Add(int a, int b);


	BVHAPI BVHHandle BuildBVH(BVHLib::Bounds3f* bounds, int boundsNum, GPUVertex* vertices, int verticesNum);

#ifdef __cplusplus
}
#endif

