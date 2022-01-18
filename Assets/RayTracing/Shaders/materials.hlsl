#ifndef MATERIALS_HLSL
#define MATERIALS_HLSL
#include "disney.hlsl"

#define BSDF_REFLECTION  1 << 0
#define BSDF_TRANSMISSION  1 << 1
#define BSDF_DIFFUSE  1 << 2
#define BSDF_GLOSSY  1 << 3
#define BSDF_SPECULAR  1 << 4
#define BSDF_ALL  BSDF_DIFFUSE | BSDF_GLOSSY | BSDF_SPECULAR | BSDF_REFLECTION | BSDF_TRANSMISSION
#define BSDF_DISNEY 1 << 5

#define TEXTURED_PARAM_MASK 0x80000000
#define IS_TEXTURED_PARAM(x) ((x) & 0x80000000)
#define GET_TEXTUREARRAY_ID(x) (((x) & 0x0000ff00) >> 8)
#define GET_TEXTUREARRAY_INDEX(x) ((x) & 0x000000ff)


//Texture2DArray albedoTexArray128;
//Texture2DArray albedoTexArray256;
//Texture2DArray albedoTexArray512;
//Texture2DArray albedoTexArray1024;
//
//Texture2DArray normalTexArray128;
//Texture2DArray normalTexArray256;
//Texture2DArray normalTexArray512;
//Texture2DArray normalTexArray1024;
Texture2DArray albedoTexArray;
Texture2DArray normalTexArray;
SamplerState Albedo_linear_repeat_sampler;
SamplerState Albedo_linear_clamp_sampler;
SamplerState Normal_linear_repeat_sampler;
SamplerState linearRepeatSampler;

float4 SampleAlbedoTexture(float2 uv, int texIndex)
{
	return albedoTexArray.SampleLevel(Albedo_linear_repeat_sampler, float3(uv, texIndex), 0);
}

struct ShadingMaterial
{
	float3 reflectance;
	float3 transmission;
	//float3 specular;
	float3 normal;
	float  metallic;
	float  roughness;
	float  eta;
};

void UnpackShadingMaterial(Material material, inout ShadingMaterial shadingMaterial, float2 uv)
{
	shadingMaterial = (ShadingMaterial)0;
	//check if using texture
	int textureArrayId = -1;
	int textureIndex = -1;
	const uint mask = asuint(material.albedoMapMask);
	shadingMaterial.reflectance = material.kd.rgb;
	if (IS_TEXTURED_PARAM(mask))
	{
		textureIndex = GET_TEXTUREARRAY_INDEX(mask);
		//textureArrayId = GET_TEXTUREARRAY_ID(mask);
		//float4 albedo = testTexture.SampleLevel(linearRepeatSampler, uv, 0);
		float4 albedo = SampleAlbedoTexture(uv, textureIndex);
		shadingMaterial.reflectance *= albedo.rgb;
	}

	//shadingMaterial.reflectance = material.kd.rgb;
	shadingMaterial.metallic = material.metallic;
}


void UnpackDisneyMaterial(Material material, inout DisneyMaterial materialDisney, float2 uv)
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

int GetMaterialBxDFNum(int bsdfType)
{
	int num = 0;
	num += bsdfType | BSDF_REFLECTION;
	num += bsdfType | BSDF_TRANSMISSION;
	num += bsdfType | BSDF_DIFFUSE;
	num += bsdfType | BSDF_GLOSSY;
	return num;
}


float3 MaterialBRDF(Material material, float3 wo, float3 wi, float2 uv, out float pdf)
{
	ShadingMaterial shadingMaterial = (ShadingMaterial)0;
	UnpackShadingMaterial(material, shadingMaterial, uv);
	//int nComponent = GetMaterialBxDFNum(material.materialType);
	float3 f = 0;
	pdf = 0;
	int nComponent = 0;
	if (material.materialType | BSDF_DIFFUSE)
	{
		nComponent++;
		f += LambertBRDF(wi, wo, shadingMaterial.reflectance);
		pdf += LambertPDF(wi, wo);
	}
	if (nComponent > 0)
	{
		pdf /= (float)nComponent;
		f /= (float)nComponent;
	}
	return f;
}


float3 SampleLambert(ShadingMaterial material, float3 wo, out float3 wi, float2 u, out float pdf)
{
	wi = CosineSampleHemisphere(u);
	if (wo.z < 0)
		wi.z *= -1;
	pdf = LambertPDF(wi, wo);
	return LambertBRDF(wi, wo, material.reflectance);
}



//wi wo is a vector which in local space of the interfaction surface
float3 SampleMaterialBRDF(Material material, float2 uv, float3 wo, out float3 wi, out float pdf, inout RNG rng)
{
//#ifdef DISNEY_BRDF
	//DisneyMaterial materialDisney;
	//UnpackMaterial(material, materialDisney, uv);
	//return SampleDisneyBRDF(wi, wo, materialDisney, pdf, rng);
//#else
	if (material.materialType & BSDF_DISNEY)
	{
		DisneyMaterial materialDisney;
		UnpackDisneyMaterial(material, materialDisney, uv);
		return SampleDisneyBRDF(wi, wo, materialDisney, pdf, rng);
	}
	else
	{
		ShadingMaterial shadingMaterial = (ShadingMaterial)0;
		UnpackShadingMaterial(material, shadingMaterial, uv);
		float2 u = Get2D(rng);
		if (material.materialType & BSDF_DIFFUSE)
		{
			return SampleLambert(shadingMaterial, wo, wi, u, pdf);
		}
		return SampleLambert(shadingMaterial, wo, wi, u, pdf);
	}
//#endif
}


#endif
