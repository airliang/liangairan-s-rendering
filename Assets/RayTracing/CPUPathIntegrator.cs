using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CPUPathIntegrator
{
    static float INV_PI = 0.31830988618379067154f;
    static float INV_TWO_PI = 0.15915494309189533577f;
    static float INV_FOUR_PI = 0.07957747154594766788f;
    static float HALF_PI = 1.57079632679489661923f;
    static float INV_HALF_PI = 0.63661977236758134308f;
    static float PI_OVER_2 = 1.57079632679489661923f;
    static float PI_OVER_4 = 0.78539816339744830961f;
    public enum BSDFMaterial
    {
        Matte,
        Plastic,
        Metal,
        Mirror,
        Glass,
    }
    struct PathVertex
    {
        public Vector3 wi;
        public Vector3 bsdfVal;
        public float bsdfPdf;
        public GPUInteraction nextISect;
        public int found;
    }

    static int BXDF_REFLECTION = 1;
    static int BXDF_TRANSMISSION = 1 << 1;
    static int BXDF_DIFFUSE = 1 << 2;
    static int BXDF_SPECULAR = 1 << 3;

    struct BSDFSample
    {
        public Vector3 reflectance;
        public Vector3 wi;   //in local space (z up space)
        public float pdf;
        public float eta;
        public int bxdfFlag;

        public bool IsSpecular()
        {
            return (bxdfFlag & BXDF_SPECULAR) > 0;
        }
    };

    static Vector3 Faceforward(Vector3 normal, Vector3 v)
    {
        return (Vector3.Dot(normal, v) < 0.0f) ? -normal : normal;
    }

    static float PowerHeuristic(int nf, float fPdf, int ng, float gPdf)
    {
        float f = nf * fPdf, g = ng * gPdf;
        return (f * f) / (f * f + g * g);
    }

    static Vector2 ConcentricSampleDisk(Vector2 u)
    {
        //mapping u to [-1,1]
        Vector2 u1 = new Vector2(u.x * 2.0f - 1, u.y * 2.0f - 1);

        if (u1.x == 0 && u1.y == 0)
            return Vector2.zero;

        //r = x
        //θ = y/x * π/4
        //最后返回x,y
        //x = rcosθ, y = rsinθ
        float theta, r;
        if (Mathf.Abs(u1.x) > Mathf.Abs(u1.y))
        {
            r = u1.x;
            theta = u1.y / u1.x * PI_OVER_4;
        }
        else
        {
            //这里要反过来看，就是把视野选择90度
            r = u1.y;
            theta = PI_OVER_2 - u1.x / u1.y * PI_OVER_4;
        }
        return r * new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
    }

    static Vector3 CosineSampleHemisphere(Vector2 u)
    {
        Vector2 rphi = ConcentricSampleDisk(u);

        float z = Mathf.Sqrt(1.0f - rphi.x * rphi.x - rphi.y * rphi.y);
        return new Vector3(rphi.x, rphi.y, z);
    }
    static bool SameHemisphere(Vector3 w, Vector3 wp)
    {
        return w.z * wp.z > 0;
    }

    static float FrDielectric(float cosThetaI, float etaI, float etaT)
    {
        cosThetaI = Mathf.Clamp(cosThetaI, -1, 1);
        // Potentially swap indices of refraction
        //etaI = 0;
        //etaT = 0;
        bool entering = cosThetaI > 0;
        if (!entering)
        {
            //swap(etaI, etaT);
            float tmp = etaI;
            etaI = etaT;
            etaT = tmp;
            cosThetaI = Mathf.Abs(cosThetaI);
        }

        // Compute _cosThetaT_ using Snell's law
        float sinThetaI = Mathf.Sqrt(Mathf.Max(0, 1 - cosThetaI * cosThetaI));
        float sinThetaT = etaI / etaT * sinThetaI;

        // Handle total internal reflection
        if (sinThetaT >= 1)
            return 1;
        float cosThetaT = Mathf.Sqrt(Mathf.Max((float)0, 1 - sinThetaT * sinThetaT));
        float Rparl = ((etaT * cosThetaI) - (etaI * cosThetaT)) /
            ((etaT * cosThetaI) + (etaI * cosThetaT));
        float Rperp = ((etaI * cosThetaI) - (etaT * cosThetaT)) /
            ((etaI * cosThetaI) + (etaT * cosThetaT));
        return (Rparl * Rparl + Rperp * Rperp) / 2;
    }

    struct BxDFFresnelSpecular
    {
        public Vector3 R;
        public Vector3 T;
        public float eta;

        public BSDFSample Sample_F(Vector2 u, Vector3 wo)
        {
            BSDFSample bsdfSample = new BSDFSample();

            float F = FrDielectric(wo.z, 1.0f, eta);
            float pdf = 0;
            if (u[0] < F)
            {
                bsdfSample.bxdfFlag = BXDF_REFLECTION | BXDF_SPECULAR;
                // Compute specular reflection for _FresnelSpecular_

                // Compute perfect specular reflection direction
                Vector3 wi = new Vector3(-wo.x, -wo.y, wo.z);
                //if (sampledType)
                //    *sampledType = BxDFType(BSDF_SPECULAR | BSDF_REFLECTION);
                bsdfSample.pdf = F;
                bsdfSample.reflectance = F * R / Mathf.Abs(wi.z);
                bsdfSample.wi = wi;
                return bsdfSample;
            }
            else
            {
                // Compute specular transmission for _FresnelSpecular_
                bsdfSample.bxdfFlag = BXDF_TRANSMISSION | BXDF_SPECULAR;
                // Figure out which $\eta$ is incident and which is transmitted
                bool entering = wo.z > 0;
                float etaI = entering ? 1 : eta;
                float etaT = entering ? eta : 1;

                // Compute ray direction for specular transmission
                Vector3 wi = Vector3.zero;
                bool bValid = Refract(wo, Faceforward(new Vector3(0, 0, 1), wo), etaI / etaT, ref wi);
                bsdfSample.wi = wi;
                if (!bValid)
                {
                    bsdfSample.reflectance = Vector3.one;
                    bsdfSample.pdf = 1;
                    return bsdfSample;
                }
                //T = 1;
                F = 0;
                Vector3 ft = T.Mul(1.0f - F);

                // Account for non-symmetry with transmission to different medium
                //if (mode == TransportMode::Radiance)
                ft *= (etaI * etaI) / (etaT * etaT);
                //if (sampledType)
                //    *sampledType = BxDFType(BSDF_SPECULAR | BSDF_TRANSMISSION);
                bsdfSample.pdf = 1 - F;
                bsdfSample.reflectance = ft / Mathf.Abs(wi.z);
                return bsdfSample;
            }
        }

        float Pdf(Vector3 wo, Vector3 wi)
        {
            return 0;
        }

        public Vector3 F(Vector3 wo, Vector3 wi, ref float pdf)
        {
            pdf = 0;
            return Vector3.zero;
        }
    };


    static bool Refract(Vector3 wi, Vector3 n, float eta, ref Vector3 wt)
    {
        float cosThetaI = Vector3.Dot(n, wi);
        float sin2ThetaI = Mathf.Max(0, (1.0f - cosThetaI * cosThetaI));
        float sin2ThetaT = eta * eta * sin2ThetaI;

        // Handle total internal reflection for transmission
        if (sin2ThetaT >= 1)
            return false;
        float cosThetaT = Mathf.Sqrt(1.0f - sin2ThetaT);
        wt = eta * -wi + (eta * cosThetaI - cosThetaT) * n;
        return true;
    }

    static Vector3 LambertBRDF(Vector3 wi, Vector3 wo, Vector3 R)
    {
        return wo.z == 0 ? Vector3.zero : R.Div(Mathf.PI);
    }

    //wi and wo must in local space
    static float LambertPDF(Vector3 wi, Vector3 wo)
    {
        return SameHemisphere(wo, wi) ? Mathf.Abs(wi.z) / Mathf.PI : 0;
    }

    static Vector3 MaterialBRDF(GPUMaterial material, GPUInteraction isect, Vector3 wo, Vector3 wi, ref float pdf)
    {
        void ComputeBxDFFresnelSpecular(GPUMaterial shadingMaterial, ref BxDFFresnelSpecular bxdf)
        {
            //UnpackFresnel(shadingMaterial, bxdf.fresnel);
            bxdf.T = shadingMaterial.transmission;
            bxdf.R = shadingMaterial.baseColor;
            bxdf.eta = shadingMaterial.eta.x;
        }

        //ShadingMaterial shadingMaterial = (ShadingMaterial)0;
        Vector3 f = Vector3.zero;
        pdf = 0;
        if (material.materialType == -1)
        {

        }
        else
        {
            //UnpackShadingMaterial(material, shadingMaterial, isect);
            int nComponent = 0;
            if (material.materialType == (int)BSDFMaterial.Glass)
            {

                nComponent = 1;
                BxDFFresnelSpecular bxdfFresnelSpecular = new BxDFFresnelSpecular();
                ComputeBxDFFresnelSpecular(material, ref bxdfFresnelSpecular);
                float pdfReflection = 0;
                f += bxdfFresnelSpecular.F(wo, wi, ref pdfReflection);
                pdf += pdfReflection;
            }
            else
            {
                nComponent = 1;
                f += LambertBRDF(wi, wo, material.baseColor);
                pdf += LambertPDF(wi, wo);
            }
            if (nComponent > 1)
            {
                pdf /= (float)nComponent;
                f /= (float)nComponent;
            }
        }

        return f;
    }

    static void ComputeBxDFFresnelSpecular(GPUMaterial shadingMaterial, ref BxDFFresnelSpecular bxdf)
    {
        //UnpackFresnel(shadingMaterial, bxdf.fresnel);
        bxdf.T = shadingMaterial.transmission;
        bxdf.R = shadingMaterial.baseColor;
        bxdf.eta = shadingMaterial.eta.x;
    }


    static BSDFSample SampleGlass(GPUMaterial material, Vector3 wo)
    {

        BxDFFresnelSpecular bxdf = new BxDFFresnelSpecular();
        ComputeBxDFFresnelSpecular(material, ref bxdf);
        Vector2 u = Get2D();
        return bxdf.Sample_F(u, wo);
    }

    static BSDFSample SampleLambert(GPUMaterial material, Vector3 wo)
    {
        BSDFSample bsdfSample = new BSDFSample();
        Vector2 u = Get2D();
        Vector3 wi = CosineSampleHemisphere(u);
        if (wo.z < 0)
            wi.z *= -1;
        bsdfSample.wi = wi;
        bsdfSample.pdf = LambertPDF(wi, wo);
        bsdfSample.reflectance = LambertBRDF(wi, wo, material.baseColor);
        return bsdfSample;
    }

    //wi wo is a vector which in local space of the interfaction surface
    static BSDFSample SampleMaterialBRDF(GPUMaterial material, GPUInteraction isect, Vector3 wo)
    {
        switch (material.materialType)
        {
            //case Disney:
            //	return 0;
            case (int)BSDFMaterial.Matte:
                return SampleLambert(material, wo);
            case (int)BSDFMaterial.Plastic:
                //return SamplePlastic(material, wo);
            case (int)BSDFMaterial.Metal:
                //return SampleMetal(material, wo);
            case (int)BSDFMaterial.Mirror:
                //return SampleMirror(material, wo);
            case (int)BSDFMaterial.Glass:
                return SampleGlass(material, wo);
            default:
                return SampleLambert(material, wo);
        }
        //return SampleGlass(material, wo);
    }

    public static GPURay SpawnRay(Vector3 p, Vector3 direction, Vector3 normal, float tMax)
    {
        float origin() { return 1.0f / 32.0f; }
        float float_scale() { return 1.0f / 65536.0f; }
        float int_scale() { return 256.0f; }

        Vector3 offset_ray(Vector3 p, Vector3 n)
        {
            Vector3Int of_i = new Vector3Int((int)(int_scale() * n.x), (int)(int_scale() * n.y), (int)(int_scale() * n.z));

            Vector3 p_i = new Vector3(
                MathUtil.Int32BitsToSingle(MathUtil.SingleToInt32Bits(p.x) + ((p.x < 0) ? -of_i.x : of_i.x)),
                MathUtil.Int32BitsToSingle(MathUtil.SingleToInt32Bits(p.y) + ((p.y < 0) ? -of_i.y : of_i.y)),
                MathUtil.Int32BitsToSingle(MathUtil.SingleToInt32Bits(p.z) + ((p.z < 0) ? -of_i.z : of_i.z)));

            return new Vector3(Mathf.Abs(p.x) < origin() ? p.x + float_scale() * n.x : p_i.x,
                Mathf.Abs(p.y) < origin() ? p.y + float_scale() * n.y : p_i.y,
                Mathf.Abs(p.z) < origin() ? p.z + float_scale() * n.z : p_i.z);
        }

        GPURay ray;
        float s = Mathf.Sign(Vector3.Dot(normal, direction));
        normal *= s;
        ray.orig = offset_ray(p, normal);
        ray.tmax = tMax;
        ray.direction = direction;
        ray.tmin = 0;
        return ray;
    }

    static bool ClosestHit(GPURay ray, ref GPUInteraction isect, GPUSceneData gpuSceneData)
    {
        bool hitted = true;
        while (true)
        {
            float hitT = 0;
            hitted = gpuSceneData.BVH.IntersectInstTest(ray, gpuSceneData.meshInstances, gpuSceneData.meshHandles, gpuSceneData.InstanceBVHAddr, out hitT, ref isect, false);
            if (!hitted)
                break;
            else
            {
                if (isect.materialID == -1)
                {
                    ray = SpawnRay(isect.p, ray.direction, -isect.normal, float.MaxValue);
                }
                else
                {
                    //alphatest check must be implemented
                    break;
                }
            }
        }

        return hitted;
    }

    static bool ShadowRayVisibilityTest(GPUShadowRay shadowRay, Vector3 normal, GPUSceneData gpuSceneData)
    {
        GPURay ray = SpawnRay(shadowRay.p0, shadowRay.p1 - shadowRay.p0, normal, 1.0f - 0.001f);
        GPUInteraction isect = new GPUInteraction();
        return !ClosestHit(ray, ref isect, gpuSceneData);

        //!IntersectP(ray, hitT, meshInstanceIndex);
    }

    static int FindIntervalSmall(int start, int cdfSize, float u, List<Vector2> funcs)
    {
        if (cdfSize < 2)
            return start;
        int first = 0, len = cdfSize;
        while (len > 0)
        {
            int nHalf = len >> 1;
            int middle = first + nHalf;
            // Bisect range based on value of _pred_ at _middle_
            Vector2 distrubution = funcs[start + middle];
            if (distrubution.y <= u)
            {
                first = middle + 1;
                len -= nHalf + 1;
            }
            else
                len = nHalf;
        }
        //if first - 1 < 0, the clamp function is useless
        return Mathf.Clamp(first - 1, 0, cdfSize - 2) + start;
    }


    static int Sample1DDiscrete(float u, GPUDistributionDiscript discript, List<Vector2> funcs, ref float pmf)
    {
        int cdfSize = discript.num + 1;
        int offset = FindIntervalSmall(discript.start, cdfSize, u, funcs);
        float cdfOffset = funcs[offset].y;
        float cdfOffset1 = funcs[offset + 1].y;
        float du = u - cdfOffset;
        if ((cdfOffset1 - cdfOffset) > 0)
        {
            du /= (cdfOffset1 - cdfOffset);
        }

        // Compute PMF for sampled offset
        // pmf is the probability, so is the sample's area / total area
        pmf = discript.funcInt > 0 ? funcs[offset].x * (discript.domain.y - discript.domain.x) / (discript.funcInt * discript.num) : 0;


        return offset - discript.start; //(int)(offset - discript.start + du) / discript.num;
    }

    static float Sample1DContinuous(float u, GPUDistributionDiscript discript, List<Vector2> funcs, ref float pdf, ref int off)
    {
        // Find surrounding CDF segments and _offset_
        int cdfSize = discript.num + 1;
        int offset = FindIntervalSmall(discript.start, cdfSize, u, funcs);
        off = offset;
        // Compute offset along CDF segment
        float cdfOffset = funcs[offset].y;
        float cdfOffset1 = funcs[offset + 1].y;
        float du = u - cdfOffset;
        if ((cdfOffset1 - cdfOffset) > 0)
        {
            du /= (cdfOffset1 - cdfOffset);
        }

        // Compute PDF for sampled offset
        pdf = (discript.funcInt > 0) ? funcs[offset].x / discript.funcInt : 0;

        // Return $x\in{}[0,1)$ corresponding to sample
        return Mathf.Lerp(discript.domain.x, discript.domain.y, (offset - discript.start + du) / discript.num);
    }

    static float DiscretePdf(int index, GPUDistributionDiscript discript, List<Vector2> funcs)
    {
        return funcs[discript.start + index].x * (discript.domain.y - discript.domain.x) / (discript.funcInt * discript.num);
    }

    static Vector2 Sample2DContinuous(Vector2 u, GPUDistributionDiscript discript, List<Vector2> marginal, List<Vector2> conditions, List<float> conditionFuncInts, ref float pdf)
    {
        float pdfMarginal = 0;
        int v = 0;
        float d1 = Sample1DContinuous(u.y, discript, marginal, ref pdfMarginal, ref v);
        int nu = 0;
        float pdfCondition = 0;
        GPUDistributionDiscript dCondition = new GPUDistributionDiscript();
        dCondition.start = v * (discript.unum + 1);   //the size of structuredbuffer is func.size + 1, because the cdfs size is func.size + 1 
        dCondition.num = discript.unum;
        dCondition.funcInt = conditionFuncInts[v];
        dCondition.domain.x = discript.domain.z;
        dCondition.domain.y = discript.domain.w;
        float d0 = Sample1DContinuous(u.x, dCondition, conditions, ref pdfCondition, ref nu);
        //p(v|u) = p(u,v) / pv(u)
        //so 
        //p(u,v) = p(v|u) * pv(u)
        pdf = pdfCondition * pdfMarginal;
        return new Vector2(d0, d1);
    }

    static float Distribution2DPdf(Vector2 u, GPUDistributionDiscript discript, List<Vector2> marginal, List<Vector2> conditions)
    {
        int iu = (int)Mathf.Clamp((u[0] * discript.unum), 0, discript.unum - 1);
        int iv = (int)Mathf.Clamp((u[1] * discript.num), 0, discript.num - 1);
        int conditionVOffset = iv * (discript.unum + 1) + iu;
        return conditions[conditionVOffset].x / discript.funcInt;
    }

    static Vector3 SamplePointOnTriangle(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 u, ref Vector3 normal, ref float pdf)
    {
        //caculate bery centric uv w = 1 - u - v
        float t = Mathf.Sqrt(u.x);
        Vector2 uv = new Vector2(1.0f - t, t * u.y);
        float w = 1 - uv.x - uv.y;

        Vector3 position = p0 * w + p1 * uv.x + p2 * uv.y;
        Vector3 crossVector = Vector3.Cross(p1 - p0, p2 - p0);
        normal = Vector3.Normalize(crossVector);
        pdf = 1.0f / crossVector.magnitude;

        return position;
    }


    static Vector3 SampleTriangleLight(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 u, GPUInteraction isect, GPULight light, ref Vector3 wi, ref Vector3 position, ref float pdf)
    {
        Vector3 Li = Vector3.zero;
        Vector3 lightPointNormal = Vector3.zero;
        float triPdf = 0;
        position = SamplePointOnTriangle(p0, p1, p2, u, ref lightPointNormal, ref triPdf);
        pdf = triPdf;
        wi = position - isect.p;
        float wiLength = wi.magnitude;
        wi = Vector3.Normalize(wi);
        float cos = Vector3.Dot(lightPointNormal, -wi);
        float absCos = Mathf.Abs(cos);
        pdf *= wiLength * wiLength / absCos;
        if (float.IsNaN(pdf) || wiLength == 0)
        {
            pdf = 0;
            return Vector3.zero;
        }

        return cos > 0 ? light.radiance : Vector3.zero;
    }

    static int SampleTriangleIndexOfLightPoint(float u, GPUDistributionDiscript discript, List<Vector2> distributions, ref float pdf)
    {
        int index = Sample1DDiscrete(u, discript, distributions, ref pdf);
        return index;
    }

    static Vector3 SampleLightRadiance(GPULight light, GPUInteraction isect, 
    ref Vector3 wi, ref float lightPdf, ref Vector3 lightPoint, GPUSceneData gpuSceneData)
    {
        if (light.type == (int)LightInstance.LightType.Area)
        {
            int discriptIndex = light.distributionDiscriptIndex;
            GPUDistributionDiscript lightDistributionDiscript = gpuSceneData.gpuDistributionDiscripts[discriptIndex];
            float u = Get1D();
            float triPdf = 0;
            lightPdf = 0;
            MeshInstance meshInstance = gpuSceneData.meshInstances[light.meshInstanceID];
            int triangleIndex = SampleTriangleIndexOfLightPoint(u, lightDistributionDiscript, gpuSceneData.Distributions1D, ref lightPdf) * 3 + meshInstance.triangleStartOffset;

            int vertexStart = triangleIndex;
            int vIndex0 = gpuSceneData.triangles[vertexStart];
            int vIndex1 = gpuSceneData.triangles[vertexStart + 1];
            int vIndex2 = gpuSceneData.triangles[vertexStart + 2];
            Vector3 p0 = gpuSceneData.gpuVertices[vIndex0].position;
            Vector3 p1 = gpuSceneData.gpuVertices[vIndex1].position;
            Vector3 p2 = gpuSceneData.gpuVertices[vIndex2].position;
            //convert to worldpos

            p0 = meshInstance.localToWorld.MultiplyPoint(p0); 
            p1 = meshInstance.localToWorld.MultiplyPoint(p1); 
            p2 = meshInstance.localToWorld.MultiplyPoint(p2);

            Vector3 Li = SampleTriangleLight(p0, p1, p2, Get2D(), isect, light, ref wi, ref lightPoint, ref triPdf);
            lightPdf *= triPdf;
            return Li;
        }
        else if (light.type == (int)LightInstance.LightType.Envmap)
        {
            Vector2 u = Get2D();
            //float3 Li = UniformSampleEnviromentLight(u, lightPdf, wi); 
            Vector3 Li = Vector3.zero;
            //if (isUniform)
            //    Li = UniformSampleEnviromentLight(u, lightPdf, wi);
            //else
            //    Li = ImportanceSampleEnviromentLight(u, lightPdf, wi);
            //Li = isUniform ? float3(0.5, 0, 0) : Li;
            lightPoint = isect.p + wi * 10000.0f;
            return Li;
        }

        wi = Vector3.zero;
        lightPdf = 0;
        lightPoint = Vector3.zero;
        return Vector3.zero;
    }

    static Vector3 Light_Le(Vector3 wi, GPULight light)
    {
        if (light.type == 0)
        {
            return light.radiance;
        }
        //else if (light.type == 1)
        //{
        //    return EnviromentLightLe(wi);
        //}
        return Vector3.zero;
    }

    static float AreaLightPdf(GPULight light, GPUInteraction isect, GPUSceneData gpuSceneData)
    {
        float lightPdf = 0;
        if (light.type == 0)
        {
            GPUDistributionDiscript discript = gpuSceneData.gpuDistributionDiscripts[light.distributionDiscriptIndex];
            int distributionIndex = (int)isect.triangleIndex;
            float pmf = DiscretePdf(distributionIndex, discript, gpuSceneData.Distributions1D);
            lightPdf = pmf * 1.0f / isect.primArea;
        }
        //else if (light.type == EnvLightType)
        //{
        //	lightPdf = EnvLightLiPdf(wi, isUniform); //INV_FOUR_PI;
        //}

        return lightPdf;
    }

    public static float Get1D()
    {
        return UnityEngine.Random.Range(0.0f, 1.0f);
    }

    public static Vector2 Get2D()
    {
        return new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
    }

    static Vector3 MIS_ShadowRay(GPULight light, GPUInteraction isect, GPUMaterial material, float lightSourcePdf, GPUSceneData gpuSceneData)
    {
        Vector3 wi = Vector3.up;
        float lightPdf = 0;
        Vector3 samplePointOnLight = Vector3.zero;
        Vector3 ld = Vector3.zero;
        Vector3 Li = SampleLightRadiance(light, isect, ref wi, ref lightPdf, ref samplePointOnLight, gpuSceneData);
        lightPdf *= lightSourcePdf;
        //lightPdf = AreaLightPdf(light, isect, wi, _UniformSampleLight) * lightSourcePdf;
        if (Li != Vector3.zero)
        {
            GPUShadowRay shadowRay = new GPUShadowRay();
            shadowRay.p0 = isect.p;
            shadowRay.p1 = samplePointOnLight;
            //shadowRay.pdf = triPdf;
            //shadowRay.lightPdf = lightPdf;

            Vector3 wiLocal = isect.WorldToLocal(wi);
            Vector3 woLocal = isect.WorldToLocal(isect.wo);
            float scatteringPdf = 0;

            Vector3 f = MaterialBRDF(material, isect, woLocal, wiLocal, ref scatteringPdf);
            if (f != Vector3.zero && scatteringPdf > 0)
            {
                bool shadowRayVisible = ShadowRayVisibilityTest(shadowRay, isect.normal, gpuSceneData);

                if (shadowRayVisible)
                {
                    f *= Mathf.Abs(Vector3.Dot(wi, isect.normal));
                    //sample psdf and compute the mis weight
                    float weight =
                        PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                    ld = f.Mul(Li) * weight / lightPdf;
                    //ld = Li / lightPdf;
                }

            }
        }

        return ld;
    }

    static Vector3 MIS_BSDF(GPUInteraction isect, GPUMaterial material, GPULight light, int lightIndex, float lightSourcePdf, ref PathVertex pathVertex, GPUSceneData gpuSceneData)
    {
        Vector3 ld = Vector3.zero;
        Vector3 woLocal = isect.WorldToLocal(isect.wo);
        //Vector3 wi;
        //float scatteringPdf = 0;
        //pathVertex = (PathVertex)0;
        Vector3 wiLocal = Vector3.zero;
        Vector2 u = Get2D();

        BSDFSample bsdfSample = SampleMaterialBRDF(material, isect, woLocal);
        Vector3 wi = isect.LocalToWorld(bsdfSample.wi);
        float scatteringPdf = bsdfSample.pdf;
        Vector3 f = bsdfSample.reflectance * Mathf.Abs(Vector3.Dot(wi, isect.normal));

        if (f != Vector3.zero && scatteringPdf > 0)
        {
            GPURay ray = SpawnRay(isect.p, wi, isect.normal, float.MaxValue);
            //Interaction lightISect = (Interaction)0;
            bool found = ClosestHit(ray, ref pathVertex.nextISect, gpuSceneData);
            //pathVertex.nextISect = lightISect; 
            //pathVertex.found = found ? 1 : 0;  //can not use this expression or it will be something error. I don't know why.

            Vector3 li = Vector3.zero;
            float lightPdf = 0;

            if (found)
            {
                pathVertex.found = 1;

                uint meshInstanceIndex = pathVertex.nextISect.meshInstanceID;
                MeshInstance meshInstance = gpuSceneData.meshInstances[(int)meshInstanceIndex];
                if (meshInstance.lightIndex == lightIndex)
                {
                    lightPdf = AreaLightPdf(light, pathVertex.nextISect, gpuSceneData) * lightSourcePdf;

                    if (lightPdf > 0)
                    {
                        li = Light_Le(wi, light);
                    }
                }
            }
            //else if (_EnvLightIndex >= 0)//(light.type == EnvLightType)
            //{
            //    Light envLight = lights[_EnvLightIndex];
            //    li = Light_Le(wi, envLight);
            //    if (light.type != EnvLightType)
            //    {
            //        lightSourcePdf = LightSourcePmf(_EnvLightIndex, _UniformSampleLight);
            //        lightPdf = EnvLightLiPdf(wi, _UniformSampleLight) * lightSourcePdf;
            //    }
            //}

            float weight = 1;
            if (!bsdfSample.IsSpecular())
                weight = PowerHeuristic(1, scatteringPdf, 1, lightPdf);
            ld = f.Mul(li) * weight / scatteringPdf;
        }

        pathVertex.wi = wi;
        pathVertex.bsdfVal = f;
        pathVertex.bsdfPdf = scatteringPdf;

        return ld;
    }

    static int ImportanceSampleLightSource(float u, GPUDistributionDiscript discript, List<Vector2> discributions, ref float pmf)
    {
        return Sample1DDiscrete(u, discript, discributions, ref pmf);
    }

    static int SampleGPULightSource(float u, GPUDistributionDiscript discript, List<Vector2> discributions, ref float pmf)
    {
        //DistributionDiscript discript = (DistributionDiscript)0;
        //discript.start = 0;
        ////the length of cdfs is N+1
        //discript.num = lightCount;
        //discript.funcInt = 
        int index  = ImportanceSampleLightSource(u, discript, discributions, ref pmf); //SampleDistribution1DDiscrete(rs.Get1D(threadId), 0, lightCount, pdf);
        
        //int index = UniformSampleLightSource(u, discript, pmf);
        return index;
    }

    static GPULight SampleLightSource(ref float lightSourcePdf, ref int lightIndex, GPUSceneData gpuSceneData)
    {

        //some error happen in SampleLightSource
        lightSourcePdf = 0;
        float u = Get1D();
        GPUDistributionDiscript discript = gpuSceneData.gpuDistributionDiscripts[0];
        lightIndex = SampleGPULightSource(u, discript, gpuSceneData.Distributions1D, ref lightSourcePdf);
        //lightIndex = 0;
        //lightSourcePdf = 0.5;
        GPULight light = gpuSceneData.gpuLights[lightIndex];
        return light;
    }

    static Vector3 EstimateDirectLighting(GPUInteraction isect, ref PathVertex pathVertex, bool breakPath, GPUSceneData gpuSceneData)
    {
        breakPath = false;
        //PathRadiance pathRadiance = (PathRadiance)0;
        //pathRadiance.beta = Vector3(1, 1, 1);
        float lightSourcePdf = 0;
        GPUMaterial material = gpuSceneData.gpuMaterials[(int)isect.materialID];
        int lightIndex = 0;
        GPULight light = SampleLightSource(ref lightSourcePdf, ref lightIndex, gpuSceneData);

        //pathVertex = (PathVertex)0;
        Vector3 ld = MIS_ShadowRay(light, isect, material, lightSourcePdf, gpuSceneData);
        ld += MIS_BSDF(isect, material, light, lightIndex, lightSourcePdf, ref pathVertex, gpuSceneData);

        if (pathVertex.bsdfPdf == 0)
        {
            breakPath = true;
        }

        return ld;
    }

    public static Vector3 PathLi(GPURay ray, GPUSceneData gpuSceneData)
    {
        Vector3 li = Vector3.zero;
        Vector3 beta = Vector3.one;
        GPUInteraction isectLast;
        PathVertex pathVertex = new PathVertex();
        GPUInteraction isect = new GPUInteraction();
        for (int bounces = 0; bounces < 5; bounces++)
        {
            bool foundIntersect = false;
            if (bounces == 0)
                foundIntersect = ClosestHit(ray, ref isect, gpuSceneData);
            else
            {
                foundIntersect = pathVertex.found == 1;
            }

            //PathRadiance pathRadiance = pathRadiances[workIndex];
            if (foundIntersect)
            {
                int meshInstanceIndex = (int)isect.meshInstanceID;
                MeshInstance meshInstance = gpuSceneData.meshInstances[meshInstanceIndex];
                int lightIndex = meshInstance.lightIndex;



                //isect.p.w = 1;
                if (lightIndex >= 0 && bounces == 0)
                {
                    GPULight light = gpuSceneData.gpuLights[lightIndex];
                    li += light.radiance.Mul(beta);
                    //color = light.radiance;
                    //isect.p.w = 0;
                }

                bool breakPath = false;
                Vector3 ld = EstimateDirectLighting(isect, ref pathVertex, breakPath, gpuSceneData);
                //li += beta * SampleLight(isect, wi, rng, pathBeta, ray);
                li += ld.Mul(beta);
                //return li;
                if (breakPath)
                    break;

                Vector3 throughput = pathVertex.bsdfVal / pathVertex.bsdfPdf;
                beta = beta.Mul(throughput);

                //Russian roulette
                if (bounces > 3)
                {
                    float q = Mathf.Max(0.05f, 1 - beta.MaxComponent());
                    if (Get1D() < q)
                    {
                        break;
                    }
                    else
                        beta /= 1 - q;
                }

                isectLast = isect;
            }
            else
            {
                //sample enviroment map
                if (bounces == 0)
                {
                    li += Vector3.zero;
                }
                break;
            }

            //ray = SpawnRay(isect.p.xyz, pathVertex.wi, isect.normal, FLT_MAX);
            isect = pathVertex.nextISect;
            //if (pathVertex.found == 1 && pathVertex.nextISect.hitT == 0)
            //{
            //    //some error happen!
            //    return 0;
            //}

        }
        return li;
    }
}
