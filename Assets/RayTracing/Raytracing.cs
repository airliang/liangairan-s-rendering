using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

//[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class Raytracing : MonoBehaviour
{
    // Start is called before the first frame update
    List<Primitive> primitives = new List<Primitive>();
    List<Mesh> sharedMeshes = new List<Mesh>();
    //Mesh[] shareMeshes = null;
    Matrix4x4[] worldMatrices = null;
    List<Vector3> positions = new List<Vector3>();
    List<int> triangles = new List<int>();
    GPUPrimitive[] gpuPrimitives = null;
    GPULight[] gpuLights = null;
    GPURay[] gpuRays = null;
    GPUInteraction[] gpuInteractions = null;

    BVHAccel bvhAccel = new BVHAccel();

    public ComputeShader generateRay;
    public ComputeShader extend;
    int kGenerateRay;
    int kExtend;
    ComputeBuffer positionBuffer;
    ComputeBuffer triangleBuffer;
    ComputeBuffer rayBuffer;
    ComputeBuffer primtiveBuffer;
    ComputeBuffer BVHBuffer;
    CommandBuffer lightBuffer;

    RenderTexture outputTexture;

    //screen is [-1,1]
    Matrix4x4 RasterToScreen;
    Matrix4x4 RasterToCamera;
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
        InitScene();
    }

    // Update is called once per frame
    void Update()
    {
        bvhAccel.DrawDebug();


        generateRay.SetMatrix("RasterToCamera", RasterToCamera);
        generateRay.SetMatrix("CameraToWorld", transform.localToWorldMatrix);

        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        //generateRay.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        //generateRay.SetBuffer(kGenerateRay, "Rays", rayBuffer);
        //generateRay.Dispatch(kGenerateRay, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);
        extend.SetFloat("_time", Time.time);
        extend.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        extend.Dispatch(kExtend, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);
    }

    void InitScene()
    {
        Shape[] shapes = GameObject.FindObjectsOfType<Shape>();
        worldMatrices = new Matrix4x4[shapes.Length];

        int vertexOffset = 0;
        int triangleOffset = 0;
        for (int i = 0; i < shapes.Length; ++i)
        {
            if (shapes[i].shapeType == Shape.ShapeType.triangleMesh)
            {
                worldMatrices[i] = shapes[i].transform.localToWorldMatrix;
                MeshFilter meshRenderer = shapes[i].GetComponent<MeshFilter>();
                Mesh mesh = meshRenderer.sharedMesh;
                
                if (mesh.subMeshCount > 1)
                {
                    triangleOffset = triangles.Count;
                    if (!sharedMeshes.Contains(mesh))
                    {
                        sharedMeshes.Add(mesh);

                        for (int j = 0; j < mesh.subMeshCount; ++j)
                        {
                            SubMeshDescriptor subMesh = mesh.GetSubMesh(j);
                            vertexOffset = subMesh.firstVertex;
                            triangleOffset = subMesh.indexStart;

                            positions.AddRange(mesh.vertices);
                            triangles.AddRange(mesh.triangles);

                            int faceNum = subMesh.indexCount / 3;

                            for (int f = 0; f < faceNum; ++f)
                            {
                                primitives.Add(new Primitive(vertexOffset, triangleOffset, i, f, shapes[i].transform, mesh));
                            }
                        }
                    }
                }
                else
                {
                    int meshId = 0;
                    vertexOffset = positions.Count;
                    triangleOffset = triangles.Count;
                    if (!sharedMeshes.Contains(mesh))
                    {
                        sharedMeshes.Add(mesh);
                        meshId = sharedMeshes.Count - 1;
                        positions.AddRange(mesh.vertices);
                        triangles.AddRange(mesh.triangles);
                    }
                    else
                    {
                        meshId = sharedMeshes.FindIndex(a => a == mesh);
                    }
                    int faceNum = mesh.triangles.Length / 3;

                    for (int f = 0; f < faceNum; ++f)
                    {
                        primitives.Add(new Primitive(vertexOffset, triangleOffset, i, f, shapes[i].transform, mesh));
                    }
                }
                
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
        bvhAccel.Build(primitives, 4, ref orderedPrims);
        gpuPrimitives = new GPUPrimitive[orderedPrims.Count];
        for (int i = 0; i < orderedPrims.Count; ++i)
        {
            gpuPrimitives[i].faceIndex = orderedPrims[i].faceIndex;
            gpuPrimitives[i].transformId = orderedPrims[i].transformId;
            gpuPrimitives[i].vertexOffset = orderedPrims[i].vertexOffset;
            gpuPrimitives[i].triangleOffset = orderedPrims[i].triangleOffset;
        }

        primtiveBuffer = new ComputeBuffer(orderedPrims.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUPrimitive)), ComputeBufferType.Structured);
        primtiveBuffer.SetData(gpuPrimitives);

        //
        BVHBuffer = new ComputeBuffer(bvhAccel.linearNodes.Length, System.Runtime.InteropServices.Marshal.SizeOf(typeof(LinearBVHNode)), ComputeBufferType.Structured);
        BVHBuffer.SetData(bvhAccel.linearNodes);

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
        Ray testRay = camera.ScreenPointToRay(new Vector3(0, 0, 0));
        Ray testRay2 = camera.ScreenPointToRay(new Vector3(0, 530, 0));

        RasterToCamera = cameraToScreen.inverse * RasterToScreen;

        
        kGenerateRay = generateRay.FindKernel("GenerateRay");
        
        generateRay.SetBuffer(kGenerateRay, "Rays", rayBuffer);
        generateRay.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        generateRay.SetMatrix("RasterToCamera", RasterToCamera);
        generateRay.SetMatrix("CameraToWorld", camera.cameraToWorldMatrix);
        generateRay.SetFloat("_time", Time.time);
        
        generateRay.Dispatch(kGenerateRay, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);

        rayBuffer.GetData(gpuRays);

        //extend inialization
        if (positionBuffer == null)
        {
            positionBuffer = new ComputeBuffer(positions.Count, 12, ComputeBufferType.Default);
        }
        positionBuffer.SetData(positions.ToArray());

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


        kExtend = extend.FindKernel("Extend");
        extend.SetBuffer(kExtend, "Rays", rayBuffer);
        extend.SetBuffer(kExtend, "Positions", positionBuffer);
        extend.SetBuffer(kExtend, "Triangles", triangleBuffer);
        extend.SetBuffer(kExtend, "Primitives", primtiveBuffer);
        extend.SetBuffer(kExtend, "BVHTree", BVHBuffer);
        extend.SetTexture(kExtend, "outputTexture", outputTexture);
        extend.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
    }

    private void OnPostRender()
    {
        
    }

    private void OnDestroy()
    {
        primitives.Clear();
        sharedMeshes.Clear();
        worldMatrices = null;
        gpuPrimitives = null;
        gpuLights = null;
        gpuRays = null;
        positions.Clear();
        triangles.Clear();
        bvhAccel.Clear();

        if (primtiveBuffer != null)
        {
            primtiveBuffer.Release();
            primtiveBuffer = null;
        }

        if (rayBuffer == null)
        {
            rayBuffer.Release();
            rayBuffer = null;
        }

        if (BVHBuffer == null)
        {
            BVHBuffer.Release();
            BVHBuffer = null;
        }

        if (positionBuffer != null)
        {
            positionBuffer.Release();
            positionBuffer = null;
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
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(outputTexture, destination);
    }
}
