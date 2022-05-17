#ifndef GPU_SCENE_DATA
#define GPU_SCENE_DATA

#include "geometry.hlsl"
#include "GPUStructs.hlsl"

StructuredBuffer<BVHNode>  BVHTree;
StructuredBuffer<float4>   WoodTriangles;
StructuredBuffer<Vertex>   Vertices;    //the origin mesh vertices of all meshes.
//the origin mesh triangle indices of all meshes, we can consider all the meshes as a big mesh, and this indices is the triangle vertex index of the whole big mesh.
//we can consider TriangleIndices as the index in Vertices declare about.
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
	uniform float cameraConeSpreadAngle;
	uniform matrix RasterToCamera;
	uniform matrix CameraToWorld;
	uniform matrix WorldToRaster;
};

#endif
