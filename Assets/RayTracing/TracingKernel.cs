using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

[Serializable]
public class RaytracingData
{
    public enum TracingView
    {
        ColorView,
        NormalView,
        DepthView,
        MipmapView,
        GBufferView,
        ShadowRayView,
        FresnelView,
        EnvmapUVView,
    }

    public enum KernelType
    {
        Mega,
        Wavefront,
    }

    public KernelType _kernelType = KernelType.Wavefront;

    public TracingView viewMode = TracingView.ColorView;

    public int SamplesPerPixel = 128;
    public int MaxDepth = 5;
    public FilterType filterType = FilterType.Gaussian;
    public Vector2 fiterRadius = Vector2.one;
    public float gaussianSigma = 0.5f;
    public bool HDR = true;
    public float _Exposure = 1;
    public bool _EnviromentMapEnable = true;
    public bool _UniformSampleLight = false;
    public bool _SaveOutputTexture = false;
}

public interface TracingKernel
{
    void Setup(Camera camera, RaytracingData data);
    void Update(Camera camera);

    void Release();

    RenderTexture GetOutputTexture();

    GPUSceneData GetGPUSceneData();

    GPUFilterData GetGPUFilterData();
}




