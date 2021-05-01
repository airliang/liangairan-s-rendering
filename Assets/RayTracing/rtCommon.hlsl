#ifndef RTCOMMON_HLSL
#define RTCOMMON_HLSL
#include "geometry.hlsl"

//buffers
StructuredBuffer<BVHNode> BVHTree;
StructuredBuffer<float3>   Positions;
StructuredBuffer<int>      Triangles;
StructuredBuffer<Primitive> Primitives;
RWStructuredBuffer<Ray>    Rays;
RWStructuredBuffer<Interaction>       Intersections;

uniform float _time;
uniform float2 rasterSize;

#endif
