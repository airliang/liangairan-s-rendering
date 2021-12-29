#ifndef FILTER_HLSL
#define FILTER_HLSL
#include "distributions.hlsl"

StructuredBuffer<float2> FilterMarginals;
StructuredBuffer<float2> FilterConditions;
uniform int MarginalNum;
uniform int ConditionNum;
uniform float4 FilterDomain;

float2 ImportanceFilterSample(float2 u)
{
	DistributionDiscript discript = (DistributionDiscript)0;
	discript.start = 0;
	discript.num = ConditionNum;
	discript.unum = MarginalNum;
	discript.domain = FilterDomain;
	float pdf = 0;
	return Sample2DContinuous(u, discript, FilterMarginals, FilterConditions, pdf);
}

float2 BoxFilterSample(float2 u)
{
	return float2(lerp(-FilterDomain.x, FilterDomain.x, u.x), lerp(-FilterDomain.y, FilterDomain.y, u.y));
}


#endif
