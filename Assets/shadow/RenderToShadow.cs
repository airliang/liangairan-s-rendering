using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class RenderToShadow : MonoBehaviour {

    private Camera mCamera;
    //public Matrix4x4 matTest;
    private Matrix4x4 mLightProjMatrix;     //camera view matrix X camera projmatrix
    private RenderTexture mDepthTexture = null;
    
    private Material mRenderToShadowMtl;
    private Material mBlurMaterial = null;
    private Matrix4x4 mMatR = Matrix4x4.identity;

    private int lightMatrixID = -1;
    private int shadowmapTexelID = -1;
    private int depthBiasID = -1;
    private int shadowLightDirectionID = -1;
    private int shadowColorId = -1;
    private int lightPosId = -1;
    //private int shadowMinId = -1;
    //private int shadowAttentionDistanceId = -1;
    //private int distanceToFollowId = -1;
    private int shadowParamsId = -1;
    private Vector4 shadowMapTexelSize;
    private static readonly int shadowMapSize = 512;
    public bool mVSM = false;
    private bool _useVSM = false;
    private CommandBuffer mRenderToShadowCommand;
    private CommandBuffer mBlurShadowmapCommand;
    //private Camera mainCamera;
    bool hasAddCommandBuf = false;
    public bool mUsePCF = true;
    public Transform mFollowTransform;
    public Transform mMainLight;
    public float distanceToFollow = 5;
    public float shadowAttentionDistance = 10;

    //public Color shadowColor;

    [Range(1.0f, 12.0f)]
    public float fFilterWidth = 5.0f;

    [Range(0.0f, 0.01f)]
    public float fDepthBias = 0.005f;

    [Range(0.0f, 1.0f)]
    public float fShadowMin = 1.0f;

    private List<Transform> lstRenderToShadow = new List<Transform>();

    //[Range(0.0f, 1.0f)]
    //public float shadowAdd = 0.2f;
    //private GameObject mGameObject;
    // Use this for initialization
    void Start () {
        mMatR.m00 = 0.5f;
        mMatR.m11 = 0.5f;
        mMatR.m22 = 0.5f;
        mMatR.m03 = 0.5f;
        mMatR.m13 = 0.5f;
        mMatR.m23 = 0.5f;
        mMatR.m33 = 1;

        //matTest = mPlane.transform.worldToLocalMatrix;
        lightMatrixID = Shader.PropertyToID("LightProjectionMatrix");
        shadowmapTexelID = Shader.PropertyToID("shadowMapTexel");
        shadowMapTexelSize = new Vector4(1.0f / shadowMapSize, 1.0f / shadowMapSize, 0, 0);

        Shader.SetGlobalTexture("_ShadowmapTex", Texture2D.whiteTexture);

    }

    // Update is called once per frame
    void Update () {
        if (_useVSM != mVSM)
        {
            //mCommandBufferDirty = true;
            _useVSM = mVSM;
        }

        if (mMainLight != null && mFollowTransform != null)
            RenderToShadowmap();


        if (mFollowTransform != null && mMainLight != null)
        {
            transform.position = mFollowTransform.position - mMainLight.forward * distanceToFollow;
            transform.forward = mMainLight.forward;
        }

        if (mCamera != null)
            mCamera.Render();


        if (mDepthTexture != null)
        {
            mMatR.m00 = 0.5f;
            mMatR.m11 = 0.5f;
            mMatR.m22 = 0.5f;
            mMatR.m03 = 0.5f;
            mMatR.m13 = 0.5f;
            mMatR.m23 = 0.5f;
            mMatR.m33 = 1;

            if (mDepthTexture != null)
            {
                Shader.SetGlobalTexture("_ShadowmapTex", mDepthTexture);
            }

            if (mBlurMaterial != null)
            {
                mBlurMaterial.SetFloat("fFilterWidth", fFilterWidth);
            }

            if (mUsePCF)
            {
                Shader.EnableKeyword("PCF_ON");
                Shader.DisableKeyword("PCF_OFF");
            }
            else
            {
                Shader.EnableKeyword("PCF_OFF");
                Shader.DisableKeyword("PCF_ON");
            }

            Shader.SetGlobalFloat("fFilterWidth", fFilterWidth);
            Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(mCamera.projectionMatrix, false);
            //Matrix4x4 projectionMatrix = mCamera.projectionMatrix;
            mLightProjMatrix = mMatR * projectionMatrix * mCamera.worldToCameraMatrix;

            if (lightMatrixID < 0)
            {
                lightMatrixID = Shader.PropertyToID("LightProjectionMatrix");
            }

            if (shadowmapTexelID < 0)
            {
                shadowmapTexelID = Shader.PropertyToID("shadowMapTexel");
            }

            if (depthBiasID < 0)
            {
                depthBiasID = Shader.PropertyToID("fDepthBias");
            } 

            if (shadowLightDirectionID < 0)
            {
                shadowLightDirectionID = Shader.PropertyToID("shadowLightDirection");
            }

            //if (shadowAttentionDistanceId < 0)
            //{
            //    shadowAttentionDistanceId = Shader.PropertyToID("shadowAttentionDistance");
            //}

            //if (distanceToFollowId < 0)
            //{
            //    distanceToFollowId = Shader.PropertyToID("followerToLight");
            //}

            if (mRenderToShadowCommand != null)
            {
                mRenderToShadowCommand.SetViewProjectionMatrices(mCamera.worldToCameraMatrix, mCamera.projectionMatrix);
                mRenderToShadowCommand.SetGlobalMatrix(lightMatrixID, mLightProjMatrix);
            }

            //if (shadowColorId < 0)
            //{
            //    shadowColorId = Shader.PropertyToID("shadowColor");
            //}

            if (lightPosId < 0)
            {
                lightPosId = Shader.PropertyToID("lightPos");
            }

            //if (shadowMinId < 0)
            //{
            //    shadowMinId = Shader.PropertyToID("shadowMin");
            //}

            if (shadowParamsId < 0)
            {
                shadowParamsId = Shader.PropertyToID("shadowParams");
            }

            if (lightMatrixID >= 0)
                Shader.SetGlobalMatrix(lightMatrixID, mLightProjMatrix);

            if (shadowmapTexelID >= 0)
                Shader.SetGlobalVector(shadowmapTexelID, shadowMapTexelSize);

            if (depthBiasID >= 0)
            {
                Shader.SetGlobalFloat(depthBiasID, fDepthBias);
            }

            if (shadowLightDirectionID >= 0)
            {
                Shader.SetGlobalVector(shadowLightDirectionID, -mCamera.transform.forward);
            }

            if (lightPosId >= 0)
                Shader.SetGlobalVector(lightPosId, mCamera.transform.position);

            //if (shadowColorId >= 0)
            //{
            //    Shader.SetGlobalColor(shadowColorId, shadowColor);
            //}

            //if (shadowMinId >= 0)
            //{
            //    Shader.SetGlobalFloat(shadowMinId, fShadowMin);
            //}

            //if (shadowAttentionDistanceId >= 0)
            //{
            //    Shader.SetGlobalFloat(shadowAttentionDistanceId, shadowAttentionDistance);
            //}

            //if (distanceToFollowId >= 0)
            //{
            //    Shader.SetGlobalFloat(distanceToFollowId, distanceToFollow);
            //}

            if (shadowParamsId >= 0)
            {
                Shader.SetGlobalVector(shadowParamsId, new Vector4(fShadowMin, shadowAttentionDistance, distanceToFollow, 1.0f));
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ForceRenderTexture();
        }
#endif
    }

    public void ForceRenderTexture()
    {
        OnWillRenderObject();
    }


    private void OnWillRenderObject()
    {
        return;

        mCamera = GetComponent<Camera>();
        if (mCamera == null)
        {
            mCamera = GetComponentInChildren<Camera>();
        }

        if (mCamera == null)
        {
            return;
        }
        
        if (mCamera.enabled)
        {
            mCamera.enabled = false;
        }
        //mCamera.cullingMask = 1 << LayerMask.NameToLayer("RenderToShadow");
        mCamera.clearFlags = CameraClearFlags.SolidColor;
        mCamera.backgroundColor = (mVSM || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal) ? new Color(0, 0, 0, 1) : Color.white;
        mCamera.depth = 0;
        mCamera.orthographic = true;
        mCamera.allowHDR = false;
        mCamera.allowMSAA = false;
        mCamera.useOcclusionCulling = false;
            
        //mCamera.orthographicSize = 0.5f;

        if (mDepthTexture == null)
        {
            //mDepthTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.Depth);
            if (mVSM)
            {
                mDepthTexture = new RenderTexture(shadowMapSize, shadowMapSize, 24, RenderTextureFormat.RGFloat);
            }
            else
                mDepthTexture = new RenderTexture(shadowMapSize, shadowMapSize, 24, RenderTextureFormat.RFloat/*, RenderTextureFormat.Depth*/);
            mDepthTexture.wrapMode = TextureWrapMode.Clamp;
            mDepthTexture.filterMode = FilterMode.Bilinear;
            mDepthTexture.autoGenerateMips = true;
            mDepthTexture.useMipMap = mVSM;
            mDepthTexture.mipMapBias = 0.0f;
        }
        mCamera.targetTexture = mDepthTexture;
        Shader shader = Shader.Find("liangairan/shadow/RenderToShadow");
        mCamera.SetReplacementShader(shader, "Shadow");
        if (mVSM)
            Shader.EnableKeyword("VSM_ON");
        else
            Shader.DisableKeyword("VSM_ON");

        if (mDepthTexture != null)
        {
            Shader.SetGlobalTexture("_ShadowmapTex", mDepthTexture);
        }


        mMatR.m00 = 0.5f;
        mMatR.m11 = 0.5f;
        mMatR.m22 = 0.5f;
        mMatR.m03 = 0.5f;
        mMatR.m13 = 0.5f;

        mMatR.m23 = 0.5f;
        
        mMatR.m33 = 1;

        Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(mCamera.projectionMatrix, false);
        //Matrix4x4 projectionMatrix = mCamera.projectionMatrix;
        mLightProjMatrix = mMatR * projectionMatrix * mCamera.worldToCameraMatrix;

        mCamera.Render();

        //mCamera.targetTexture = null;//否则后面的mDepthTexture不能被写入

        //对shadowmap进行blur操作
        if (mVSM)
        {
            BlurShadowmap();
        }

        //mGenShadowmap = true;
    }

    private void BlurShadowmap()
    {
        if (mBlurMaterial == null)
        {
            mBlurMaterial = new Material(Shader.Find("liangairan/shadow/BlurShadow"));
        }
        mBlurMaterial.SetFloat("fFilterWidth", 5.0f);

        RenderTexture rtTmp = RenderTexture.GetTemporary(mDepthTexture.width, mDepthTexture.height, 0, mDepthTexture.format);
        Graphics.Blit(mDepthTexture, rtTmp, mBlurMaterial, 0);

        Graphics.Blit(rtTmp, mDepthTexture);

        RenderTexture.ReleaseTemporary(rtTmp);
    }

    private void RenderToShadowmap()
    {
        //mCamera.TryGetCullingParameters();
        if (mRenderToShadowCommand == null)
        {
            mRenderToShadowCommand = new CommandBuffer();
            mRenderToShadowCommand.name = "RenderToShadow";
        }

        
        if (mCamera == null)
        {
            mCamera = GetComponent<Camera>();
            if (mCamera == null)
                mCamera = GetComponentInChildren<Camera>();
        }

        if (mCamera == null)
        {
            return;
        }

        if (mCamera.enabled)
        {
            mCamera.enabled = false;
        }
        //mCamera.targetTexture = null;
        //mCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, mRenderToShadowCommand);
        mRenderToShadowCommand.Clear();

        mCamera.clearFlags = CameraClearFlags.SolidColor;
     
        mCamera.backgroundColor = mVSM ? new Color(0, 0, 0, 1) : Color.white;
        mCamera.depth = -1;
        mCamera.orthographic = true;
        mCamera.allowHDR = false;
        mCamera.allowMSAA = false;
        mCamera.useOcclusionCulling = false;
        //mCamera.cullingMask = 0;

        //mCamera.orthographicSize = 0.5f;


        if (mDepthTexture == null)
        {
            //mDepthTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.Depth);
            if (mVSM)
            {
                mDepthTexture = new RenderTexture(shadowMapSize, shadowMapSize, 24, RenderTextureFormat.RGFloat);
            }
            else
                mDepthTexture = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 24, RenderTextureFormat.RHalf);//new RenderTexture(shadowMapSize, shadowMapSize, 24, RenderTextureFormat.RHalf);
            mDepthTexture.name = "shadowmap";
            mDepthTexture.wrapMode = TextureWrapMode.Clamp;
            mDepthTexture.filterMode = FilterMode.Bilinear;
            mDepthTexture.autoGenerateMips = true;
            mDepthTexture.useMipMap = mVSM;
            mDepthTexture.mipMapBias = 0.0f;
        }
        else
        {
            if (mVSM)
            {
                if (mDepthTexture.format != RenderTextureFormat.RGFloat)
                {
                    DestroyImmediate(mDepthTexture);
                    mDepthTexture = new RenderTexture(shadowMapSize, shadowMapSize, 24, RenderTextureFormat.RGFloat);
                }
                mDepthTexture.autoGenerateMips = true;
                mDepthTexture.useMipMap = true;
                mDepthTexture.name = "shadowmap";
            }
            else
            {
                if (mDepthTexture.format != RenderTextureFormat.RHalf)
                {
                    DestroyImmediate(mDepthTexture);
                    mDepthTexture = new RenderTexture(shadowMapSize, shadowMapSize, 24, RenderTextureFormat.RHalf);
                    mDepthTexture.autoGenerateMips = false;
                    mDepthTexture.useMipMap = false;
                    mDepthTexture.name = "shadowmap";
                }

            }
        }

        RenderTargetIdentifier renderTargetIdentifier = new RenderTargetIdentifier(mDepthTexture);

        mRenderToShadowCommand.SetRenderTarget(renderTargetIdentifier, 0);
        mRenderToShadowCommand.ClearRenderTarget(true, true, (mVSM || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal) ? Color.black : Color.white);

        //mRenderToShadowCommand.SetViewProjectionMatrices(mCamera.worldToCameraMatrix, mCamera.projectionMatrix);

        //Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(mCamera.projectionMatrix, false);
        //mLightProjMatrix = mMatR * projectionMatrix * mCamera.worldToCameraMatrix;
        //mRenderToShadowCommand.SetGlobalMatrix(lightMatrixID, mLightProjMatrix);
        if (mRenderToShadowMtl == null)
        {
            mRenderToShadowMtl = new Material(Shader.Find("liangairan/shadow/RenderToShadow"));
        }

        if (mVSM)
            mRenderToShadowCommand.EnableShaderKeyword("VSM_ON");
        else
            mRenderToShadowCommand.DisableShaderKeyword("VSM_ON");

        if (mFollowTransform != null)
        {
            Renderer renderer = mFollowTransform.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int j = 0; j < materials.Length; ++j)
                {
                    mRenderToShadowCommand.DrawRenderer(renderer, mRenderToShadowMtl, j);
                }
            }

            Renderer[] childRenderers = mFollowTransform.GetComponentsInChildren<Renderer>();
            for (int j = 0; j < childRenderers.Length; ++j)
            {
                Material[] materials = childRenderers[j].sharedMaterials;
                for (int k = 0; k < materials.Length; ++k)
                {
                    mRenderToShadowCommand.DrawRenderer(childRenderers[j], mRenderToShadowMtl, k);
                }
            }
        }

        for (int i = 0; i < lstRenderToShadow.Count; ++i)
        {
            Renderer[] childRenderers = lstRenderToShadow[i].GetComponentsInChildren<Renderer>();
            for (int j = 0; j < childRenderers.Length; ++j)
            {
                Material[] materials = childRenderers[j].sharedMaterials;
                for (int k = 0; k < materials.Length; ++k)
                {
                    mRenderToShadowCommand.DrawRenderer(childRenderers[j], mRenderToShadowMtl, k);
                }
            }
        }

        if (mVSM)
        {
            mRenderToShadowCommand.SetRenderTarget(new RenderTargetIdentifier(), 0);

            if (mBlurMaterial == null)
            {
                mBlurMaterial = new Material(Shader.Find("liangairan/shadow/BlurShadow"));
            }
            mBlurMaterial.SetFloat("fFilterWidth", 5.0f);

            //RenderTexture rtTmp = RenderTexture.GetTemporary(mDepthTexture.width, mDepthTexture.height, 0, mDepthTexture.format);
            int blurShadowId = Shader.PropertyToID("_BlurShadowTex");
            mRenderToShadowCommand.GetTemporaryRT(blurShadowId, mDepthTexture.width, mDepthTexture.height, 0, FilterMode.Bilinear, mDepthTexture.format);

            RenderTargetIdentifier rtTmpIdentifier = new RenderTargetIdentifier(blurShadowId);
            mRenderToShadowCommand.Blit(mDepthTexture, rtTmpIdentifier, mBlurMaterial, 0);

            mRenderToShadowCommand.Blit(rtTmpIdentifier, renderTargetIdentifier, mBlurMaterial, 1);
            mRenderToShadowCommand.ReleaseTemporaryRT(blurShadowId);
        }

        if (!hasAddCommandBuf)
        {
            mCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, mRenderToShadowCommand);
            hasAddCommandBuf = true;
        }
    }

    private void OnGUI()
    {
        if (mDepthTexture != null)
            GUI.DrawTexture(new Rect(10, 10, 256, 256), mDepthTexture);
    }

    private void OnDestroy()
    {
        if (mCamera != null)
        {
            mCamera.targetTexture = null;
            mCamera.RemoveAllCommandBuffers();
            mCamera = null;
        }
        if (mDepthTexture != null)
        {
            mDepthTexture.Release();
            //RenderTexture.ReleaseTemporary(mDepthTexture);
#if UNITY_EDITOR
            //Object.DestroyImmediate(mDepthTexture);
#else
            //Object.Destroy(mDepthTexture);
#endif
        }
        mDepthTexture = null;

        if (mRenderToShadowMtl != null)
        {
            Object.DestroyImmediate(mRenderToShadowMtl);
            mRenderToShadowMtl = null;
        }

        if (mRenderToShadowCommand != null)
        {
            mRenderToShadowCommand.Clear();
            mRenderToShadowCommand = null;
        }
    }

    public void AddRenderToShadow(Transform obj)
    {
        if (!lstRenderToShadow.Contains(obj))
            lstRenderToShadow.Add(obj);
    }
}
