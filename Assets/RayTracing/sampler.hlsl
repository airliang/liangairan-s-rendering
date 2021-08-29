//shadertoy:https://www.shadertoy.com/view/4djSRW#
#ifndef SAMPLER_HLSL
#define SAMPLER_HLSL
#include "mathdef.hlsl"

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

//同心圆盘采样
float2 ConcentricSampleDisk(float2 u)
{
	//mapping u to [-1,1]
	float2 u1 = float2(u.x * 2.0f - 1, u.y * 2.0f - 1);

	if (u1.x == 0 && u1.y == 0)
		return float2(0, 0);

	//r = x
	//θ = y/x * π/4
	//最后返回x,y
	//x = rcosθ, y = rsinθ
	float theta, r;
	if (abs(u1.x) > abs(u1.y))
	{
		r = u1.x;
		theta = u1.y / u1.x * INV_FOUR_PI;
	}
	else
	{
		//这里要反过来看，就是把视野选择90度
		r = u1.y;
		theta = INV_TWO_PI - u1.x / u1.y * INV_FOUR_PI;
	}
	return r * float2(cos(theta), sin(theta));
}

//
//由于radiance是以cosθ作为权重，所以越接近顶点的radiance贡献越大
//如果希望收敛更快，pdf最好是和贡献差不多
//可以近似地认为：p(ω)∝cosθ
//假设p(ω) = kcosθ
//∫[Hemisphere]p(ω)dω = ∫[Hemisphere]kcosθsinθdθdφ
//= ∫[Hemisphere]ksinθdsinθdφ
//= ∫[0, 2π]k/2dφ = kπ= 1
// k = 1/π
//p(ω) = cosθ/π
//根据∫p(ω)dω = ∫p(θ, φ)dθdφ => p(ω)sinθ = p(θ,φ)
//p(θ,φ) = cosθsinθ/π
float3 CosineSampleHemisphere(float2 u)
{
	//先采样单位圆盘的点
	//得到r φ
    float2 rphi = ConcentricSampleDisk(u);

	//把p(r,φ)转到p(θ,φ)
	//r = sinθ
	//Jt = ∂r/∂θ  ∂r/∂φ  =  cosθ 0
	//     ∂φ/∂θ ∂φ/∂φ     0    1
	//p(r,φ) = p(θ,φ)/|Jt| = sinθ/π = r/π

	//由于随机变量r,φ所对应的随机变量θ,φ的pdf p(θ,φ)刚好满足
	//p(ω) = cosθ/π，所以直接拿r,φ做随机变量就能满足p(ω)的概率密度函数
	float z = sqrt(1.0f - rphi.x * rphi.x - rphi.y * rphi.y);
	return float3(rphi.x, rphi.y, z);
}

struct CameraSample 
{
    float2 pFilm;
    //Point2f pLens;
    //Float time;
};

struct RNG
{
    uint state;
    uint s1;
};

RWStructuredBuffer<RNG>    RNGs;

float UniformFloat(inout RNG rng)
{
    uint lcg_a = 1664525u;
    uint lcg_c = 1013904223u;
    rng.state = lcg_a * rng.state + lcg_c;
    rng.s1 = 0;
    return (rng.state & 0x00ffffffu) * (1.0f / (0x01000000u));
}

float2 Get2D(uint threadId)
{
    RNG rng = RNGs[threadId];
    float2 u = float2(UniformFloat(rng), UniformFloat(rng));
    RNGs[threadId] = rng;
    return u;
}

float Get1D(uint threadId)
{
    RNG rng = RNGs[threadId];
    float u = UniformFloat(rng);
    RNGs[threadId] = rng;
    return u;
}

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

    float2 Get2D(int rngIndex)
    {
        RNG rng = RNGs[rngIndex];
        float2 u = float2(UniformFloat(rng), UniformFloat(rng));
        RNGs[rngIndex] = rng;
        return u;
    }

    CameraSample GetCameraSample(float2 pRaster, float t)
    {
        CameraSample camSample;
        camSample.pFilm = pRaster + Get2D(pRaster, t);
        return camSample;
    }

    CameraSample GetCameraSample(float2 pRaster, int rngIndex)
    {
        CameraSample camSample;
        camSample.pFilm = pRaster + Get2D(rngIndex); // *0.5 - float2(0.5, 0.5);
        return camSample;
    }
};

RandomSampler rs;

#endif



