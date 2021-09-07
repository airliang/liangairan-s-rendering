#ifndef GEOMETRY_HLSL
#define GEOMETRY_HLSL

#include "mathdef.hlsl"

struct Ray
{
    float4 orig;
    float4 direction;

	float tMax()
	{
		return orig.w;
	}

	float t()
	{
		return direction.w;
	}
};


struct Interaction  //64byte
{
	float4 p;   //交点
	//float time;        //应该是相交的ray的参数t
	float4 wo;
	float4 normal;
	float4 primitive;   //0 is triangle index, y is material index
	float4 uv;
	float4 ns;   
	float4 dpdu;
	float4 dpdv;
	float4 tangent;
	float4 bitangent;
	//int    primitive; //intersect with primitives index, -1 represents no intersection
};

struct AreaLight
{
	float3 Lemit;  //xyz radiance
	float  Area;   //area
};

struct Bounds
{
	float3 min;
	float3 max;

	float3 MinOrMax(int n)
	{
		return n == 0 ? min : max;
	}

	float3 corner(int n)
	{
		return float3(MinOrMax(n & 1).x,
			MinOrMax((n & 2) ? 1 : 0).y,
			MinOrMax((n & 4) ? 1 : 0).z);

	}

	float3 center()
	{
		return (min + max) * 0.5;
	}

	float radius()
	{
		return length(max - min) * 0.5;
	}
};

struct Primitive
{
	int vertexOffset;
	int triangleOffset;
	int transformId; //
	int faceIndex;   //
};

struct BVHNode
{
	//Bounds b0;
	//Bounds b1;
	//int    idx0;   //inner node for left child index, leaf for primitive index in primitive array 
	//int    idx1;   //inner node for right child index, leaf for primitive's count
	//int    c0;  //
	//int    c1;
	float4 b0xy;
	float4 b1xy;
	float4 b01z;
	float4 cids;   //x leftchild node index, y rightchild node index, if the node is leaf, nodeindex is negative
};

struct Vertex
{
	float4 position;
	float4 uv;
};

bool BoundIntersectP(Ray ray, Bounds bounds, float3 invDir, int dirIsNeg[3])
{
	// Check for ray intersection against $x$ and $y$ slabs
	float tMin = (bounds.MinOrMax(dirIsNeg[0]).x - ray.orig.x) * invDir.x;
	float tMax = (bounds.MinOrMax(1 - dirIsNeg[0]).x - ray.orig.x) * invDir.x;
	float tyMin = (bounds.MinOrMax(dirIsNeg[1]).y - ray.orig.y) * invDir.y;
	float tyMax = (bounds.MinOrMax(1 - dirIsNeg[1]).y - ray.orig.y) * invDir.y;

	// Update _tMax_ and _tyMax_ to ensure robust bounds intersection
	//tMax *= 1 + 2 * gamma(3);
	//tyMax *= 1 + 2 * gamma(3);
	if (tMin > tyMax || tyMin > tMax)
		return false;
	if (tyMin > tMin)
		tMin = tyMin;
	if (tyMax < tMax)
		tMax = tyMax;

	// Check for ray intersection against $z$ slab
	float tzMin = (bounds.MinOrMax(dirIsNeg[2]).z - ray.orig.z) * invDir.z;
	float tzMax = (bounds.MinOrMax(1 - dirIsNeg[2]).z - ray.orig.z) * invDir.z;

	// Update _tzMax_ to ensure robust bounds intersection
	//tzMax *= 1 + 2 * gamma(3);
	if (tMin > tzMax || tzMin > tMax)
		return false;
	if (tzMin > tMin) tMin = tzMin;
	if (tzMax < tMax) tMax = tzMax;
	return (tMin < ray.tMax()) && (tMax > 0);
}

bool BoundRayIntersect(Ray ray, float4 bxy, float2 bz, float3 invDir, int3 signs, out float hitTMin)
{
	float4 rayOrig = ray.orig;
	
	// Check for ray intersection against $x$ and $y$ slabs
	float tMin = (bxy[signs.x] - rayOrig.x) * invDir.x;
	float tMax = (bxy[1 - signs.x] - rayOrig.x) * invDir.x;
	float tyMin = (bxy[signs.y + 2] - rayOrig.y) * invDir.y;
	float tyMax = (bxy[1 - signs.y + 2] - rayOrig.y) * invDir.y;

	// Update _tMax_ and _tyMax_ to ensure robust bounds intersection
	//tMax *= 1 + 2 * gamma(3);
	//tyMax *= 1 + 2 * gamma(3);
	if (tMin > tyMax || tyMin > tMax)
		return false;
	//if (tyMin > tMin)
	//	tMin = tyMin;
	//if (tyMax < tMax)
	//	tMax = tyMax;
	tMin = max(tMin, tyMin);
	tMax = min(tMax, tyMax);

	// Check for ray intersection against $z$ slab
	float tzMin = (bz[signs.z] - rayOrig.z) * invDir.z;
	float tzMax = (bz[1 - signs.z] - rayOrig.z) * invDir.z;

	// Update _tzMax_ to ensure robust bounds intersection
	//tzMax *= 1 + 2 * gamma(3);
	if (tMin > tzMax || tzMin > tMax)
		return false;
	//if (tzMin > tMin) tMin = tzMin;
	//if (tzMax < tMax) tMax = tzMax;
	tMin = max(tMin, tzMin);
	tMax = min(tMax, tzMax);
	hitTMin = tMin;
	return (tMin < ray.tMax()) && (tMax > 0);
}

bool BoundIntersect(Ray ray, Bounds bounds, float3 invDir, int dirIsNeg[3])
{
	float tMin = (bounds.MinOrMax(dirIsNeg[0]).x - ray.orig.x) * invDir.x;
	float tMax = (bounds.MinOrMax(1 - dirIsNeg[0]).x - ray.orig.x) * invDir.x;
	float tyMin = (bounds.MinOrMax(dirIsNeg[1]).y - ray.orig.y) * invDir.y;
	float tyMax = (bounds.MinOrMax(1 - dirIsNeg[1]).y - ray.orig.y) * invDir.y;

	// Update _tMax_ and _tyMax_ to ensure robust bounds intersection
	tMax *= 1 + 2 * gamma(3);
	tyMax *= 1 + 2 * gamma(3);
	if (tMin > tyMax || tyMin > tMax)
		return false;
	if (tyMin > tMin)
		tMin = tyMin;
	if (tyMax < tMax)
		tMax = tyMax;

	// Check for ray intersection against $z$ slab
	float tzMin = (bounds.MinOrMax(dirIsNeg[2]).z - ray.orig.z) * invDir.z;
	float tzMax = (bounds.MinOrMax(1 - dirIsNeg[2]).z - ray.orig.z) * invDir.z;

	// Update _tzMax_ to ensure robust bounds intersection
	tzMax *= 1 + 2 * gamma(3);
	if (tMin > tzMax || tzMin > tMax)
		return false;
	if (tzMin > tMin) tMin = tzMin;
	if (tzMax < tMax) tMax = tzMax;
	return (tMin < ray.tMax()) && (tMax > 0);
}

float MinComponent(float3 v) {
	return min(v.x, min(v.y, v.z));
}


float MaxComponent(const float3 v) {
	return max(v.x, max(v.y, v.z));
}

int MaxDimension(float3 v) 
{
	return (v.x > v.y) ? ((v.x > v.z) ? 0 : 2) : ((v.y > v.z) ? 1 : 2);
}

float3 Permute(float3 v, int x, int y, int z)
{
	return float3(v[x], v[y], v[z]);
}

void GetUVs(out float2 uv[3]) 
{
	uv[0] = float2(0, 0);
	uv[1] = float2(1, 0);
	uv[2] = float2(1, 1);
}

void CoordinateSystem(float3 v1, out float3 v2,
	out float3 v3)
{
	//构造v2，v2 dot v1 = 0
	if (abs(v1.x) > abs(v1.y))
		v2 = float3(-v1.z, 0, v1.x) / sqrt(v1.x * v1.x + v1.z * v1.z);
	else
		v2 = float3(0, v1.z, -v1.y) / sqrt(v1.y * v1.y + v1.z * v1.z);
	v3 = cross(v1, v2);
}

float3 WorldToLocal(float3 v, float3 n, float3 ts, float3 ss)
{
	return float3(dot(v, ss), dot(v, ts), dot(v, n));
}

float3 LocalToWorld(float3 v, float3 ns, float3 ts, float3 ss)
{
	return float3(ss.x * v.x + ts.x * v.y + ns.x * v.z,
		ss.y * v.x + ts.y * v.y + ns.y * v.z,
		ss.z * v.x + ts.z * v.y + ns.z * v.z);
}

//w is localspace(z-up) vector 
float AbsCosTheta(float3 w)
{
	return abs(w.z);
}

bool SameHemisphere(float3 w, float3 wp)
{
	return w.z * wp.z > 0;
}

Ray TransformRay(float4x4 mat, Ray ray)
{
	Ray output;
	output.orig = mul(mat, float4(ray.orig.xyz, 1));
	output.direction = mul(mat, float4(ray.direction.xyz, 0));
}

#endif