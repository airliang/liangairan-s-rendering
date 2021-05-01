//shadertoy:https://www.shadertoy.com/view/4djSRW#

//----------------------------------------------------------------------------------------
//  1 out, 1 in...
float hash11(float p)
{
    p = frac(p * .1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

//----------------------------------------------------------------------------------------
//  1 out, 2 in...
float hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//----------------------------------------------------------------------------------------
//  2 out, 1 in...
float2 hash21(float p)
{
    float3 p3 = frac(float3(p, p, p) * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);

}

//----------------------------------------------------------------------------------------
///  2 out, 2 in...
float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);

}

struct CameraSample 
{
    float2 pFilm;
    //Point2f pLens;
    //Float time;
};

class RandomSampler
{
    float2 Get2D(float2 p, float t)
    {
        float v = .152;
        float2 pos = (p * v + frac(t) * 1500. + 50.0);

        return hash22(pos);
    }

    float Get1D(float p, float t)
    {
        float v = .152;
        float pos = (p * v + frac(t) * 1500. + 50.0);

        return hash11(pos);
    }

    CameraSample GetCameraSample(float2 pRaster, float t)
    {
        CameraSample camSample;
        camSample.pFilm = pRaster + Get2D(pRaster, t);
        return camSample;
    }
};




