
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

float BoundRayIntersect(in Ray r, in float3 invdir, in float3 bmin, in float3 bmax/*, out float hitTMin*/)
{
	float3 f = (bmax - r.orig) * invdir;
	float3 n = (bmin - r.orig) * invdir;

	float3 tmax = max(f, n);
	float3 tmin = min(f, n);

	float t1 = min(min(tmax.x, min(tmax.y, tmax.z)), r.tmax);
	float t0 = max(max(tmin.x, max(tmin.y, tmin.z)), r.tmin);
	//hitTMin = min(t0, t1);
	return (t1 >= t0) ? (t0 > 0.f ? t0 : t1) : -1.0;
}

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

int2 GetTLASNodeChild(float4 cids)
{
	return int2(asint(cids.xz));
}

int2 GetTopLevelLeaveMeshInstance(float4 cids)
{
	return asint(cids.yw);//int2(asint(cids.z), asint(cids.w));
}


bool IntersectMeshBVH(Ray ray, int bvhOffset, out HitInfo hitInfo, bool anyHit)
{
	//interaction = (Interaction)0;
	hitInfo = (HitInfo)0;
	hitInfo.triAddr = -1;
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
	float hitT = ray.tmax;

	//float ooeps = pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number

	//float3 invDir = float3(1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps),
	//	1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps),
	//	1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps)
	//	);
	float3 invDir = 1.0 / rayDir;

	int stackIndex = 0;
	int hitIndex = -1;

	while (nodeAddr != INVALID_INDEX)
	{
		while ((uint)nodeAddr < (uint)INVALID_INDEX)
		{
			BVHNode curNode = BVHTree[nodeAddr];
			int4 cnodes = asint(curNode.cids);

			//left child ray-bound intersection test
			//float tMin = tmin;
			float tLeft = BoundRayIntersect(ray, invDir, curNode.b0min, curNode.b0max);
			bool traverseChild0 = tLeft > 0;
			//float tMin1 = tmin;

			float tRight = BoundRayIntersect(ray, invDir, curNode.b1min, curNode.b1max);
			bool traverseChild1 = tRight > 0;
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
				tmin = (traverseChild0) ? tLeft : tRight;
				//primitivesNum = (traverseChild0) ? cnodes.z : cnodes.w;
				//primitivesNum2 = (traverseChild0) ? cnodes.w : cnodes.z;
				//if (!swp)
				//	swp = primitivesNum2 > 0 && primitivesNum == 0;
				// Both children were intersected => push the farther one.
				if (traverseChild0 && traverseChild1)
				{
					bool swp = (tRight < tLeft);
					if (swp)
					{
						//swap(nodeAddr, cnodes.y);
						int tmp = nodeAddr;
						nodeAddr = cnodes.y;
						cnodes.y = tmp;
					}
					traversalStack[++stackIndex] = cnodes.y;
					tmin = min(tRight, tLeft);
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
					hitInfo.hitT = hitT;
					hitInfo.triAddr = triAddr;
					hitInfo.baryCoord = uv;
					hitInfo.triangleIndexInMesh = triangleIndex;
					hitIndex = triAddr;

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
bool IntersectBVH(Ray ray, out HitInfo hitInfo, bool anyHit)
{
	//interaction = (Interaction)0;
	//Interaction tmpInteraction;
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

	if (nodeAddr == 0)
	{
		MeshInstance meshInstance = MeshInstances[0];
		Ray rayTemp = TransformRay(meshInstance.worldToLocal, ray);
		//invDir = GetInverseDirection(ray.direction);
		float bvhHit = hitT;
		int meshHitTriangleIndex = -1;
		
		hitInfo.triAddr = -1;
		hitInfo.hitT = hitT;
		if (IntersectMeshBVH(rayTemp, 0, /*meshInstance.localToWorld, meshInstance.worldToLocal, bvhHit, meshHitTriangleIndex,*/ hitInfo, anyHit))
		{
			if (hitInfo.hitT < hitT)
			{
				hitBVHNode == 0;
				hitT = hitInfo.hitT;
				hitIndex = hitInfo.triAddr;
				hitInfo.meshInstanceId = 0;
				//tmpInteraction.materialID = meshInstance.GetMaterialID();
				//tmpInteraction.meshInstanceID = 0;
				//tmpInteraction.wo.xyz = -ray.direction;
				//interaction = tmpInteraction;
			}
		}
	}
	else
	{
		while (nodeAddr != EntrypointSentinel)
		{

			BVHNode curNode = BVHTree[nodeAddr];
			int4 cnodes = asint(curNode.cids);

			float t0 = BoundRayIntersect(ray, invDir, curNode.b0min, curNode.b0max);
			bool traverseChild0 = t0 > 0;
			//if (t0 < 0) t0 = 0;

			float t1 = BoundRayIntersect(ray, invDir, curNode.b1min, curNode.b1max);
			bool traverseChild1 = t1 > 0;
			//if (t1 < 0) t1 = 0;

			int2 next = GetTLASNodeChild(curNode.cids); //new Vector2Int(INVALID_INDEX, INVALID_INDEX);
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
					//float bvhHit = hitT;
					int meshHitTriangleIndex = -1;
					HitInfo tmpHitInfo = (HitInfo)0;
					//tmpHitInfo.hitT = hitT;
					tmpHitInfo.triAddr = -1;
					
					if (IntersectMeshBVH(rayTemp, next[i], tmpHitInfo, anyHit))
					{
						if (tmpHitInfo.hitT < hitT)
						{
							//tmpInteraction.materialID = meshInstance.GetMaterialID();
							//tmpInteraction.meshInstanceID = nextMeshInstanceIds[i];
							//tmpInteraction.wo.xyz = -ray.direction;
							tmpHitInfo.meshInstanceId = nextMeshInstanceIds[i];
							hitT = tmpHitInfo.hitT;
							hitIndex = tmpHitInfo.triAddr;
							hitBVHNode = next[i];
							//interaction = tmpInteraction;
							hitInfo = tmpHitInfo;

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


bool BVHHit(Ray ray, out HitInfo hitInfo, bool anyHit)
{
	float hitT = ray.tmax;
	hitInfo = (HitInfo)0;
	hitInfo.triAddr = -1;
	int INVALID_INDEX = EntrypointSentinel;

	//GPURay TempRay = new GPURay();
	int traversalStack[64];
	int   stackIndex = 0;
	traversalStack[stackIndex++] = INVALID_INDEX;
	int curIndex = instBVHAddr;
	//bool blas = false;
	float ooeps = pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number
	float3 rayDir = ray.direction;
	float3 invDir = float3(1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps),
		1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps),
		1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps)
		);
	
	int hitIndex = -1;
	
	while (curIndex != INVALID_INDEX)
	{
		BVHNode curNode = BVHTree[curIndex];
		curIndex = INVALID_INDEX;
		int4 cnodes = asint(curNode.cids);
		int  leftNode = cnodes.x;
		int  leftNodeMask = cnodes.y;
		int  rightNode = cnodes.z;
		int  rightNodeMask = cnodes.w;
		float tLeftChildHit = BoundRayIntersect(ray, invDir, curNode.b0min, curNode.b0max);
		float tRightChildHit = BoundRayIntersect(ray, invDir, curNode.b1min, curNode.b1max);
		bool isLeftChildLeaf = leftNodeMask >= 0;
		bool isRightChildLeaf = rightNodeMask >= 0;
		int2 leafNode = int2(INVALID_INDEX, INVALID_INDEX);
		int2 leafNodeMask = int2(-1, -1);

		if (tLeftChildHit > 0.0 && tRightChildHit > 0.0)
		{
			int deferred = INVALID_INDEX;
			if (tLeftChildHit > tRightChildHit)
			{
				curIndex = isRightChildLeaf ? INVALID_INDEX : rightNode;
				deferred = leftNode;
				leafNode[0] = isRightChildLeaf ? rightNode : INVALID_INDEX;
				leafNodeMask[0] = isRightChildLeaf ? rightNodeMask : -1;
				leafNode[1] = isLeftChildLeaf ? deferred : INVALID_INDEX;
				leafNodeMask[1] = isLeftChildLeaf ? leftNodeMask : -1;
			}
			else
			{
				curIndex = isLeftChildLeaf ? INVALID_INDEX : leftNode;
				deferred = rightNode;
				leafNode[0] = isLeftChildLeaf ? leftNode : INVALID_INDEX;
				leafNodeMask[0] = isLeftChildLeaf ? leftNodeMask : -1;
				leafNode[1] = isRightChildLeaf ? deferred : INVALID_INDEX;
				leafNodeMask[1] = isRightChildLeaf ? rightNodeMask : -1;
			}
			
			if (leafNode[1] == INVALID_INDEX)
				traversalStack[stackIndex++] = deferred;
			if (leafNode[0] == INVALID_INDEX && leafNode[1] == INVALID_INDEX)
				continue;
		}
		else if (tLeftChildHit > 0)
		{
			if (!isLeftChildLeaf)
			{
				curIndex = leftNode;
				continue;
			}
			else
			{
				leafNode[0] = leftNode;
				leafNodeMask[0] = leftNodeMask;
			}
		}
		else if (tRightChildHit > 0)
		{
			if (!isRightChildLeaf)
			{
				curIndex = rightNode;
				continue;
			}
			else
			{
				leafNode[0] = rightNode;
				leafNodeMask[0] = rightNodeMask;
			}
		}
		if (curIndex == INVALID_INDEX)
			curIndex = traversalStack[--stackIndex];

		//case 1: tlas leafnode
		for (int i = 0; i < 2; ++i)
		{
			if (leafNode[i] != INVALID_INDEX)
			{
				MeshInstance meshInstance = MeshInstances[leafNodeMask[i]];
				Ray rayTemp = TransformRay(meshInstance.worldToLocal, ray);
				//invDir = GetInverseDirection(ray.direction);
				//float bvhHit = hitT;
				int meshHitTriangleIndex = -1;
				HitInfo tmpHitInfo = (HitInfo)0;
				//tmpHitInfo.hitT = hitT;
				tmpHitInfo.triAddr = -1;

				if (IntersectMeshBVH(rayTemp, leafNode[i], tmpHitInfo, anyHit))
				{
					if (tmpHitInfo.hitT < hitT)
					{
						tmpHitInfo.meshInstanceId = leafNodeMask[i];
						hitT = tmpHitInfo.hitT;
						hitIndex = tmpHitInfo.triAddr;
						hitInfo = tmpHitInfo;

						if (anyHit)
							return true;
					}
				}
			}
		}
		
	}

	return hitIndex > -1;
}



bool ClosestHit(Ray ray, out HitInfo hitInfo)
{
	hitInfo = (HitInfo)0;
	return BVHHit(ray, hitInfo, false);
	//return IntersectBVH(ray, hitInfo, false);
}

bool AnyHit(Ray ray)
{
	//bool hitted = true;
	HitInfo hitInfo = (HitInfo)0;
	return BVHHit(ray, hitInfo, true);
	//return IntersectBVH(ray, hitInfo, true);

	//return hitted;
}




bool ShadowRayVisibilityTest(float3 p0, float3 p1, float3 normal)
{
	Ray ray = SpawnRay(p0, p1 - p0, normal, 1.0 - ShadowEpsilon);
	return !AnyHit(ray);

	//!IntersectP(ray, hitT, meshInstanceIndex);
}

#endif
