#ifndef RTCOMMON_HLSL
#define RTCOMMON_HLSL
#include "geometry.hlsl"
//#include "bxdf.hlsl"
//以后把这些struct放到一个structbuffer.hlsl的文件
struct BXDF
{
	float4 materialParam;
	float4 kd;
	float4 ks;
	float4 kr;
};

//struct MeshHandle
//{
//	int4 offsets;
//	Bounds bounds;
//};

struct MeshInstance
{
	float4x4 localToWorld;
	float4x4 worldToLocal;
	int4     indices;  //x-meshhandle y-material index z-light index w

	int GetMaterialID()
	{
		return indices.y;
	}
};

//buffers


//StructuredBuffer<Primitive> Primitives;
//StructuredBuffer<float4x4> WorldMatrices;



//StructuredBuffer<MeshHandle> MeshHandles;


uniform float _time;
uniform float2 rasterSize;

uniform float3 testBoundMax;
uniform float3 testBoundMin;


#endif
