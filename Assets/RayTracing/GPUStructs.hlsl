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
	float4 materialParams;  //x-materialtype, y-sigma, z-roughness
	float4 kd;
	float4 ks;
	float4 kr;
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
};



#endif
