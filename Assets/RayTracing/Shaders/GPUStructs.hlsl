#ifndef GPUSTRUCTS_HLSL
#define GPUSTRUCTS_HLSL

struct MeshInstance
{
	float4x4 localToWorld;
	float4x4 worldToLocal;
	int4     indices;  //x-meshhandle y-material index z-light index w-bvhoffset
	int		 triangleStartOffset;  //triangle index start in trianglebuffer
	int		 trianglesNum;
	//int      indexStart;    //the index start offset.attually is the last mesh's total vertex count.

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
	int type;   //0-arealight 1-envmap light
	int meshInstanceID;
	int distributionDiscriptIndex;   //triangle area distribution
	//int trianglesNum;
	float  radius;  //for point light
	float  intensity;
	float3 radiance;
	float textureMask;
};

struct Material
{
	int materialType; 
	float3 kd;
	float3 ks;
	float metallic;
	float specular;
	float roughness;
	float anisotropy;
	float3 eta;
	float3 k;             //metal material absorption
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



struct RayCone
{
	float spreadAngle;
	float width;
};

struct ShadingMaterial
{
	int    materialType;
	float3 reflectance;
	float3 transmission;
	float3 specular;
	float3 normal;
	float3 k;
	float  roughness;
	float  roughnessV;
	float3  eta;
};

struct DistributionDiscript
{
	//the distribution array index
	int start;
	//number of distribution, or number of the v-direction (marginals) if distribution is 2D
	int num;
	//number of 2D distribution, or number of the u-direction (conditionals) if distribution is 2D
	int unum;
	float funcInt;
	float4 domain;  //discript function domain, x as min y as max if 1D distribution, xy-domain of marginal zw-domain of conditional if 2D distribution
};

struct Interaction  //64byte
{
	float3 p;   //交点
	float  hitT;
	//float time;        //应该是相交的ray的参数t
	float3 wo;
	float2 uv;
	float3 normal;
	float3 tangent;  //the same as pbrt's ss(x)
	float3 bitangent; //the same as pbrt's ts(y)
	float  primArea;
	uint   materialID;
	uint   meshInstanceID;
	uint   triangleIndex;  //triangle index in this mesh
	//float  spreadAngle;   //ray cone angle use for mipmapping
	float  coneWidth;     //ray cone width at this surface point
	float  screenSpaceArea;
	float  uvArea;
	//int3   vertexIndices;
	//int    primitive; //intersect with primitives index, -1 represents no intersection
	bool IsHit()
	{
		return hitT > 0;
	}

	float3 WorldToLocal(float3 v)
	{
		return float3(dot(tangent, v), dot(bitangent, v), dot(normal, v));
	}

	float3 LocalToWorld(float3 v)
	{
		//return float3(tangent.x * v.x + bitangent.x * v.y + normal.x * v.z,
		//	tangent.y * v.x + bitangent.y * v.y + normal.y * v.z,
		//	tangent.z * v.x + bitangent.z * v.y + normal.z * v.z
		//	);
		return tangent * v.x + bitangent * v.y + normal * v.z;
	}
};

struct PathVertex
{
	float3 wi;
	float3 bsdfVal;
	float  bsdfPdf;
	Interaction nextISect;
	int    found;
};

RayCone Propagate(RayCone preCone, float surfaceSpreadAngle, float hitT)
{
	RayCone newCone;
	newCone.width = preCone.spreadAngle * hitT + preCone.width;
	newCone.spreadAngle = preCone.spreadAngle + surfaceSpreadAngle;
	return newCone;
}

RayCone ComputeRayCone(RayCone preCone, float distance, float pixelSpreadAngle)
{
	//RayCone rayCone;
	//rayCone.width = preSpreadAngle * distance;
	//rayCone.spreadAngle = lastSpreadAngle;
	//float gamma = cameraConeSpreadAngle;
	return Propagate(preCone, pixelSpreadAngle, distance);
}

#endif
