#ifndef BXDF_HLSL
#define BXDF_HLSL
#include "sampler.hlsl"
#include "geometry.hlsl"
#include "fresnel.hlsl"

float3 Reflect(float3 wo, float3 n) {
    return -wo + 2 * dot(wo, n) * n;
}

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
    roughness = max(roughness, 0.001);
    float x = log(roughness);
    return 1.62142f + 0.819955f * x + 0.1734f * x * x +
        0.0171201f * x * x * x + 0.000640711f * x * x * x * x;
}

float TrowbridgeReitzLambda(float3 w, float alphax, float alphay)
{
    float absTanTheta = abs(TanTheta(w));
    if (isinf(absTanTheta))
        return 0;
    // Compute _alpha_ for direction _w_
    float alpha =
        sqrt(Cos2Phi(w) * alphax * alphax + Sin2Phi(w) * alphay * alphay);
    float alpha2Tan2Theta = (alpha * absTanTheta) * (alpha * absTanTheta);
    return (-1.0 + sqrt(1.0 + alpha2Tan2Theta)) * 0.5;
}

float TrowbridgeReitzD(float3 wh, float alphax, float alphay)
{
    float tan2Theta = Tan2Theta(wh);
    if (isinf(tan2Theta))
        return 0;
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
        if (u[1] > 0.5f) 
            phi += PI;
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

float MicrofacetReflectionPdf(float3 wo, float3 wi, float alphax, float alphay)
{
    if (!SameHemisphere(wo, wi))
        return 0;
    float3 wh = normalize(wo + wi);
    float D = TrowbridgeReitzD(wh, alphax, alphay);
    return MicrofacetPdf(D, wh) / (4.0 * dot(wo, wh));
}

struct BxDFPlastic
{
    float alphax;
    float alphay;
    float etaI;
    float etaT;
    float3 R;
    

    float3 Sample_wh(float2 u, float3 wo)
    {
        return SampleTrowbridgeReitzDistributionVector(wo, u, alphax, alphay);
    }

    float3 Sample_F(float2 u, float3 wo, out float3 wi, out float pdf)
    {
        if (wo.z == 0)
            return float3(0, 0, 0);

        float3 wh = Sample_wh(u, wo);
        wh = normalize(wh);
        if (dot(wo, wh) < 0)
            return float3(0, 0, 0);

        wi = reflect(-wo, wh);
        if (!SameHemisphere(wo, wi))
            return float3(0, 0, 0);

        float D = TrowbridgeReitzD(wh, alphax, alphay);

        pdf = MicrofacetPdf(D, wh) / (4 * dot(wo, wh));
        float cosThetaI = AbsCosTheta(wi);
        float cosThetaO = AbsCosTheta(wo);
        float3 F = FrDielectric(cosThetaI, etaI, etaT);
        return R * D * MicrofacetG(wo, wi, alphax, alphay) * F * 0.25 / (cosThetaI * cosThetaO);
    }

    float Pdf(float3 wo, float3 wi)
    {
        return MicrofacetReflectionPdf(wo, wi, alphax, alphay);
    }

    float3 F(float3 wo, float3 wi, out float pdf)
    {
        float cosThetaO = AbsCosTheta(wo);
        float cosThetaI = AbsCosTheta(wi);
        float3 wh = wi + wo;
        pdf = 0;
        // Handle degenerate cases for microfacet reflection
        if (cosThetaI == 0 || cosThetaO == 0)
            return float3(0, 0, 0);
        if (wh.x == 0 && wh.y == 0 && wh.z == 0)
            return float3(0, 0, 0);
        wh = normalize(wh);
        // For the Fresnel call, make sure that wh is in the same hemisphere
        // as the surface normal, so that TIR is handled correctly.
        float3 F = FrDielectric(dot(wi, Faceforward(wh, float3(0, 0, 1))), etaI, etaT);
        
        float D = TrowbridgeReitzD(wh, alphax, alphay);
        pdf = MicrofacetPdf(D, wh) * 0.25 / (dot(wo, wh));

        return R * D * MicrofacetG(wo, wi, alphax, alphay) * F * 0.25 /
            (cosThetaI * cosThetaO);
    }

    float3 Fresnel(float3 wo, float3 wi)
    {
        float cosThetaI = AbsCosTheta(wi);
        return FrDielectric(cosThetaI, etaI, etaT);
    }
};

struct BxDFMetal
{
    float alphax;
    float alphay;
    float3 R;
    float3 K;
    float3 etaI;
    float3 etaT;

    float3 Sample_wh(float2 u, float3 wo)
    {
        return SampleTrowbridgeReitzDistributionVector(wo, u, alphax, alphay);
    }

    float3 Sample_F(float2 u, float3 wo, out float3 wi, out float pdf)
    {
        if (wo.z == 0)
            return float3(0, 0, 0);

        float3 wh = Sample_wh(u, wo);
        wh = normalize(wh);
        if (dot(wo, wh) < 0)
            return float3(0, 0, 0);

        wi = normalize(reflect(-wo, wh));
        if (!SameHemisphere(wo, wi))
            return float3(0, 0, 0);

        float D = TrowbridgeReitzD(wh, alphax, alphay);

        pdf = MicrofacetPdf(D, wh) * 0.25 / (dot(wo, wh));
        float cosThetaI = AbsCosTheta(wi);
        float cosThetaO = AbsCosTheta(wo);
        //etaI = float3(1, 1, 1);
        //etaT = float3(0, 0, 0);
        //K = 0; // float3(3.9747, 2.38, 1.5998);
        float3 Fresnel = FrConductor(abs(dot(wi, Faceforward(wh, float3(0, 0, 1)))), etaI, etaT, K);
        return R * D * MicrofacetG(wo, wi, alphax, alphay) * Fresnel / (4 * cosThetaI * cosThetaO);
    }

    float Pdf(float3 wo, float3 wi)
    {
        return MicrofacetReflectionPdf(wo, wi, alphax, alphay);
    }

    float F(float3 wo, float3 wi, out float pdf)
    {
        float cosThetaO = AbsCosTheta(wo);
        float cosThetaI = AbsCosTheta(wi);
        float3 wh = wi + wo;
        pdf = 0;
        // Handle degenerate cases for microfacet reflection
        if (cosThetaI == 0 || cosThetaO == 0)
            return float3(0, 0, 0);
        if (wh.x == 0 && wh.y == 0 && wh.z == 0)
            return float3(0, 0, 0);
        wh = normalize(wh);
        // For the Fresnel call, make sure that wh is in the same hemisphere
        // as the surface normal, so that TIR is handled correctly.
        //K = 0; // float3(3.9747, 2.38, 1.5998);
        //etaI = float3(1, 1, 1);
        //etaT = float3(0, 0, 0);
        float3 Fresnel = FrConductor(abs(dot(wi, Faceforward(wh, float3(0, 0, 1)))), etaI, etaT, K);
        float D = TrowbridgeReitzD(wh, alphax, alphay);
        pdf = MicrofacetPdf(D, wh) * 0.25 / (dot(wo, wh));
        return R * D * MicrofacetG(wo, wi, alphax, alphay) * Fresnel /
            (4 * cosThetaI * cosThetaO);
    }

    float3 Fresnel(float3 wo, float3 wi)
    {
        float3 wh = wi + wo;
        wh = normalize(wh);
        if (!SameHemisphere(wo, wi))
            return float3(0, 0, 0);
        return FrConductor(abs(dot(wi, Faceforward(wh, float3(0, 0, 1)))), 1, etaT, K);
    }
};

struct BxDFMicrofacetTransmission
{
    float etaA;
    float etaB;
    float3 T;
};

struct BxDFLambertReflection
{
    float3 R;

    float3 Sample_wh(float2 u, float3 wo)
    {
        return CosineSampleHemisphere(u);
    }

    float3 Sample_F(float2 u, float3 wo, out float3 wi, out float pdf)
    {
        wi = CosineSampleHemisphere(u);
        if (wo.z < 0)
            wi.z *= -1;
        pdf = LambertPDF(wi, wo);
        return LambertBRDF(wi, wo, R);
    }

    float Pdf(float3 wo, float3 wi)
    {
        return LambertPDF(wi, wo);
    }

    float3 Fresnel(float3 wo, float3 wi)
    {
        return 1;
    }
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


/*
float3 MicrofacetReflectionF(float3 wo, float3 wi, BxDFMicrofacetReflection bxdf, out float pdf)
{
    float cosThetaO = AbsCosTheta(wo);
    float cosThetaI = AbsCosTheta(wi);
    float3 wh = wi + wo;
    pdf = 0;
    // Handle degenerate cases for microfacet reflection
    if (cosThetaI == 0 || cosThetaO == 0) 
        return float3(0, 0, 0);
    if (wh.x == 0 && wh.y == 0 && wh.z == 0) 
        return float3(0, 0, 0);
    wh = normalize(wh);
    // For the Fresnel call, make sure that wh is in the same hemisphere
    // as the surface normal, so that TIR is handled correctly.
    //float3 F = bxdf.fresnel.fresnelType == FresnelDielectric ? FrDielectric(dot(wi, Faceforward(wh, float3(0, 0, 1))), bxdf.fresnel.etaI.x, bxdf.fresnel.etaT.x)
    //   : FrConductor(dot(wi, Faceforward(wh, float3(0, 0, 1))), bxdf.fresnel.etaI, bxdf.fresnel.etaT, bxdf.fresnel.k);
    float3 F = FrSchlick(bxdf.R, dot(wo, wh));
    float D = TrowbridgeReitzD(wh, bxdf.alphax, bxdf.alphay);
    pdf = MicrofacetPdf(D, wh) / (4.0 * dot(wo, wh));

    return bxdf.R * D * MicrofacetG(wo, wi, bxdf.alphax, bxdf.alphay) * F /
        (4 * cosThetaI * cosThetaO);
}



float3 SampleMicrofacetReflectionF(BxDFMicrofacetReflection bxdf, float2 u, float3 wo, out float3 wi, out float pdf)
{
    if (wo.z == 0)
        return float3(0, 0, 0);

    float3 wh = SampleTrowbridgeReitzDistributionVector(wo, u, bxdf.alphax, bxdf.alphay);
    wh = normalize(wh);
    if (dot(wo, wh) < 0)
        return float3(0, 0, 0);

    wi = reflect(-wo, wh);
    if (!SameHemisphere(wo, wi))
        return float3(0, 0, 0);

    float D = TrowbridgeReitzD(wh, bxdf.alphax, bxdf.alphay);

    pdf = MicrofacetPdf(D, wh) / (4 * dot(wo, wh));
    float cosThetaI = AbsCosTheta(wi);
    float cosThetaO = AbsCosTheta(wo);
    //float3 F = bxdf.fresnel.fresnelType == FresnelDielectric ? FrDielectric(cosThetaI, bxdf.fresnel.etaI.x, bxdf.fresnel.etaT.x)
    //     : FrConductor(cosThetaI, bxdf.fresnel.etaI, bxdf.fresnel.etaT, bxdf.fresnel.k);
    float3 F = FrSchlick(bxdf.R, dot(wo, wh));
    return bxdf.R * D * MicrofacetG(wo, wi, bxdf.alphax, bxdf.alphay) * F * 0.25 / (cosThetaI * cosThetaO);
}
*/
#endif
