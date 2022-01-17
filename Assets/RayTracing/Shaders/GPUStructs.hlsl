#ifndef GPUSTRUCTS_HLSL
#define GPUSTRUCTS_HLSL

struct MeshInstance
{
	float4x4 localToWorld;
	float4x4 worldToLocal;
	int4     indices;  //x-meshhandle y-material index z-light index w-bvhoffset
	int		 triangleStartOffset;  //triangle index start in trianglebuffer
	int		 trianglesNum;

	int GetLightIndex()
	{
		return indices.z;
	}

	int GetMaterialID()
	{
		return indices.y;
	}

	int GetBVHOffset()
	{
		return indices.w;
	}
};

struct Light
{
	int type;
	int meshInstanceID;
	int distributionDiscriptIndex;   //triangle area distribution
	//int trianglesNum;
	float  radius;  //for point light
	float  intensity;
	float3 radiance;
};

struct Material
{
	int materialType; 
	float4 kd;
	float metallic;
	float specular;
	float roughness;
	float anisotropy;
	float albedoMapMask;
	float normalMapMask;
	float metallicMapMask;
};

struct DisneyMaterial
{
	float3 baseColor;
	float  metallic;
	float  specular;
	float  roughness;
	float  specularTint;
	float  anisotropy;
	float  sheen;
	float  sheenTint;
	float  clearcoat;
	float  clearcoatGloss;
	float  ior;
	float  specularTransmission;
};

struct CameraSample
{
	float2 pFilm;
	//Point2f pLens;
	//Float time;
};

struct RNG
{
	uint state;
	uint s1;
};

struct PathRadiance
{
	float3 li; //one path compute li
	float3 beta;  //one path compute throughput
};

struct DistributionDiscript
{
	//the distribution array index
	int start;
	//number of distribution
	int num;
	//number of 2D distribution
	int unum;
	int c;
	float4 domain;  //discript function domain, x as min y as max if 1D distribution, xy-domain of marginal zw-domain of conditional if 2D distribution
};



#endif
