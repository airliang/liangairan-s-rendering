#ifndef MATERIALS_HLSL
#define MATERIALS_HLSL
#include "disney.hlsl"
#include "bvhaccel.hlsl"
#include "bxdf.hlsl"

#define BSDF_REFLECTION  1 << 0
#define BSDF_TRANSMISSION  1 << 1
#define BSDF_DIFFUSE  1 << 2
#define BSDF_GLOSSY  1 << 3
#define BSDF_SPECULAR  1 << 4
#define BSDF_ALL  BSDF_DIFFUSE | BSDF_GLOSSY | BSDF_SPECULAR | BSDF_REFLECTION | BSDF_TRANSMISSION
#define BSDF_DISNEY 1 << 5

#define Matte 0
#define Plastic 1
#define Metal 2
#define Mirror 3
#define Glass 4
#define RoughDielectric 5
#define Disney 6

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
Texture2DArray glossySpecularTexArray;
SamplerState Albedo_linear_repeat_sampler;
SamplerState Albedo_linear_clamp_sampler;
SamplerState Normal_linear_repeat_sampler;
SamplerState linearRepeatSampler;

float4 SampleAlbedoTexture(float2 uv, int texIndex, float mipmapLevel)
{
	return albedoTexArray.SampleLevel(Albedo_linear_repeat_sampler, float3(uv, texIndex), mipmapLevel);
}

float4 SampleGlossySpecularTexture(float2 uv, int texIndex, float mipmapLevel)
{
	return glossySpecularTexArray.SampleLevel(Albedo_linear_repeat_sampler, float3(uv, texIndex), mipmapLevel);
}

float GetTriangleLODConstant(Interaction isect)
{
	/*
	Vertex vertex0 = Vertices[isect.vertexIndices.x];
	Vertex vertex1 = Vertices[isect.vertexIndices.y];
	Vertex vertex2 = Vertices[isect.vertexIndices.z];
	const float4 v0 = vertex0.position;
	const float4 v1 = vertex1.position;
	const float4 v2 = vertex2.position;

	const float2 uv0 = vertex0.uv;
	const float2 uv1 = vertex1.uv;
	const float2 uv2 = vertex2.uv;
	
	MeshInstance meshInstance = MeshInstances[isect.meshInstanceID];
	float4x4 objectToWorld = meshInstance.localToWorld;

	float4 v0World = mul(objectToWorld, float4(v0.xyz, 1));
	float4 v1World = mul(objectToWorld, float4(v1.xyz, 1));
	float4 v2World = mul(objectToWorld, float4(v2.xyz, 1));

	float4 v0Screen = mul(WorldToRaster, v0World);
	float4 v1Screen = mul(WorldToRaster, v1World);
	float4 v2Screen = mul(WorldToRaster, v2World);
	v0Screen /= v0Screen.w;
	v1Screen /= v1Screen.w;
	v2Screen /= v2Screen.w;


	float P_a = length(cross(v2Screen.xyz - v0Screen.xyz, v1Screen.xyz - v0Screen.xyz)); //ComputeTriangleArea(); // Eq. 5
	float T_a = 512 * 512 * length(cross(float3(uv2, 1) - float3(uv0, 1), float3(uv1, 1) - float3(uv0, 1))); //ComputeTextureCoordsArea(); // Eq. 4
	return 0.5 * max(log2(T_a / P_a), 0); // Eq. 3
	*/
	float P_a = isect.screenSpaceArea;     // Eq. 5
	float T_a = isect.uvArea * 512 * 512;  // Eq. 4
	return 0.5 * max(log2(T_a / P_a), 0); // Eq. 3
}


float ComputeTextureLOD(Interaction isect)
{
	float lambda = GetTriangleLODConstant(isect);
	lambda += max(log2(abs(isect.coneWidth)), 0);
	//lambda += 0.5 * log2(512 * 512);
	lambda -= max(log2(abs(dot(isect.wo, isect.normal))), 0);
	return max(lambda, 0);
}

void UnpackShadingMaterial(Material material, inout ShadingMaterial shadingMaterial, Interaction isect)
{
	shadingMaterial = (ShadingMaterial)0;
	//check if using texture
	shadingMaterial.materialType = material.materialType;
	int textureArrayId = -1;
	int textureIndex = -1;
	const uint mask = asuint(material.albedoMapMask);
	shadingMaterial.reflectance = material.kd.rgb;
	if (IS_TEXTURED_PARAM(mask))
	{
		textureIndex = GET_TEXTUREARRAY_INDEX(mask);
		//textureArrayId = GET_TEXTUREARRAY_ID(mask);
		//float4 albedo = testTexture.SampleLevel(linearRepeatSampler, uv, mipmapLevel);
		float mipmapLevel = ComputeTextureLOD(isect);
		float4 albedo = SampleAlbedoTexture(isect.uv.xy, textureIndex, mipmapLevel);
		shadingMaterial.reflectance *= albedo.rgb;
	}

	if (material.materialType == Plastic)
	{
		shadingMaterial.specular = material.ks;
		shadingMaterial.roughness = material.roughness;
		shadingMaterial.roughnessV = material.anisotropy;
	}
	else if (material.materialType == Metal)
	{
		shadingMaterial.roughness = material.roughness;
		shadingMaterial.roughnessV = material.anisotropy;
		shadingMaterial.eta = material.eta;
		shadingMaterial.k = material.k;
	}
	
	//mask = asuint(material.metallicMapMask);
	//if (IS_TEXTURED_PARAM(mask))
	//{
	//	textureIndex = GET_TEXTUREARRAY_INDEX(mask);
	//	float mipmapLevel = ComputeTextureLOD(isect);
	//	float4 glossyColor = SampleGlossySpecularTexture(isect.uv.xy, textureIndex, mipmapLevel);
	//	shadingMaterial.specular *= glossyColor.rgb;
	//}

	//shadingMaterial.metallic = material.metallic;
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

void ComputeBxDFLambertReflection(ShadingMaterial shadingMaterial, out BxDFLambertReflection bxdf)
{
	bxdf.R = shadingMaterial.reflectance;
}

void ComputeBxDFPlastic(ShadingMaterial shadingMaterial, out BxDFPlastic bxdf)
{
	bxdf = (BxDFPlastic)0;
	bxdf.alphax = RoughnessToAlpha(shadingMaterial.roughness);
	bxdf.alphay = RoughnessToAlpha(shadingMaterial.roughnessV);
	bxdf.R = shadingMaterial.specular;
	//bxdf.fresnel.fresnelType = FresnelDielectric;
	bxdf.etaI = 1.5;
	bxdf.etaT = 1;
	//bxdf.fresnel.k = 0;

}

void ComputeBxDFMetal(ShadingMaterial shadingMaterial, out BxDFMetal bxdf)
{
	bxdf = (BxDFMetal)0;
	bxdf.alphax = RoughnessToAlpha(shadingMaterial.roughness);
	bxdf.alphay = RoughnessToAlpha(shadingMaterial.roughnessV);
	bxdf.R = 1;
	bxdf.etaI = float3(1, 1, 1);
	bxdf.etaT = shadingMaterial.eta;
	bxdf.K = shadingMaterial.k;
}

void ComputeBxDFMicrofacetTransmission(ShadingMaterial shadingMaterial, out BxDFMicrofacetTransmission bxdf)
{

}

void ComputeBxDFSpecularReflection(ShadingMaterial shadingMaterial, out BxDFSpecularReflection bxdf)
{
	bxdf.R = shadingMaterial.reflectance;
}

void ComputeBxDFSpecularTransmission(ShadingMaterial shadingMaterial, out BxDFSpecularTransmission bxdf)
{

}

float3 MaterialBRDF(Material material, Interaction isect, float3 wo, float3 wi, out float pdf)
{
	ShadingMaterial shadingMaterial = (ShadingMaterial)0;
	float3 f = 0;
	pdf = 0;
	if (shadingMaterial.materialType == Disney)
	{

	}
	else
	{
		UnpackShadingMaterial(material, shadingMaterial, isect);
		int nComponent = 0;
		if (shadingMaterial.materialType == Plastic)
		{
			nComponent = 2;
			f += LambertBRDF(wi, wo, shadingMaterial.reflectance);
			pdf += LambertPDF(wi, wo);
			BxDFPlastic bxdfPlastic;
			ComputeBxDFPlastic(shadingMaterial, bxdfPlastic);
			float pdfMicroReflection = 0;
			f += bxdfPlastic.F(wo, wi, pdfMicroReflection);//MicrofacetReflectionF(wo, wi, bxdf, pdfMicroReflection);
			pdf += pdfMicroReflection;//MicrofacetReflectionPdf(wo, wi, bxdf.alphax, bxdf.alphay);
		}
		else if (shadingMaterial.materialType == Metal)
		{
			nComponent = 1;
			BxDFMetal bxdfMetal;
			ComputeBxDFMetal(shadingMaterial, bxdfMetal);
			f += bxdfMetal.F(wo, wi, pdf);  //MicrofacetReflectionF(wo, wi, bxdf, pdf);
		}
		else
		{
			nComponent = 1;
			f += LambertBRDF(wi, wo, shadingMaterial.reflectance);
			pdf += LambertPDF(wi, wo);
		}
		if (nComponent > 1)
		{
			pdf /= (float)nComponent;
			f /= (float)nComponent;
		}
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

float3 SamplePlastic(ShadingMaterial material, float3 wo, out float3 wi, inout RNG rng, out float pdf)
{
	BxDFLambertReflection lambert;
	ComputeBxDFLambertReflection(material, lambert);
	BxDFPlastic bxdfPlastic;
	ComputeBxDFPlastic(material, bxdfPlastic);
	float matchingComponent = 2;
	float2 u = Get2D(rng);
	int compIndex = min(floor(u[0] * matchingComponent), matchingComponent - 1);
	float3 f = 0;
	pdf = 0;

	float2 uRemapped = float2(min(u[0] * matchingComponent - compIndex, ONE_MINUS_EPSILON), u[1]);
	//choose one of the bxdf to sample the wi vector
	if (compIndex == 0)
		f = SampleLambert(material, wo, wi, uRemapped, pdf);
	else
		f = bxdfPlastic.Sample_F(uRemapped, wo, wi, pdf);//SampleMicrofacetReflectionF(bxdf, uRemapped, wo, wi, pdf);
	//choosing pdf caculate
	pdf /= 2;

	return f;
}

float3 SampleMetal(ShadingMaterial material, float3 wo, out float3 wi, inout RNG rng, out float pdf)
{
	BxDFMetal bxdf;
	ComputeBxDFMetal(material, bxdf);
	float2 u = Get2D(rng);
	pdf = 0;
	return bxdf.Sample_F(u, wo, wi, pdf);
}

//wi wo is a vector which in local space of the interfaction surface
float3 SampleMaterialBRDF(Material material, Interaction isect, float3 wo, out float3 wi, out float pdf, inout RNG rng)
{
	ShadingMaterial shadingMaterial = (ShadingMaterial)0;
	UnpackShadingMaterial(material, shadingMaterial, isect);
		
	switch (shadingMaterial.materialType)
	{
	case Disney:
		return 0;
	case Matte:
	{
		float2 u = Get2D(rng);
		return SampleLambert(shadingMaterial, wo, wi, u, pdf);
	}
	case Plastic:
		return SamplePlastic(shadingMaterial, wo, wi, rng, pdf);
	case Metal:
		return SampleMetal(shadingMaterial, wo, wi, rng, pdf);
	case Mirror:
		return 0;
	case Glass:
		return 0;
	default:
	{
		float2 u = Get2D(rng);
		return SampleLambert(shadingMaterial, wo, wi, u, pdf);
	}
	}
}


#endif
