#pragma once

#define BVH_EXPORT  __declspec(dllexport)

namespace BVHLib
{
	extern "C" BVH_EXPORT int Add(int a, int b);
}
