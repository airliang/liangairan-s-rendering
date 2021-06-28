#ifndef RTCOMMON_HLSL
#define RTCOMMON_HLSL
#include "geometry.hlsl"

//buffers
StructuredBuffer<BVHNode> BVHTree;
StructuredBuffer<float4>   Positions;
StructuredBuffer<int>      Triangles;
StructuredBuffer<Primitive> Primitives;
StructuredBuffer<float4x4> WorldMatrices;
RWStructuredBuffer<Ray>    Rays;
RWStructuredBuffer<Interaction>       Intersections;

uniform float _time;
uniform float2 rasterSize;

uniform float3 testBoundMax;
uniform float3 testBoundMin;

#endif
