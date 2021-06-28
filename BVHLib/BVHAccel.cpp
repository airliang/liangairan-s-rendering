#include "BVHAccel.h"

namespace BVHLib
{
	BVHAccel BVHAccel::s_BVH;

	BVH_EXPORT int Add(int a, int b)
	{
		return a + b;
	}

	BVH_EXPORT float SendArrayToCPP(Vector3f* positions, int size)
	{
		if (size > 5)
			return positions[5].y;
		else
		{
			return positions[0].x + positions[1].y;
		}
	}

	BVH_EXPORT void GetArrayFromCPP(Vector3f* positions, int size)
	{
		positions[0] = Vector3f::one;
		positions[1].x = Pi;
	}

	BVH_EXPORT int AddMesh(Vector3f* positions, int* indices, int vertices, int triangles)
	{
		return BVHAccel::GetInstance().AddPrimitive(positions, indices, vertices, triangles);
	}


	struct BVHPrimitiveInfo
	{
		BVHPrimitiveInfo() {}
		BVHPrimitiveInfo(size_t primitiveNumber, const Bounds3f& bounds)
			: primitiveNumber(primitiveNumber),
			bounds(bounds),
			centroid(.5f * bounds.pMin + .5f * bounds.pMax) {}
		//在primitives数组中的索引
		size_t primitiveNumber;
		//对应primitive的worldbound
		Bounds3f bounds;
		//bound的中心点
		Point3f centroid;
	};

	struct BVHBuildNode
	{
		// BVHBuildNode Public Methods
		//调用该方法表面BVHBuildNode就是一个叶子节点
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
		//在BVHAccel::primivites中的索引
		int firstPrimOffset;
		//leaf中挂了多少个primitive
		int nPrimitives;
	};

	struct LinearBVHNode {
		Bounds3f bounds;
		union {
			//在primtives数组中的索引
			int primitivesOffset;    // leaf

			int secondChildOffset;   // interior
		};
		//如果是叶子节点，拥有的primitves的数量
		//因为在build tree node时，primitive已经根据放置的node做了排序
		//所以primitve是和node紧凑的，在同一个树叶下primitve的顺序是连续放在primitives数组下的
		uint16_t nPrimitives;  // 0 -> interior node
		uint8_t axis;          // interior node: xyz
		uint8_t pad[1];        // ensure 32 byte total size
	};

	struct BucketInfo
	{
		//拥有的primitive的数量
		int count = 0;
		//bucket的bounds
		Bounds3f bounds;
	};



	BVHAccel::BVHAccel()
	{

	}

	BVHAccel::~BVHAccel()
	{

	}

	int BVHAccel::AddPrimitive(Vector3f* positions, int* indices, int vertices, int triangles)
	{

	}



}
