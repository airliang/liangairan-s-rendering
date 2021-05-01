#include "geometry.hlsl"
#include "rtCommon.hlsl"
#ifndef BVHACCEL_HLSL
#define BVHACCEL_HLSL

bool SceneIntersectTest(Ray ray)
{
	float3 invDir = 1.0 / ray.direction.xyz;
	int dirIsNeg[3] = { invDir.x < 0, invDir.y < 0, invDir.z < 0 };
	int currentNodeIndex = 0; //��ǰ���ڷ��ʵ�node


	BVHNode node = BVHTree[currentNodeIndex];
	if (BoundIntersectP(ray, node.bounds, invDir, dirIsNeg))
		return true;

	return false;
}

bool SceneIntersect(Ray ray, out Interaction interaction)
{
	bool hit = false;
	float3 invDir = 1.0 / ray.direction.xyz;
	int dirIsNeg[3] = { invDir.x < 0, invDir.y < 0, invDir.z < 0 };
	int currentNodeIndex = 0; //��ǰ���ڷ��ʵ�node
	//��һ��Ҫ���ʵ�node��nodesToVisit��index
	int toVisitOffset = 0;
	//nodesToVisit��һ��Ҫ���ʵ�node��stack
	int nodesToVisit[64];
	while (true)
	{
		BVHNode node = BVHTree[currentNodeIndex];
		if (BoundIntersect(ray, node.bounds, invDir, dirIsNeg))
		{
			//�����Ҷ�ӽڵ�
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
				//���߷����������ķ�������ǳ���90��
				//�ȷ��ʵڶ����ӽڵ�
				//��һ���ڵ����ǵ�һ���ӽڵ�(currentNodeIndex + 1)
				//���߷����������ķ��� < 90�����෴
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



#endif
