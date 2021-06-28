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
	float3 p;   //交点
	float time;        //应该是相交的ray的参数t
	float4 wo;
	float4 normal;
	float3 pError;   //floating-point error
	int   primitive; //intersect with primitives index, -1 represents no intersection
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

/*
float max(float3 v) { return max(max(v.x, v.y), v.z); }
// box.rotation = object-to-world, invRayDir unused if oriented
bool OurIntersectBox(Bounds box, Ray ray, out float distance, out float3 normal,
	const bool canStartInBox, in float3 _invRayDir) 
{
	ray.orig.xyz = ray.orig.xyz - box.center();
	
	float boxInvRadius = 1.0 / box.radius();
	float winding = canStartInBox && (max(abs(ray.orig.xyz) * boxInvRadius)
		< 1.0) ? -1 : 1;
	float3 sgn = -sign(ray.dir);
	// Distance to plane
	float3 d = box.radius() * winding * sgn - ray.orig.xyz;
	
	d *= _invRayDir;
# define TEST(U, VW) (d.U >= 0.0) && \
all(lessThan(abs(ray.origin.VW + ray.dir.VW * d.U), box.radius.VW))
	bvec3 test = bvec3(TEST(x, yz), TEST(y, zx), TEST(z, xy));
	sgn = test.x ? vec3(sgn.x, 0, 0) : (test.y ? vec3(0, sgn.y, 0) :
		vec3(0, 0, test.z ? sgn.z : 0));
# undef TEST
	distance = (sgn.x != 0) ? d.x : ((sgn.y != 0) ? d.y : d.z);
	normal = oriented ? (box.rot * sgn) : sgn;
	return (sgn.x != 0) || (sgn.y != 0) || (sgn.z != 0);
}
*/

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

bool TriangleIntersect(Ray ray, float3 p0, float3 p1, float3 p2, int primitive, out Interaction intersect)
{
	// Translate vertices based on ray origin
	float3 p0t = p0 - ray.orig;
	float3 p1t = p1 - ray.orig;
	float3 p2t = p2 - ray.orig;

	// Permute components of triangle vertices and ray direction
	int kz = MaxDimension(abs(ray.direction));
	int kx = kz + 1;
	if (kx == 3) kx = 0;
	int ky = kx + 1;
	if (ky == 3) ky = 0;
	float3 d = Permute(ray.direction, kx, ky, kz);
	p0t = Permute(p0t, kx, ky, kz);
	p1t = Permute(p1t, kx, ky, kz);
	p2t = Permute(p2t, kx, ky, kz);

	// Apply shear transformation to translated vertex positions
	float Sx = -d.x / d.z;
	float Sy = -d.y / d.z;
	float Sz = 1.f / d.z;
	p0t.x += Sx * p0t.z;
	p0t.y += Sy * p0t.z;
	p1t.x += Sx * p1t.z;
	p1t.y += Sy * p1t.z;
	p2t.x += Sx * p2t.z;
	p2t.y += Sy * p2t.z;

	// Compute edge function coefficients _e0_, _e1_, and _e2_
	float e0 = p1t.x * p2t.y - p1t.y * p2t.x;
	float e1 = p2t.x * p0t.y - p2t.y * p0t.x;
	float e2 = p0t.x * p1t.y - p0t.y * p1t.x;

	// Fall back to double precision test at triangle edges
	/*
	if ((e0 == 0.0f || e1 == 0.0f || e2 == 0.0f))
	{
		double p2txp1ty = (double)p2t.x * (double)p1t.y;
		double p2typ1tx = (double)p2t.y * (double)p1t.x;
		e0 = (float)(p2typ1tx - p2txp1ty);
		double p0txp2ty = (double)p0t.x * (double)p2t.y;
		double p0typ2tx = (double)p0t.y * (double)p2t.x;
		e1 = (float)(p0typ2tx - p0txp2ty);
		double p1txp0ty = (double)p1t.x * (double)p0t.y;
		double p1typ0tx = (double)p1t.y * (double)p0t.x;
		e2 = (float)(p1typ0tx - p1txp0ty);
	}
	*/

	// Perform triangle edge and determinant tests
	if ((e0 < 0 || e1 < 0 || e2 < 0) && (e0 > 0 || e1 > 0 || e2 > 0))
		return false;
	Float det = e0 + e1 + e2;
	if (det == 0)
		return false;

	// Compute scaled hit distance to triangle and test against ray $t$ range
	p0t.z *= Sz;
	p1t.z *= Sz;
	p2t.z *= Sz;
	float tScaled = e0 * p0t.z + e1 * p1t.z + e2 * p2t.z;
	if (det < 0 && (tScaled >= 0 || tScaled < ray.tMax() * det))
		return false;
	else if (det > 0 && (tScaled <= 0 || tScaled > ray.tMax() * det))
		return false;

	// Compute barycentric coordinates and $t$ value for triangle intersection
	float invDet = 1.0 / det;
	float b0 = e0 * invDet;
	float b1 = e1 * invDet;
	float b2 = e2 * invDet;
	float t = tScaled * invDet;

	// Ensure that computed triangle $t$ is conservatively greater than zero

	// Compute $\delta_z$ term for triangle $t$ error bounds
	float maxZt = MaxComponent(abs(float3(p0t.z, p1t.z, p2t.z)));
	float deltaZ = gamma(3) * maxZt;

	// Compute $\delta_x$ and $\delta_y$ terms for triangle $t$ error bounds
	float maxXt = MaxComponent(abs(float3(p0t.x, p1t.x, p2t.x)));
	float maxYt = MaxComponent(abs(float3(p0t.y, p1t.y, p2t.y)));
	float deltaX = gamma(5) * (maxXt + maxZt);
	float deltaY = gamma(5) * (maxYt + maxZt);

	// Compute $\delta_e$ term for triangle $t$ error bounds
	float deltaE =
		2 * (gamma(2) * maxXt * maxYt + deltaY * maxXt + deltaX * maxYt);

	// Compute $\delta_t$ term for triangle $t$ error bounds and check _t_
	float maxE = MaxComponent(abs(float3(e0, e1, e2)));
	float deltaT = 3 *
		(gamma(3) * maxE * maxZt + deltaE * maxZt + deltaZ * maxE) *
		abs(invDet);
	if (t <= deltaT)
		return false;

	
	// Compute triangle partial derivatives
	//not consider the uvs in the first version
	
	float3 dpdu, dpdv;
	float2 uv[3];
	GetUVs(uv);

	// Compute deltas for triangle partial derivatives
	float2 duv02 = uv[0] - uv[2], duv12 = uv[1] - uv[2];
	float3 dp02 = p0 - p2, dp12 = p1 - p2;
	float determinant = duv02[0] * duv12[1] - duv02[1] * duv12[0];
	bool degenerateUV = abs(determinant) < 1e-8;
	if (!degenerateUV)
	{
		Float invdet = 1 / determinant;
		dpdu = (duv12[1] * dp02 - duv02[1] * dp12) * invdet;
		dpdv = (-duv12[0] * dp02 + duv02[0] * dp12) * invdet;
	}
	if (degenerateUV || length(cross(dpdu, dpdv)) == 0)
	{
		// Handle zero determinant for triangle partial derivative matrix
		float3 ng = cross(p2 - p0, p1 - p0);
		if (length(ng) == 0)
			// The triangle is actually degenerate; the intersection is
			// bogus.
			return false;

		CoordinateSystem(normalize(ng), dpdu, dpdv);
	}
	

	// Compute error bounds for triangle intersection
	float xAbsSum =
		(abs(b0 * p0.x) + abs(b1 * p1.x) + abs(b2 * p2.x));
	float yAbsSum =
		(abs(b0 * p0.y) + abs(b1 * p1.y) + abs(b2 * p2.y));
	float zAbsSum =
		(abs(b0 * p0.z) + abs(b1 * p1.z) + abs(b2 * p2.z));
	float3 pError = gamma(7) * float3(xAbsSum, yAbsSum, zAbsSum);

	// Interpolate $(u,v)$ parametric coordinates and hit point
	float3 pHit = b0 * p0 + b1 * p1 + b2 * p2;
	float2 uvHit = b0 * uv[0] + b1 * uv[1] + b2 * uv[2];

	// Test intersection against alpha texture, if present
	//if (testAlphaTexture && mesh->alphaMask) {
	//	SurfaceInteraction isectLocal(pHit, Vector3f(0, 0, 0), uvHit, -ray.d,
	//		dpdu, dpdv, Normal3f(0, 0, 0),
	//		Normal3f(0, 0, 0), ray.time, this);
	//	if (mesh->alphaMask->Evaluate(isectLocal) == 0) return false;
	//}

	// Fill in _SurfaceInteraction_ from triangle hit
	//* isect = SurfaceInteraction(pHit, pError, uvHit, -ray.d, dpdu, dpdv,
	//	float3(0, 0, 0), float3(0, 0, 0), ray.time(),
	//	this);

	// Override surface normal in _isect_ for triangle
	//isect->normal = isect->shading.n = normalize(cross(dp02, dp12));
	
	intersect.p = pHit;
	intersect.pError = pError;
	intersect.normal = float4(normalize(cross(dp02, dp12)), 0);
	intersect.time = ray.t();
	intersect.wo = -ray.direction;
	intersect.primitive = primitive;
	return true;

}

#endif