#ifndef DISTRIBUTIONS_HLSL
#define DISTRIBUTIONS_HLSL

struct DistributionDiscript
{
    //the distribution array index
    int start;
    //number of distribution, or number of the v-direction (marginals) if distribution is 2D
    int num;
    //number of 2D distribution, or number of the u-direction (conditionals) if distribution is 2D
    int unum;
    int c;
    float4 domain;  //discript function domain, x as min y as max if 1D distribution, xy-domain of marginal zw-domain of conditional if 2D distribution
};

StructuredBuffer<DistributionDiscript> DistributionDiscripts;

//binary search
int FindIntervalSmall(int start, int cdfSize, float u, StructuredBuffer<float2> funcs)
{
    if (cdfSize < 2)
        return start;
    int first = 0, len = cdfSize;
    while (len > 0)
    {
        int nHalf = len >> 1;
        int middle = first + nHalf;
        // Bisect range based on value of _pred_ at _middle_
        float2 distrubution = funcs[start + middle];
        if (distrubution.y <= u)
        {
            first = middle + 1;
            len -= nHalf + 1;
        }
        else
            len = nHalf;
    }
    //if first - 1 < 0, the clamp function is useless
    return clamp(first - 1, 0, cdfSize - 2) + start;
}


int Sample1DDiscrete(float u, DistributionDiscript discript, StructuredBuffer<float2> funcs, out float pdf)
{
    int cdfSize = discript.num + 1;
    int offset = FindIntervalSmall(discript.start, cdfSize, u, funcs);
    float du = u - funcs[offset].y;
    if ((funcs[offset + 1].y - funcs[offset].y) > 0)
    {
        du /= (funcs[offset + 1].y - funcs[offset].y);
    }

    // Compute PDF for sampled offset
    pdf = funcs[offset].x;


    return offset - discript.start; //(int)(offset - discript.start + du) / discript.num;
}

float Sample1DContinuous(float u, DistributionDiscript discript, float2 domain, StructuredBuffer<float2> funcs, out float pdf, out int off)
{
    // Find surrounding CDF segments and _offset_
    int cdfSize = discript.num + 1;
    int offset = FindIntervalSmall(discript.start, cdfSize, u, funcs);
    off = offset;
    // Compute offset along CDF segment
    float du = u - funcs[offset].y;
    if ((funcs[offset + 1].y - funcs[offset].y) > 0)
    {
        du /= (funcs[offset + 1].y - funcs[offset].y);
    }

    // Compute PDF for sampled offset
    pdf = funcs[offset].x;//(distribution.funcInt > 0) ? funcs[offset].x / distribution.funcInt : 0;

    // Return $x\in{}[0,1)$ corresponding to sample
    return lerp(domain.x, domain.y, (offset - discript.start + du) / discript.num);
}

float DiscretePdf(int index, StructuredBuffer<float2> funcs)
{
    return funcs[index].x;
}

float2 Sample2DContinuous(float2 u, DistributionDiscript discript, StructuredBuffer<float2> marginal, StructuredBuffer<float2> conditions, out float pdf)
{
    float pdfMarginal;
    int v;
    float d1 = Sample1DContinuous(u.y, discript, discript.domain.xy, marginal, pdfMarginal, v);
    int nu;
    float pdfCondition;
    DistributionDiscript dCondition = (DistributionDiscript)0;
    dCondition.start = v * (discript.unum + 1);   //the size of structuredbuffer is func.size + 1, because the cdfs size is func.size + 1 
    dCondition.num = discript.unum;
    float d0 = Sample1DContinuous(u.x, dCondition, discript.domain.zw, conditions, pdfCondition, nu);
    //p(v|u) = p(u,v) / pv(u)
    //so 
    //p(u,v) = p(v|u) * pv(u)
    pdf = pdfCondition * pdfMarginal;
    return float2(d0, d1);
}

float Distribution2DPdf(float2 u, DistributionDiscript discript, StructuredBuffer<float2> marginal, StructuredBuffer<float2> conditions)
{
    int iu = clamp(int(u[0] * discript.unum), 0, discript.unum - 1);
    int iv = clamp(int(u[1] * discript.num), 0, discript.num - 1);
    return 0;//conditions[iv]->func[iu] / pMarginal->funcInt;
}

#endif
