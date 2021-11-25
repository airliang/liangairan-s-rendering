using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingTest
{
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
            if (wiLength == 0)
            {
                Li = Vector3.zero;
                pdf = 0;
            }
            wi = Vector3.Normalize(wi);
            pdf *= wiLength * wiLength / Mathf.Abs(Vector3.Dot(lightPointNormal, -wi));

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
		}
		else
		{
			shadowRay.radiance = Vector3.zero;
		}

        return shadowRay;

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
