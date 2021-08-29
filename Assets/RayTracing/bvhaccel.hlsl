#include "geometry.hlsl"
#include "rtCommon.hlsl"
#ifndef BVHACCEL_HLSL
#define BVHACCEL_HLSL

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

bool SceneIntersectTest(Ray ray)
{
	float4 rayDir = ray.direction;
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
	Bounds bounds;
	bounds.min = float3(node.b0xy.x, node.b0xy.z, node.b01z.x);
	bounds.max = float3(node.b0xy.y, node.b0xy.w, node.b01z.y);
	//bounds.min = testBoundMin;
	//bounds.max = testBoundMax;
	//if (BoundIntersectP(ray, bounds, invDir, dirIsNeg))
	float tMin = 0;
	if (BoundRayIntersect(ray, node.b0xy, node.b01z.xy, invDir, int3(signX, signY, signZ), tMin))
		return true;
	if (BoundRayIntersect(ray, node.b1xy, node.b01z.zw, invDir, int3(signX, signY, signZ), tMin))
		return true;

	return false;
}

bool SceneIntersectTest2(Ray ray)
{
	float4 rayOrig = ray.orig;
	float4 rayDir = ray.direction;
	int traversalStack[STACK_SIZE];
	traversalStack[0] = EntrypointSentinel;
	int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).
	int nodeAddr = 0;
	int primitivesNum = 0;   //当前节点的primitives数量
	int primitivesNum2 = 0;
	int triIdx = 0;
	float tmin = rayDir.w;
	float hitT = rayOrig.w;  //tmax
	float origx = rayOrig.x;
	float origy = rayOrig.y;
	float origz = rayOrig.z;            // Ray origin.
	float ooeps = pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number
	//float idirx = 1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps); // inverse ray direction
	//float idiry = 1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps); // inverse ray direction
	//float idirz = 1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps); // inverse ray direction
	float3 invDir = float3(1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps),
		1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps),
		1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps)
		);
	float dirx = rayDir.x;
	float diry = rayDir.y;
	float dirz = rayDir.z;
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
	
	//这个nodeAddr从哪里来？
	while (nodeAddr != EntrypointSentinel)
	{
		while (uint(nodeAddr) < uint(EntrypointSentinel))
		{
			BVHNode curNode = BVHTree[nodeAddr];
			int4 cnodes = asint(curNode.cids);

			/*
			const float c0lox = curNode.b0xy.x * idirx - oodx;
			const float c0hix = curNode.b0xy.y * idirx - oodx;
			const float c0loy = curNode.b0xy.z * idiry - oody;
			const float c0hiy = curNode.b0xy.w * idiry - oody;
			const float c0loz = curNode.b01z.x * idirz - oodz;
			const float c0hiz = curNode.b01z.y * idirz - oodz;
			const float c1loz = curNode.b01z.z * idirz - oodz;
			const float c1hiz = curNode.b01z.w * idirz - oodz;
			const float c0min = spanBeginKepler(c0lox, c0hix, c0loy, c0hiy, c0loz, c0hiz, tmin);
			const float c0max = spanEndKepler(c0lox, c0hix, c0loy, c0hiy, c0loz, c0hiz, hitT);
			const float c1lox = curNode.b1xy.x * idirx - oodx;
			const float c1hix = curNode.b1xy.y * idirx - oodx;
			const float c1loy = curNode.b1xy.z * idiry - oody;
			const float c1hiy = curNode.b1xy.w * idiry - oody;
			const float c1min = spanBeginKepler(c1lox, c1hix, c1loy, c1hiy, c1loz, c1hiz, tmin);
			const float c1max = spanEndKepler(c1lox, c1hix, c1loy, c1hiy, c1loz, c1hiz, hitT);

			bool swp = (c1min < c0min);

			bool traverseChild0 = (c0max >= c0min);
			bool traverseChild1 = (c1max >= c1min);
			*/
			float tMin = 0;
			bool traverseChild0 = BoundRayIntersect(ray, curNode.b0xy, curNode.b01z.xy, invDir, signs, tMin);
			float tMin1 = 0;
			bool traverseChild1 = BoundRayIntersect(ray, curNode.b1xy, curNode.b01z.zw, invDir, signs, tMin1);
			tmin = min(tMin1, tMin);

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
				primitivesNum = (traverseChild0) ? cnodes.z : cnodes.w;
				primitivesNum2 = (traverseChild0) ? cnodes.w : cnodes.z;
				// Both children were intersected => push the farther one.
				if (traverseChild0 && traverseChild1)
				{
					if (swp)
					{
						//swap(nodeAddr, cnodes.y);
						int tmp = nodeAddr;
						nodeAddr = cnodes.y;
						cnodes.y = tmp;
						tmp = primitivesNum;
						primitivesNum = primitivesNum2;
						primitivesNum2 = tmp;
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
				break;
		}

		//遍历叶子
		while (leafAddr < 0)
		{
			for (int triAddr = ~leafAddr; triAddr < ~leafAddr + primitivesNum * 3; triAddr += 3)
			{
				const float4 m0 = WoodTriangles[triAddr];     //matrix row 0 
				const float4 m1 = WoodTriangles[triAddr + 1]; //matrix row 1 
				const float4 m2 = WoodTriangles[triAddr + 2]; //matrix row 2

				//Oz is a point, must plus w
				float Oz = m2.w + origx * m2.x + origy * m2.y + origz * m2.z;
				//Dz is a vector
				float invDz = 1.0f / (dirx * m2.x + diry * m2.y + dirz * m2.z);
				float t = -Oz * invDz;

				//if t is in bounding and less than the ray.tMax
				if (t > tmin && t < hitT)
				{
					// Compute and check barycentric u.
					float Ox = m0.w + origx * m0.x + origy * m0.y + origz * m0.z;
					float Dx = dirx * m0.x + diry * m0.y + dirz * m0.z;
					float u = Ox + t * Dx;

					if (u >= 0.0f)
					{
						// Compute and check barycentric v.
						float Oy = m1.w + origx * m1.x + origy * m1.y + origz * m1.z;
						float Dy = dirx * m1.x + diry * m1.y + dirz * m1.z;
						float v = Oy + t * Dy;

						if (v >= 0.0f && u + v <= 1.0f)
						{
							// Record intersection.
							// Closest intersection not required => terminate.
							hitT = t;
							hitIndex = triAddr;
							//return true;
							//if (anyHit)
							//{
							//	nodeAddr = EntrypointSentinel;
							//	break;
							//}
							//计算法线
						}
					}
				}

			} // triangle

			// Another leaf was postponed => process it as well.
			leafAddr = nodeAddr;
			if (nodeAddr < 0)
			{
				nodeAddr = traversalStack[stackIndex--];
				primitivesNum = primitivesNum2;
			}
		} // leaf
	}
	return false;
}

/*
bool SceneIntersect(Ray ray, out Interaction interaction)
{
	bool hit = false;
	float3 invDir = 1.0 / ray.direction.xyz;
	int dirIsNeg[3] = { invDir.x < 0, invDir.y < 0, invDir.z < 0 };
	int currentNodeIndex = 0; //当前正在访问的node
	//下一个要访问的node在nodesToVisit的index
	int toVisitOffset = 0;
	//nodesToVisit是一个要访问的node的stack
	int nodesToVisit[64];
	while (true)
	{
		BVHNode node = BVHTree[currentNodeIndex];
		if (BoundIntersect(ray, node.b0, invDir, dirIsNeg))
		{
			//如果是叶子节点
			if (node.nPrimitives > 0)
			{
				for (int i = 0; i < node.nPrimitives; i++)
				{
					Primitive pri = Primitives[node.primitivesOffset + i];
					float3 p0 = Positions[Triangles[pri.triangleOffset]];
					float3 p1 = Positions[Triangles[pri.triangleOffset + 1]];
					float3 p2 = Positions[Triangles[pri.triangleOffset + 2]];
					if (TriangleIntersect(ray, p0, p1, p2, node.primitivesOffset + i, interaction))
					{
						hit = true;
					}
				}
				if (toVisitOffset == 0)
					break;
				currentNodeIndex = nodesToVisit[--toVisitOffset];
			}
			else
			{
				//射线方向和沿着轴的方向如果是超过90度
				//先访问第二个子节点
				//下一个节点则是第一个子节点(currentNodeIndex + 1)
				//射线方向和沿着轴的方向 < 90度则相反
				if (dirIsNeg[node.axis])
				{
					nodesToVisit[toVisitOffset++] = currentNodeIndex + 1;
					currentNodeIndex = node.secondChildOffset;
				}
				else
				{
					nodesToVisit[toVisitOffset++] = node.secondChildOffset;
					currentNodeIndex = currentNodeIndex + 1;
				}
			}
		}
		else
		{
			if (toVisitOffset == 0)
				break;
			currentNodeIndex = nodesToVisit[--toVisitOffset];
		}
	}
	return hit;
}
*/

bool IntersectBVHandTriangles(Ray ray, out Interaction interaction)
{
	interaction = (Interaction)0;
	float4 rayOrig = ray.orig;
	float4 rayDir = ray.direction;
	int traversalStack[STACK_SIZE];
	traversalStack[0] = EntrypointSentinel;
	int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).
	int nodeAddr = 0;
	int primitivesNum = 0;   //当前节点的primitives数量
	int primitivesNum2 = 0;
	int triIdx = 0;
	float tmin = rayDir.w;
	float hitT = rayOrig.w;  //tmax
	float origx = rayOrig.x;
	float origy = rayOrig.y;
	float origz = rayOrig.z;            // Ray origin.
	float ooeps = pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number
	//float idirx = 1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps); // inverse ray direction
	//float idiry = 1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps); // inverse ray direction
	//float idirz = 1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps); // inverse ray direction
	float3 invDir = float3(1.0f / (abs(rayDir.x) > ooeps ? rayDir.x : sign(rayDir.x) * ooeps),
		1.0f / (abs(rayDir.y) > ooeps ? rayDir.y : sign(rayDir.y) * ooeps),
		1.0f / (abs(rayDir.z) > ooeps ? rayDir.z : sign(rayDir.z) * ooeps)
		);
	float dirx = rayDir.x;
	float diry = rayDir.y;
	float dirz = rayDir.z;
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

			/*
			const float c0lox = curNode.b0xy.x * idirx - oodx;
			const float c0hix = curNode.b0xy.y * idirx - oodx;
			const float c0loy = curNode.b0xy.z * idiry - oody;
			const float c0hiy = curNode.b0xy.w * idiry - oody;
			const float c0loz = curNode.b01z.x * idirz - oodz;
			const float c0hiz = curNode.b01z.y * idirz - oodz;
			const float c1loz = curNode.b01z.z * idirz - oodz;
			const float c1hiz = curNode.b01z.w * idirz - oodz;
			const float c0min = spanBeginKepler(c0lox, c0hix, c0loy, c0hiy, c0loz, c0hiz, tmin);
			const float c0max = spanEndKepler(c0lox, c0hix, c0loy, c0hiy, c0loz, c0hiz, hitT);
			const float c1lox = curNode.b1xy.x * idirx - oodx;
			const float c1hix = curNode.b1xy.y * idirx - oodx;
			const float c1loy = curNode.b1xy.z * idiry - oody;
			const float c1hiy = curNode.b1xy.w * idiry - oody;
			const float c1min = spanBeginKepler(c1lox, c1hix, c1loy, c1hiy, c1loz, c1hiz, tmin);
			const float c1max = spanEndKepler(c1lox, c1hix, c1loy, c1hiy, c1loz, c1hiz, hitT);

			bool swp = (c1min < c0min);

			bool traverseChild0 = (c0max >= c0min);
			bool traverseChild1 = (c1max >= c1min);
			*/
			float tMin = 0;
			bool traverseChild0 = BoundRayIntersect(ray, curNode.b0xy, curNode.b01z.xy, invDir, signs, tMin);
			float tMin1 = 0;
			bool traverseChild1 = BoundRayIntersect(ray, curNode.b1xy, curNode.b01z.zw, invDir, signs, tMin1);

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
				primitivesNum = (traverseChild0) ? cnodes.z : cnodes.w;
				primitivesNum2 = (traverseChild0) ? cnodes.w : cnodes.z;
				// Both children were intersected => push the farther one.
				if (traverseChild0 && traverseChild1)
				{
					if (swp)
					{
						//swap(nodeAddr, cnodes.y);
						int tmp = nodeAddr;
						nodeAddr = cnodes.y;
						cnodes.y = tmp;
						tmp = primitivesNum;
						primitivesNum = primitivesNum2;
						primitivesNum2 = tmp;
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
			for (int triAddr = ~leafAddr; triAddr < ~leafAddr + primitivesNum * 3; triAddr += 3)
			{
				const float4 m0 = WoodTriangles[triAddr];     //matrix row 0 
				const float4 m1 = WoodTriangles[triAddr + 1]; //matrix row 1 
				const float4 m2 = WoodTriangles[triAddr + 2]; //matrix row 2

				float3 normal = normalize(cross(m0, m1));
				if (dot(normal, rayDir.xyz) >= 0)
				{
					//三角形背面
					continue;
				}

				//Oz is a point, must plus w
				float Oz = m2.w + origx * m2.x + origy * m2.y + origz * m2.z;
				//Dz is a vector
				float invDz = 1.0f / (dirx * m2.x + diry * m2.y + dirz * m2.z);
				float t = -Oz * invDz;

				//if t is in bounding and less than the ray.tMax
				if (t > tmin && t < hitT)
				{
					// Compute and check barycentric u.
					float Ox = m0.w + origx * m0.x + origy * m0.y + origz * m0.z;
					float Dx = dirx * m0.x + diry * m0.y + dirz * m0.z;
					float u = Ox + t * Dx;

					if (u >= 0.0f)
					{
						// Compute and check barycentric v.
						float Oy = m1.w + origx * m1.x + origy * m1.y + origz * m1.z;
						float Dy = dirx * m1.x + diry * m1.y + dirz * m1.z;
						float v = Oy + t * Dy;

						if (v >= 0.0f && u + v <= 1.0f)
						{
							// Record intersection.
							// Closest intersection not required => terminate.
							hitT = t;
							hitIndex = triAddr;
							const float4 v0 = WVertices[triAddr].position;
							const float4 v1 = WVertices[triAddr + 1].position;
							const float4 v2 = WVertices[triAddr + 2].position;
							float4 hitPos = v0 * u + v1 * v + v2 * (1.0 - u - v);

							hitPos.xyz = offset_ray(hitPos.xyz, normal);
							hitPos.w = hitT;
							materialIndex = v0.w;

							const float4 uv0 = WVertices[triAddr].uv;
							const float4 uv1 = WVertices[triAddr + 1].uv;
							const float4 uv2 = WVertices[triAddr + 2].uv;

							
							//return true;
							//if (anyHit)
							//{
							//	nodeAddr = EntrypointSentinel;
							//	break;
							//}
							//计算法线
							interaction.normal.xyz = normal;
							interaction.p = hitPos;
							interaction.uv = uv0 * u + uv1 * v + uv2 * (1.0 - u - v);

							//计算切线
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
						}
					}
				}
			} // triangle

			// Another leaf was postponed => process it as well.
			leafAddr = nodeAddr;
			if (nodeAddr < 0)
			{
				nodeAddr = traversalStack[stackIndex--];
				primitivesNum = primitivesNum2;
			}
		} // leaf
	}

	//if (hitIndex == -1) 
	//{ 
	//	STORE_RESULT(rayidx, -1, hitT); 
	//}
	//else 
	//{ 
	//	STORE_RESULT(rayidx, FETCH_TEXTURE(triIndices, hitIndex, int), hitT); 
	//}
	interaction.primitive.x = asfloat(hitIndex);
	interaction.primitive.y = materialIndex;
	return hitIndex != -1;
}

#endif
