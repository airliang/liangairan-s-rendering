using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingTest
{
    static float INV_PI      = 0.31830988618379067154f;
    static float INV_TWO_PI  = 0.15915494309189533577f;
    static float INV_FOUR_PI = 0.07957747154594766788f;
    static float HALF_PI     = 1.57079632679489661923f;
    static float INV_HALF_PI = 0.63661977236758134308f;
    static float PI_OVER_2 = 1.57079632679489661923f;
    static float PI_OVER_4 = 0.78539816339744830961f;
    //同心圆盘采样
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

    static Vector3 LambertBRDF(Vector3 wi, Vector3 wo, Vector3 R)
    {
        return R * (1.0f / Mathf.PI);
    }

    static float AbsCosTheta(Vector3 w)
    {
        return Mathf.Abs(w.z);
    }

    //wi and wo must in local space
    static float LambertPDF(Vector3 wi, Vector3 wo)
    {
        return SameHemisphere(wo, wi) ? AbsCosTheta(wi) * (1.0f / Mathf.PI) : 0;
    }

    static Vector3 SampleLambert(GPUMaterial material, Vector3 wo, out Vector3 wi, Vector2 u, out float pdf)
    {
        wi = CosineSampleHemisphere(u);
        if (wo.z < 0)
            wi.z *= -1;
        pdf = LambertPDF(wo, wi);
        return LambertBRDF(wi, wo, material.kd.LinearToVector3());
    }
    static float PowerHeuristic(int nf, float fPdf, int ng, float gPdf)
    {
        float f = nf * fPdf, g = ng * gPdf;
        return (f * f) / (f * f + g * g);
    }

    static int FindIntervalSmall(List<Vector2> Distributions1D, int start, int size, float u)
    {
        int first = 0, len = size;
        while (len > 0)
        {
            int nHalf = len >> 1;
            int middle = first + nHalf;
            // Bisect range based on value of _pred_ at _middle_
            Vector2 distrubution = Distributions1D[start + middle];
            if (distrubution.y <= u)
            {
                first = middle + 1;
                len -= nHalf + 1;
            }
            else
                len = nHalf;
        }
        return Mathf.Clamp(first - 1, 0, size - 2) + start;
    }

    static int SampleDistribution1DDiscrete(List<Vector2> Distributions1D, float u, int start, int num, out float pdf)
    {
        int offset = FindIntervalSmall(Distributions1D, start, num, u);
        pdf = Distributions1D[start + offset].x;
        return offset;
    }

    public static void SampleLightTest(List<Vector2> Distributions1D, List<GPULight> gpuLights, List<MeshInstance> meshInstances, 
        List<int> triangles, List<GPUVertex> gpuVertices)
    {
        GPULight SampleLightSource(float u, out float pdf, out int index)
        {
            index = SampleDistribution1DDiscrete(Distributions1D, u, 0, gpuLights.Count, out pdf);
            return gpuLights[index];
        }

        int SampleLightTriangle(int start, int count, float u, out float pdf)
        {
            //get light mesh triangle index
            int index = SampleDistribution1DDiscrete(Distributions1D, u, start, count, out pdf);
            return index;
        }

        Vector3 SampleTrianglePoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 u, out Vector3 normal, out float pdf)
        {
            //caculate bery centric uv w = 1 - u - v
            float t = Mathf.Sqrt(u.x);
            Vector2 uv = new Vector2(1.0f - t, t * u.y);
            float w = 1 - uv.x - uv.y;

            Vector3 position = p0 * w + p1 * uv.x + p2 * uv.y;
            Vector3 crossVector = Vector3.Cross(p1 - p0, p2 - p0);
            normal = crossVector.normalized;
            pdf = 1.0f / crossVector.magnitude;

            return position;
        }

        Vector3 SampleTriangleLightRadiance(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 u, Vector3 p, Vector3 normal, GPULight light, out Vector3 wi, out Vector3 position, out float pdf)
        {
            Vector3 Li = light.radiance;
            Vector3 lightPointNormal;
            float triPdf = 0;
            position = SampleTrianglePoint(p0, p1, p2, u, out lightPointNormal, out triPdf);
            pdf = triPdf;
            wi = position - p;
            float wiLength = Vector3.Magnitude(wi);
            if (wiLength == 0)
            {
                Li = Vector3.zero;
                pdf = 0;
            }
            wi = Vector3.Normalize(wi);
            pdf *= wiLength * wiLength / Mathf.Abs(Vector3.Dot(lightPointNormal, -wi));

            return Li;
        }

        float u = UnityEngine.Random.Range(0.0f, 1.0f);

        int lightIndex = 0;
        float lightSourcePdf = 0;
        GPULight gpuLight = SampleLightSource(u, out lightSourcePdf, out lightIndex);

        u = UnityEngine.Random.Range(0.0f, 1.0f);
        MeshInstance meshInstance = meshInstances[gpuLight.meshInstanceID];
        float lightPdf = 0;
        int triangleIndex = (SampleLightTriangle(gpuLight.distributeAddress, gpuLight.trianglesNum, u, out lightPdf) - gpuLights.Count) * 3 + meshInstance.triangleStartOffset;

        int vertexStart = triangleIndex;
        int vIndex0 = triangles[vertexStart];
        int vIndex1 = triangles[vertexStart + 1];
        int vIndex2 = triangles[vertexStart + 2];
        Vector3 p0 = gpuVertices[vIndex0].position;
        Vector3 p1 = gpuVertices[vIndex1].position;
        Vector3 p2 = gpuVertices[vIndex2].position;
        //convert to worldpos

        p0 = meshInstance.localToWorld.MultiplyPoint(p0);
        p1 = meshInstance.localToWorld.MultiplyPoint(p1);
        p2 = meshInstance.localToWorld.MultiplyPoint(p2);

        //Vector3 lightPointNormal;
        Vector3 trianglePoint;
        //SampleTrianglePoint(p0, p1, p2, rs.Get2D(threadId), lightPointNormal, trianglePoint, triPdf);
        Vector3 wi;
        Vector2 uv = new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
        float triPdf = 0.0f;
        Vector3 Li = SampleTriangleLightRadiance(p0, p1, p2, uv, new Vector3(-1.8f, 2.7f, 2.2f), Vector3.up, gpuLight, out wi, out trianglePoint, out triPdf);
        lightPdf *= triPdf;
    }

	public static GPUShadowRay SampleShadowRayTest(BVHAccel bvhAccel, int instBVHOffset, List<Vector2> Distributions1D, List<GPULight> gpuLights, GPUInteraction isect, 
        List<MeshInstance> meshInstances, GPULight light, List<int> TriangleIndices, List<GPUVertex> Vertices, List<GPUMaterial> materials)
    {
        int SampleLightTriangle(int start, int count, float u, out float pdf)
        {
            //get light mesh triangle index
            int index = SampleDistribution1DDiscrete(Distributions1D, u, start, count, out pdf);
            return index;
        }

        Vector3 SampleTrianglePoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 u, out Vector3 normal, out float pdf)
        {
            //caculate bery centric uv w = 1 - u - v
            float t = Mathf.Sqrt(u.x);
            Vector2 uv = new Vector2(1.0f - t, t * u.y);
            float w = 1 - uv.x - uv.y;

            Vector3 position = p0 * w + p1 * uv.x + p2 * uv.y;
            Vector3 crossVector = Vector3.Cross(p1 - p0, p2 - p0);
            normal = crossVector.normalized;
            pdf = 1.0f / crossVector.magnitude;

            return position;
        }

        Vector3 SampleTriangleLightRadiance(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 u, GPUInteraction isect, GPULight light, out Vector3 wi, out Vector3 position, out float pdf)
        {
            Vector3 Li = light.radiance;
            Vector3 lightPointNormal;
            float triPdf = 0;
            position = SampleTrianglePoint(p0, p1, p2, u, out lightPointNormal, out triPdf);
            pdf = triPdf;
            wi = position - (Vector3)isect.p;
            float wiLength = Vector3.Magnitude(wi);
            if (wiLength == 0 || pdf == 0)
            {
                pdf = 0;
                return Vector3.zero;
            }
            wi = Vector3.Normalize(wi);
            pdf *= wiLength * wiLength / Mathf.Abs(Vector3.Dot(lightPointNormal, -wi));
            if (float.IsInfinity(pdf))
                pdf = 0.0f;

            return Li;
        }

        GPUShadowRay shadowRay = new GPUShadowRay();

		int distributionAddress = light.distributeAddress;
		float u = UnityEngine.Random.Range(0.0f, 1.0f);
		float triPdf = 0;
		float lightPdf = 0;
		MeshInstance meshInstance = meshInstances[light.meshInstanceID];
		int triangleIndex = (SampleLightTriangle(distributionAddress, light.trianglesNum, u, out lightPdf) - gpuLights.Count) * 3 + meshInstance.triangleStartOffset;
		int vertexStart = triangleIndex;
		int vIndex0 = TriangleIndices[vertexStart];
		int vIndex1 = TriangleIndices[vertexStart + 1];
		int vIndex2 = TriangleIndices[vertexStart + 2];
		Vector3 p0 = Vertices[vIndex0].position;
		Vector3 p1 = Vertices[vIndex1].position;
		Vector3 p2 = Vertices[vIndex2].position;
        //convert to worldpos

        p0 = meshInstance.localToWorld.MultiplyPoint(p0);
		p1 = meshInstance.localToWorld.MultiplyPoint(p1);
        p2 = meshInstance.localToWorld.MultiplyPoint(p2);

        //Vector3 lightPointNormal;
        Vector3 trianglePoint;
		//SampleTrianglePoint(p0, p1, p2, rs.Get2D(threadId), lightPointNormal, trianglePoint, triPdf);
		Vector3 wi;
		Vector3 Li = SampleTriangleLightRadiance(p0, p1, p2, new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f)), isect, light, out wi, out trianglePoint, out triPdf);
		lightPdf *= triPdf;

        if (lightPdf > 0)
        {
            shadowRay.p0 = isect.p;
            shadowRay.p1 = trianglePoint;
            //shadowRay.pdf = triPdf;
            shadowRay.lightPdf = lightPdf;
            //Vector3 Li = light.radiance;
            //shadowRay.lightNormal = lightPointNormal;
            //Vector3 wi = normalize(shadowRay.p1 - shadowRay.p0);

            //sample bsdf
            GPUMaterial material = materials[(int)isect.materialID];
            Vector3 wiLocal = isect.WorldToLocal(wi);
            Vector3 woLocal = isect.WorldToLocal(isect.wo);
            float cos = Vector3.Dot(wi, isect.normal);

            Vector3 f = LambertBRDF(woLocal, wiLocal, new Vector3(material.kd.r, material.kd.g, material.kd.b)) * Mathf.Abs(Vector3.Dot(wi, isect.normal));
            float scatteringPdf = LambertPDF(woLocal, wiLocal);
            int meshInstanceIndex = -1;
            float hitT = 0;
            if (ShadowRayVisibilityTest(shadowRay, isect.normal, bvhAccel, meshInstances, instBVHOffset, out hitT, out meshInstanceIndex))
            {
                //sample psdf and compute the mis weight
                shadowRay.weight =
                    PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                shadowRay.radiance = new Vector3(f.x * Li.x, f.y * Li.y, f.z * Li.z) * shadowRay.weight / lightPdf;
                shadowRay.visibility = 1;
            }
            else
            {
                shadowRay.radiance = Vector3.zero;
                shadowRay.visibility = 0;
            }
        }
		

        return shadowRay;

	}

    static float AreaLightPdf(BVHAccel bvhaccel, GPURay ray, GPULight light, int lightsNum, 
        List<MeshInstance> meshInstances, List<int> TriangleIndices, List<GPUVertex> Vertices, List<Vector2> distributions1D)
    {
        //intersect the light mesh triangle
        float bvhHit = ray.tmax;
        int meshHitTriangleIndex;  //wood triangle addr
        float lightPdf = 0;
        int distributionIndex = lightsNum;
        //getting the mesh of the light
        MeshInstance meshInstance = meshInstances[light.meshInstanceID];

        //convert to mesh local space
        GPURay rayTemp = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);

        //check the ray intersecting the light mesh
        if (bvhaccel.IntersectMeshBVHP(rayTemp, meshInstance.bvhOffset, out bvhHit, out meshHitTriangleIndex))
        {
            int triAddr = meshHitTriangleIndex;
            int vIndex0 = bvhaccel.m_woodTriangleIndices[triAddr];
            int vIndex1 = bvhaccel.m_woodTriangleIndices[triAddr + 1];
            int vIndex2 = bvhaccel.m_woodTriangleIndices[triAddr + 2];
            Vector3 p0 = Vertices[vIndex0].position;
            Vector3 p1 = Vertices[vIndex1].position;
            Vector3 p2 = Vertices[vIndex2].position;

            p0 = meshInstance.localToWorld.MultiplyPoint(p0);//mul(meshInstance.localToWorld, float4(p0, 1.0));
            p1 = meshInstance.localToWorld.MultiplyPoint(p1);//mul(meshInstance.localToWorld, float4(p1, 1.0));
            p2 = meshInstance.localToWorld.MultiplyPoint(p2);//mul(meshInstance.localToWorld, float4(p2, 1.0));

            lightPdf = 1.0f / Vector3.Cross(p0 - p1, p0 - p2).magnitude;

            distributionIndex += vIndex0 / 3;

            lightPdf *= distributions1D[distributionIndex].x;
        }

        return lightPdf;
    }

    public static Vector3 EstimateDirect(BVHAccel bvhaccel, GPUShadowRay shadowRay, GPUInteraction isect, Vector2 u, 
        List<GPUMaterial> materials, List<GPULight> lights, List<MeshInstance> meshInstances, List<int> TriangleIndices, List<GPUVertex> Vertices, List<Vector2> distributions1D)
    {
        Vector3 Ld = shadowRay.radiance;
        GPUMaterial material = materials[(int)isect.materialID];
        Vector3 woLocal = isect.WorldToLocal(isect.wo);
        Vector3 wi;
        float scatteringPdf = 0;
        Vector3 wiLocal;
        Vector3 f = SampleLambert(material, woLocal, out wiLocal, u, out scatteringPdf);

        wi = isect.LocalToWorld(wiLocal);
        f *= Mathf.Abs(Vector3.Dot(wi, isect.normal));

        if (scatteringPdf > 0)
        {
            GPULight light = lights[MathUtil.SingleToInt32Bits(shadowRay.lightIndex)];
            GPURay ray = SpawnRay(isect.p, wi, isect.normal, float.MaxValue);
            //for test
            //Vector3 lightPos = new Vector3(-2.750401f, 5.5f, -6.640179f);
            //wi = lightPos - (Vector3)isect.p;
            //wi.Normalize();
            //ray = SpawnRay(isect.p, wi, isect.normal, float.MaxValue);
            //end for test
            float lightPdf = AreaLightPdf(bvhaccel, ray, light, lights.Count, meshInstances, TriangleIndices, Vertices, distributions1D);
            if (lightPdf > 0)
            {
                //caculate the mis weight
                float weight = PowerHeuristic(1, lightPdf, 1, scatteringPdf);

                Ld += f.Mul(light.radiance) * weight / scatteringPdf;
            }
        }


        return Ld;
    }

    public static GPURay GeneratePath(ref GPUInteraction isect, ref GPUPathRadiance pathRadiance, List<GPUMaterial> materials, out bool breakLoop)
    {
        breakLoop = false;
        Vector3 beta = pathRadiance.beta;
        GPUMaterial material = materials[(int)isect.materialID];
        Vector3 woLocal = isect.WorldToLocal(isect.wo);
        Vector3 wi;
        float scatteringPdf = 0;
        Vector3 wiLocal;
        Vector2 u = new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
        Vector3 f = SampleLambert(material, woLocal, out wiLocal, u, out scatteringPdf);
        if (f == Vector3.zero || scatteringPdf == 0)
        {
            //terminate the path tracing
            breakLoop = true;
        }
        wi = isect.LocalToWorld(wiLocal);
        beta = beta.Mul(f * Mathf.Abs(Vector3.Dot(wi, isect.normal)) / scatteringPdf);
        pathRadiance.beta = beta;

        GPURay ray = SpawnRay(isect.p, wi, isect.normal, float.MaxValue);

        return ray;
    }

    public static GPURay SpawnRay(Vector3 p, Vector3 direction, Vector3 normal, float tMax)
    {
        GPURay ray = new GPURay();
        ray.orig = BVHAccel.offset_ray(p, normal);
        ray.tmax = tMax;
        ray.direction = direction;
        ray.tmin = 0;
        return ray;
    }

    public static bool ShadowRayVisibilityTest(GPUShadowRay shadowRay, Vector3 normal, BVHAccel bvhAccel, List<MeshInstance> meshInstances, int instBVHOffset, out float hitT, out int meshInstanceIndex)
	{
		GPURay ray = new GPURay();
		ray.orig = BVHAccel.offset_ray(shadowRay.p0, normal);
		ray.tmax = 1.0f - 0.0001f;
		ray.direction = shadowRay.p1 - shadowRay.p0;
		ray.tmin = 0;
        hitT = 0;

        return !bvhAccel.IntersectInstTestP(ray, meshInstances, instBVHOffset, out hitT, out meshInstanceIndex);
	}
}
