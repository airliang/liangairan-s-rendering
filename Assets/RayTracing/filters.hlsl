#ifndef FILTER_HLSL
#define FILTER_HLSL
#include ""distributions.hlsl"

StructuredBuffer<float2> FilterMarginals;
StructuredBuffer<float2> FilterDistributions;
uniform int MarginalCount;
uniform int ConditionalVCount;

class GaussianFilter
{
	float ReverseSampleMarginal(float2 u, out int offset)
	{
        //offset = FindInterval<float>(MarginalCount, index = > (cdf[index] <= u));

        //// Compute offset along CDF segment
        //float du = u - cdf[offset];
        //if ((cdf[offset + 1] - cdf[offset]) > 0)
        //{
        //    du /= (cdf[offset + 1] - cdf[offset]);
        //}

        //return Mathf.Lerp(cdf[offset], cdf[offset + 1], du);

        return 
	}
};


#endif
