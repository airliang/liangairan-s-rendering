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

//[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class Raytracing : MonoBehaviour
{
    // Start is called before the first frame update
    List<Primitive> primitives = new List<Primitive>();
    List<Mesh> sharedMeshes = new List<Mesh>();
    List<RTMesh> sharedRTMeshes = new List<RTMesh>();
    //Mesh[] shareMeshes = null;
    //Matrix4x4[] worldMatrices = null;
    List<Vector3> positions = new List<Vector3>();
    List<int> triangles = new List<int>();
    GPUPrimitive[] gpuPrimitives = null;
    GPULight[] gpuLights = null;
    GPURay[] gpuRays = null;
    GPURandomSampler[] gpuRandomSamplers = null;
    GPUInteraction[] gpuInteractions = null;
    GPUMaterial[] gpuMaterials = null;

    BVHAccel bvhAccel = new BVHAccel();

    public ComputeShader generateRay;
    public ComputeShader extend;
    public ComputeShader initRandom;
    int kGeneratePrimaryRay;
    //generate path ray
    int kGeneratePath;
    //generate next event estimate light sample ray
    int kGenerateExplicit;
    int kRayTraversal;
    int kInitRandom;
    ComputeBuffer woodTriBuffer;
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
    //ComputeBuffer transformBuffer;

    RenderTexture outputTexture;

    //screen is [-1,1]
    Matrix4x4 RasterToScreen;
    Matrix4x4 RasterToCamera;

    Camera cameraComponent = null;

    const int MAX_PATH = 5;

    //for test
    //IndepententSampler indepententSampler = new IndepententSampler();
    //IndepententSampler indepententSampler2 = new IndepententSampler();
    int kTestSampler;
    void Start()
    {
        //this is a bvh
        /*
        Debug.Log(BVHInterface.Add(5, 6));

        Vector3[] positions = new Vector3[6];
        positions[0] = Vector3.zero;
        positions[1] = Vector3.one;
        positions[5].y = Mathf.PI;
        Debug.Log(BVHInterface.SendArrayToCPP(positions, 6));

        BVHInterface.GetArrayFromCPP(positions, 6);
        Debug.Log("positions[1].x = " + positions[1].x);
        */
        //indepententSampler.Init(5, 5);
        //indepententSampler2.Init(5, 6);
        //float random1 = indepententSampler.UniformFloat();
        //float random2 = indepententSampler2.UniformFloat();

        BVHAccel.TestPartition();
        InitScene();
    }

    // Update is called once per frame
    void Update()
    {
        bvhAccel.DrawDebug();

        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        if (generateRay != null)
            generateRay.SetFloat("_time", Time.time);

        for (int i = 0; i < MAX_PATH; ++i)
        {
            if (i == 0)
            {
                //generateRay.SetBuffer(kGeneratePrimaryRay, "Rays", rayBuffer);
                generateRay.SetMatrix("RasterToCamera", RasterToCamera);
                generateRay.SetMatrix("CameraToWorld", cameraComponent.cameraToWorldMatrix);
                generateRay.Dispatch(kGeneratePrimaryRay, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);

                Camera camera = GetComponent<Camera>();
                TestRay(cameraComponent, 0);
            }
            else
            {
                //path ray generation
                //generateRay.Dispatch(kGeneratePath, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);

                //next event estimation
            }

            //extend.SetBuffer(kRayTraversal, "Rays", rayBuffer);
            extend.SetFloat("_time", Time.time);
            extend.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));

            extend.SetVector("testBoundMax", bvhAccel.linearNodes[0].bounds.max);
            extend.SetVector("testBoundMin", bvhAccel.linearNodes[0].bounds.min);
            extend.Dispatch(kRayTraversal, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);
        }

        //Debug.Log(indepententSampler.UniformFloat());
        
        //generateRay.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        //generateRay.SetBuffer(kGenerateRay, "Rays", rayBuffer);
        //generateRay.Dispatch(kGenerateRay, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);
        
    }

    void InitScene()
    {
        Shape[] shapes = GameObject.FindObjectsOfType<Shape>();
        //worldMatrices = new Matrix4x4[shapes.Length];
        if (shapes.Length == 0)
            return;
        gpuMaterials = new GPUMaterial[shapes.Length];
        int vertexOffset = 0;
        int renderObjectsNum = 0;

        List<Vector2> uvs = new List<Vector2>();
        //int triangleOffset = 0;
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
                    int primitiveTriangleOffset = positions.Count;
                    int primitiveVertexOffset = vertexOffset;


                    for (int j = 0; j < mesh.vertices.Length; ++j)
                    {
                        positions.Add(shapes[i].transform.localToWorldMatrix.MultiplyPoint(mesh.vertices[j]));
                        uvs.Add(mesh.uv[j]);
                    }
                    for (int j = 0; j < mesh.triangles.Length; ++j)
                    {
                        triangles.Add(mesh.triangles[j] + primitiveTriangleOffset);
                    }

                    int faceNum = mesh.triangles.Length / 3;

                    for (int f = 0; f < faceNum; ++f)
                    {
                        int tri0 = triangles[f * 3 + primitiveTriangleOffset];
                        int tri1 = triangles[f * 3 + 1 + primitiveTriangleOffset];
                        int tri2 = triangles[f * 3 + 2 + primitiveTriangleOffset];
                        primitives.Add(new Primitive(tri0, tri1, tri2, positions[tri0], positions[tri1], positions[tri2], renderObjectsNum));
                    }
                }
                else
                {
                    int meshId = 0;
                    int primitiveTriangleOffset = positions.Count;
                    int primitiveVertexOffset = triangles.Count;
                    
                    for (int j = 0; j < mesh.vertices.Length; ++j)
                    {
                        positions.Add(shapes[i].transform.localToWorldMatrix.MultiplyPoint(mesh.vertices[j]));
                        uvs.Add(mesh.uv[j]);
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
                        primitives.Add(new Primitive(tri0, tri1, tri2, positions[tri0], positions[tri1], positions[tri2], renderObjectsNum));
                    }
                }

                if (bsdfMaterial.materialType == BSDFMaterial.BSDFType.Matte)
                {
                    gpuMaterials[renderObjectsNum] = new GPUMaterial((int)bsdfMaterial.materialType, bsdfMaterial.matte.sigma.constantValue, 0, Color.white, Color.white, Color.white);
                }
                else if (bsdfMaterial.materialType == BSDFMaterial.BSDFType.Plastic)
                {
                    gpuMaterials[renderObjectsNum] = new GPUMaterial((int)bsdfMaterial.materialType, 0, bsdfMaterial.plastic.roughnessTexture.constantValue, 
                        bsdfMaterial.plastic.kd.spectrum, bsdfMaterial.plastic.ks.spectrum, Color.white);
                }
                else if (bsdfMaterial.materialType == BSDFMaterial.BSDFType.Mirror)
                {
                    gpuMaterials[renderObjectsNum] = new GPUMaterial((int)bsdfMaterial.materialType, 0, 0,
                        Color.white, Color.white, bsdfMaterial.mirror.kr.spectrum);
                }
                else if (bsdfMaterial.materialType == BSDFMaterial.BSDFType.Glass)
                {
                    gpuMaterials[renderObjectsNum] = new GPUMaterial((int)bsdfMaterial.materialType, 0, bsdfMaterial.glass.uRougness.constantValue,
                        Color.white, bsdfMaterial.glass.ks.spectrum, bsdfMaterial.glass.kr.spectrum);
                }

                renderObjectsNum++;
            }
        }

        RTLight[] lights = GameObject.FindObjectsOfType<RTLight>();
        gpuLights = new GPULight[lights.Length];
        for (int i = 0; i < lights.Length; ++i)
        {
            gpuLights[i].type = (int)lights[i].lightType;
            gpuLights[i].color = lights[i].color;
            gpuLights[i].intensity = lights[i].intensity;
            gpuLights[i].pointRadius = lights[i].pointRadius;
        }


        List<Primitive> orderedPrims = new List<Primitive>();
        bvhAccel.Build(primitives, orderedPrims, positions, uvs, triangles);
        /*
        gpuPrimitives = new GPUPrimitive[orderedPrims.Count];
        for (int i = 0; i < orderedPrims.Count; ++i)
        {
            //gpuPrimitives[i].vertexOffset = orderedPrims[i].vertexOffset;
            //gpuPrimitives[i].triangleOffset = orderedPrims[i].triangleOffset;
            //gpuPrimitives[i].transformId = orderedPrims[i].transformId;
        }

        primtiveBuffer = new ComputeBuffer(orderedPrims.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUPrimitive)), ComputeBufferType.Structured);
        primtiveBuffer.SetData(gpuPrimitives);
        */

        //
        //int BoundsSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Bounds));
        int BVHNodeSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUBVHNode));
        BVHBuffer = new ComputeBuffer(bvhAccel.m_nodes.Length, BVHNodeSize, ComputeBufferType.Structured);
        BVHBuffer.SetData(bvhAccel.m_nodes);

        if (rayBuffer == null)
        {
            rayBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPURay)), ComputeBufferType.Structured);
        }
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        if (gpuRays == null)
        {
            gpuRays = new GPURay[Screen.width * Screen.height];
            rayBuffer.SetData(gpuRays);
        }

        if (samplerBuffer == null)
        {
            samplerBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPURandomSampler)), ComputeBufferType.Structured);
        }
        if (gpuRandomSamplers == null)
        {
            gpuRandomSamplers = new GPURandomSampler[Screen.width * Screen.height];
            samplerBuffer.SetData(gpuRandomSamplers);
        }

        if (pathRadianceBuffer == null)
        {
            pathRadianceBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4)), ComputeBufferType.Structured);
        }


        //generate ray
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
        
        generateRay.SetBuffer(kGeneratePrimaryRay, "Rays", rayBuffer);
        generateRay.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        generateRay.SetMatrix("RasterToCamera", RasterToCamera);
        generateRay.SetMatrix("CameraToWorld", camera.cameraToWorldMatrix);
        generateRay.SetFloat("_time", Time.time);
        generateRay.SetBuffer(kGeneratePrimaryRay, "RNGs", samplerBuffer);
        

        //generateRay.Dispatch(kGeneratePrimaryRay, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);
        //TestRay(camera, 0);
        kGeneratePath = generateRay.FindKernel("GeneratePath");
        generateRay.SetBuffer(kGeneratePath, "Rays", rayBuffer);

        kInitRandom = initRandom.FindKernel("CSInitSampler");
        initRandom.SetBuffer(kInitRandom, "RNGs", samplerBuffer);
        initRandom.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        initRandom.Dispatch(kInitRandom, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);
        //for test
        samplerBuffer.GetData(gpuRandomSamplers);

        kTestSampler = initRandom.FindKernel("CSTestSampler");
        initRandom.SetBuffer(kTestSampler, "RNGs", samplerBuffer);
        initRandom.Dispatch(kTestSampler, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);
        samplerBuffer.GetData(gpuRandomSamplers);


        //extend inialization
        if (woodTriBuffer == null)
        {
            woodTriBuffer = new ComputeBuffer(bvhAccel.m_woodTriangleVertices.Count, 16, ComputeBufferType.Structured);
        }
        woodTriBuffer.SetData(bvhAccel.m_woodTriangleVertices.ToArray());

        if (verticesBuffer == null)
        {
            verticesBuffer = new ComputeBuffer(bvhAccel.m_worldVertices.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUVertex)), ComputeBufferType.Structured);
        }
        verticesBuffer.SetData(bvhAccel.m_worldVertices.ToArray());

        if (triangleBuffer == null)
        {
            triangleBuffer = new ComputeBuffer(triangles.Count, 4, ComputeBufferType.Default);
        }
        triangleBuffer.SetData(triangles.ToArray());

        if (outputTexture == null)
        {
            outputTexture = new RenderTexture(Screen.width, Screen.height, 0);
            outputTexture.enableRandomWrite = true;
        }

        if (intersectBuffer == null)
        {
            intersectBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUInteraction)), ComputeBufferType.Structured);
        }
        if (gpuInteractions == null)
        {
            gpuInteractions = new GPUInteraction[Screen.width * Screen.height];
        }
        intersectBuffer.SetData(gpuInteractions);

        if (materialBuffer == null)
        {
            materialBuffer = new ComputeBuffer(gpuMaterials.Length, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUMaterial)), ComputeBufferType.Structured);
        }
        materialBuffer.SetData(gpuMaterials);

        generateRay.SetBuffer(kGeneratePath, "Materials", materialBuffer);
        generateRay.SetBuffer(kGeneratePath, "RNGs", samplerBuffer);
        generateRay.SetBuffer(kGeneratePath, "Intersections", intersectBuffer);
        generateRay.SetBuffer(kGeneratePath, "PathRadiances", pathRadianceBuffer);

        kRayTraversal = extend.FindKernel("RayTraversal");
        extend.SetBuffer(kRayTraversal, "Rays", rayBuffer);
        extend.SetBuffer(kRayTraversal, "WoodTriangles", woodTriBuffer);
        extend.SetBuffer(kRayTraversal, "WVertices", verticesBuffer);
        //extend.SetBuffer(kRayTraversal, "Primitives", primtiveBuffer);
        extend.SetBuffer(kRayTraversal, "BVHTree", BVHBuffer);
        extend.SetBuffer(kRayTraversal, "Intersections", intersectBuffer);
        //extend.SetBuffer(kRayTraversal, "WorldMatrices", transformBuffer);
        extend.SetTexture(kRayTraversal, "outputTexture", outputTexture);
        extend.SetBuffer(kRayTraversal, "RNGs", samplerBuffer);
        extend.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));

        cameraComponent = camera;
    }

    private void OnPostRender()
    {
        
    }

    private void OnDestroy()
    {
        primitives.Clear();
        sharedMeshes.Clear();
        gpuPrimitives = null;
        gpuLights = null;
        gpuRays = null;
        gpuInteractions = null;
        //worldMatrices = null;
        positions.Clear();
        triangles.Clear();
        bvhAccel.Clear();

        if (primtiveBuffer != null)
        {
            primtiveBuffer.Release();
            primtiveBuffer = null;
        }

        if (rayBuffer != null)
        {
            rayBuffer.Release();
            rayBuffer = null;
        }

        if (samplerBuffer != null)
        {
            samplerBuffer.Release();
            samplerBuffer = null;
        }

        if (BVHBuffer != null)
        {
            BVHBuffer.Release();
            BVHBuffer = null;
        }

        if (woodTriBuffer != null)
        {
            woodTriBuffer.Release();
            woodTriBuffer = null;
        }

        if (verticesBuffer != null)
        {
            verticesBuffer.Release();
            verticesBuffer = null;
        }

        if (triangleBuffer != null)
        {
            triangleBuffer.Release();
            triangleBuffer = null;
        }

        if (primtiveBuffer != null)
        {
            primtiveBuffer.Release();
            primtiveBuffer = null;
        }

        if (outputTexture != null)
        {
            outputTexture.Release();
            Destroy(outputTexture);
            outputTexture = null;
        }

        if (intersectBuffer != null)
        {
            intersectBuffer.Release();
            intersectBuffer = null;
        }

        if (materialBuffer != null)
        {
            materialBuffer.Release();
            materialBuffer = null;
        }

        if (pathRadianceBuffer != null)
        {
            pathRadianceBuffer.Release();
            pathRadianceBuffer = null;
        }

        cameraComponent = null;
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
        return (tMin < ray.orig.w) && (tMax > 0);
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
    bool SceneIntersectTest(GPURay ray)
    {
        Vector3 invDir =  new Vector3(1.0f / ray.direction.x, 1.0f / ray.direction.y, 1.0f / ray.direction.z);
        int[] dirIsNeg = new int[3];
        dirIsNeg[0] = invDir.x < 0 ? 1 : 0;
        dirIsNeg[1] = invDir.y < 0 ? 1 : 0;
        dirIsNeg[2] = invDir.z < 0 ? 1 : 0;
        int currentNodeIndex = 0; //当前正在访问的node


        LinearBVHNode node = bvhAccel.linearNodes[currentNodeIndex];
        if (BoundIntersectP(ray, node.bounds, invDir, dirIsNeg))
            return true;

        return false;
    }
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
        GPURay ray = gpuRays[(int)rasterHeight * (int)rasterWidth - 1];
        bool bIntersectTest = bvhAccel.IntersectTest(ray);//IntersectRay(bvhAccel.linearNodes[0].bounds, ray);
        if (bIntersectTest)
        {
            Debug.DrawRay(ray.orig, ray.direction * 20.0f, Color.blue, duration);
        }
        else
            Debug.DrawRay(ray.orig, ray.direction * 20.0f, Color.red, duration);

        int x = (int)rasterWidth / 2 + 60;
        int y = (int)rasterHeight / 2;
        int index = x + y * (int)rasterWidth;
        //index = 700 + 360 * (int)rasterWidth;
        GPURay gpuRay = gpuRays[index];

        bIntersectTest = IntersectRay(bvhAccel.linearNodes[0].bounds, gpuRay);
        
        bIntersectTest = bvhAccel.IntersectTest(gpuRay);//SceneIntersectTest(gpuRay);
        if (bIntersectTest)
        {
            Debug.DrawRay(gpuRay.orig, gpuRay.direction * 20.0f, Color.blue, duration);
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
}
