using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

class RTMesh
{
    public int vertexStart = 0;
    public int triangleStart = 0;
}

class AreaLightResource
{
    
    //public List<Vector2> triangleDistributions = new List<Vector2>();
    //在GPUDistributionDiscript中的地址
    public int discriptAddress = -1;
    public Distribution1D triangleDistributions = null;
    public List<float> triangleAreas = new List<float>();
}

class AreaLightInstance
{
    public AreaLightResource light;
    public int meshInstanceID = -1;
    public float area = 0;
    public float intensity;
    public float pointRadius;
    public Vector3 radiance;
}

public class RTSceneData
{
    public List<Primitive> primitives = new List<Primitive>();
    public List<MeshHandle> meshHandles = new List<MeshHandle>();
    public List<MeshInstance> meshInstances = new List<MeshInstance>();
    public List<int> triangles = new List<int>();
    public List<GPUVertex> gpuVertices = new List<GPUVertex>();
    public List<GPULight> gpuLights = new List<GPULight>();
}


//[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class Raytracing : MonoBehaviour
{
    // Start is called before the first frame update
    List<Primitive> primitives = new List<Primitive>();

    List<MeshHandle> meshHandles = new List<MeshHandle>();
    List<MeshInstance> meshInstances = new List<MeshInstance>();

    List<int> triangles = new List<int>();
    List<GPUVertex> gpuVertices = new List<GPUVertex>();
    //GPUPrimitive[] gpuPrimitives = null;
    List<GPULight> gpuLights = new List<GPULight>();
    GPURay[] gpuRays = null;
    GPURandomSampler[] gpuRandomSamplers = null;
    GPUInteraction[] gpuInteractions = null;
    List<GPUMaterial> gpuMaterials = new List<GPUMaterial>();
    List<Vector2> Distributions1D = new List<Vector2>();
    List<GPUDistributionDiscript> gpuDistributionDiscripts = new List<GPUDistributionDiscript>();
    //Dictionary<Mesh, Distribution1D<Vector2>> meshTriangleDistribution = new Dictionary<Mesh, Distribution1D<Vector2>>();
    Dictionary<Mesh, AreaLightResource> meshDistributions = new Dictionary<Mesh, AreaLightResource>();
    List<AreaLightInstance> areaLightInstances = new List<AreaLightInstance>();
    uint[] RayQueueSizeArray;

    BVHAccel bvhAccel = new BVHAccel();

    public ComputeShader generateRay;
    public ComputeShader generatePath;
    public ComputeShader RayTravel;
    public ComputeShader initRandom;
    public ComputeShader SampleShadowRay;
    public ComputeShader EstimateDirect;
    public ComputeShader ImageReconstruction;
    int kGeneratePrimaryRay = -1;
    //generate path ray
    int kGeneratePath = -1;
    //generate next event estimate light sample ray
    int kRayTraversal = -1;
    int kInitRandom = -1;
    int kSampleShadowRay = -1;
    int kEstimateDirect = -1;
    int kImageReconstruction = -1;
    ComputeBuffer woodTriBuffer;
    ComputeBuffer woodTriIndexBuffer;
    ComputeBuffer verticesBuffer;
    ComputeBuffer triangleBuffer;
    ComputeBuffer rayBuffer;
    ComputeBuffer samplerBuffer;
    ComputeBuffer primtiveBuffer;
    ComputeBuffer BVHBuffer;
    ComputeBuffer intersectBuffer;
    ComputeBuffer lightBuffer;
    ComputeBuffer materialBuffer;
    ComputeBuffer pathRadianceBuffer;
    //ComputeBuffer meshHandleBuffer;
    ComputeBuffer meshInstanceBuffer;
    ComputeBuffer distribution1DBuffer;
    ComputeBuffer distributionDiscriptBuffer;
    ComputeBuffer shadowRayBuffer;
    ComputeBuffer imageSpectrumsBuffer;
    //ComputeBuffer pathStatesBuffer;
    ComputeBuffer filterMarginalBuffer;
    ComputeBuffer filterConditionBuffer;
    ComputeBuffer rayQueueSizeBuffer;
    ComputeBuffer rayQueueBuffer;
    //ComputeBuffer transformBuffer;

    RenderTexture outputTexture;

    //screen is [-1,1]
    Matrix4x4 RasterToScreen;
    Matrix4x4 RasterToCamera;

    Camera cameraComponent = null;

    const int MAX_PATH = 5;
    int samplesPerPixel = 64;

    //for test
    //IndepententSampler indepententSampler = new IndepententSampler();
    //IndepententSampler indepententSampler2 = new IndepententSampler();
    int kTestSampler;

    public bool useInstanceBVH = true;
    //toplevel bvh在bvh buffer中的位置
    int instBVHNodeAddr = -1;
    int framesNum = 0;
    Mesh rectangleMesh;

    //image pixel filter
    Filter filter;
    void Start()
    {
        BVHAccel.TestPartition();

        RayQueueSizeArray = new uint[MAX_PATH * 5];
        for (int i = 0; i < RayQueueSizeArray.Length; ++i)
        {
            RayQueueSizeArray[i] = 0;
        }

        InitScene();
        SampleLightTest();

        GPUFilterSample uv = filter.Sample(MathUtil.GetRandom01());
        Debug.Log(uv.p);

        IntersectionTest();
        //TestPath();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonUp(0))
        {
            //Debug.Log(Input.mousePosition); 
            OnePathTracing((int)Input.mousePosition.x, (int)Input.mousePosition.y, (int)Screen.width, 1);
        }
        //bvhAccel.DrawDebug(meshInstances, useInstanceBVH);
        if (framesNum >= samplesPerPixel)
        {
            //GPUFilterSample uv = filter.Sample(MathUtil.GetRandom01());
            //Debug.Log(uv.p);
            return;
        }

        rayQueueSizeBuffer.SetData(RayQueueSizeArray);

        int rasterWidth = Screen.width;
        int rasterHeight = Screen.height;
        if (generateRay != null)
            generateRay.SetFloat("_time", Time.time);

        int queueSizeIndex = 0;

        generateRay.SetMatrix("RasterToCamera", RasterToCamera);
        generateRay.SetMatrix("CameraToWorld", cameraComponent.cameraToWorldMatrix);
        generateRay.Dispatch(kGeneratePrimaryRay, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);
        //SampleShadowRay.SetTexture(kSampleShadowRay, "outputTexture", outputTexture);
        //RayTravel.SetTexture(kSampleShadowRay, "outputTexture", outputTexture);
        //generatePath.SetTexture(kGeneratePath, "outputTexture", outputTexture);
        //Camera camera = GetComponent<Camera>();
        //TestRay(cameraComponent, 0);
        
        for (int i = 0; i < MAX_PATH; ++i)
        {
            //RayTravel.SetBuffer(kRayTraversal, "Rays", rayBuffer);
            RayTravel.SetFloat("_time", Time.time);
            RayTravel.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));

            //RayTravel.SetVector("testBoundMax", bvhAccel.linearNodes[0].bounds.max);
            //RayTravel.SetVector("testBoundMin", bvhAccel.linearNodes[0].bounds.min);
            RayTravel.SetInt("bounces", i);
            RayTravel.SetInt("queueSizeIndex", queueSizeIndex++);
            RayTravel.Dispatch(kRayTraversal, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);

            SampleShadowRay.SetInt("bounces", i);
            SampleShadowRay.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
            SampleShadowRay.SetInt("lightsNum", gpuLights.Count);
            SampleShadowRay.SetInt("queueSizeIndex", queueSizeIndex);
            SampleShadowRay.Dispatch(kSampleShadowRay, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);

            EstimateDirect.SetInt("queueSizeIndex", queueSizeIndex);
            EstimateDirect.Dispatch(kEstimateDirect, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);
            
            generatePath.SetInt("bounces", i);
            generatePath.SetInt("queueSizeIndex", queueSizeIndex++);
            generatePath.Dispatch(kGeneratePath, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);
        }

        ImageReconstruction.SetInt("framesNum", ++framesNum);
        ImageReconstruction.Dispatch(kImageReconstruction, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);
    }

    void SetupMaterials(Shape shape, BSDFMaterial bsdfMaterial)
    {
        Renderer renderer = shape.GetComponent<MeshRenderer>();
        Color _Color = renderer.sharedMaterial.GetColor("_Color");
        GPUMaterial gpuMtl = new GPUMaterial();
        gpuMtl.kd = bsdfMaterial.matte.kd.spectrum.linear; //_Color.linear;


        gpuMaterials.Add(gpuMtl);
    }

    void SetupSceneData()
    {
        Shape[] shapes = GameObject.FindObjectsOfType<Shape>();
        //worldMatrices = new Matrix4x4[shapes.Length];
        if (shapes.Length == 0)
            return;

        int renderObjectsNum = 0;
        

        //use area as the distribution
        //List<Vector2> lightsDistribution = new List<Vector2>();
        //List<float> lightAreas = new List<float>();

        if (useInstanceBVH)
        {
            //Dictionary<Mesh, int> sharedMeshes = new Dictionary<Mesh, int>();
            List<Mesh> sharedMeshes = new List<Mesh>();
            int lightObjectsNum = 0;
            //sharedMeshes.Find(s => s == this);
            //List<Vector2> uvs = new List<Vector2>();
            //int triangleOffset = 0;
            //先生成MeshHandle
            for (int i = 0; i < shapes.Length; ++i)
            {
                //worldMatrices[i] = shapes[i].transform.localToWorldMatrix;

                BSDFMaterial bsdfMaterial = shapes[i].GetComponent<BSDFMaterial>();
                if ((shapes[i].shapeType == Shape.ShapeType.triangleMesh || shapes[i].shapeType == Shape.ShapeType.rectangle) && bsdfMaterial != null)
                {
                    //material这部分暂时先不处理，写死成lambert diffuse
                    SetupMaterials(shapes[i], bsdfMaterial);

                    if (shapes[i].isAreaLight)
                    {
                        lightObjectsNum++;
                    }

                    MeshFilter meshRenderer = shapes[i].GetComponent<MeshFilter>();
                    Mesh mesh = meshRenderer.sharedMesh;
                    if (shapes[i].shapeType == Shape.ShapeType.rectangle)
                    {
                        mesh = GetRectangleMesh();
                    }

                    if (sharedMeshes.Contains(mesh))
                    {
                        continue;
                    }

                    sharedMeshes.Add(mesh);

                    int meshId = i;
                    int vertexOffset = gpuVertices.Count;
                    int triangleOffset = triangles.Count;
                    int primitiveTriangleOffset = gpuVertices.Count;
                    int primitiveVertexOffset = triangles.Count;


                    MeshHandle meshHandle = new MeshHandle(vertexOffset, triangleOffset, mesh.vertexCount, mesh.triangles.Length, mesh.bounds);
                    meshHandles.Add(meshHandle);
                    //创建该meshHandle的bvh

                    for (int j = 0; j < mesh.vertices.Length; ++j)
                    {
                        GPUVertex vertex = new GPUVertex();
                        vertex.position = mesh.vertices[j];
                        vertex.uv = mesh.uv[j];
                        gpuVertices.Add(vertex);
                    }
                    for (int j = 0; j < mesh.triangles.Length; ++j)
                    {
                        triangles.Add(mesh.triangles[j] + vertexOffset);
                    }
                }
            }

            float totalLightArea = 0;
            
            List<Vector2> lightTriangleDistributions = new List<Vector2>();
           
            List<Transform> meshTransforms = new List<Transform>();
            
            //生成meshinstance和对应的material
            for (int i = 0; i < shapes.Length; ++i)
            {
                BSDFMaterial bsdfMaterial = shapes[i].GetComponent<BSDFMaterial>();
                Transform transform = shapes[i].transform;
                int lightIndex = -1;
                if ((shapes[i].shapeType == Shape.ShapeType.triangleMesh || shapes[i].shapeType == Shape.ShapeType.rectangle) && bsdfMaterial != null)
                {

                    MeshFilter meshRenderer = shapes[i].GetComponent<MeshFilter>();
                    Mesh mesh = meshRenderer.sharedMesh;
                    if (shapes[i].shapeType == Shape.ShapeType.rectangle)
                    {
                        mesh = GetRectangleMesh();
                    }
                    int meshIndex = sharedMeshes.FindIndex(s => s == mesh);

                    if (shapes[i].isAreaLight)
                    {
                        AreaLightResource areaLight = null;
                        if (!meshDistributions.TryGetValue(mesh, out areaLight))
                        {
                            areaLight = new AreaLightResource();
                            //compute the mesh triangle distribution
                            for (int t = 0; t < mesh.triangles.Length; t += 3)
                            {
                                Vector3 p0 = mesh.vertices[mesh.triangles[t]];
                                Vector3 p1 = mesh.vertices[mesh.triangles[t + 1]];
                                Vector3 p2 = mesh.vertices[mesh.triangles[t + 2]];
                                float triangleArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                                areaLight.triangleAreas.Add(triangleArea);
                            }

                            //areaLight.distributionAddress = lightObjectsNum + lightTriangleDistributions.Count;
                            areaLight.triangleDistributions = new Distribution1D(areaLight.triangleAreas.ToArray(), 0, areaLight.triangleAreas.Count, 0, areaLight.triangleAreas.Count);
                            meshDistributions.Add(mesh, areaLight);
                            //lightTriangleDistributions.AddRange(areaLight.triangleDistributions);
                        }

                        float lightArea = 0;
                        //List<float> triangleAreas = new List<float>();
                        for (int t = 0; t < mesh.triangles.Length; t += 3)
                        {
                            Vector3 p0 = transform.TransformPoint(mesh.vertices[mesh.triangles[t]]);
                            Vector3 p1 = transform.TransformPoint(mesh.vertices[mesh.triangles[t + 1]]);
                            Vector3 p2 = transform.TransformPoint(mesh.vertices[mesh.triangles[t + 2]]);
                            float triangleArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                            //triangleAreas.Add(triangleArea);
                            //areaLight.area += triangleArea;
                            lightArea += triangleArea;
                        }

                        AreaLightInstance areaLightInstance = new AreaLightInstance();
                        areaLightInstance.light = areaLight;
                        areaLightInstance.meshInstanceID = meshInstances.Count;
                        areaLightInstance.area = lightArea;
                        areaLightInstance.radiance = shapes[i].lightSpectrum.linear.ToVector3().Mul(shapes[i].spectrumScale);
                        areaLightInstance.pointRadius = 0;
                        areaLightInstance.intensity = shapes[i].lightIntensity;
                        areaLightInstances.Add(areaLightInstance);

                        lightIndex = gpuLights.Count;
                        GPULight gpuLight = new GPULight();
                        gpuLight.radiance = shapes[i].lightSpectrum.linear.ToVector3().Mul(shapes[i].spectrumScale);
                        gpuLight.intensity = shapes[i].lightIntensity;
                        //gpuLight.trianglesNum = mesh.triangles.Length / 3;
                        gpuLight.pointRadius = 0;
                        //why add 1? because the first discript is the light object distributions.
                        gpuLight.distributionDiscriptIndex = gpuLights.Count + 1;
                        gpuLight.meshInstanceID = meshInstances.Count;
                        gpuLights.Add(gpuLight);

                    }

                    int materialIndex = i;

                    if (mesh.subMeshCount > 1)
                    {
                    }
                    else
                    {
                        MeshHandle meshHandle = meshHandles[meshIndex];
                        MeshInstance meshInstance = new MeshInstance(transform.localToWorldMatrix, transform.worldToLocalMatrix, meshIndex, 
                            materialIndex, lightIndex, meshHandle.triangleOffset, meshHandle.triangleCount);
                        meshInstances.Add(meshInstance);
                    }

                    meshTransforms.Add(transform);
                }
            }

            /*
            //构建light的分布
            
            List<Vector2> lightObjectDistribution = new List<Vector2>();
            float allLightsTotalArea = 0;
            for (int i = 0; i < areaLightInstances.Count; ++i)
            {
                allLightsTotalArea += areaLightInstances[i].light.area;
            }
            float cdf = 0;
            for (int i = 0; i < areaLightInstances.Count; ++i)
            {
                float pdf = areaLightInstances[i].light.area / allLightsTotalArea;
                cdf += pdf;
                lightObjectDistribution.Add(new Vector2(pdf, cdf));
            }

            //构建Distribution1D
            Distributions1D.AddRange(lightObjectDistribution);
            Distributions1D.AddRange(lightTriangleDistributions);
            */

            //创建bvh

            instBVHNodeAddr = bvhAccel.Build(meshTransforms, meshInstances, meshHandles, gpuVertices, triangles);


            //创建对应的computebuffer
            //meshHandleBuffer = new ComputeBuffer(meshHandles.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(MeshHandle)), ComputeBufferType.Structured);
            //meshHandleBuffer.SetData(meshHandles.ToArray());

            meshInstanceBuffer = new ComputeBuffer(meshInstances.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(MeshInstance)), ComputeBufferType.Structured);
            meshInstanceBuffer.SetData(meshInstances.ToArray());

            if (gpuLights.Count > 0)
            {
                lightBuffer = new ComputeBuffer(gpuLights.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPULight)), ComputeBufferType.Structured);
                lightBuffer.SetData(gpuLights.ToArray());
            }
            else
            {
                lightBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPULight)), ComputeBufferType.Structured);
            }
            
            //if (Distributions1D.Count > 0)
            //{
            //    distribution1DBuffer = new ComputeBuffer(Distributions1D.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Default);
            //    distribution1DBuffer.SetData(Distributions1D.ToArray());
            //}
            //else
            //{
            //    distribution1DBuffer = new ComputeBuffer(1, sizeof(float) * 2, ComputeBufferType.Default);
            //}
            
        }
        else
        {
            for (int i = 0; i < shapes.Length; ++i)
            {
                //worldMatrices[i] = shapes[i].transform.localToWorldMatrix;
                BSDFMaterial bsdfMaterial = shapes[i].GetComponent<BSDFMaterial>();
                if (shapes[i].shapeType == Shape.ShapeType.triangleMesh && bsdfMaterial != null)
                {
                    MeshFilter meshRenderer = shapes[i].GetComponent<MeshFilter>();
                    Mesh mesh = meshRenderer.sharedMesh;

                    if (mesh.subMeshCount > 1)
                    {
                        int primitiveTriangleOffset = gpuVertices.Count;
                        int primitiveVertexOffset = triangles.Count;

                        for (int j = 0; j < mesh.vertices.Length; ++j)
                        {
                            //positions.Add(shapes[i].transform.localToWorldMatrix.MultiplyPoint(mesh.vertices[j]));
                            //uvs.Add(mesh.uv[j]);
                            GPUVertex vertex = new GPUVertex();
                            vertex.position = shapes[i].transform.localToWorldMatrix.MultiplyPoint(mesh.vertices[j]);
                            vertex.uv = mesh.uv[j];
                            gpuVertices.Add(vertex);
                        }
                        for (int j = 0; j < mesh.triangles.Length; ++j)
                        {
                            triangles.Add(mesh.triangles[j] + primitiveVertexOffset);
                        }

                        int faceNum = mesh.triangles.Length / 3;

                        for (int f = 0; f < faceNum; ++f)
                        {
                            int tri0 = triangles[f * 3 + primitiveTriangleOffset];
                            int tri1 = triangles[f * 3 + 1 + primitiveTriangleOffset];
                            int tri2 = triangles[f * 3 + 2 + primitiveTriangleOffset];
                            primitives.Add(new Primitive(tri0, tri1, tri2, gpuVertices[tri0].position, gpuVertices[tri1].position, gpuVertices[tri2].position, renderObjectsNum, -1));
                        }
                    }
                    else
                    {
                        int primitiveTriangleOffset = gpuVertices.Count;
                        int primitiveVertexOffset = triangles.Count;

                        for (int j = 0; j < mesh.vertices.Length; ++j)
                        {
                            //positions.Add(shapes[i].transform.localToWorldMatrix.MultiplyPoint(mesh.vertices[j]));
                            //uvs.Add(mesh.uv[j]);
                            GPUVertex vertex = new GPUVertex();
                            vertex.position = shapes[i].transform.localToWorldMatrix.MultiplyPoint(mesh.vertices[j]);
                            vertex.uv = mesh.uv[j];
                            gpuVertices.Add(vertex);
                        }
                        for (int j = 0; j < mesh.triangles.Length; ++j)
                        {
                            triangles.Add(mesh.triangles[j] + primitiveTriangleOffset);
                        }

                        int faceNum = mesh.triangles.Length / 3;

                        for (int f = 0; f < faceNum; ++f)
                        {
                            int tri0 = triangles[f * 3 + primitiveVertexOffset];
                            int tri1 = triangles[f * 3 + 1 + primitiveVertexOffset];
                            int tri2 = triangles[f * 3 + 2 + primitiveVertexOffset];
                            primitives.Add(new Primitive(tri0, tri1, tri2, gpuVertices[tri0].position, gpuVertices[tri1].position, gpuVertices[tri2].position, renderObjectsNum, -1));
                        }
                    }

                    renderObjectsNum++;
                }
            }

            //for (int j = 0; j < positions.Count; ++j)
            //{
            //    GPUVertex vertex = new GPUVertex();
            //    vertex.position = positions[j];
            //    vertex.uv = uvs[j];
            //    gpuVertices.Add(vertex);
            //}

            //List<Primitive> orderedPrims = new List<Primitive>();
            bvhAccel.Build(primitives, gpuVertices, triangles);
        }

        int BVHNodeSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUBVHNode));
        if (BVHBuffer == null)
        {
            BVHBuffer = new ComputeBuffer(bvhAccel.m_nodes.Count, BVHNodeSize, ComputeBufferType.Structured);
            BVHBuffer.SetData(bvhAccel.m_nodes.ToArray());
        }

        if (woodTriBuffer == null)
        {
            woodTriBuffer = new ComputeBuffer(bvhAccel.m_woodTriangleVertices.Count, 16, ComputeBufferType.Structured);
        }
        woodTriBuffer.SetData(bvhAccel.m_woodTriangleVertices.ToArray());

        if (woodTriIndexBuffer == null)
        {
            woodTriIndexBuffer = new ComputeBuffer(bvhAccel.m_woodTriangleIndices.Count, sizeof(int), ComputeBufferType.Structured);
        }
        woodTriIndexBuffer.SetData(bvhAccel.m_woodTriangleIndices.ToArray());

        if (verticesBuffer == null)
        {
            int vertexSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUVertex));
            verticesBuffer = new ComputeBuffer(gpuVertices.Count, vertexSize, ComputeBufferType.Structured);
        }
        verticesBuffer.SetData(gpuVertices.ToArray());

        if (triangleBuffer == null)
        {
            triangleBuffer = new ComputeBuffer(triangles.Count, 4, ComputeBufferType.Default);
        }
        triangleBuffer.SetData(triangles.ToArray());

        if (materialBuffer == null)
        {
            materialBuffer = new ComputeBuffer(gpuMaterials.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUMaterial)), ComputeBufferType.Structured);
        }
        materialBuffer.SetData(gpuMaterials);
    }

    public void InitScene()
    {
        filter = new GaussianFilter(new Vector2(1.0f, 1.0f));
        if (outputTexture == null)
        {
            outputTexture = new RenderTexture(Screen.width, Screen.height, 0);
            outputTexture.enableRandomWrite = true;
        }
        SetupSceneData();

        SetupDistributions();

        SetupSamplers();

        //generate ray
        //init the camera parameters
        SetupGenerateRay();

        SetupRayTraversal();
        //generateRay.Dispatch(kGeneratePrimaryRay, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);
        //TestRay(camera, 0);
        SetupEstimateNextEvent();

        SetupGeneratePath();

        SetupImageReconstruction();
    }

    void SetupDistributions()
    {
        Distributions1D.Clear();
        //first, 
        List<float> lightObjectDistribution = new List<float>();
        for (int i = 0; i < areaLightInstances.Count; ++i)
        {
            lightObjectDistribution.Add(areaLightInstances[i].area);
        }
        Distribution1D lightObjDistribution = new Distribution1D(lightObjectDistribution.ToArray(), 0, lightObjectDistribution.Count, 0, lightObjectDistribution.Count);

        Distributions1D.AddRange(lightObjDistribution.GetGPUDistributions());
        GPUDistributionDiscript discript = new GPUDistributionDiscript
        {
            start = 0,
            num = lightObjectDistribution.Count,
            unum = 0,
            c = 0,
            domain = new Vector4(lightObjDistribution.size.x, lightObjDistribution.size.y, 0, 0)
        };
        gpuDistributionDiscripts.Add(discript);

        var areaLightEnumerator = meshDistributions.GetEnumerator();
        while (areaLightEnumerator.MoveNext())
        {
            AreaLightResource areaLightResource = areaLightEnumerator.Current.Value;
            discript = new GPUDistributionDiscript
            {
                start = Distributions1D.Count,
                num = areaLightResource.triangleAreas.Count,
                unum = 0,
                c = 0,
                domain = new Vector4(areaLightResource.triangleDistributions.size.x, areaLightResource.triangleDistributions.size.y, 0, 0)
            };
            areaLightResource.discriptAddress = gpuDistributionDiscripts.Count;
            gpuDistributionDiscripts.Add(discript);
            Distributions1D.AddRange(areaLightResource.triangleDistributions.GetGPUDistributions());
        }

        if (distribution1DBuffer == null && Distributions1D.Count > 0)
        {
            distribution1DBuffer = new ComputeBuffer(Distributions1D.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Default);
            distribution1DBuffer.SetData(Distributions1D.ToArray());
        }

        if (distributionDiscriptBuffer == null)
        {
            distributionDiscriptBuffer = new ComputeBuffer(gpuDistributionDiscripts.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUDistributionDiscript)), ComputeBufferType.Structured);
            distributionDiscriptBuffer.SetData(gpuDistributionDiscripts.ToArray());
        }

        //Vector2Int distributionSize = filter.GetDistributionSize();
        //GPUDistributionDiscript filterDiscript = new GPUDistributionDiscript
        //{
        //    start = gpuDistributionDiscripts.Count,
        //    num = distributionSize.y,
        //    unum = distributionSize.x,
        //    c = 0
        //};

    }

    void SetupSamplers()
    {
        if (samplerBuffer == null)
        {
            samplerBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPURandomSampler)), ComputeBufferType.Structured);
        }
        if (gpuRandomSamplers == null)
        {
            gpuRandomSamplers = new GPURandomSampler[Screen.width * Screen.height];
            samplerBuffer.SetData(gpuRandomSamplers);
        }

        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        kInitRandom = initRandom.FindKernel("CSInitSampler");
        initRandom.SetBuffer(kInitRandom, "RNGs", samplerBuffer);
        initRandom.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        initRandom.Dispatch(kInitRandom, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);
        //for test
        samplerBuffer.GetData(gpuRandomSamplers);

        //kTestSampler = initRandom.FindKernel("CSTestSampler");
        //initRandom.SetBuffer(kTestSampler, "RNGs", samplerBuffer);
        //initRandom.Dispatch(kTestSampler, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);
        //samplerBuffer.GetData(gpuRandomSamplers);
    }

    void SetupGenerateRay()
    {
        //generate ray
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        //init the camera parameters
        Camera camera = GetComponent<Camera>();
        Matrix4x4 screenToRaster = new Matrix4x4();

        screenToRaster = Matrix4x4.Scale(new Vector3(rasterWidth, rasterHeight, 1)) *
            Matrix4x4.Scale(new Vector3(0.5f, 0.5f, 0.5f)) *
            Matrix4x4.Translate(new Vector3(1, 1, 1));

        RasterToScreen = screenToRaster.inverse;

        float aspect = rasterWidth / rasterHeight;

        Matrix4x4 cameraToScreen = camera.orthographic ? Matrix4x4.Ortho(-camera.orthographicSize * aspect, camera.orthographicSize * aspect,
            -camera.orthographicSize, camera.orthographicSize, camera.nearClipPlane, camera.farClipPlane)
            : Matrix4x4.Perspective(camera.fieldOfView, aspect, camera.nearClipPlane, camera.farClipPlane);


        RasterToCamera = cameraToScreen.inverse * RasterToScreen;


        kGeneratePrimaryRay = generateRay.FindKernel("GeneratePrimary");

        if (rayBuffer == null)
        {
            rayBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPURay)), ComputeBufferType.Structured);
        }

        if (pathRadianceBuffer == null)
        {
            pathRadianceBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUPathRadiance)), ComputeBufferType.Structured);
        }

        //if (pathStatesBuffer == null)
        //{
        //    pathStatesBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(int), ComputeBufferType.Structured);
        //}

        if (rayQueueBuffer == null)
        {
            rayQueueBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(int), ComputeBufferType.Structured);
        }

        if (rayQueueSizeBuffer == null)
        {
            rayQueueSizeBuffer = new ComputeBuffer(RayQueueSizeArray.Length, sizeof(uint), ComputeBufferType.Structured);
        }

        if (gpuRays == null)
        {
            gpuRays = new GPURay[Screen.width * Screen.height];
            rayBuffer.SetData(gpuRays);
        }

        Vector2Int filterSize = filter.GetDistributionSize();
        if (filterMarginalBuffer == null)
        {
            List<Vector2> marginal = filter.GetGPUMarginalDistributions();
            filterMarginalBuffer = new ComputeBuffer(marginal.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Structured);
            
            filterMarginalBuffer.SetData(marginal.ToArray());
        }

        if (filterConditionBuffer == null)
        {
            List<Vector2> conditional = filter.GetGPUConditionalDistributions();
            filterConditionBuffer = new ComputeBuffer(filterSize.x * (filterSize.y + 1), System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Structured);
            filterConditionBuffer.SetData(conditional.ToArray());
        }    

        //if (gpuPathRadiances == null)
        //{
        //    gpuPathRadiances = new GPUPathRadiance[Screen.width * Screen.height];
        //    pathRadianceBuffer.SetData(gpuPathRadiances);
        //}

        generateRay.SetBuffer(kGeneratePrimaryRay, "Rays", rayBuffer);
        generateRay.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        generateRay.SetMatrix("RasterToCamera", RasterToCamera);
        generateRay.SetMatrix("CameraToWorld", camera.cameraToWorldMatrix);
        generateRay.SetFloat("_time", Time.time);
        generateRay.SetBuffer(kGeneratePrimaryRay, "RNGs", samplerBuffer);
        generateRay.SetBuffer(kGeneratePrimaryRay, "pathRadiances", pathRadianceBuffer);
        //generateRay.SetBuffer(kGeneratePrimaryRay, "pathStates", pathStatesBuffer);
        generateRay.SetBuffer(kGeneratePrimaryRay, "FilterMarginals", filterMarginalBuffer);
        generateRay.SetBuffer(kGeneratePrimaryRay, "FilterConditions", filterConditionBuffer);
        generateRay.SetBuffer(kGeneratePrimaryRay, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        generateRay.SetBuffer(kGeneratePrimaryRay, "_RayQueue", rayQueueBuffer);
        generateRay.SetInt("MarginalNum", filterSize.x);
        generateRay.SetInt("ConditionNum", filterSize.y);
        Bounds2D domain = filter.GetDomain();
        generateRay.SetVector("FilterDomain", new Vector4(domain.min[0], domain.max[0], domain.min[1], domain.max[1]));

        cameraComponent = camera;
    }

    void SetupEstimateNextEvent()
    {
        if (shadowRayBuffer == null)
        {
            shadowRayBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUShadowRay)), ComputeBufferType.Default);
        }

        kSampleShadowRay = SampleShadowRay.FindKernel("CSMain");

        SampleShadowRay.SetBuffer(kSampleShadowRay, "ShadowRays", shadowRayBuffer);
        SampleShadowRay.SetBuffer(kSampleShadowRay, "Intersections", intersectBuffer);
        SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "lights", lightBuffer);
        SampleShadowRay.SetBuffer(kSampleShadowRay, "materials", materialBuffer);
        SampleShadowRay.SetBuffer(kSampleShadowRay, "WoodTriangles", woodTriBuffer);
        SampleShadowRay.SetBuffer(kSampleShadowRay, "Vertices", verticesBuffer);
        SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "TriangleIndices", triangleBuffer);
        //RayTravel.SetBuffer(kRayTraversal, "Primitives", primtiveBuffer);
        SampleShadowRay.SetBuffer(kSampleShadowRay, "BVHTree", BVHBuffer);
        //SampleShadowRay.SetBuffer(kSampleShadowRay, "Intersections", intersectBuffer);
        SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "MeshInstances", meshInstanceBuffer);
        //RayTravel.SetBuffer(kRayTraversal, "WorldMatrices", transformBuffer);
        SampleShadowRay.SetBuffer(kSampleShadowRay, "RNGs", samplerBuffer);
        SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "WoodTriangleIndices", woodTriIndexBuffer);
        SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "Distributions1D", distribution1DBuffer);
        SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "DistributionDiscripts", distributionDiscriptBuffer);
        //SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "pathStates", pathStatesBuffer);
        SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "_RayQueue", rayQueueBuffer);
        SampleShadowRay.SetVector("rasterSize", new Vector4(Screen.width, Screen.height, 0, 0));
        SampleShadowRay.SetInt("instBVHAddr", instBVHNodeAddr);
        SampleShadowRay.SetInt("bvhNodesNum", bvhAccel.m_nodes.Count);
        SampleShadowRay.SetInt("lightsNum", gpuLights.Count);
        //SampleShadowRay.SetTexture(kSampleShadowRay, "outputTexture", outputTexture);


        kEstimateDirect = EstimateDirect.FindKernel("CSMain");
        EstimateDirect.SetBuffer(kEstimateDirect, "ShadowRays", shadowRayBuffer);
        EstimateDirect.SetBuffer(kEstimateDirect, "Intersections", intersectBuffer);
        //EstimateDirect.SetBuffer(kEstimateDirect, "lights", lightBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "lights", lightBuffer);
        EstimateDirect.SetBuffer(kEstimateDirect, "materials", materialBuffer);
        EstimateDirect.SetBuffer(kEstimateDirect, "WoodTriangles", woodTriBuffer);
        EstimateDirect.SetBuffer(kEstimateDirect, "Vertices", verticesBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "TriangleIndices", triangleBuffer);
        //RayTravel.SetBuffer(kRayTraversal, "Primitives", primtiveBuffer);
        EstimateDirect.SetBuffer(kEstimateDirect, "BVHTree", BVHBuffer);
        EstimateDirect.SetBuffer(kEstimateDirect, "Intersections", intersectBuffer);
        //if (meshInstanceBuffer != null)
        //    EstimateDirect.SetBuffer(kEstimateDirect, "MeshInstances", meshInstanceBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "MeshInstances", meshInstanceBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "WoodTriangleIndices", woodTriIndexBuffer);
        //RayTravel.SetBuffer(kRayTraversal, "WorldMatrices", transformBuffer);
        EstimateDirect.SetBuffer(kEstimateDirect, "RNGs", samplerBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "Distributions1D", distribution1DBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "DistributionDiscripts", distributionDiscriptBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "pathRadiances", pathRadianceBuffer);
        //SetComputeBuffer(EstimateDirect, kEstimateDirect, "pathStates", pathStatesBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_RayQueue", rayQueueBuffer);
        EstimateDirect.SetVector("rasterSize", new Vector4(Screen.width, Screen.height, 0, 0));
        EstimateDirect.SetInt("lightsNum", gpuLights.Count);
        EstimateDirect.SetTexture(kSampleShadowRay, "outputTexture", outputTexture);
        //EstimateDirect.Dispatch(kEstimateDirect, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);
    }

    void SetupGeneratePath()
    {
        kGeneratePath = generatePath.FindKernel("GeneratePath");
        generatePath.SetBuffer(kGeneratePath, "Rays", rayBuffer);
        generatePath.SetBuffer(kGeneratePath, "materials", materialBuffer);
        generatePath.SetBuffer(kGeneratePath, "RNGs", samplerBuffer);
        generatePath.SetBuffer(kGeneratePath, "Intersections", intersectBuffer);
        generatePath.SetBuffer(kGeneratePath, "pathRadiances", pathRadianceBuffer);
        //generatePath.SetBuffer(kGeneratePath, "lights", lightBuffer);
        SetComputeBuffer(generatePath, kGeneratePath, "lights", lightBuffer);
        //generatePath.SetBuffer(kGeneratePath, "Distributions1D", distribution1DBuffer);
        //SetComputeBuffer(generatePath, kGeneratePath, "Distributions1D", distribution1DBuffer);
        //SetComputeBuffer(generatePath, kGeneratePath, "pathStates", pathStatesBuffer);
        SetComputeBuffer(generatePath, kGeneratePath, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        SetComputeBuffer(generatePath, kGeneratePath, "_RayQueue", rayQueueBuffer);
        generatePath.SetVector("rasterSize", new Vector4(Screen.width, Screen.height, 0, 0));
    }

    void SetupImageReconstruction()
    {
        if (imageSpectrumsBuffer == null)
            imageSpectrumsBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Structured);

        kImageReconstruction = ImageReconstruction.FindKernel("CSMain");
        ImageReconstruction.SetBuffer(kImageReconstruction, "pathRadiances", pathRadianceBuffer);
        ImageReconstruction.SetBuffer(kImageReconstruction, "spectrums", imageSpectrumsBuffer);
        ImageReconstruction.SetTexture(kImageReconstruction, "outputTexture", outputTexture);
        ImageReconstruction.SetVector("rasterSize", new Vector4(Screen.width, Screen.height, 0, 0));
    }

    void SetComputeBuffer(ComputeShader cs, int kernel, string name, ComputeBuffer buffer)
    {
        if (cs != null && buffer != null)
        {
            cs.SetBuffer(kernel, name, buffer);
        }
    }
    void SetupRayTraversal()
    {
        if (intersectBuffer == null)
        {
            intersectBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUInteraction)), ComputeBufferType.Structured);
        }
        if (gpuInteractions == null)
        {
            gpuInteractions = new GPUInteraction[Screen.width * Screen.height];
        }
        intersectBuffer.SetData(gpuInteractions);
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        kRayTraversal = RayTravel.FindKernel("RayTraversal");
        RayTravel.SetBuffer(kRayTraversal, "Rays", rayBuffer);
        RayTravel.SetBuffer(kRayTraversal, "WoodTriangles", woodTriBuffer);
        RayTravel.SetBuffer(kRayTraversal, "Vertices", verticesBuffer);
        //RayTravel.SetBuffer(kRayTraversal, "Primitives", primtiveBuffer);
        RayTravel.SetBuffer(kRayTraversal, "BVHTree", BVHBuffer);
        RayTravel.SetBuffer(kRayTraversal, "Intersections", intersectBuffer);
        if (meshInstanceBuffer != null)
            RayTravel.SetBuffer(kRayTraversal, "MeshInstances", meshInstanceBuffer);
        //RayTravel.SetBuffer(kRayTraversal, "WorldMatrices", transformBuffer);
        RayTravel.SetTexture(kRayTraversal, "outputTexture", outputTexture);
        RayTravel.SetBuffer(kRayTraversal, "RNGs", samplerBuffer);
        RayTravel.SetBuffer(kRayTraversal, "pathRadiances", pathRadianceBuffer);
        SetComputeBuffer(RayTravel, kRayTraversal, "lights", lightBuffer);
        SetComputeBuffer(RayTravel, kRayTraversal, "WoodTriangleIndices", woodTriIndexBuffer);
        //SetComputeBuffer(RayTravel, kRayTraversal, "pathStates", pathStatesBuffer);
        SetComputeBuffer(RayTravel, kRayTraversal, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        SetComputeBuffer(RayTravel, kRayTraversal, "_RayQueue", rayQueueBuffer);
        RayTravel.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        RayTravel.SetInt("instBVHAddr", instBVHNodeAddr);
        RayTravel.SetInt("bvhNodesNum", bvhAccel.m_nodes.Count);
    }
    private void OnPostRender()
    {
        
    }

    Mesh GetRectangleMesh()
    {
        if (rectangleMesh == null)
        {
            rectangleMesh = new Mesh();

            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(-5.0f, 0.0f, 5.0f);
            vertices[1] = new Vector3(5.0f, 0.0f, 5.0f);
            vertices[2] = new Vector3(-5.0f, 0.0f, -5.0f);
            vertices[3] = new Vector3(5.0f, 0.0f, -5.0f);
            rectangleMesh.vertices = vertices;
            int[] triangles = new int[] { 0, 1, 2, 1, 3, 2 };
            rectangleMesh.triangles = triangles;

            Vector3[] normals = new Vector3[4];
            for (int i = 0; i < 4; ++i)
            {
                normals[i] = Vector3.up;
            }
            rectangleMesh.normals = normals;

            Vector2[] uvs = new Vector2[] { new Vector2(0.0f, 0.0f),
                    new Vector2(1.0f, 0.0f), new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f) };
            rectangleMesh.uv = uvs;
        }

        return rectangleMesh;
    }
    private void OnDestroy()
    {
        primitives.Clear();
        gpuLights = null;
        gpuRays = null;
        gpuInteractions = null;
        //worldMatrices = null;
        //positions.Clear();
        gpuVertices.Clear();
        triangles.Clear();
        bvhAccel.Clear();
        meshHandles.Clear();
        meshInstances.Clear();

        Distributions1D.Clear();
        meshDistributions.Clear();

        ReleaseGPUDatas();

        if (rectangleMesh != null)
        {
            rectangleMesh.Clear();
            rectangleMesh = null;
        }

        cameraComponent = null;
    }

    public void ReleaseGPUDatas()
    {
        void ReleaseComputeBuffer(ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }

        ReleaseComputeBuffer(primtiveBuffer);
        ReleaseComputeBuffer(rayBuffer);
        ReleaseComputeBuffer(samplerBuffer);
        ReleaseComputeBuffer(woodTriBuffer);
        ReleaseComputeBuffer(woodTriIndexBuffer);
        ReleaseComputeBuffer(verticesBuffer);
        ReleaseComputeBuffer(triangleBuffer);
        ReleaseComputeBuffer(intersectBuffer);
        ReleaseComputeBuffer(materialBuffer);
        ReleaseComputeBuffer(lightBuffer);
        ReleaseComputeBuffer(distribution1DBuffer);
        ReleaseComputeBuffer(distributionDiscriptBuffer);
        ReleaseComputeBuffer(shadowRayBuffer);
        ReleaseComputeBuffer(pathRadianceBuffer);
        ReleaseComputeBuffer(meshInstanceBuffer);
        ReleaseComputeBuffer(BVHBuffer);
        ReleaseComputeBuffer(imageSpectrumsBuffer);
        //ReleaseComputeBuffer(pathStatesBuffer);
        ReleaseComputeBuffer(filterMarginalBuffer);
        ReleaseComputeBuffer(filterConditionBuffer);
        ReleaseComputeBuffer(rayQueueBuffer);
        ReleaseComputeBuffer(rayQueueSizeBuffer);

        if (outputTexture != null)
        {
            outputTexture.Release();
            Destroy(outputTexture);
            outputTexture = null;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(outputTexture, destination);
    }

    Vector3 MinOrMax(GPUBounds box, int n)
    {
        return n == 0 ? box.min : box.max;
    }
    Vector3 Corner(GPUBounds box, int n)
    {
        return new Vector3(MinOrMax(box, n & 1).x,
            MinOrMax(box, (n & 2) > 0 ? 1 : 0).y,
            MinOrMax(box, (n & 4) > 0 ? 1 : 0).z);
    }
    bool BoundIntersectP(GPURay ray, GPUBounds bounds, Vector3 invDir, int[] dirIsNeg)
    {
        // Check for ray intersection against $x$ and $y$ slabs
        float tMin = (MinOrMax(bounds, dirIsNeg[0]).x - ray.orig.x) * invDir.x;
        float tMax = (MinOrMax(bounds, 1 - dirIsNeg[0]).x - ray.orig.x) * invDir.x;
        float tyMin = (MinOrMax(bounds, dirIsNeg[1]).y - ray.orig.y) * invDir.y;
        Vector3 corner4 = MinOrMax(bounds, 1 - dirIsNeg[1]);
        float tyMax = (MinOrMax(bounds, 1 - dirIsNeg[1]).y - ray.orig.y) * invDir.y;

        // Update _tMax_ and _tyMax_ to ensure robust bounds intersection
        //tMax *= 1 + 2 * gamma(3);
        //tyMax *= 1 + 2 * gamma(3);
        if (tMin > tyMax || tyMin > tMax)
            return false;
        if (tyMin > tMin)
            tMin = tyMin;
        if (tyMax < tMax)
            tMax = tyMax;

        // Check for ray intersection against $z$ slab
        float tzMin = (MinOrMax(bounds, dirIsNeg[2]).z - ray.orig.z) * invDir.z;
        float tzMax = (MinOrMax(bounds, 1 - dirIsNeg[2]).z - ray.orig.z) * invDir.z;

        // Update _tzMax_ to ensure robust bounds intersection
        //tzMax *= 1 + 2 * gamma(3);
        if (tMin > tzMax || tzMin > tMax)
            return false;
        if (tzMin > tMin) tMin = tzMin;
        if (tzMax < tMax) tMax = tzMax;
        return (tMin < ray.tMax) && (tMax > 0);
    }

    bool IntersectRay(GPUBounds bounds, GPURay ray)
    {
        Bounds unityBounds = new Bounds();
        unityBounds.SetMinMax(bounds.min, bounds.max);
        Ray unityRay = new Ray();
        unityRay.origin = ray.orig;
        unityRay.direction = ray.direction;
        return unityBounds.IntersectRay(unityRay);
    }
    //bool SceneIntersectTest(GPURay ray)
    //{
    //    Vector3 invDir =  new Vector3(1.0f / ray.direction.x, 1.0f / ray.direction.y, 1.0f / ray.direction.z);
    //    int[] dirIsNeg = new int[3];
    //    dirIsNeg[0] = invDir.x < 0 ? 1 : 0;
    //    dirIsNeg[1] = invDir.y < 0 ? 1 : 0;
    //    dirIsNeg[2] = invDir.z < 0 ? 1 : 0;
    //    int currentNodeIndex = 0; //当前正在访问的node


    //    LinearBVHNode node = bvhAccel.linearNodes[currentNodeIndex];
    //    if (BoundIntersectP(ray, node.bounds, invDir, dirIsNeg))
    //        return true;

    //    return false;
    //}
    private void TestRay(Camera camera, float duration)
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        Ray testRay = camera.ScreenPointToRay(new Vector3(rasterWidth - 1, 0, 0));
        Debug.DrawRay(testRay.origin, testRay.direction * 20.0f, Color.white, duration);
        Ray testRay2 = camera.ScreenPointToRay(new Vector3(0, 530, 0));
        Debug.DrawRay(testRay2.origin, testRay2.direction * 20.0f, Color.blue, duration);

        
        rayBuffer.GetData(gpuRays);

        //test for correction
        //GPURay ray = gpuRays[(int)rasterHeight * (int)rasterWidth - 1];
        //bool bIntersectTest = bvhAccel.IntersectTest(ray);//IntersectRay(bvhAccel.linearNodes[0].bounds, ray);
        //if (bIntersectTest)
        //{
        //    Debug.DrawRay(ray.orig, ray.direction * 20.0f, Color.blue, duration);
        //}
        //else
        //    Debug.DrawRay(ray.orig, ray.direction * 20.0f, Color.red, duration);

        int x = 226;//(int)rasterWidth / 2 + 60;
        int y = 280;//(int)rasterHeight / 2;
        int index = x + y * (int)rasterWidth;
        //index = 700 + 360 * (int)rasterWidth;
        GPURay gpuRay = gpuRays[index];

        //bIntersectTest = IntersectRay(bvhAccel.linearNodes[0].bounds, gpuRay);
        float hitT = float.MaxValue;
        GPUInteraction interaction;
        bool bIntersectTest = useInstanceBVH ? bvhAccel.IntersectInstTest(gpuRay, meshInstances, meshHandles, bvhAccel.instBVHNodeAddr, out hitT, out interaction) : bvhAccel.IntersectBVHTriangleTest(gpuRay, 0, out hitT);//SceneIntersectTest(gpuRay);
        if (bIntersectTest)
        {
            Debug.DrawRay(gpuRay.orig, gpuRay.direction * hitT, Color.blue, duration);
        }
        else
        {
            Debug.DrawRay(gpuRay.orig, gpuRay.direction * 20.0f, Color.red, duration);
        }
        //testRay2 = camera.ScreenPointToRay(new Vector3(0, (int)rasterHeight / 2, 0));
        //Debug.DrawRay(testRay2.origin, testRay2.direction * 20.0f, Color.yellow, 100.0f);

        Vector3 nearPlanePoint = RasterToCamera.MultiplyPoint(new Vector3(rasterWidth - 1, 0, 0));
        Vector3 orig = camera.cameraToWorldMatrix.MultiplyPoint(Vector3.zero);
        Vector3 dir = camera.cameraToWorldMatrix.MultiplyPoint(nearPlanePoint) - orig;
        dir.Normalize();
        Ray cpuRay = new Ray();
        cpuRay.origin = orig;
        cpuRay.direction = dir;
        Debug.DrawRay(cpuRay.origin, cpuRay.direction * 20.0f, Color.cyan, duration);
        //test end
    }

    private void TestPath()
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        //rayBuffer.GetData(gpuRays);

        int x = 358;//216;//(int)rasterWidth / 2 + 60;
        int y = 477;//206;//(int)rasterHeight / 2;
        //int x = 361;
        //int y = 420;
        OnePathTracing(x, y, (int)rasterWidth, 1);
    }

    

    public Vector3 OnePathTracing(int x, int y, int rasterWidth, int spp)
    {
        Debug.Log("###############OnePathtracing debug begin:x=" + x + " y=" + y);
        int index = x + y * rasterWidth;
        //index = 700 + 360 * (int)rasterWidth;
        //GPURay gpuRay = gpuRays[index];
        Vector3 rgbSum = Vector3.zero;
        Vector3 finalRadiance = Vector3.zero;
        for (int i = 0; i < spp; ++i)
        {
            GPURay gpuRay = RayTracingTest.GenerateRay(x, y, RasterToCamera, cameraComponent.cameraToWorldMatrix, filter);

            //bIntersectTest = IntersectRay(bvhAccel.linearNodes[0].bounds, gpuRay);
            float hitT = float.MaxValue;
            GPUInteraction interaction = new GPUInteraction();
            GPUPathRadiance pathRadiance = new GPUPathRadiance();
            pathRadiance.beta = Vector3.one;
            for (int p = 0; p < MAX_PATH; ++p)
            {
                bool bIntersectTest = useInstanceBVH ? bvhAccel.IntersectInstTest(gpuRay, meshInstances, meshHandles, bvhAccel.instBVHNodeAddr, out hitT, out interaction) : bvhAccel.IntersectBVHTriangleTest(gpuRay, 0, out hitT);//SceneIntersectTest(gpuRay);
                if (bIntersectTest)
                {
                    GPUShadowRay shadowRay = RayTracingTest.SampleShadowRayTest(bvhAccel, instBVHNodeAddr, Distributions1D, gpuLights, interaction, meshInstances, gpuLights[0], triangles, gpuVertices, gpuMaterials, gpuDistributionDiscripts);
                    Debug.Log("bounce=" + p + " shadowray radiance=" + shadowRay.radiance.ToDetailString() + " pdf=" + shadowRay.lightPdf);
                    float scatteringpdf = 0;
                    Vector3 onePathLightRadiance = RayTracingTest.EstimateDirect(bvhAccel, shadowRay, interaction, new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f)),
                        gpuMaterials, gpuLights, meshInstances, triangles, gpuVertices, Distributions1D, out scatteringpdf);
                    Debug.Log("bounce=" + p + " after estimateDirect, pathLightRadiance=" + onePathLightRadiance.ToDetailString() + " pdf=" + scatteringpdf);
                    pathRadiance.li += pathRadiance.beta.Mul(onePathLightRadiance);
                    bool breakLoop = false;
                    
                    gpuRay = RayTracingTest.GeneratePath(ref interaction, ref pathRadiance, gpuRay, gpuMaterials, out breakLoop, out scatteringpdf);
                    Debug.Log("bounce=" + p + " generatePath, beta=" + pathRadiance.beta.ToDetailString() + " scatteringPdf=" + scatteringpdf);

                    if (breakLoop)
                    {
                        break;
                    }

                    if (p > 3)
                    {
                        float q = Mathf.Max(0.05f, 1 - pathRadiance.beta.MaxComponent());
                        if (UnityEngine.Random.Range(0.0f, 1.0f) < q)
                        {
                            break;
                        }
                        else
                            pathRadiance.beta /= 1 - q;
                    }
                }
                else
                {
                    //Debug.DrawRay(gpuRay.orig, gpuRay.direction * 20.0f, Color.red, duration);
                    break;
                }
            }
            rgbSum += pathRadiance.li;
            int frameNum = i + 1;
            finalRadiance = rgbSum / frameNum;
            Debug.Log("###############OnePathtracing debug end:finalRadiance=" + finalRadiance.ToDetailString());
        }

        return finalRadiance;
    }

    public void IntersectionTest()
    {
        GPURay gpuRay = new GPURay();
        gpuRay.orig = new Vector3(0.02604653f, 4.881493f, -4.587191f);
        gpuRay.direction = new Vector3(-0.4131184f, 0.4631883f, 0.7840853f);
        Debug.DrawRay(gpuRay.orig, gpuRay.direction * 10.0f, Color.green, 100.0f);
        gpuRay.tmax = float.MaxValue;
        gpuRay.tmin = 0;
        float hitT = float.MaxValue;
        GPUInteraction interaction = new GPUInteraction();
        bool bIntersectTest = bvhAccel.IntersectInstTest(gpuRay, meshInstances, meshHandles, bvhAccel.instBVHNodeAddr, out hitT, out interaction);
        if (!bIntersectTest)
        {
            Debug.Log("IntersectionTest failed!");
        }
    }

    //test the light sampling
    void SampleLightTest()
    {
        GPULight SampleLightSource(float u, out float pdf, out int index)
        {
            index = GPUDistributionTest.Sample1DDiscrete(u, gpuDistributionDiscripts[0], Distributions1D, out pdf);
            return gpuLights[index];
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
        int triangleIndex = (GPUDistributionTest.Sample1DDiscrete(u, gpuDistributionDiscripts[gpuLight.distributionDiscriptIndex], Distributions1D, out lightPdf) - gpuLights.Count) * 3 + meshInstance.triangleStartOffset;
        //(SampleLightTriangle(gpuLight.distributeAddress, gpuLight.trianglesNum, u, out lightPdf) - gpuLights.Count) * 3 + meshInstance.triangleStartOffset;

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

        //float3 lightPointNormal;
        Vector3 trianglePoint;
        //SampleTrianglePoint(p0, p1, p2, rs.Get2D(threadId), lightPointNormal, trianglePoint, triPdf);
        Vector3 wi;
        Vector2 uv = new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
        float triPdf = 0.0f;
        Vector3 Li = SampleTriangleLightRadiance(p0, p1, p2, uv, new Vector3(-1.8f, 2.7f, 2.2f), Vector3.up, gpuLight, out wi, out trianglePoint, out triPdf);
        lightPdf *= triPdf;
    }
}
