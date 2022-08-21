using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;


[Serializable]
public class WavefrontKernel : TracingKernel
{
    private RaytracingData _rayTracingData;
    GPUSceneData gpuSceneData;
    GPUFilterData gpuFilterData;

    ComputeShader generateRay;
    ComputeShader RayTravel;
    ComputeShader InitRandom;
    ComputeShader RayMiss;
    ComputeShader RayQueueClear;
    ComputeShader SampleShadowRay;
    ComputeShader EstimateDirect;
    ComputeShader ImageReconstruction;
    ComputeShader DebugView;
    int kGeneratePrimaryRay = -1;
    int kRayTraversal = -1;
    int kRayMiss = -1;
    int kInitRandom = -1;
    int kSampleShadowRay = -1;
    int kEstimateDirect = -1;
    int kRayQueueClear = -1;
    int kImageReconstruction = -1;
    int kDebugView = -1;

    private CommandBuffer renderGBufferCmd;
    public float _Exposure = 1;

    ComputeBuffer rayBuffer;
    ComputeBuffer samplerBuffer;
    ComputeBuffer pathRadianceBuffer;

    ComputeBuffer shadowRayBuffer;
    RenderTexture imageSpectrumsBuffer;


    ComputeBuffer rayQueueSizeBuffer;
    ComputeBuffer rayQueueBuffer;
    ComputeBuffer nextRayQueueBuffer;

    RenderTexture outputTexture;
    RenderTexture rayConeGBuffer;

    //screen is [-1,1]
    //Matrix4x4 RasterToScreen;
    //Matrix4x4 RasterToCamera;
    //Matrix4x4 WorldToRaster;


    int MAX_PATH = 5;
    int MIN_PATH = 3;
    //int samplesPerPixel = 1024;


    int framesNum = 0;
    Material gBufferMaterial = null;
    //image pixel filter
    Filter filter;

    //float cameraConeSpreadAngle = 0;
    uint[] RayQueueSizeArray;
    //uint[] gpuRandomSamplers = null;

    private MeshRenderer[] meshRenderers = null;

    public WavefrontKernel(WavefrontResource resource)
    {
        generateRay = resource.generateRay;
        RayTravel = resource.RayTravel;
        InitRandom = resource.InitRandom;
        SampleShadowRay = resource.SampleShadowRay;
        EstimateDirect = resource.EstimateDirect;
        ImageReconstruction = resource.ImageReconstruction;
        DebugView = resource.DebugView;
        RayMiss = resource.RayMiss;
        RayQueueClear = resource.RayQueueClear;
    }

    public GPUSceneData GetGPUSceneData()
    {
        return gpuSceneData;
    }

    public GPUFilterData GetGPUFilterData()
    {
        return gpuFilterData;
    }

    public void Setup(Camera camera, RaytracingData data)
    {
        _rayTracingData = data;
        MAX_PATH = data.MaxDepth;

        if (outputTexture == null)
        {
            outputTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf, 0);
            outputTexture.enableRandomWrite = true;
        }

        gpuSceneData = new GPUSceneData(data._UniformSampleLight, data._EnviromentMapEnable, data._UseBVHPlugin);
        meshRenderers = GameObject.FindObjectsOfType<MeshRenderer>();
        gpuSceneData.Setup(meshRenderers, camera);

        gpuFilterData = new GPUFilterData();
        if (data.filterType == FilterType.Gaussian)
        {
            filter = new GaussianFilter(data.fiterRadius, data.gaussianSigma);
        }
        gpuFilterData.Setup(filter);

        RayQueueSizeArray = new uint[MAX_PATH * 5];
        for (int i = 0; i < RayQueueSizeArray.Length; ++i)
        {
            RayQueueSizeArray[i] = 0;
        }

        SetupSamplers();

        //generate ray
        //init the camera parameters
        Profiler.BeginSample("SetupGenerateRay");
        SetupGenerateRay(camera);
        Profiler.EndSample();

        Profiler.BeginSample("SetupRayTraversal");
        SetupRayTraversal();
        Profiler.EndSample();

        Profiler.BeginSample("SetupEstimateNextEvent");
        SetupEstimateNextEvent();
        Profiler.EndSample();

        //SetupGeneratePath();
        Profiler.BeginSample("SetupImageReconstruction");
        SetupImageReconstruction();
        Profiler.EndSample();

        SetupRayQueueClear();
    }

    public RenderTexture GetOutputTexture()
    {
        return outputTexture;
    }

    public void Release()
    {
        void ReleaseComputeBuffer(ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }

        void ReleaseRenderTexture(RenderTexture texture)
        {
            if (texture != null)
            {
                texture.Release();
                Object.Destroy(texture);
                texture = null;
            }
        }

        void ReleaseTexture(Texture texture)
        {
            if (texture != null)
            {
                Object.Destroy(texture);
                texture = null;
            }
        }

        if (gpuSceneData != null)
            gpuSceneData.Release();

        if (gpuFilterData != null)
            gpuFilterData.Release();

        if (outputTexture != null)
        {
            outputTexture.Release();
            Object.Destroy(outputTexture);
            outputTexture = null;
        }

        if (rayConeGBuffer != null)
        {
            rayConeGBuffer.Release();
            Object.Destroy(rayConeGBuffer);
            rayConeGBuffer = null;
        }

        ReleaseRenderTexture(imageSpectrumsBuffer);

        ReleaseComputeBuffer(samplerBuffer);
        ReleaseComputeBuffer(rayBuffer);
        ReleaseComputeBuffer(pathRadianceBuffer);
        ReleaseComputeBuffer(shadowRayBuffer);
        ReleaseComputeBuffer(rayQueueSizeBuffer);
        ReleaseComputeBuffer(rayQueueBuffer);
        ReleaseComputeBuffer(nextRayQueueBuffer);
    }

    public void Update(Camera camera)
    {
        
        //if (Input.GetMouseButtonUp(0))
        //{
        //    OnePathTracing((int)Input.mousePosition.x, (int)Input.mousePosition.y, (int)Screen.width, 1);
        //    Vector3 K = new Vector3(3.9747f, 2.38f, 1.5998f);
        //    Vector3 etaT = new Vector3(0.1428f, 0.3741f, 1.4394f);
        //    float cosTheta = 0.3f;
        //    Vector3 fr = RayTracingTest.FrConductor(cosTheta, Vector3.one, etaT, K);
        //    Debug.Log("cosTheta = " + cosTheta + " fresnel = " + fr);
        //}

        //bvhAccel.DrawDebug(meshInstances, true);

        if (framesNum++ >= _rayTracingData.SamplesPerPixel)
        {
            //GPUFilterSample uv = filter.Sample(MathUtil.GetRandom01());
            //Debug.Log(uv.p);
            return;
        }

        RenderToGBuffer(camera);

        rayQueueSizeBuffer.SetData(RayQueueSizeArray);

        int rasterWidth = Screen.width;
        int rasterHeight = Screen.height;
        if (generateRay != null)
            generateRay.SetFloat("_time", Time.time);

        int curRaySizeIndex = 0;
        int nextRaySizeIndex = 1;

        generateRay.SetMatrix("RasterToCamera", gpuSceneData.RasterToCamera);
        generateRay.SetMatrix("CameraToWorld", camera.cameraToWorldMatrix);
        
        int threadGroupX = Screen.width / 8 + ((Screen.width % 8) != 0 ? 1 : 0);
        int threadGroupY = Screen.height / 8 + ((Screen.height % 8) != 0 ? 1 : 0);
        generateRay.Dispatch(kGeneratePrimaryRay, threadGroupX, threadGroupY, 1);
        ComputeBuffer curRayQueue = rayQueueBuffer;
        ComputeBuffer nextRayQueue = nextRayQueueBuffer;

        //for (int y = 0; y < rasterHeight; y += 8)
        {
            if (_rayTracingData.viewMode == RaytracingData.TracingView.ColorView)
            {
                for (int i = 0; i < MAX_PATH; ++i)
                {
                    RayTravel.SetFloat("_time", Time.time);
                    RayTravel.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));

                    RayTravel.SetInt("bounces", i);
                    RayTravel.SetInt("curQueueSizeIndex", curRaySizeIndex);
                    RayTravel.SetBuffer(kRayTraversal, "_RayQueue", curRayQueue);
                    RayTravel.Dispatch(kRayTraversal, threadGroupX, threadGroupY, 1);

                    SampleShadowRay.SetInt("bounces", i);
                    SampleShadowRay.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
                    SampleShadowRay.SetInt("curQueueSizeIndex", curRaySizeIndex);
                    SampleShadowRay.SetBuffer(kSampleShadowRay, "_RayQueue", curRayQueue);
                    SampleShadowRay.Dispatch(kSampleShadowRay, threadGroupX, threadGroupY, 1);

                    EstimateDirect.SetInt("bounces", i);
                    EstimateDirect.SetInt("curQueueSizeIndex", curRaySizeIndex);
                    EstimateDirect.SetInt("nextQueueSizeIndex", nextRaySizeIndex);
                    EstimateDirect.SetBuffer(kEstimateDirect, "_RayQueue", curRayQueue);
                    EstimateDirect.SetBuffer(kEstimateDirect, "_NextRayQueue", nextRayQueue);
                    EstimateDirect.Dispatch(kEstimateDirect, threadGroupX, threadGroupY, 1);

                    RayQueueClear.SetInt("curQueueSizeIndex", curRaySizeIndex);
                    RayQueueClear.Dispatch(kRayQueueClear, 1, 1, 1);

                    ComputeBuffer tmpBuffer = nextRayQueue;
                    nextRayQueue = curRayQueue;
                    curRayQueue = tmpBuffer;
                    int tmpIndex = nextRaySizeIndex;
                    nextRaySizeIndex = curRaySizeIndex;
                    curRaySizeIndex = tmpIndex;
                }

                ImageReconstruction.SetInt("framesNum", framesNum);
                ImageReconstruction.SetFloat("_Exposure", _Exposure);
                ImageReconstruction.Dispatch(kImageReconstruction, threadGroupX, threadGroupY, 1);
            }
            else
            {
                if (kDebugView < 0)
                    SetupDebugView(camera);

                DebugView.SetInt("debugView", (int)_rayTracingData.viewMode);
                DebugView.SetInt("bounces", 0);

                DebugView.Dispatch(kDebugView, Screen.width / 8 + 1, Screen.height / 8 + 1, 1);
                
            }
        }
    }

    private void RenderToGBuffer(Camera camera)
    {
        if (rayConeGBuffer == null)
        {
            rayConeGBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RGHalf);
            rayConeGBuffer.name = "RayConeGBuffer";
            rayConeGBuffer.enableRandomWrite = true;
        }


        if (gBufferMaterial == null)
        {
            Shader renderToGBuffer = Shader.Find("RayTracing/RayCone");
            gBufferMaterial = new Material(renderToGBuffer);
        }

        if (renderGBufferCmd == null)
        {
            renderGBufferCmd = new CommandBuffer();
            renderGBufferCmd.name = "RayConeGBuffer Commands";
        }
        CommandBuffer cmd = renderGBufferCmd;//new CommandBuffer();
        cmd.Clear();
        cmd.BeginSample("Render GBuffer");
        cmd.SetRenderTarget(rayConeGBuffer);
        cmd.ClearRenderTarget(true, true, Color.black);
        cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
        cmd.SetViewport(new Rect(0, 0, (float)Screen.width, (float)Screen.height));

        Plane[] frustums = GeometryUtility.CalculateFrustumPlanes(camera);
        for (int i = 0; i < meshRenderers.Length; ++i)
        {
            if (GeometryUtility.TestPlanesAABB(frustums, meshRenderers[i].bounds))
                cmd.DrawRenderer(meshRenderers[i], gBufferMaterial);
        }
        cmd.EndSample("Render GBuffer");
        Graphics.ExecuteCommandBuffer(cmd);

    }

    void SetupSamplers()
    {
        if (samplerBuffer == null)
        {
            samplerBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(uint), ComputeBufferType.Structured);
        }
        //if (gpuRandomSamplers == null)
        //{
        //    gpuRandomSamplers = new uint[Screen.width * Screen.height];
        //    samplerBuffer.SetData(gpuRandomSamplers);
        //}

        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        kInitRandom = InitRandom.FindKernel("CSInitSampler");
        InitRandom.SetBuffer(kInitRandom, "RNGs", samplerBuffer);
        InitRandom.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        InitRandom.Dispatch(kInitRandom, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);
        //for test
        //samplerBuffer.GetData(gpuRandomSamplers);

        //kTestSampler = initRandom.FindKernel("CSTestSampler");
        //initRandom.SetBuffer(kTestSampler, "RNGs", samplerBuffer);
        //initRandom.Dispatch(kTestSampler, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);
        //samplerBuffer.GetData(gpuRandomSamplers);
    }

    void SetupGenerateRay(Camera camera)
    {
        //generate ray
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        //init the camera parameters

        kGeneratePrimaryRay = generateRay.FindKernel("GeneratePrimary");

        if (rayBuffer == null)
        {
            rayBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPURay)), ComputeBufferType.Structured);
        }

        if (pathRadianceBuffer == null)
        {
            pathRadianceBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUPathRadiance)), ComputeBufferType.Structured);
        }

        if (rayQueueBuffer == null)
        {
            rayQueueBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(int), ComputeBufferType.Structured);
        }

        if (rayQueueSizeBuffer == null)
        {
            rayQueueSizeBuffer = new ComputeBuffer(RayQueueSizeArray.Length, sizeof(uint), ComputeBufferType.Structured);
        }


        generateRay.SetBuffer(kGeneratePrimaryRay, "Rays", rayBuffer);
        generateRay.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        generateRay.SetMatrix("RasterToCamera", gpuSceneData.RasterToCamera);
        generateRay.SetMatrix("CameraToWorld", camera.cameraToWorldMatrix);
        generateRay.SetFloat("_time", Time.time);
        generateRay.SetBuffer(kGeneratePrimaryRay, "RNGs", samplerBuffer);
        generateRay.SetBuffer(kGeneratePrimaryRay, "pathRadiances", pathRadianceBuffer);
        gpuFilterData.SetComputeShaderGPUData(generateRay, kGeneratePrimaryRay);
        //generateRay.SetBuffer(kGeneratePrimaryRay, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        //generateRay.SetBuffer(kGeneratePrimaryRay, "_RayQueue", rayQueueBuffer);
    }

    void SetupEstimateNextEvent()
    {
        if (shadowRayBuffer == null)
        {
            shadowRayBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUShadowRay)), ComputeBufferType.Default);
        }

        if (nextRayQueueBuffer == null)
        {
            nextRayQueueBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(int), ComputeBufferType.Structured);
        }

        kSampleShadowRay = SampleShadowRay.FindKernel("CSMain");
        gpuSceneData.SetComputeShaderGPUData(SampleShadowRay, kSampleShadowRay);

        SampleShadowRay.SetBuffer(kSampleShadowRay, "ShadowRays", shadowRayBuffer);
        SampleShadowRay.SetBuffer(kSampleShadowRay, "RNGs", samplerBuffer);

        SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        SetComputeBuffer(SampleShadowRay, kSampleShadowRay, "_RayQueue", rayQueueBuffer);
        SampleShadowRay.SetVector("rasterSize", new Vector4(Screen.width, Screen.height, 0, 0));
        //SampleShadowRay.SetMatrix("WorldToRaster", WorldToRaster);
        SetTextures(SampleShadowRay, kSampleShadowRay);

        kEstimateDirect = EstimateDirect.FindKernel("CSMain");
        gpuSceneData.SetComputeShaderGPUData(EstimateDirect, kEstimateDirect);
        EstimateDirect.SetBuffer(kEstimateDirect, "ShadowRays", shadowRayBuffer);
        //SetComputeBuffer(EstimateDirect, kEstimateDirect, "Rays", rayBuffer);
    
        EstimateDirect.SetBuffer(kEstimateDirect, "RNGs", samplerBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "pathRadiances", pathRadianceBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_RayQueue", rayQueueBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_NextRayQueue", nextRayQueueBuffer);
        EstimateDirect.SetVector("rasterSize", new Vector4(Screen.width, Screen.height, 0, 0));
        //EstimateDirect.SetMatrix("WorldToRaster", WorldToRaster);
        SetTextures(EstimateDirect, kEstimateDirect);
        EstimateDirect.SetInt("MIN_DEPTH", _rayTracingData.MinDepth);
    }


    void SetupImageReconstruction()
    {
        if (imageSpectrumsBuffer == null)
        {
            imageSpectrumsBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, 0);//new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Structured);
            imageSpectrumsBuffer.enableRandomWrite = true;
            imageSpectrumsBuffer.filterMode = FilterMode.Point;
        }

        kImageReconstruction = ImageReconstruction.FindKernel("CSMain");
        ImageReconstruction.SetBuffer(kImageReconstruction, "pathRadiances", pathRadianceBuffer);
        //ImageReconstruction.SetBuffer(kImageReconstruction, "spectrums", imageSpectrumsBuffer);
        SetComputeTexture(ImageReconstruction, kImageReconstruction, "spectrums", imageSpectrumsBuffer);
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

    void SetComputeTexture(ComputeShader cs, int kernel, string name, Texture texture)
    {
        if (cs != null && texture != null)
        {
            cs.SetTexture(kernel, name, texture);
        }
    }

    void SetTextures(ComputeShader cs, int kernel)
    {
        cs.SetTexture(kernel, "albedoTexArray", RayTracingTextures.Instance.GetAlbedo2DArray(128));
        cs.SetTexture(kernel, "normalTexArray", RayTracingTextures.Instance.GetNormal2DArray(128));
    }

    

    void SetupDebugView(Camera camera)
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        kDebugView = DebugView.FindKernel("CSMain");
        gpuSceneData.SetComputeShaderGPUData(DebugView, kDebugView);
        DebugView.SetBuffer(kDebugView, "Rays", rayBuffer);
        DebugView.SetBuffer(kDebugView, "RNGs", samplerBuffer);
        DebugView.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        
        //DebugView.SetFloat("cameraConeSpreadAngle", cameraConeSpreadAngle);
        //DebugView.SetMatrix("WorldToRaster", WorldToRaster);
        DebugView.SetFloat("cameraFar", camera.farClipPlane);
        SetTextures(DebugView, kDebugView);
        DebugView.SetTexture(kDebugView, "outputTexture", outputTexture);

        if (rayConeGBuffer == null)
        {
            rayConeGBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            rayConeGBuffer.name = "RayConeGBuffer";
            rayConeGBuffer.enableRandomWrite = true;
        }

        DebugView.SetTexture(kDebugView, "RayConeGBuffer", rayConeGBuffer);
    }

    void SetupRayTraversal()
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        kRayTraversal = RayTravel.FindKernel("RayTraversal");
        gpuSceneData.SetComputeShaderGPUData(RayTravel, kRayTraversal);

        RayTravel.SetBuffer(kRayTraversal, "Rays", rayBuffer);
        RayTravel.SetBuffer(kRayTraversal, "RNGs", samplerBuffer);
        RayTravel.SetBuffer(kRayTraversal, "pathRadiances", pathRadianceBuffer);

        SetComputeBuffer(RayTravel, kRayTraversal, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        SetComputeBuffer(RayTravel, kRayTraversal, "_RayQueue", rayQueueBuffer);
        RayTravel.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        //RayTravel.SetFloat("cameraConeSpreadAngle", cameraConeSpreadAngle);
        SetTextures(RayTravel, kRayTraversal);

        if (rayConeGBuffer == null)
        {
            rayConeGBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RGHalf);
            rayConeGBuffer.name = "RayConeGBuffer";
            rayConeGBuffer.enableRandomWrite = true;
        }
        RayTravel.SetTexture(kRayTraversal, "RayConeGBuffer", rayConeGBuffer);
        //RayTravel.SetTexture(kRayTraversal, "outputTexture", outputTexture);
    }

    void SetupRayQueueClear()
    {
        if (kRayQueueClear == -1)
        {
            kRayQueueClear = RayQueueClear.FindKernel("CSMain");
            RayQueueClear.SetBuffer(kRayQueueClear, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        }
    }
}
