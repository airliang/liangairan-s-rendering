#ifndef MATERIALS_HLSL
#define MATERIALS_HLSL
#include "disney.hlsl"


void UnpackMaterial(Material material, inout DisneyMaterial materialDisney, float2 uv)
{
	materialDisney.baseColor = material.kd.xyz;
	materialDisney.metallic = 0;
	materialDisney.specular = 0.0;
	materialDisney.roughness = 0;
	materialDisney.specularTint = 0;
	materialDisney.anisotropy = 0.0;
	materialDisney.sheen = 0;
	materialDisney.sheenTint = 0;
	materialDisney.clearcoat = 0;
	materialDisney.clearcoatGloss = 0;
	materialDisney.ior = 1;
	materialDisney.specularTransmission = 0;
}

//wi wo is a vector which in local space of the interfaction surface
float3 SampleMaterialBRDF(Material material, float2 uv, float3 wo, out float3 wi, out float pdf, inout RNG rng)
{
//#ifdef DISNEY_BRDF
	//DisneyMaterial materialDisney;
	//UnpackMaterial(material, materialDisney, uv);
	//return SampleDisneyBRDF(wi, wo, materialDisney, pdf, rng);
//#else
	float2 u = Get2D(rng);
	return SampleLambert(material, wo, wi, u, pdf);
//#endif
}


#endif
