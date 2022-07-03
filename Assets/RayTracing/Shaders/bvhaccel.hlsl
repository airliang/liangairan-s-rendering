
#ifndef BVHACCEL_HLSL
#define BVHACCEL_HLSL

#include "gpuSceneData.hlsl"

#define STACK_SIZE 64
#define EntrypointSentinel 0x76543210



int min_min(int a, int b, int c)
{
	return min(min(a, b), c);
}
int min_max(int a, int b, int c)
{
	return max(min(a, b), c);

}
int max_min(int a, int b, int c)
{
	return min(max(a, b), c);
}

int max_max(int a, int b, int c)
{
	return max(max(a, b), c);
}

float fmin_fmin(float a, float b, float c) { return asfloat(min_min(asint(a), asint(b), asint(c))); }
float fmin_fmax(float a, float b, float c) { return asfloat(min_max(asint(a), asint(b), asint(c))); }
float fmax_fmin(float a, float b, float c) { return asfloat(max_min(asint(a), asint(b), asint(c))); }
float fmax_fmax(float a, float b, float c) { return asfloat(max_max(asint(a), asint(b), asint(c))); }

float spanBeginKepler(float a0, float a1, float b0, float b1, float c0, float c1, float d) { return fmax_fmax(min(a0, a1), min(b0, b1), fmin_fmax(c0, c1, d)); }
float spanEndKepler(float a0, float a1, float b0, float b1, float c0, float c1, float d) { return fmin_fmin(max(a0, a1), max(b0, b1), fmax_fmin(c0, c1, d)); }

bool BoundRayIntersect(in Ray r, in float3 invdir, in float3 bmin, in float3 bmax, out float hitTMin)
{
	float3 f = (bmax - r.orig) * invdir;
	float3 n = (bmin - r.orig) * invdir;

	float3 tmax = max(f, n);
	float3 tmin = min(f, n);

	float t1 = min(min(tmax.x, min(tmax.y, tmax.z)), r.tmax);
	float t0 = max(max(tmin.x, max(tmin.y, tmin.z)), r.tmin);
	hitTMin = min(t0, t1);
	return t1 >= t0;
}

bool BoundRayIntersect(Ray ray, float4 bxy, float2 bz, float3 invDir, int3 signs, out float hitTMin)
{
	hitTMin = 0;
	float3 rayOrig = ray.orig;

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
	return (tMin < ray.tmax) && (tMax > 0);
}

bool SceneIntersectTest(Ray ray)
{
	float3 rayDir = ray.direction;
	//float3 invDir = 1.0 / ray.direction.xyz;
	float ooeps = pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number
	//float idirx = 1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps); // inverse ray direction
	//float idiry = 1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps); // inverse ray direction
	//float idirz = 1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps); // inverse ray direction
	float3 invDir = float3(1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps),
		1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps),
		1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps)
		);
	//int dirIsNeg[3] = { invDir.x < 0, invDir.y < 0, invDir.z < 0 };
	int currentNodeIndex = 0; //当前正在访问的node
	int signX = sign(invDir.x);
	int signY = sign(invDir.y);
	int signZ = sign(invDir.z);
	signX = signX < 0 ? 1 : 0;
	signY = signY < 0 ? 1 : 0;
	signZ = signZ < 0 ? 1 : 0;

	BVHNode node = BVHTree[currentNodeIndex];
	//bounds.min = testBoundMin;
	//bounds.max = testBoundMax;
	//if (BoundIntersectP(ray, bounds, invDir, dirIsNeg))
	float tMin = 0;
	//float hitT = ray.tmax;
	if (BoundRayIntersect(ray, invDir, node.b0min, node.b0max, tMin))
		return true;
	if (BoundRayIntersect(ray, invDir, node.b1min, node.b1max, tMin))
		return true;

	return false;
}

/*
bool RayBoundIntersect(float3 rayOrig, float4 bxy, float2 bz, float idirx, float idiry, float idirz, float rayTMax, out float tMin)
{
	int signX = sign(idirx);
	int signY = sign(idiry);
	int signZ = sign(idirz);
	signX = signX < 0 ? 1 : 0;
	signY = signY < 0 ? 1 : 0;
	signZ = signZ < 0 ? 1 : 0;

	tMin = (bxy[signX] - rayOrig.x) * idirx;
	float tMax = (bxy[1 - signX] - rayOrig.x) * idirx;
	float tyMin = (bxy[signY + 2] - rayOrig.y) * idiry;
	float tyMax = (bxy[1 - signY + 2] - rayOrig.y) * idiry;

	if (tMin > tyMax || tyMin > tMax)
		return false;

	tMin = max(tMin, tyMin);

	tMax = min(tMax, tyMax);

	float tzMin = (bz[signZ] - rayOrig.z) * idirz;
	float tzMax = (bz[1 - signZ] - rayOrig.z) * idirz;

	if ((tMin > tzMax) || (tzMin > tMax))
		return false;
	tMin = max(tMin, tzMin);
	tMax = min(tMax, tzMax);

	if (rayTMax < tMin)
		return false;

	return true;
}
*/

bool WoodTriangleRayIntersect(float3 rayOrig, float3 rayDir, float4 m0, float4 m1, float4 m2, float tmin, float tmax, inout float2 uv, out float hitT)
{
	//Oz is a point, must plus w
	float Oz = m2.w + dot(rayOrig, m2.xyz);//ray.orig.x * m2.x + ray.orig.y * m2.y + ray.orig.z * m2.z;
	//Dz is a vector
	float invDz = 1.0f / dot(rayDir, m2.xyz);//(ray.direction.x * m2.x + ray.direction.y * m2.y + ray.direction.z * m2.z);
	float t = -Oz * invDz;
	//t *= 1 + 2 * gamma(3);
	//hitT = tmax;
	//if t is in bounding and less than the ray.tMax
	if (t >= tmin && t < tmax)
	{
		// Compute and check barycentric u.
		float Ox = m0.w + dot(rayOrig, m0.xyz);//ray.orig.x * m0.x + ray.orig.y * m0.y + ray.orig.z * m0.z;
		float Dx = dot(rayDir, m0.xyz);//dirx * m0.x + diry * m0.y + dirz * m0.z;
		float u = Ox + t * Dx;

		if (u >= 0.0f)
		{
			// Compute and check barycentric v.
			float Oy = m1.w + dot(rayOrig, m1.xyz);//ray.orig.x * m1.x + ray.orig.y * m1.y + ray.orig.z * m1.z;
			float Dy = dot(rayDir, m1.xyz);//dirx * m1.x + diry * m1.y + dirz * m1.z;
			float v = Oy + t * Dy;

			if (v >= 0.0f && u + v <= 1.0f)
			{
				uv = float2(u, v);
				hitT = t;
				return true;
			}
		}
	}
	return false;
}

int2 GetNodeChildIndex(float4 cids)
{
	return int2(asint(cids.x), asint(cids.y));
}

int2 GetTopLevelLeaveMeshInstance(float4 cids)
{
	return int2(asint(cids.z), asint(cids.w));
}

//ray和bvh求交，如果是instance bvh，那么bvhOffset是toplevel bvh得到后的bottom level bvh的offset
//instance bvh，在求交前要把ray转到object space里
bool IntersectBVHandTriangles(Ray ray, int bvhOffset, out Interaction interaction)
{
	interaction = (Interaction)0;
	//float4 rayOrig = ray.orig;
	//float4 rayDir = ray.direction;
	int traversalStack[STACK_SIZE];
	traversalStack[0] = EntrypointSentinel;
	int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).
	uint nodeAddr = bvhOffset;
	//int primitivesNum = 0;   //当前节点的primitives数量
	//int primitivesNum2 = 0;
	int triIdx = 0;
	float tmin = ray.tmin;
	float hitT = ray.tmax;  //tmax
          // Ray origin.
	float ooeps = pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number
	//float idirx = 1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps); // inverse ray direction
	//float idiry = 1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps); // inverse ray direction
	//float idirz = 1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps); // inverse ray direction
	float3 invDir = float3(1.0f / (abs(ray.direction.x) > ooeps ? ray.direction.x : sign(ray.direction.x) * ooeps),
		1.0f / (abs(ray.direction.y) > ooeps ? ray.direction.y : sign(ray.direction.y) * ooeps),
		1.0f / (abs(ray.direction.z) > ooeps ? ray.direction.z : sign(ray.direction.z) * ooeps)
		);

	//float oodx = rayOrig.x * idirx;  // ray origin / ray direction
	//float oody = rayOrig.y * idiry;  // ray origin / ray direction
	//float oodz = rayOrig.z * idirz;  // ray origin / ray direction
	int   stackIndex = 0;   //当前traversalStack的索引号
	int   hitIndex = -1;
	int signX = sign(invDir.x);
	int signY = sign(invDir.y);
	int signZ = sign(invDir.z);
	signX = signX < 0 ? 1 : 0;
	signY = signY < 0 ? 1 : 0;
	signZ = signZ < 0 ? 1 : 0;
	int3 signs = int3(signX, signY, signZ);
	float materialIndex = 0;

	//这个nodeAddr从哪里来？
	while (nodeAddr != EntrypointSentinel)
	{
		while (uint(nodeAddr) < uint(EntrypointSentinel))
		{
			BVHNode curNode = BVHTree[nodeAddr];
			int4 cnodes = asint(curNode.cids);

			
			float tMin = 0;
			//bool traverseChild0 = BoundRayIntersect(ray, curNode.b0xy, curNode.b01z.xy, invDir, signs, tMin);
			bool traverseChild0 = BoundRayIntersect(ray, invDir, curNode.b0min, curNode.b0max, tMin);
			//if (tMin < 0)
			//	tMin = 0;
			float tMin1 = 0;
			bool traverseChild1 = BoundRayIntersect(ray, invDir, curNode.b1min, curNode.b1max, tMin1);
			//if (tMin1 < 0)
			//	tMin1 = 0;

			bool swp = (tMin1 < tMin);

			if (!traverseChild0 && !traverseChild1)
			{
				nodeAddr = traversalStack[stackIndex];
				stackIndex--;
				//return false;
			}
			// Otherwise => fetch child pointers.
			else
			{
				nodeAddr = (traverseChild0) ? cnodes.x : cnodes.y;
				//primitivesNum = (traverseChild0) ? cnodes.z : cnodes.w;
				//primitivesNum2 = (traverseChild0) ? cnodes.w : cnodes.z;
				// Both children were intersected => push the farther one.
				if (traverseChild0 && traverseChild1)
				{
					if (swp)
					{
						//swap(nodeAddr, cnodes.y);
						int tmp = nodeAddr;
						nodeAddr = cnodes.y;
						cnodes.y = tmp;
						//tmp = primitivesNum;
						//primitivesNum = primitivesNum2;
						//primitivesNum2 = tmp;
					}
					//stackPtr += 4;
					//stackIndex++;
					//*(int*)stackPtr = cnodes.y;
					traversalStack[++stackIndex] = cnodes.y;
				}
			}

			// First leaf => postpone and continue traversal.
			if (nodeAddr < 0 && leafAddr >= 0)     // Postpone max 1
			{
				//leafAddr2= leafAddr;          // postpone 2
				leafAddr = nodeAddr;            //leafAddr成了当前要处理的节点
				nodeAddr = traversalStack[stackIndex];  //出栈，nodeAddr这个时候是下一个要访问的node
				stackIndex--;
			}

			if (!(leafAddr >= 0))   //leaf node小于0，需要处理叶子节点，退出循环
			{
				break;
			}
		}

		//遍历叶子
		while (leafAddr < 0)
		{
			for (int triAddr = ~leafAddr; /*triAddr < ~leafAddr + primitivesNum * 3*/; triAddr += 3)
			{
				const float4 m0 = WoodTriangles[triAddr];     //matrix row 0 

				if (asint(m0.x) == 0x7FFFFFFF)
					break;

				const float4 m1 = WoodTriangles[triAddr + 1]; //matrix row 1 
				const float4 m2 = WoodTriangles[triAddr + 2]; //matrix row 2

				float3 normal = normalize(cross(m0.xyz, m1.xyz));
				if (dot(normal, ray.direction.xyz) >= 0)
				{
					//三角形背面
					continue;
				}

				float2 uv = 0;
				float triangleHit = 0;
				bool hitTriangle = WoodTriangleRayIntersect(ray.orig.xyz, ray.direction.xyz, m0, m1, m2, tmin, ray.tmax, uv, triangleHit);
				if (hitTriangle)
				{
					hitT = triangleHit;
					hitIndex = triAddr;
					int vertexIndex0 = WoodTriangleIndices[triAddr];
					int vertexIndex1 = WoodTriangleIndices[triAddr + 1];
					int vertexIndex2 = WoodTriangleIndices[triAddr + 2];
					const float3 v0 = Vertices[vertexIndex0].position;
					const float3 v1 = Vertices[vertexIndex1].position;
					const float3 v2 = Vertices[vertexIndex2].position;
					float4 hitPos = float4(v0 * uv.x + v1 * uv.y + v2 * (1.0 - uv.x - uv.y), 1);

					//hitPos.xyz = offset_ray(hitPos.xyz, normal);
					hitPos.w = hitT;
					//materialIndex = v0.w;

					const float2 uv0 = Vertices[vertexIndex0].uv;
					const float2 uv1 = Vertices[vertexIndex1].uv;
					const float2 uv2 = Vertices[vertexIndex2].uv;

					const float3 normal0 = Vertices[vertexIndex0].normal;
					const float3 normal1 = Vertices[vertexIndex1].normal;
					const float3 normal2 = Vertices[vertexIndex2].normal;

					interaction.normal.xyz = normalize(normal0 * uv.x + normal1 * uv.y + normal2 * (1.0 - uv.x - uv.y));
					interaction.p = hitPos.xyz;
					interaction.uv = uv0 * uv.x + uv1 * uv.y + uv2 * (1.0 - uv.x - uv.y);
					//interaction.vertexIndices = int3(vertexIndex0, vertexIndex1, vertexIndex2);
				}
			} // triangle

			// Another leaf was postponed => process it as well.
			leafAddr = nodeAddr;
			if (nodeAddr < 0)
			{
				nodeAddr = traversalStack[stackIndex--];
				//primitivesNum = primitivesNum2;
			}
		} // leaf
	}

	interaction.materialID = materialIndex;
	return hitIndex != -1;
}

bool IntersectMeshBVH(Ray ray, int bvhOffset, float4x4 objectToWorld, float4x4 worldToObject, int meshIndexStart, out float hitT, out int hitIndex, out Interaction interaction, bool anyHit)
{
	interaction = (Interaction)0;
	int INVALID_INDEX = EntrypointSentinel;

	//GPURay TempRay = new GPURay();
	int traversalStack[64];
	traversalStack[0] = INVALID_INDEX;
	int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).

	//instBVHOffset >= m_nodes.Count说明没有inst
	int nodeAddr = bvhOffset;

	float3 rayOrig = ray.orig;
	float3 rayDir = ray.direction;
	float tmin = ray.tmin;
	hitT = ray.tmax;

	float ooeps = pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number

	float3 invDir = float3(1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps),
		1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps),
		1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps)
		);
	int signX = sign(invDir.x);
	int signY = sign(invDir.y);
	int signZ = sign(invDir.z);
	signX = signX < 0 ? 1 : 0;
	signY = signY < 0 ? 1 : 0;
	signZ = signZ < 0 ? 1 : 0;
	int3 signs = int3(signX, signY, signZ);
	int stackIndex = 0;
	hitIndex = -1;

	while (nodeAddr != INVALID_INDEX)
	{
		while ((uint)nodeAddr < (uint)INVALID_INDEX)
		{
			BVHNode curNode = BVHTree[nodeAddr];
			int4 cnodes = asint(curNode.cids);

			//left child ray-bound intersection test
			float tMin = tmin;

			bool traverseChild0 = BoundRayIntersect(ray, invDir, curNode.b0min, curNode.b0max, tMin);
			//if (tMin < 0) tMin = 0;
			//right child ray-bound intersection test
			float tMin1 = tmin;

			bool traverseChild1 = BoundRayIntersect(ray, invDir, curNode.b1min, curNode.b1max, tMin1);
			//if (tMin1 < 0) tMin1 = 0;

			if (!traverseChild0 && !traverseChild1)
			{
				nodeAddr = traversalStack[stackIndex];
				stackIndex--;
			}
			// Otherwise => fetch child pointers.
			else
			{
				nodeAddr = (traverseChild0) ? cnodes.x : cnodes.y;
				tmin = (traverseChild0) ? tMin : tMin1;
				//primitivesNum = (traverseChild0) ? cnodes.z : cnodes.w;
				//primitivesNum2 = (traverseChild0) ? cnodes.w : cnodes.z;
				//if (!swp)
				//	swp = primitivesNum2 > 0 && primitivesNum == 0;
				// Both children were intersected => push the farther one.
				if (traverseChild0 && traverseChild1)
				{
					bool swp = (tMin1 < tMin);
					if (swp)
					{
						//swap(nodeAddr, cnodes.y);
						int tmp = nodeAddr;
						nodeAddr = cnodes.y;
						cnodes.y = tmp;
					}
					traversalStack[++stackIndex] = cnodes.y;
					tmin = min(tMin1, tMin);
				}
			}

			// First leaf => postpone and continue traversal.
			if (nodeAddr < 0 && leafAddr >= 0)     // Postpone max 1
			{
				//leafAddr2= leafAddr;          // postpone 2
				leafAddr = nodeAddr;            //leafAddr成了当前要处理的节点
				nodeAddr = traversalStack[stackIndex];  //出栈，nodeAddr这个时候是下一个要访问的node
				stackIndex--;
			}

			if (!(leafAddr >= 0))   //leaf node小于0，需要处理叶子节点，退出循环
				break;
		}

		//遍历叶子
		while (leafAddr < 0)
		{
			int triangleIndex = 0;
			for (int triAddr = ~leafAddr; ; triAddr += 3)
			{
				float4 m0 = WoodTriangles[triAddr];     //matrix row 0 

				if (asint(m0.x) == 0x7fffffff)
					break;

				float4 m1 = WoodTriangles[triAddr + 1]; //matrix row 1 
				float4 m2 = WoodTriangles[triAddr + 2]; //matrix row 2

				//Oz is a point, must plus w
				//float Oz = m2.w + dot(rayOrig.xyz, m2.xyz);//origx * m2.x + origy * m2.y + origz * m2.z;
														   //Dz is a vector
				//float invDz = 1.0f / dot(rayDir.xyz, m2.xyz);//(dirx * m2.x + diry * m2.y + dirz * m2.z);
				//float t = -Oz * invDz;

				//this is the correct local normal, the same as cross(v1- v0, v2 - v0)
				float3 normal = normalize(cross(m0.xyz, m1.xyz)).xyz;

				
				//local normal
				//normal = normalize(cross(v1.xyz - v0.xyz, v2.xyz - v0.xyz));
				//if (dot(normal, rayDir.xyz) >= 0)
				//{
				//	triangleIndex++;
				//	continue;
				//}

				float2 uv = 0;
				float triangleHit = 0;
				bool hitTriangle = WoodTriangleRayIntersect(rayOrig.xyz, rayDir.xyz, m0, m1, m2, ray.tmin, ray.tmax, uv, triangleHit);
				if (hitTriangle && (hitT > triangleHit/* && triangleHit >= tmin*/))
				{
					hitT = triangleHit;
					hitIndex = triAddr;
					int vertexIndex0 = WoodTriangleIndices[triAddr];
					int vertexIndex1 = WoodTriangleIndices[triAddr + 1];
					int vertexIndex2 = WoodTriangleIndices[triAddr + 2];
					Vertex vertex0 = Vertices[vertexIndex0];
					Vertex vertex1 = Vertices[vertexIndex1];
					Vertex vertex2 = Vertices[vertexIndex2];
					const float3 v0 = vertex0.position;
					const float3 v1 = vertex1.position;
					const float3 v2 = vertex2.position;
					float4 hitPos = float4(v0 * uv.x + v1 * uv.y + v2 * (1.0 - uv.x - uv.y), 1);
					//hitPos.w = 1;
					hitPos = mul(objectToWorld, hitPos);

					float3 p0 = mul(objectToWorld, float4(v0.xyz, 1.0)).xyz;
					float3 p1 = mul(objectToWorld, float4(v1.xyz, 1.0)).xyz;
					float3 p2 = mul(objectToWorld, float4(v2.xyz, 1.0)).xyz;
					float triAreaInWorld = length(cross(p0 - p1, p0 - p2)) * 0.5;

					float3 normal0 = vertex0.normal;
					float3 normal1 = vertex1.normal;
					float3 normal2 = vertex2.normal;

					normal = normalize(normal0 * uv.x + normal1 * uv.y + normal2 * (1.0 - uv.x - uv.y));

					float3 worldNormal = normalize(mul(normal, (float3x3)worldToObject));

					//float4 v0World = mul(objectToWorld, float4(v0.xyz, 1));
					//float4 v1World = mul(objectToWorld, float4(v1.xyz, 1));
					//float4 v2World = mul(objectToWorld, float4(v2.xyz, 1));

					//worldNormal = normalize(cross(v1World.xyz - v0World.xyz, v2World.xyz - v0World.xyz));

					//hitPos.xyz = offset_ray(hitPos.xyz, worldNormal);
					//hitPos.w = hitT;
					//materialIndex = v0.w;

					const float2 uv0 = vertex0.uv;
					const float2 uv1 = vertex1.uv;
					const float2 uv2 = vertex2.uv;


					//return true;
					//if (anyHit)
					//{
					//	nodeAddr = EntrypointSentinel;
					//	break;
					//}
					//计算法线
					//change into worldnormal
					//interaction.vertexIndices = int3(vertexIndex0, vertexIndex1, vertexIndex2);
					interaction.normal.xyz = worldNormal;
					interaction.p.xyz = hitPos.xyz;//offset_ray(hitPos.xyz, worldNormal);
					interaction.hitT = hitT;
					interaction.uv = uv0 * uv.x + uv1 * uv.y + uv2 * (1.0 - uv.x - uv.y);

					//interaction.tangent = normalize(v0World.xyz - hitPos.xyz);
					//interaction.bitangent = normalize(cross(interaction.normal.xyz, interaction.tangent));
					float3 dpdu = float3(1, 0, 0);
					float3 dpdv = float3(0, 1, 0);
					CoordinateSystem(worldNormal, dpdu, dpdv);
					interaction.tangent.xyz = normalize(dpdu.xyz);
					interaction.bitangent.xyz = normalize(cross(interaction.tangent.xyz, worldNormal));
					interaction.primArea = triAreaInWorld;
					interaction.triangleIndex = triangleIndex;
					interaction.uvArea = length(cross(float3(uv2, 1) - float3(uv0, 1), float3(uv1, 1) - float3(uv0, 1)));

					float4 v0Screen = mul(WorldToRaster, float4(p0, 1));
					float4 v1Screen = mul(WorldToRaster, float4(p1, 1));
					float4 v2Screen = mul(WorldToRaster, float4(p2, 1));
					v0Screen /= v0Screen.w;
					v1Screen /= v1Screen.w;
					v2Screen /= v2Screen.w;
					interaction.screenSpaceArea = length(cross(v2Screen.xyz - v0Screen.xyz, v1Screen.xyz - v0Screen.xyz));
					//计算切线
					/*
					float3 dp02 = v0.xyz - v2.xyz;
					float3 dp12 = v1.xyz - v2.xyz;
					float2 duv02 = uv0.xy - uv2.xy;
					float2 duv12 = uv1.xy - uv2.xy;
					float determinant = duv02[0] * duv12[1] - duv02[1] * duv12[0];
					bool degenerateUV = abs(determinant) < 1e-8;
					if (!degenerateUV)
					{
						float invdet = 1.0 / determinant;
						interaction.dpdu.xyz = (duv12[1] * dp02 - duv02[1] * dp12) * invdet;
						interaction.dpdv.xyz = (-duv12[0] * dp02 + duv02[0] * dp12) * invdet;
					}
					else
					{
						CoordinateSystem(normal, interaction.dpdu.xyz, interaction.dpdv.xyz);
					}

					interaction.tangent.xyz = normalize(interaction.dpdu.xyz);
					interaction.bitangent.xyz = normalize(cross(interaction.tangent.xyz, normal));
					*/
					if (anyHit)
						return true;
				}
				triangleIndex++;
			} // triangle

			// Another leaf was postponed => process it as well.
			leafAddr = nodeAddr;
			if (nodeAddr < 0)
			{
				nodeAddr = traversalStack[stackIndex--];
				//primitivesNum = primitivesNum2;
			}
		} // leaf
	}

	return hitIndex != -1;
}

//bvhOffset x-left child bvh offset y-right child bvh offset
bool IntersectBVH(Ray ray, out Interaction interaction, bool anyHit)
{
	interaction = (Interaction)0;
	Interaction tmpInteraction;
	float3 rayOrig = ray.orig;
	float3 rayDir = ray.direction;
	int traversalStack[STACK_SIZE];
	//int meshInstanceStack[STACK_SIZE];
	traversalStack[0] = EntrypointSentinel;
	//meshInstanceStack[0] = -1;
	//int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).
	uint nodeAddr = instBVHAddr >= bvhNodesNum ? 0 : instBVHAddr;
	//int primitivesNum = 0;   //当前节点的primitives数量
	//int primitivesNum2 = 0;
	//int triIdx = 0;
	float tmin = ray.tmin;
	float hitT = ray.tmax;  //tmax         // Ray origin.
	float ooeps = pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number

	float3 invDir = float3(1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps),
		1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps),
		1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps)
		);

	//float oodx = rayOrig.x * idirx;  // ray origin / ray direction
	//float oody = rayOrig.y * idiry;  // ray origin / ray direction
	//float oodz = rayOrig.z * idirz;  // ray origin / ray direction
	int   stackIndex = 0;   //当前traversalStack的索引号
	int   hitIndex = -1;
	int   hitBVHNode = -1;
	int signX = sign(invDir.x);
	int signY = sign(invDir.y);
	int signZ = sign(invDir.z);
	signX = signX < 0 ? 1 : 0;
	signY = signY < 0 ? 1 : 0;
	signZ = signZ < 0 ? 1 : 0;
	int3 signs = int3(signX, signY, signZ);
	//int meshInstanceIndex = 0;

	if (nodeAddr == 0)
	{
		MeshInstance meshInstance = MeshInstances[0];
		Ray rayTemp = TransformRay(meshInstance.worldToLocal, ray);
		//invDir = GetInverseDirection(ray.direction);
		float bvhHit = hitT;
		int meshHitTriangleIndex = -1;
		if (IntersectMeshBVH(rayTemp, 0, meshInstance.localToWorld, meshInstance.worldToLocal, meshInstance.triangleStartOffset, bvhHit, meshHitTriangleIndex, tmpInteraction, anyHit))
		{
			if (bvhHit < hitT)
			{
				hitT = bvhHit;
				hitIndex = meshHitTriangleIndex;
				tmpInteraction.materialID = meshInstance.GetMaterialID();
				tmpInteraction.meshInstanceID = 0;
				tmpInteraction.wo.xyz = -ray.direction;
				hitBVHNode == 0;
				interaction = tmpInteraction;
			}
		}
	}
	else
	{
		while (nodeAddr != EntrypointSentinel)
		{

			BVHNode curNode = BVHTree[nodeAddr];
			int4 cnodes = asint(curNode.cids);

			float t0 = tmin;
			//bool traverseChild0 = RayBoundIntersect(ray.orig.xyz, curNode.b0xy, curNode.b01z.xy, invDir.x, invDir.y, invDir.z, hitT, t0);
			//bool traverseChild0 = BoundRayIntersect(ray, curNode.b0xy, curNode.b01z.xy, invDir, signs, t0);
			bool traverseChild0 = BoundRayIntersect(ray, invDir, curNode.b0min, curNode.b0max, t0);
			if (t0 < 0) t0 = 0;

			float t1 = tmin;
			//bool traverseChild1 = RayBoundIntersect(ray.orig.xyz, curNode.b1xy, curNode.b01z.zw, invDir.x, invDir.y, invDir.z, hitT, t1);
			//bool traverseChild1 = BoundRayIntersect(ray, curNode.b1xy, curNode.b01z.zw, invDir, signs, t1);
			bool traverseChild1 = BoundRayIntersect(ray, invDir, curNode.b1min, curNode.b1max, t1);
			if (t1 < 0) t1 = 0;

			int2 next = GetNodeChildIndex(curNode.cids); //new Vector2Int(INVALID_INDEX, INVALID_INDEX);
			int2 nextMeshInstanceIds = nodeAddr >= instBVHAddr ? GetTopLevelLeaveMeshInstance(curNode.cids) : int2(-1, -1);
			if (!traverseChild0)
			{
				next.x = EntrypointSentinel;
				nextMeshInstanceIds.x = -1;
			}
			if (!traverseChild1)
			{
				next.y = EntrypointSentinel;
				nextMeshInstanceIds.y = -1;
			}

			bool swp = false;
			//3 cases after boundrayintersect
			if (traverseChild0 && traverseChild1)
			{
				//ray.SetTMin(min(t1, t0));
				//两个都命中
				swp = (t1 < t0);
				if (swp)
				{
					next = int2(next.y, next.x);
					nextMeshInstanceIds = int2(nextMeshInstanceIds.y, nextMeshInstanceIds.x);
				}

				bool curNodeIsX = false;
				//next.y入栈
				if (next.x >= instBVHAddr)
				{
					nodeAddr = next.x;
					curNodeIsX = true;
				}
				else
				{
					if (next.y >= instBVHAddr)
						nodeAddr = next.y;
					else
						nodeAddr = traversalStack[stackIndex--];
				}
				//这里入栈的可能是bottomlevel bvh leaf
				if (next.y >= instBVHAddr && curNodeIsX)
					traversalStack[++stackIndex] = next.y;

			}
			else if (!traverseChild0 && !traverseChild1)
			{
				//两个都不命中
				//meshInstanceIndex = nodeAddr >= instBVHOffset ? meshInstanceStack[stackIndex + 1] : meshInstanceIndex;
				nodeAddr = traversalStack[stackIndex--];

			}
			else
			{
				//只有其中一个命中
				if (nodeAddr >= instBVHAddr)
				{
					//meshInstanceIndex = traverseChild0 ? nextMeshInstanceIds.x : nextMeshInstanceIds.y;
					int nextNode = traverseChild0 ? next.x : next.y;
					//ray.SetTMin(traverseChild0 ? t0 : t1);
					if (nextNode >= instBVHAddr)
					{
						nodeAddr = nextNode;
					}
					else
						nodeAddr = traversalStack[stackIndex--];
				}
			}


			for (int i = 0; i < 2; ++i)
			{
				//如果next是bottom level bvh node
				if (0 <= next[i] && next[i] < instBVHAddr)
				{
					MeshInstance meshInstance = MeshInstances[nextMeshInstanceIds[i]];
					Ray rayTemp = TransformRay(meshInstance.worldToLocal, ray);
					//invDir = GetInverseDirection(ray.direction);
					float bvhHit = hitT;
					int meshHitTriangleIndex = -1;
					if (IntersectMeshBVH(rayTemp, next[i], meshInstance.localToWorld, meshInstance.worldToLocal, meshInstance.triangleStartOffset, bvhHit, meshHitTriangleIndex, tmpInteraction, anyHit))
					{
						if (bvhHit < hitT)
						{
							tmpInteraction.materialID = meshInstance.GetMaterialID();
							tmpInteraction.meshInstanceID = nextMeshInstanceIds[i];
							tmpInteraction.wo.xyz = -ray.direction;
							hitT = bvhHit;
							hitIndex = meshHitTriangleIndex;
							hitBVHNode = next[i];
							interaction = tmpInteraction;

							if (anyHit)
								return true;
						}
					}
				}
			}
		}
	}
	return hitIndex > -1;
}

/*
bool IntersectMeshBVHP(Ray ray, int bvhOffset, out float hitT, out int hitIndex)
{
	int INVALID_INDEX = EntrypointSentinel;

	//GPURay TempRay = new GPURay();
	int traversalStack[64];
	traversalStack[0] = INVALID_INDEX;
	int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).

	//instBVHOffset >= m_nodes.Count说明没有inst
	int nodeAddr = bvhOffset;
	float3 rayOrig = ray.orig;
	float3 rayDir = ray.direction;
	float tmin = ray.tmin;
	hitT = ray.tmax;

	float ooeps = pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number

	float3 invDir = float3(1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps),
		1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps),
		1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps)
		);
	int signX = sign(invDir.x);
	int signY = sign(invDir.y);
	int signZ = sign(invDir.z);
	signX = signX < 0 ? 1 : 0;
	signY = signY < 0 ? 1 : 0;
	signZ = signZ < 0 ? 1 : 0;
	int3 signs = int3(signX, signY, signZ);
	int stackIndex = 0;
	hitIndex = -1;

	while (nodeAddr != INVALID_INDEX)
	{
		while ((uint)nodeAddr < (uint)INVALID_INDEX)
		{
			BVHNode curNode = BVHTree[nodeAddr];
			int4 cnodes = asint(curNode.cids);

			//left child ray-bound intersection test
			float tMin = 0;
			//bool traverseChild0 = RayBoundIntersect(rayOrig, curNode.b0xy, curNode.b01z.xy, invDir.x, invDir.y, invDir.z, hitT, tMin);
			//bool traverseChild0 = BoundRayIntersect(ray, curNode.b0xy, curNode.b01z.xy, invDir, signs, tMin);
			bool traverseChild0 = BoundRayIntersect(ray, invDir, curNode.b0min, curNode.b0max, tMin);
			//right child ray-bound intersection test
			float tMin1 = 0;
			//bool traverseChild1 = RayBoundIntersect(rayOrig, curNode.b1xy, curNode.b01z.zw, invDir.x, invDir.y, invDir.z, hitT, tMin1);
			//bool traverseChild1 = BoundRayIntersect(ray, curNode.b1xy, curNode.b01z.zw, invDir, signs, tMin1);
			bool traverseChild1 = BoundRayIntersect(ray, invDir, curNode.b1min, curNode.b1max, tMin1);

			bool swp = (tMin1 < tMin);

			//tmin = min(tMin1, tMin);

			if (!traverseChild0 && !traverseChild1)
			{
				nodeAddr = traversalStack[stackIndex];
				stackIndex--;
			}
			// Otherwise => fetch child pointers.
			else
			{
				nodeAddr = (traverseChild0) ? cnodes.x : cnodes.y;

				if (traverseChild0 && traverseChild1)
				{
					if (swp)
					{
						//swap(nodeAddr, cnodes.y);
						int tmp = nodeAddr;
						nodeAddr = cnodes.y;
						cnodes.y = tmp;
					}
					traversalStack[++stackIndex] = cnodes.y;
				}
			}

			// First leaf => postpone and continue traversal.
			if (nodeAddr < 0 && leafAddr >= 0)     // Postpone max 1
			{
				//leafAddr2= leafAddr;          // postpone 2
				leafAddr = nodeAddr;            //leafAddr成了当前要处理的节点
				nodeAddr = traversalStack[stackIndex];  //出栈，nodeAddr这个时候是下一个要访问的node
				stackIndex--;
			}

			if (!(leafAddr >= 0))   //leaf node小于0，需要处理叶子节点，退出循环
				break;
		}

		//遍历叶子
		while (leafAddr < 0)
		{
			for (int triAddr = ~leafAddr; ; triAddr += 3)
			{
				float4 m0 = WoodTriangles[triAddr];     //matrix row 0 

				if (asint(m0.x) == 0x7fffffff)
					break;

				float4 m1 = WoodTriangles[triAddr + 1]; //matrix row 1 
				float4 m2 = WoodTriangles[triAddr + 2]; //matrix row 2

				//Oz is a point, must plus w
				//float Oz = m2.w + dot(rayOrig.xyz, m2.xyz);//origx * m2.x + origy * m2.y + origz * m2.z;
														   //Dz is a vector
				//float invDz = 1.0f / dot(rayDir.xyz, m2.xyz);//(dirx * m2.x + diry * m2.y + dirz * m2.z);
				//float t = -Oz * invDz;

				float3 normal = normalize(cross(m0, m1)).xyz;
				//if (dot(normal, rayDir.xyz) >= 0)
				//{
				//	continue;
				//}

				float2 uv = 0;
				float triangleHit = 0;
				bool hitTriangle = WoodTriangleRayIntersect(rayOrig, rayDir, m0, m1, m2, ray.tmin, ray.tmax, uv, triangleHit);
				if (hitTriangle)
				{
					hitT = triangleHit;
					hitIndex = triAddr;
					//int vertexIndex0 = WoodTriangleIndices[triAddr];
					//int vertexIndex1 = WoodTriangleIndices[triAddr + 1];
					//int vertexIndex2 = WoodTriangleIndices[triAddr + 2];
					//const float4 v0 = Vertices[vertexIndex0].position;
					//const float4 v1 = Vertices[vertexIndex1].position;
					//const float4 v2 = Vertices[vertexIndex2].position;
					//hitIndex = vertexIndex0 / 3;
				}

			} // triangle

			// Another leaf was postponed => process it as well.
			leafAddr = nodeAddr;
			if (nodeAddr < 0)
			{
				nodeAddr = traversalStack[stackIndex--];
			}
		} // leaf
	}

	return hitIndex != -1;
}

bool IntersectP(Ray ray, out float hitT, out int meshInstanceIndex)
{
	meshInstanceIndex = -1;
	float3 rayOrig = ray.orig.xyz;
	float3 rayDir = ray.direction.xyz;
	int traversalStack[STACK_SIZE];
	//int meshInstanceStack[STACK_SIZE];
	traversalStack[0] = EntrypointSentinel;
	//meshInstanceStack[0] = -1;
	//int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).
	int nodeAddr = instBVHAddr >= bvhNodesNum ? 0 : instBVHAddr;
	//int primitivesNum = 0;   //当前节点的primitives数量
	//int primitivesNum2 = 0;
	//int triIdx = 0;
	float tmin = ray.tmin;
	hitT = ray.tmax;  //tmax         // Ray origin.
	float ooeps = pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number

	float3 invDir = float3(1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps),
		1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps),
		1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps)
		);

	//float oodx = rayOrig.x * idirx;  // ray origin / ray direction
	//float oody = rayOrig.y * idiry;  // ray origin / ray direction
	//float oodz = rayOrig.z * idirz;  // ray origin / ray direction
	int   stackIndex = 0;   //当前traversalStack的索引号
	int   hitIndex = -1;
	int   hitBVHNode = -1;
	int signX = sign(invDir.x);
	int signY = sign(invDir.y);
	int signZ = sign(invDir.z);
	signX = signX < 0 ? 1 : 0;
	signY = signY < 0 ? 1 : 0;
	signZ = signZ < 0 ? 1 : 0;
	int3 signs = int3(signX, signY, signZ);
	//int meshInstanceIndex = 0;

	if (nodeAddr == 0)
	{
		MeshInstance meshInstance = MeshInstances[0];
		Ray rayTemp = TransformRay(meshInstance.worldToLocal, ray);
		//rayTemp.orig.xyz += rayTemp.direction.xyz * ShadowEpsilon;
		//rayTemp.orig.w -= rayTemp.direction.xyz * ShadowEpsilon;
		//invDir = GetInverseDirection(ray.direction);
		float bvhHit = hitT;
		int meshHitTriangleIndex = -1;
		if (IntersectMeshBVHP(rayTemp, 0, bvhHit, meshHitTriangleIndex))
		{
			if (bvhHit < hitT)
			{
				hitT = bvhHit;
				hitIndex = meshHitTriangleIndex;
				hitBVHNode == 0;
				meshInstanceIndex = 0;
			}
		}
	}
	else
	{
		while (nodeAddr != EntrypointSentinel)
		{

			BVHNode curNode = BVHTree[nodeAddr];
			int4 cnodes = asint(curNode.cids);

			float t0 = 0;

			bool traverseChild0 = BoundRayIntersect(ray, invDir, curNode.b0min, curNode.b0max, t0);
			//if (t0 < 0) t0 = 0;

			float t1 = 0;

			bool traverseChild1 = BoundRayIntersect(ray, invDir, curNode.b1min, curNode.b1max, t1);
			//if (t1 < 0) t1 = 0;

			int2 next = GetNodeChildIndex(curNode.cids); //new Vector2Int(INVALID_INDEX, INVALID_INDEX);
			int2 nextMeshInstanceIds = nodeAddr >= instBVHAddr ? GetTopLevelLeaveMeshInstance(curNode.cids) : int2(-1, -1);
			if (!traverseChild0)
			{
				next.x = EntrypointSentinel;
				nextMeshInstanceIds.x = -1;
			}
			if (!traverseChild1)
			{
				next.y = EntrypointSentinel;
				nextMeshInstanceIds.y = -1;
			}

			bool swp = false;
			//3 cases after boundrayintersect
			if (traverseChild0 && traverseChild1)
			{
				//两个都命中
				swp = (t1 < t0);
				if (swp)
				{
					next = int2(next.y, next.x);
					nextMeshInstanceIds = int2(nextMeshInstanceIds.y, nextMeshInstanceIds.x);
				}

				bool curNodeIsX = false;
				//next.y入栈
				if (next.x >= instBVHAddr)
				{
					nodeAddr = next.x;
					curNodeIsX = true;
				}
				else
				{
					if (next.y >= instBVHAddr)
						nodeAddr = next.y;
					else
						nodeAddr = traversalStack[stackIndex--];
				}
				//这里入栈的可能是bottomlevel bvh leaf
				if (next.y >= instBVHAddr && curNodeIsX)
					traversalStack[++stackIndex] = next.y;
				//ray.SetTMin(min(t0, t1));
			}
			else if (!traverseChild0 && !traverseChild1)
			{
				//两个都不命中
				//meshInstanceIndex = nodeAddr >= instBVHOffset ? meshInstanceStack[stackIndex + 1] : meshInstanceIndex;
				nodeAddr = traversalStack[stackIndex--];

			}
			else
			{
				//只有其中一个命中
				if (nodeAddr >= instBVHAddr)
				{
					//meshInstanceIndex = traverseChild0 ? nextMeshInstanceIds.x : nextMeshInstanceIds.y;
					int nextNode = traverseChild0 ? next.x : next.y;
					if (nextNode >= instBVHAddr)
					{
						nodeAddr = nextNode;
					}
					else
						nodeAddr = traversalStack[stackIndex--];

					//ray.SetTMin(traverseChild0 ? t0 : t1);
				}
			}


			for (int i = 0; i < 2; ++i)
			{
				//如果next是bottom level bvh node
				if (0 <= next[i] && next[i] < instBVHAddr)
				{
					MeshInstance meshInstance = MeshInstances[nextMeshInstanceIds[i]];
					Ray rayTemp = TransformRay(meshInstance.worldToLocal, ray);
					//rayTemp.orig.xyz += rayTemp.direction.xyz * ShadowEpsilon;
					//rayTemp.orig.w -= normalize(rayTemp.direction.xyz) * ShadowEpsilon;
					//invDir = GetInverseDirection(ray.direction);
					float bvhHit = hitT;
					int meshHitTriangleIndex = -1;
					if (IntersectMeshBVHP(rayTemp, next[i], bvhHit, meshHitTriangleIndex))
					{
						if (bvhHit < hitT)
						{
							hitT = bvhHit;
							hitIndex = meshHitTriangleIndex;
							hitBVHNode = next[i];
							meshInstanceIndex = nextMeshInstanceIds[i];
						}
					}
				}
			}
		}
	}
	return hitIndex > -1;
}
*/

bool ClosestHit(Ray ray, out Interaction interaction)
{
	bool hitted = true;
	while (true)
	{
		hitted = IntersectBVH(ray, interaction, false);
		if (!hitted)
			break;
		else
		{
			if (interaction.materialID == -1)
			{
				ray = SpawnRay(interaction.p, ray.direction, -interaction.normal, FLT_MAX);
			}
			else
			{
				//alphatest check must be implemented
				break;
			}
		}
	}
	
	return hitted;
}

bool AnyHit(Ray ray, out Interaction interaction)
{
	bool hitted = true;
	while (true)
	{
		hitted = IntersectBVH(ray, interaction, true);
		if (!hitted)
			break;
		else
		{
			if (interaction.materialID == -1)
			{
				ray = SpawnRay(interaction.p, ray.direction, -interaction.normal, FLT_MAX);
			}
			else
			{
				//alphatest check must be implemented
				break;
			}
		}
	}

	return hitted;
}


bool ShadowRayVisibilityTest(ShadowRay shadowRay, float3 normal)
{
	Ray ray = SpawnRay(shadowRay.p0, shadowRay.p1 - shadowRay.p0, normal, 1.0 - ShadowEpsilon);
	Interaction isect = (Interaction)0;
	return !AnyHit(ray, isect);

	//!IntersectP(ray, hitT, meshInstanceIndex);
}

#endif
