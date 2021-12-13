#ifndef FILTER_HLSL
#define FILTER_HLSL
#include "distributions.hlsl"

StructuredBuffer<float2> FilterMarginals;
StructuredBuffer<float2> FilterConditions;
uniform int MarginalNum;
uniform int ConditionNum;

float2 ImportanceFilterSample(float2 u)
{
	DistributionDiscript discript = (DistributionDiscript)0;
	discript.start = 0;
	discript.num = ConditionNum;
	discript.unum = MarginalNum;
	float pdf = 0;
	return Sample2DContinuous(u, discript, FilterMarginals, FilterConditions, pdf);
}


#endif
