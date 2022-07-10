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
		int numIndices;
		int* sortIndices;
		void* bvhBuilder;
	};

	struct GPUVertex
	{
		BVHLib::Vector3f position;
		BVHLib::Vector2f uv;
		BVHLib::Vector3f normal;
	};

	struct GPUBVHNode
	{
		BVHLib::Vector3f b0min;
		BVHLib::Vector3f b0max;
		BVHLib::Vector3f b1min;
		BVHLib::Vector3f b1max;
		BVHLib::Vector4f cids;
	};

	struct Primitive
	{
		BVHLib::Vector3i triIndices;
		BVHLib::Bounds3f worldBound;
		int meshInstIndex;
		int meshIndex;
	};

	struct LinearBVHNode
	{
		BVHLib::Bounds3f bounds;  //64bytes

		int leftChildIdx;    // leaf
		int rightChildIdx;   // interior

		int firstPrimOffset;
		int nPrimitives;  // 0 -> interior node

		bool IsLeaf()
		{
			return nPrimitives > 0;
		}
	};

	struct BVHWoodData
	{
		void* nodes;
		void* woodVertices;
		void* woodTriangles;
		int   nodesNum;
		int   woodVerticesNum;
		int   woodTrianglesNum;
	};

	BVHAPI int Add(int a, int b);


	BVHAPI BVHHandle BuildBVH(BVHLib::Bounds3f* bounds, int boundsNum, int _maxPrimsInNode);
	BVHAPI void		 FlattenBVHTree(const BVHHandle* handle, LinearBVHNode* linearNodes);
	//BVHAPI BVHWoodData CreateCompact(const BVHHandle* handle, Primitive* primitives, int primsNum, GPUVertex* vertices, int verticesNum, bool isBottomLevel, int* instBVHOffset, int offsetsNum);
	BVHAPI void      GetBVHData(GPUBVHNode* nodes, int nodesNum, BVHLib::Vector4f* woodVertices, int verticesNum, int* woodTrangles, int trianglesNum);
	BVHAPI void      ReleaseBVH(const BVHHandle* handle);

#ifdef __cplusplus
}
#endif

