#ifndef MATERIALS_HLSL
#define MATERIALS_HLSL
#include "sampler.hlsl"
#include "geometry.hlsl"

#define Matte 0
#define Plastic 1
#define Mirror 2
#define Metal 3
#define Glass 4
//gpu中只分两种，reflection和refraction


//float AbsCosTheta(float3 w)
//{
//	return abs(w.z);
//}


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


float3 LambertBRDF(float3 wi, float3 wo, float3 R)
{
	return wo.z == 0 ? 0 : R * INV_PI;
}

//wi and wo must in local space
float LambertPDF(float3 wi, float3 wo)
{
	return SameHemisphere(wo, wi) ? AbsCosTheta(wi) * INV_PI : 0;;
}


float3 SampleLambert(Material material, float3 wo, out float3 wi, float2 u, out float pdf)
{
	wi = CosineSampleHemisphere(u);
	if (wo.z < 0)
		wi.z *= -1;
	pdf = LambertPDF(wi, wo);
	return LambertBRDF(wi, wo, material.kd.rgb);
}

#endif
