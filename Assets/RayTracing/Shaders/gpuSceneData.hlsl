#ifndef GPU_SCENE_DATA
#define GPU_SCENE_DATA

#include "geometry.hlsl"
#include "GPUStructs.hlsl"

StructuredBuffer<BVHNode>  BVHTree;
StructuredBuffer<float4>   WoodTriangles;
StructuredBuffer<Vertex>   Vertices;
StructuredBuffer<int>      TriangleIndices;
StructuredBuffer<int>      WoodTriangleIndices;
StructuredBuffer<MeshInstance> MeshInstances;
StructuredBuffer<Material> materials;
StructuredBuffer<Light> lights;
StructuredBuffer<float2> Distributions1D;
StructuredBuffer<DistributionDiscript> DistributionDiscripts;

cbuffer cb
{
	uniform int instBVHAddr;
	uniform int bvhNodesNum;
	uniform float worldRadius;
};

#endif
