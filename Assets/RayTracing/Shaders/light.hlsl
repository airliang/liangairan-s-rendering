
#ifndef LIGHT_HLSL
#define LIGHT_HLSL
#include "geometry.hlsl"
#include "GPUStructs.hlsl"
#include "distributions.hlsl"

#define AreaLightType 0
#define EnvLightType 1
#define PointLightType 2

uniform int   enviromentTextureMask;
uniform float3 enviromentColor;
uniform float3 enviromentColorScale;

TextureCube _EnvMap;
SamplerState _EnvMap_linear_repeat_sampler;

float3 SampleEnviromentLight(float3 dir)
{
	if (enviromentTextureMask == 1)
	{
		return _EnvMap.SampleLevel(_EnvMap_linear_repeat_sampler, dir, 0).rgb;
	}
	return enviromentColor;
}


float3 SampleTriangleLight(float3 p0, float3 p1, float3 p2, float2 u, Interaction isect, Light light, out float3 wi, out float3 position, out float pdf)
{
	float3 Li = 0;
	float3 lightPointNormal;
	float triPdf = 0;
	position = SamplePointOnTriangle(p0, p1, p2, u, lightPointNormal, triPdf);
	pdf = triPdf;
	wi = position - isect.p.xyz;
	float wiLength = length(wi);
	wi = normalize(wi);
	float cos = dot(lightPointNormal, -wi);
	float absCos = abs(cos);
	pdf *= wiLength * wiLength / absCos;
	if (isinf(pdf) || wiLength == 0)
	{
		pdf = 0;
		return 0;
	}
	
	return cos > 0 ? light.radiance : 0;
}

int SampleTriangleIndexOfLightPoint(float u, DistributionDiscript discript, StructuredBuffer<float2> distributions, out float pdf)
{
	//get light mesh triangle index
	int index = Sample1DDiscrete(u, discript, distributions, pdf);
	return index;
}

float3 SampleLightRadiance(StructuredBuffer<float2> lightDistribution, Light light, Interaction isect, inout RNG rng, out float3 wi, out float lightPdf, out float3 lightPoint)
{
	if (light.type == AreaLightType)
	{
		int discriptIndex = light.distributionDiscriptIndex;
		DistributionDiscript lightDistributionDiscript = DistributionDiscripts[discriptIndex];
		float u = Get1D(rng);
		float triPdf = 0;
		lightPdf = 0;
		MeshInstance meshInstance = MeshInstances[light.meshInstanceID];
		int triangleIndex = SampleTriangleIndexOfLightPoint(u, lightDistributionDiscript, lightDistribution, lightPdf) * 3 + meshInstance.triangleStartOffset;

		int vertexStart = triangleIndex;
		int vIndex0 = TriangleIndices[vertexStart];
		int vIndex1 = TriangleIndices[vertexStart + 1];
		int vIndex2 = TriangleIndices[vertexStart + 2];
		float3 p0 = Vertices[vIndex0].position.xyz;
		float3 p1 = Vertices[vIndex1].position.xyz;
		float3 p2 = Vertices[vIndex2].position.xyz;
		//convert to worldpos

		p0 = mul(meshInstance.localToWorld, float4(p0, 1)).xyz;
		p1 = mul(meshInstance.localToWorld, float4(p1, 1)).xyz;
		p2 = mul(meshInstance.localToWorld, float4(p2, 1)).xyz;

		float3 Li = SampleTriangleLight(p0, p1, p2, Get2D(rng), isect, light, wi, lightPoint, triPdf);
		lightPdf *= triPdf;
		return Li;
	}
	else if (light.type == EnvLightType)
	{
		float2 u = Get2D(rng);
		float mapPdf = 1.0 / (4 * PI);
		float theta = u[1] * PI;
		float phi = u[0] * 2 * PI;
		float cosTheta = cos(theta);
		float sinTheta = sin(theta);
		float sinPhi = sin(phi);
		float cosPhi = cos(phi);
		wi = isect.LocalToWorld(float3(sinTheta * cosPhi, sinTheta * sinPhi, cosTheta));
		lightPdf = mapPdf;
		lightPoint = isect.p + wi * 10000.0f;
		return SampleEnviromentLight(wi);
	}

	wi = float3(0, 0, 0);
	lightPdf = 0;
	lightPoint = float3(0, 0, 0);
	return float3(0, 0, 0);
}

int SampleLightSource(float u, int lightCount, StructuredBuffer<float2> discributions, out float pmf)
{
	DistributionDiscript discript = (DistributionDiscript)0;
	discript.start = 0;
	//the length of cdfs is N+1
	discript.num = lightCount + 1;
	int index = Sample1DDiscrete(u, discript, discributions, pmf); //SampleDistribution1DDiscrete(rs.Get1D(threadId), 0, lightCount, pdf);
	return index;
}

float3 Light_Le(float3 wi, Light light)
{
	if (light.type == AreaLightType)
	{
		return light.radiance;
	}
	else if (light.type == EnvLightType)
	{
		return SampleEnviromentLight(wi);
	}
	return 0;
}

#endif



