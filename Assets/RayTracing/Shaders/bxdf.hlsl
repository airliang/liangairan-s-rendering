#ifndef BXDF_HLSL
#define BXDF_HLSL
#include "sampler.hlsl"
#include "geometry.hlsl"

//#define Matte 0
//#define Plastic 1
//#define Mirror 2
//#define Metal 3
//#define Glass 4
//gpu中只分两种，reflection和refraction



float3 LambertBRDF(float3 wi, float3 wo, float3 R)
{
	return wo.z == 0 ? 0 : R * INV_PI;
}

//wi and wo must in local space
float LambertPDF(float3 wi, float3 wo)
{
	return SameHemisphere(wo, wi) ? AbsCosTheta(wi) * INV_PI : 0;
}



#endif
