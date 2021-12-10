
#ifndef LIGHT_HLSL
#define LIGHT_HLSL
#include "geometry.hlsl"
#include "GPUStructs.hlsl"

float3 SampleTriangleLight(float3 p0, float3 p1, float3 p2, float2 u, Interaction isect, Light light, out float3 wi, out float3 position, out float pdf)
{
	float3 Li = 0;
	float3 lightPointNormal;
	float triPdf = 0;
	position = SampleTrianglePoint(p0, p1, p2, u, lightPointNormal, triPdf);
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

#endif



