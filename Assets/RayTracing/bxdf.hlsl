#ifndef BXDF_HLSL
#define BXDF_HLSL
#include "sampler.hlsl"
#include "geometry.hlsl"

#define Matte 0
#define Plastic 1
#define Mirror 2
#define Metal 3
#define Glass 4
//gpu中只分两种，reflection和refraction


float4 LambertDiffuse(BXDF bxdf, float3 wo, float3 wi, float2 u, out float pdf, float3 normal)
{
	pdf = SameHemisphere(wi, wo) ? AbsCosTheta(wi) * INV_PI : 0;
	return bxdf.kd * INV_PI;
}

struct BSDF
{
	float3 ng;  //geometry normal
	float3 ns;  //shading  normal
	float3 ss;  //tengent


};

void ComputeScatteringFunctions(Interaction isect)
{
	//int material = asint(isect.primitive.y);
	//if (material == Matte)
	//{

	//}
	//不用区分材质类型，直接处理diffuse和specular的

}

float Pdf(float3 wi, float3 wo)
{
	return 0;
}

float Reflection(float3 wi, float3 wo)
{
	return 0;
}

float4 SampleReflection(out float3 wi, float3 wo, float2 u, out float pdf, float3 normal, BXDF bxdf)
{
	//
	wi = CosineSampleHemisphere(u);
	if (wo.z < 0)
		wi.z *= -1;
	//pdf = Pdf(wo, *wi);
	return LambertDiffuse(bxdf, wo, wi, u, pdf, normal);
}

float4 SampleRefraction(out float3 wi, float3 wo, float2 u, out float pdf, float3 normal)
{
	return 0;
}

float4 SampleBSDF(out float3 wi, out float pdf, float2 u, float3x3 local2world, Interaction isect, BXDF bxdf)
{
	float4 f = 0;
	//float3 lo = WorldToLocal(isect.wo, isect.ns, isect.bitangent, isect.tangent);
	float3 lo = mul(isect.wo, local2world);
	float3 li = 0;
	//int matIndex = asint(isect.primitive.y);
	pdf = 0;
	/*
	if (matType == Glass)
	{
		//随机采样出refraction还是reflection
		f += sampleRefraction(li, lo, u, pdf, isect.ns);
		//pdf += ReflectionPDF(lo, wi);
		//f += ReflectionF(li, lo, u, pdf, isect.ns);
	}
	else
	*/
	{
		//BXDF bxdf = Materials[matIndex];
		//暂时统一用lambert diffuse brdf
		f += SampleReflection(li, lo, u, pdf, isect.normal, bxdf);
	}
	
	//暂时不需要这么复杂，直接用meshInstance的矩阵来转换
	//wi = LocalToWorld(li, isect.normal, isect.bitangent, isect.tangent);
	wi = mul(local2world, li);

	return pdf == 0 ? 0 : f;
}



#endif
