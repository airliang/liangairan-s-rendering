#ifndef GEOMETRY_HLSL
#define GEOMETRY_HLSL

#include "mathdef.hlsl"

struct Ray
{
    float4 orig;
    float4 direction;

	//代表该ray的最远距离
	float tMax()
	{
		return orig.w;
	}

	//ray当前的位置
	float tMin()
	{
		return direction.w;
	}

	void SetTMin(float tmin)
	{
		direction.w = tmin;
	}
};

float origin() { return 1.0f / 32.0f; }
float float_scale() { return 1.0f / 65536.0f; }
float int_scale() { return 256.0f; }

// Normal points outward for rays exiting the surface, else is flipped.
float3 offset_ray(const float3 p, const float3 n)
{
	int3 of_i = int3(int_scale() * n.x, int_scale() * n.y, int_scale() * n.z);

	float3 p_i = float3(
		asfloat(asint(p.x) + ((p.x < 0) ? -of_i.x : of_i.x)),
		asfloat(asint(p.y) + ((p.y < 0) ? -of_i.y : of_i.y)),
		asfloat(asint(p.z) + ((p.z < 0) ? -of_i.z : of_i.z)));

	return float3(abs(p.x) < origin() ? p.x + float_scale() * n.x : p_i.x,
		abs(p.y) < origin() ? p.y + float_scale() * n.y : p_i.y,
		abs(p.z) < origin() ? p.z + float_scale() * n.z : p_i.z);
}

Ray SpawnRay(float3 p, float3 direction, float3 normal, float tMax)
{
	Ray ray;
	ray.orig.xyz = offset_ray(p, normal);
	ray.orig.w = tMax;
	ray.direction.xyz = direction;
	ray.direction.w = 0;
	return ray;
}

struct ShadowRay
{
	float3 p0;   //isect position
	float3 p1;   //light sample point position
	float3 radiance;  //light radiance
	//float3 lightNormal;  //light sample point normal
	//mis weight
	float  weight;
	float  lightSourcePdf;        //Light Radiance pdf
	float  lightPdf;   //light sampling pdf
	float  visibility; //1 is visible, 0 invisible
	float  lightIndex; 
};

struct BSDFSample
{
	float3 direction;
	float  weight;
	float  pdf;
};

struct Interaction  //64byte
{
	float4 p;   //交点
	//float time;        //应该是相交的ray的参数t
	float4 wo;
	float3 normal;
	
	//float4 primitive;   //0 is triangle index, y is material index
	float4 uv;
	float4 row1;
	float4 row2;
	float4 row3;
	//float4 ns;   
	//float4 dpdu;
	//float4 dpdv;
	float3 tangent;  //the same as pbrt's ss(x)
	float3 bitangent; //the same as pbrt's ts(y)
	uint   materialID;
	uint   meshInstanceID;
	//int    primitive; //intersect with primitives index, -1 represents no intersection
	bool IsHit()
	{
		return p.w > 0;
	}

	float3 WorldToLocal(float3 v)
	{
		return float3(dot(tangent, v), dot(bitangent, v), dot(normal, v));
	}

	float3 LocalToWorld(float3 v)
	{
		return float3(tangent.x * v.x + bitangent.x * v.y + normal.x * v.z,
			tangent.y * v.x + bitangent.y * v.y + normal.y * v.z,
			tangent.z * v.x + bitangent.z * v.y + normal.z * v.z
			);
	}
};

struct AreaLight
{
	float3 Lemit;  //xyz radiance
	float  Area;   //area
};

struct Triangle
{
	float3 p0;
	float3 p1;
	float3 p2;
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
	output.orig.w = ray.orig.w;
	output.direction = mul(mat, float4(ray.direction.xyz, 0));
	output.direction.w = ray.direction.w;
	return output;
}

//return the triangle point
//p0 p1 p2 is the local position of a mesh
float3 SampleTrianglePoint(float3 p0, float3 p1, float3 p2, float2 u, out float3 normal, out float pdf)
{
	//caculate bery centric uv w = 1 - u - v
	float t = sqrt(u.x);
	float2 uv = float2(1.0 - t, t * u.y);
	float w = 1 - uv.x - uv.y;

	float3 position = p0 * w + p1 * uv.x + p2 * uv.y;
	float3 crossVector = cross(p1 - p0, p2 - p0);
	normal = normalize(crossVector);
	pdf = 1.0 / length(crossVector);

	return position;
}


#endif