#ifndef LIGHT_HLSL
#define LIGHT_HLSL
#include "geometry.hlsl"
#include "sampler.hlsl"
#include "bvhaccel.hlsl"

struct Light
{
	int type;
	int meshInstanceID;
	int distributeAddress;   //triangle area distribution
	int trianglesNum;
	float  radius;  //for point light
	float  intensity;
	float3 radiance;
};

StructuredBuffer<Light> lights;



int SampleLightTriangle(int start, int count, float u, out float pdf)
{
	//get light mesh triangle index
	int index = SampleDistribution1DDiscrete(u, start, count, pdf);
	return index;
}

Light SampleLight(out float pdf, uint threadId)
{
	int index = SampleDistribution1DDiscrete(0, lightsNum, rs.Get1D(threadId), pdf);
	return lights[index];
}

//just for area light
float3 SampleLightRadiance(uint threadId, Light light, float3 position, out float pdf, out Ray shadowRay)
{
	MeshInstance meshInstance = MeshInstances[light.meshInstanceID];
	int distributionAddress = light.distributeAddress;
	float u = rs.Get1D(threadId);
	float triPdf = 1;
	int triangleIndex = SampleLightTriangle(distributionAddress, light.trianglesNum, u, pdf);
	int vertexStart = triangleIndex * 3 - lightsNum;
	float3 p0 = Vertices[vertexStart].position.xyz;
	float3 p1 = Vertices[vertexStart].position.xyz;
	float3 p2 = Vertices[vertexStart].position.xyz;
	//get the mesh triangle
	float3 normal;
	float3 trianglePoint;
	SampleTrianglePoint(p0, p1, p2, rs.Get2D(threadId), normal, trianglePoint, triPdf);
	trianglePoint = mul(meshInstance.localToWorld, trianglePoint);
	normal = mul(float4(normal, 0), meshInstance.worldToLocal);
	wi = normalize(trianglePoint - position);

	shadowRay.orig.xyz = offset_ray(trianglePoint, normal);

	return 0;
}

#endif
