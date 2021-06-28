#pragma once
#include "math/geometry.h"

#define BVH_EXPORT  __declspec(dllexport)

namespace BVHLib
{

	extern "C" BVH_EXPORT int Add(int a, int b);

	extern "C" BVH_EXPORT float SendArrayToCPP(Vector3f* positions, int size);

	extern "C" BVH_EXPORT void GetArrayFromCPP(Vector3f* positions, int size);

	//return the total bvh nodes number
	extern "C" BVH_EXPORT int AddMesh(Vector3f* positions, int* indices, int triangles, int vertices);

	extern "C" BVH_EXPORT int BuildBVH();

	class BVHAccel
	{
	public:
		static BVHAccel GetInstance()
		{
			return s_BVH;
		}
		~BVHAccel();

		int AddPrimitive(Vector3f* positions, int* indices, int vertices, int triangles);
	private:
		BVHAccel();

		static BVHAccel s_BVH;
	};
}
