#ifndef BXDF_HLSL
#define BXDF_HLSL
#include "sampler.hlsl"
#include "geometry.hlsl"
#include "fresnel.hlsl"


struct BxDFMicrofacetReflection
{
    float alphax;
    float alphay;
    float3 R;
    float  D; //brdf distribution D term
    float  G; //brdf geometry shadow mask G term
    FresnelData fresnel;
};

struct BxDFMicrofacetTransmission
{
    float etaA;
    float etaB;
    float3 T;
    FresnelData fresnel;
};

struct BxDFLambertReflection
{
    float3 R;
};

struct BxDFSpecularReflection
{
    float3 R;
};

struct BxDFSpecularTransmission
{
    float  etaA;
    float  etaB;
    float3 T;
    FresnelData fresnel;
};

float3 LambertBRDF(float3 wi, float3 wo, float3 R)
{
	return wo.z == 0 ? 0 : R * INV_PI;
}

//wi and wo must in local space
float LambertPDF(float3 wi, float3 wo)
{
	return SameHemisphere(wo, wi) ? AbsCosTheta(wi) * INV_PI : 0;
}

float RoughnessToAlpha(float roughness) {
    roughness = max(roughness, (float)1e-3);
    float x = log(roughness);
    return 1.62142f + 0.819955f * x + 0.1734f * x * x +
        0.0171201f * x * x * x + 0.000640711f * x * x * x * x;
}

float TrowbridgeReitzLambda(float3 w, float alphax, float alphay) 
{
    float absTanTheta = abs(TanTheta(w));
    if (isinf(absTanTheta)) 
        return 0.;
    // Compute _alpha_ for direction _w_
    float alpha =
        sqrt(Cos2Phi(w) * alphax * alphax + Sin2Phi(w) * alphay * alphay);
    float alpha2Tan2Theta = (alpha * absTanTheta) * (alpha * absTanTheta);
    return (-1 + sqrt(1.f + alpha2Tan2Theta)) / 2;
}

float TrowbridgeReitzD(float3 wh, float alphax, float alphay)
{
    float tan2Theta = Tan2Theta(wh);
    //if (std::isinf(tan2Theta)) 
    //    return 0.;
    const float cos4Theta = Cos2Theta(wh) * Cos2Theta(wh);
    float e =
        (Cos2Phi(wh) / (alphax * alphax) + Sin2Phi(wh) / (alphay * alphay)) *
        tan2Theta;
    return 1 / (PI * alphax * alphay * cos4Theta * (1 + e) * (1 + e));
}

float3 SampleTrowbridgeReitzDistributionVector(float3 wo, float2 u, float alphax, float alphay)
{
    float phi = (2.0 * PI) * u[1];
    float cosTheta = 0;
    if (alphax == alphay)
    {
        float tanTheta2 = alphax * alphax * u[0] / (1.0 - u[0]);
        cosTheta = 1.0 / sqrt(1.0 + tanTheta2);
    }
    else
    {
        //https://agraphicsguy.wordpress.com/2018/07/18/sampling-anisotropic-microfacet-brdf/
        phi = atan(alphay / alphax * tan(2 * PI * u[1] + 0.5 * PI));
        if (u[1] > .5f) phi += PI;
        float sinPhi = sin(phi);
        float cosPhi = cos(phi);
        float alphax2 = alphax * alphax, alphay2 = alphay * alphay;
        float alpha2 =
            1 / (cosPhi * cosPhi / alphax2 + sinPhi * sinPhi / alphay2);
        float tanTheta2 = alpha2 * u[0] / (1 - u[0]);
        cosTheta = 1 / sqrt(1 + tanTheta2);
    }

    float sinTheta =
        sqrt(max(0, 1.0 - cosTheta * cosTheta));
    float3 wh = SphericalDirection(sinTheta, cosTheta, phi);
    if (!SameHemisphere(wo, wh)) 
        wh = -wh;

    return wh;
}

float MicrofacetG(float3 wo, float3 wi, float alphax, float alphay)
{
    return 1.0 / (1 + TrowbridgeReitzLambda(wo, alphax, alphay) + TrowbridgeReitzLambda(wi, alphax, alphay));
}

float MicrofacetPdf(float D, float3 wh)
{
    return D * AbsCosTheta(wh);
}

float3 MicrofacetReflectionF(float3 wo, float3 wi, BxDFMicrofacetReflection bxdf, out float pdf)
{
    float cosThetaO = AbsCosTheta(wo);
    float cosThetaI = AbsCosTheta(wi);
    float3 wh = wi + wo;
    // Handle degenerate cases for microfacet reflection
    if (cosThetaI == 0 || cosThetaO == 0) 
        return float3(0, 0, 0);
    if (wh.x == 0 && wh.y == 0 && wh.z == 0) 
        return float3(0, 0, 0);
    wh = normalize(wh);
    // For the Fresnel call, make sure that wh is in the same hemisphere
    // as the surface normal, so that TIR is handled correctly.
    float3 F = bxdf.fresnel.fresnelType == FresnelDielectric ? FrDielectric(dot(wi, Faceforward(wh, float3(0, 0, 1))), bxdf.fresnel.etaI.x, bxdf.fresnel.etaT.x)
        : FrConductor(dot(wi, Faceforward(wh, float3(0, 0, 1))), bxdf.fresnel.etaI, bxdf.fresnel.etaT, bxdf.fresnel.k);
    //float F = FrSchlick(bxdf.R, dot(wo, wh));
    float D = TrowbridgeReitzD(wh, bxdf.alphax, bxdf.alphay);
    pdf = MicrofacetPdf(D, wh) / (4.0 * dot(wo, wh));

    return bxdf.R * D * MicrofacetG(wo, wi, bxdf.alphax, bxdf.alphay) * F /
        (4 * cosThetaI * cosThetaO);
}

float MicrofacetReflectionPdf(float3 wo, float3 wi, float alphax, float alphay) 
{
    if (!SameHemisphere(wo, wi)) 
        return 0;
    float3 wh = normalize(wo + wi);
    float D = TrowbridgeReitzD(wh, alphax, alphay);
    return MicrofacetPdf(D, wh) / (4.0 * dot(wo, wh));
}

float3 SampleMicrofacetReflectionF(BxDFMicrofacetReflection bxdf, float2 u, float3 wo, out float3 wi, out float pdf)
{
    if (wo.z == 0)
        return float3(0, 0, 0);

    float3 wh = SampleTrowbridgeReitzDistributionVector(wo, u, bxdf.alphax, bxdf.alphay);
    wh = normalize(wh);
    if (dot(wo, wh) < 0)
        return float3(0, 0, 0);

    wi = reflect(wo, wh);
    if (!SameHemisphere(wo, wi))
        return float3(0, 0, 0);

    float D = TrowbridgeReitzD(wh, bxdf.alphax, bxdf.alphay);

    pdf = MicrofacetPdf(D, wh) / (4 * dot(wo, wh));
    float cosThetaI = AbsCosTheta(wi);
    float cosThetaO = AbsCosTheta(wo);
    float3 F = bxdf.fresnel.fresnelType == FresnelDielectric ? FrDielectric(cosThetaI, bxdf.fresnel.etaI.x, bxdf.fresnel.etaT.x)
        : FrConductor(cosThetaI, bxdf.fresnel.etaI, bxdf.fresnel.etaT, bxdf.fresnel.k);
    //float F = FrSchlick(bxdf.R, dot(wo, wh));
    return bxdf.R * D * MicrofacetG(wo, wi, bxdf.alphax, bxdf.alphay) * F / (4.0 * cosThetaI * cosThetaO);
}

#endif
