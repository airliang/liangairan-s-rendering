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
	int4     indices;
};

//buffers
StructuredBuffer<BVHNode> BVHTree;
StructuredBuffer<BXDF>    Materials;
StructuredBuffer<float4>   WoodTriangles;
StructuredBuffer<Vertex>   WVertices;
StructuredBuffer<int>      Triangles;
//StructuredBuffer<Primitive> Primitives;
//StructuredBuffer<float4x4> WorldMatrices;
RWStructuredBuffer<Ray>    Rays;
RWStructuredBuffer<Interaction>       Intersections;
RWStructuredBuffer<float4> PathRadiances;  //x L, y-beta, z- nee pdf
//StructuredBuffer<MeshHandle> MeshHandles;
StructuredBuffer<MeshInstance> MeshInstances;

uniform float _time;
uniform float2 rasterSize;

uniform float3 testBoundMax;
uniform float3 testBoundMin;

uniform int instBVHAddr;
uniform int bvhNodesNum;
#endif
