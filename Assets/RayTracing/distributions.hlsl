#ifndef DISTRIBUTIONS_HLSL
#define DISTRIBUTIONS_HLSL

StructuredBuffer<DistributionDiscript> DistributionDiscripts;

//binary search
int FindIntervalSmall(int start, int size, float u, StructuredBuffer<float2> funcs)
{
    if (size < 2)
        return start;
    int first = 0, len = size;
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
    return clamp(first - 1, 0, size - 2) + start;
}


int Sample1DDiscrete(float u, DistributionDiscript discript, StructuredBuffer<float2> funcs, out float pdf)
{
    int offset = FindIntervalSmall(discript.start, discript.num, u, funcs);
    float du = u - funcs[offset].y;
    if ((funcs[offset + 1].y - funcs[offset].y) > 0)
    {
        du /= (funcs[offset + 1].y - funcs[offset].y);
    }

    // Compute PDF for sampled offset
    pdf = funcs[offset].x;


    return offset - discript.start; //(int)(offset - discript.start + du) / discript.num;
}

float Sample1DContinuous(float u, DistributionDiscript discript, StructuredBuffer<float2> funcs, out float pdf, out int off)
{
    // Find surrounding CDF segments and _offset_
    int offset = FindIntervalSmall(discript.start, discript.num, u, funcs);
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
    return (offset - discript.start + du) / discript.num;
}

float DiscretePdf(int index, StructuredBuffer<float2> funcs)
{
    return funcs[index].x;
}

float2 Sample2DContinuous(float2 u, DistributionDiscript dMargin, StructuredBuffer<float2> marginal, StructuredBuffer<float2> conditions, out float pdf)
{
    float pdfMarginal;
    int v;
    float d1 = Sample1DContinuous(u.y, dMargin, marginal, pdfMarginal, v);
    int nu;
    float pdfCondition;
    DistributionDiscript dCondition = (DistributionDiscript)0;
    dCondition.start = dMargin.num + v * dMargin.unum;
    dCondition.num = dMargin.unum;
    float d0 = Sample1DContinuous(u.x, dCondition, conditions, pdfCondition, nu);
    //p(v|u) = p(u,v) / pv(u)
    //so 
    //p(u,v) = p(v|u) * pv(u)
    pdf = pdfCondition * pdfMarginal;
    return float2(d0, d1);
}

#endif
