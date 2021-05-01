using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class ShadowCreateParams
{
    public int shadowMapSize = 512;
    public float orthographicSize = 1.5f;
    public float fDepthBias = 0.00001f;

    public float fMaxDepthBias = 0.00015f;

    public float fDepthBiasFar = 0.0008f;

    public float fMaxDepthBiasFar = 0.0004f;
}

[ExecuteInEditMode]
public class CascadeShadow : MonoBehaviour
{

    const RenderTextureFormat shadowMapFormat = RenderTextureFormat.Depth;
    private Camera mCamera;
    //public Matrix4x4 matTest;
    private Matrix4x4 mLightProjMatrix;     //camera view matrix X camera projmatrix
    private RenderTexture mDepthTexture = null;

    private Material mRenderToShadowMtl;
    //private Material mBlurMaterial = null;
    private Matrix4x4 mMatR = Matrix4x4.identity;

    private int lightMatrixID = -1;
    private int shadowmapTexelID = -1;
    private int depthBiasID = -1;
    private int shadowLightDirectionID = -1;
    private int shadowColorId = -1;
    private int lightPosId = -1;
    private int shadowMapId = -1;

    //private int shadowMinId = -1;
    private int shadowParamsId = -1;
    private int targetPosDistanceId = -1;
    private Vector4 shadowMapTexelSize;
    private static int shadowMapSize = 512;
    //public bool mVSM = false;
    //private bool _useVSM = false;
    private CommandBuffer mRenderToShadowCommand;
    //private CommandBuffer mBlurShadowmapCommand;
    //private Camera mainCamera;
    bool hasAddCommandBuf = false;
    public bool mUsePCF = true;
    public Transform mFollowTransform;
    public Transform mMainLight;
    public float distanceToFollow = 5;
    public float shadowAttentionDistance = 5;  //衰减距离
    //public float shadowColorScale = 1.0f;
    private Vector3 mCenterOffset;

    private static bool s_ShadowMapEnable = false;
    private Vector4 depthBias;
    private Transform mainCamera;
    private Vector4 targetPosAndDistance;
    //private Vector3 origEulerAngles;
    public Color shadowColor;

    [Range(1.0f, 12.0f)]
    public float fFilterWidth = 5.0f;

    [Range(0.0f, 0.01f)]
    public float fDepthBias = 0.00001f;
    [Range(0.0f, 0.01f)]
    public float fMaxDepthBias = 0.00015f;
    [Range(0.0f, 0.05f)]
    public float fDepthBiasFar = 0.0008f;
    [Range(0.0f, 0.05f)]
    public float fMaxDepthBiasFar = 0.0004f;

    [Range(0.0f, 1.0f)]
    public float fShadowMin = 0.0f;

    private List<Transform> lstRenderToShadow = new List<Transform>();

    //private List<string> lstExcludeNames = new List<string>();

    //private bool mUseCommandBuf = true;

    //是否使用级联
    private bool mUseCascade = false;
    private Matrix4x4 mLightProjMatrixClear;     //清晰的light矩阵
    private RenderTexture mClearDepthTexture = null;    //清晰的DepthTexture
    private int clearShadowMapId = -1;
    private int clearLightMatrixID = -1;
    private Camera mCameraClear;    //清晰的摄像机
    private static GameObject clearShadowCaster = null;
    private static GameObject ShadowCaster = null;
    private Vector4 shadowMapTexelSizeClear;
    private int shadowmapTexelClearID = -1;

    Vector4 shadowParams;

    private float cascadeNear = 10.0f;    //第一个cascade的范围，从camera.near到cascadeNear
    private float cascadeFar = 50.0f;   //第二个cascade的范围，
    private Vector2 cascadeEndClipSpace;
    private int cascadeEndClipSpaceID = -1;

#if UNITY_EDITOR
    public bool debugMode = false;
#endif // UNITY_EDITOR

    //private int shadowmapUpdateCount = 0;
    //private int shadowmapUpdateCountFar = 0;

    Camera cameraMain = null;  //场景的主摄像机

    //bounding in viewspace
    public enum CascadeCorner
    {
        bottomleft = 0,
        topleft,
        bottomright,
        topright,
        bottomleft_far,
        topleft_far,
        bottomright_far,
        topright_far,
    }

    //in view space
    private Vector4[] frustrumCornersLevel0 = new Vector4[8];
    private Vector4[] frustrumCornersLevel1 = new Vector4[8];

    //center in world space
    private Vector3 frustrumCenterLevel0;
    private Vector3 frustrumCenterLevel1;

    //[Range(0.0f, 1.0f)]
    //public float shadowAdd = 0.2f;
    //private GameObject mGameObject;
    // Use this for initialization
    void Start () {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
        {
            mMatR.m00 = 0.5f;
            mMatR.m11 = 0.5f;
            mMatR.m22 = 0.5f;
            mMatR.m03 = 0.5f;
            mMatR.m13 = 0.5f;
            mMatR.m23 = 0.5f;
            mMatR.m33 = 1;
        }
        else
        {
            mMatR.m00 = 0.5f;
            mMatR.m11 = 0.5f;
            mMatR.m22 = 1;
            mMatR.m03 = 0.5f;
            mMatR.m13 = 0.5f;
            mMatR.m23 = 0;
            mMatR.m33 = 1;
        }

        //matTest = mPlane.transform.worldToLocalMatrix;
        lightMatrixID = Shader.PropertyToID("LightProjectionMatrix");
        shadowmapTexelID = Shader.PropertyToID("shadowMapTexel");
        shadowMapTexelSize = new Vector4(1.0f / shadowMapSize, 1.0f / shadowMapSize, 0, 0);
        shadowColor = new Color(183.0f / 255, 194.0f / 255, 197.0f / 255);
        ShadowCaster = gameObject;
    }

    // Update is called once per frame
    void Update () {
        //if (!s_ShadowMapEnable)
        //{
        //    return;
        //}
        if (mFollowTransform != null && mMainLight != null)
        {
            if (mUseCascade)
            {
                clearShadowCaster.transform.forward = mMainLight.forward;
                //clearShadowCaster.transform.position = frustrumCenterLevel0 - mMainLight.forward * distanceToFollow;
                CaculateCascadeBoundsInLight();
            }
            else
            {
                transform.position = mFollowTransform.position + mCenterOffset - mMainLight.forward * distanceToFollow;
                transform.forward = mMainLight.forward;
                Vector3 origLocalEuler = transform.eulerAngles;
                if (mainCamera != null)
                {
                    //shadowmap中的摄像机的up，forward要与mainCamera的up，forward共面
                    Vector3 forward = transform.forward;
                    Vector3 right = Vector3.Cross(mainCamera.forward, forward);
                    Vector3 up = Vector3.Cross(forward, right);
                    transform.rotation = Quaternion.LookRotation(forward, up);
                    transform.position += transform.up * mCamera.orthographicSize * 0.75f;

                    //Camera cameraMain = mainCamera.GetComponent<Camera>();
                    //Vector4 centerInView = new Vector4(0, 0, -(cameraMain.nearClipPlane + cascadeNear) * 0.5f, 1);
                    //frustrumCenterLevel0 = cameraMain.cameraToWorldMatrix * centerInView;
                    //centerInView = new Vector4(0, 0, -(cascadeNear + cascadeFar) * 0.5f, 1);
                    //frustrumCenterLevel1 = cameraMain.cameraToWorldMatrix * centerInView;
                    //transform.position = frustrumCenterLevel1 - mMainLight.forward * distanceToFollow;
                }

                if (mCameraClear != null)
                {
                    clearShadowCaster.transform.position = mFollowTransform.position + mCenterOffset - mMainLight.forward * distanceToFollow;
                    clearShadowCaster.transform.localRotation = transform.localRotation;
                    mCameraClear.transform.position += clearShadowCaster.transform.up * mCameraClear.orthographicSize * 0.25f;
                }
            }
            
            
        }


        if (mDepthTexture == null)
        {

            mDepthTexture = new RenderTexture(shadowMapSize, shadowMapSize, 24, shadowMapFormat);
            mDepthTexture.name = "Shadowmap_Far";
            mDepthTexture.wrapMode = TextureWrapMode.Clamp;
            mDepthTexture.filterMode = FilterMode.Bilinear;
            mDepthTexture.autoGenerateMips = true;
            mDepthTexture.useMipMap = false;
            mDepthTexture.mipMapBias = 0.0f;
        }

        if (mCamera.targetTexture != mDepthTexture)
        {
            mCamera.targetTexture = mDepthTexture;
        }

        if (mUseCascade)
        {
            if (mClearDepthTexture == null)
            {
                mClearDepthTexture = new RenderTexture(1024, 1024, 24, shadowMapFormat);
                mClearDepthTexture.name = "Shadowmap_Near";
                mClearDepthTexture.wrapMode = TextureWrapMode.Clamp;
                mClearDepthTexture.filterMode = FilterMode.Bilinear;
                mClearDepthTexture.autoGenerateMips = false;
                mClearDepthTexture.useMipMap = false;
            }

            if (mCameraClear.targetTexture != mClearDepthTexture)
            {
                mCameraClear.targetTexture = mClearDepthTexture;
            }
        }
  

        if (mDepthTexture != null)
        {

            Matrix4x4 projectionMatrix;
            //Shader.SetGlobalFloat("fFilterWidth", fFilterWidth);
            if (mCamera != null)
            {
                projectionMatrix = GL.GetGPUProjectionMatrix(mCamera.projectionMatrix, false);
                //Matrix4x4 projectionMatrix = mCamera.projectionMatrix;
                mLightProjMatrix = mMatR * projectionMatrix * mCamera.worldToCameraMatrix;
            }
            

            if (shadowMapId < 0)
            {
                shadowMapId = Shader.PropertyToID("_ShadowmapTex");
            }

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

            //if (mRenderToShadowCommand != null)
            //{
            //    mRenderToShadowCommand.SetViewProjectionMatrices(mCamera.worldToCameraMatrix, mCamera.projectionMatrix);
            //    mRenderToShadowCommand.SetGlobalMatrix(lightMatrixID, mLightProjMatrix);
            //}

            if (shadowColorId < 0)
            {
                shadowColorId = Shader.PropertyToID("_ShadowColor");
            }

            if (lightPosId < 0)
            {
                lightPosId = Shader.PropertyToID("lightPos");
            }

            if (shadowParamsId < 0)
            {
                shadowParamsId = Shader.PropertyToID("shadowParams");
            }

            if (targetPosDistanceId < 0)
            {
                targetPosDistanceId = Shader.PropertyToID("targetPositionAndDistance");
            }

            //if (shadowMinId < 0)
            //{
            //    shadowMinId = Shader.PropertyToID("shadowMin");
            //}

            if (lightMatrixID >= 0)
                Shader.SetGlobalMatrix(lightMatrixID, mLightProjMatrix);

            if (shadowmapTexelID >= 0)
                Shader.SetGlobalVector(shadowmapTexelID, shadowMapTexelSize);

            if (depthBiasID >= 0)
            {
                //Shader.SetGlobalFloat(depthBiasID, fDepthBias);
                depthBias.x = fDepthBias;
                depthBias.y = fMaxDepthBias;
                depthBias.z = fDepthBiasFar;
                depthBias.w = fMaxDepthBiasFar;
                Shader.SetGlobalVector(depthBiasID, depthBias);
            }

            if (shadowLightDirectionID >= 0)
            {
                Shader.SetGlobalVector(shadowLightDirectionID, -mCamera.transform.forward);
            }

            if (lightPosId >= 0)
                Shader.SetGlobalVector(lightPosId, mCamera.transform.position);

            if (shadowColorId >= 0)
            {
                Shader.SetGlobalColor(shadowColorId, shadowColor);
            }

            //if (shadowMinId >= 0)
            //{
            //    Shader.SetGlobalFloat(shadowMinId, fShadowMin);
            //}

            if (shadowMapId >= 0)
            {
                Shader.SetGlobalTexture(shadowMapId, mDepthTexture);
            }

            if (shadowParamsId >= 0)
            {
                shadowParams.x = fShadowMin;
                shadowParams.y = shadowAttentionDistance;
                shadowParams.z = distanceToFollow;
                Shader.SetGlobalVector(shadowParamsId, shadowParams);
            }

            if (targetPosDistanceId > 0)
            {
                targetPosAndDistance = mFollowTransform != null ? mFollowTransform.position : Vector3.zero;
                targetPosAndDistance.w = shadowAttentionDistance;
                Shader.SetGlobalVector(targetPosDistanceId, targetPosAndDistance);
            }

            //处理cascade
            if (mUseCascade && mainCamera != null)
            {
                if (clearShadowMapId < 0)
                {
                    clearShadowMapId = Shader.PropertyToID("_ClearShadowmapTex");
                }

                if (clearLightMatrixID < 0)
                {
                    clearLightMatrixID = Shader.PropertyToID("LightProjectionMatrixNear");
                }

                if (shadowmapTexelClearID < 0)
                {
                    shadowmapTexelClearID = Shader.PropertyToID("shadowMapTexelClear");
                }

                if (cascadeEndClipSpaceID < 0)
                {
                    cascadeEndClipSpaceID = Shader.PropertyToID("cascadeEndClipSpace");
                }

                if (mCameraClear != null)
                {
                    projectionMatrix = GL.GetGPUProjectionMatrix(mCameraClear.projectionMatrix, false);
                    //Matrix4x4 projectionMatrix = mCamera.projectionMatrix;
                    mLightProjMatrixClear = mMatR * projectionMatrix * mCameraClear.worldToCameraMatrix;
                }
                
                if (cameraMain == null)
                    cameraMain = mainCamera.GetComponent<Camera>();
                projectionMatrix = GL.GetGPUProjectionMatrix(cameraMain.projectionMatrix, false);
                Vector4 viewNear = new Vector4(0, 0, -cascadeNear, 1.0f);
                Vector4 clipNear = projectionMatrix * viewNear;
                viewNear.z = -cascadeFar;
                Vector4 clipFar = projectionMatrix * viewNear;
                cascadeEndClipSpace.x = clipNear.z / clipNear.w;
                cascadeEndClipSpace.y = clipFar.z / clipFar.w;

                if (clearLightMatrixID >= 0)
                    Shader.SetGlobalMatrix(clearLightMatrixID, mLightProjMatrixClear);

                if (clearShadowMapId >= 0)
                {
                    Shader.SetGlobalTexture(clearShadowMapId, mClearDepthTexture);
                }

                if (shadowmapTexelClearID > 0)
                {
                    Shader.SetGlobalVector(shadowmapTexelClearID, shadowMapTexelSizeClear);
                }

                if (cascadeEndClipSpaceID > 0)
                {
                    Shader.SetGlobalVector(cascadeEndClipSpaceID, cascadeEndClipSpace);
                }
            }
        }
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



        mRenderToShadowCommand.BeginSample(mRenderToShadowCommand.name);
        if (mDepthTexture == null)
        {
            //mDepthTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.Depth);
            mDepthTexture = new RenderTexture(shadowMapSize, shadowMapSize, 24, shadowMapFormat);
            mDepthTexture.name = "Shadowmap_Far";
            //mDepthTexture.dimension = TextureDimension.
            mDepthTexture.wrapMode = TextureWrapMode.Clamp;
            mDepthTexture.filterMode = FilterMode.Bilinear;
            mDepthTexture.autoGenerateMips = false;
            mDepthTexture.useMipMap = false;
            mDepthTexture.mipMapBias = 0.0f;
        }
        else
        {
            if (mDepthTexture.format != shadowMapFormat)
            {
                DestroyImmediate(mDepthTexture);
                mDepthTexture = new RenderTexture(shadowMapSize, shadowMapSize, 24, shadowMapFormat);
                mDepthTexture.name = "Shadowmap_Far";
                mDepthTexture.autoGenerateMips = false;
                mDepthTexture.useMipMap = false;
            }
        }

        RenderTargetIdentifier renderTargetIdentifier = new RenderTargetIdentifier(mDepthTexture);

        mRenderToShadowCommand.SetRenderTarget(renderTargetIdentifier, 0);
        mRenderToShadowCommand.ClearRenderTarget(true, true, (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal) ? Color.black : Color.white);
        //mRenderToShadowCommand.SetViewProjectionMatrices(mCamera.worldToCameraMatrix, mCamera.projectionMatrix);

        //Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(mCamera.projectionMatrix, false);
        //mLightProjMatrix = mMatR * projectionMatrix * mCamera.worldToCameraMatrix;
        //mRenderToShadowCommand.SetGlobalMatrix(lightMatrixID, mLightProjMatrix);
        if (mRenderToShadowMtl == null)
        {
            Shader renderToShadow = Shader.Find("Omega/shadow/RenderToShadow");
            mRenderToShadowMtl = new Material(renderToShadow);
        }


        for (int i = 0; i < lstRenderToShadow.Count; ++i)
        {

            Renderer[] childRenderers = lstRenderToShadow[i].GetComponentsInChildren<Renderer>();
            for (int j = 0; j < childRenderers.Length; ++j)
            {
                if (childRenderers[j].sharedMaterials != null/* && IsNameValid(childRenderers[j].transform.name)*/ && childRenderers[j].transform.gameObject.activeSelf)
                {
                    Material[] materials = childRenderers[j].sharedMaterials;
                    for (int k = 0; k < materials.Length; ++k)
                    {
                        if (materials[k] != null && materials[k].renderQueue < 3000)
                            mRenderToShadowCommand.DrawRenderer(childRenderers[j], mRenderToShadowMtl, k);
                    }
                }
            }
        }

        mRenderToShadowCommand.EndSample(mRenderToShadowCommand.name);

        if (!hasAddCommandBuf)
        {
            mCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, mRenderToShadowCommand);
            if (mDepthTexture != null)
            {
                Shader.SetGlobalTexture("_ShadowmapTex", mDepthTexture);
            }
            hasAddCommandBuf = true;
        }
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (debugMode)
        {
            if (mDepthTexture != null)
                GUI.DrawTexture(new Rect(10, 10, 256, 256), mDepthTexture);

            if (mClearDepthTexture != null)
                GUI.DrawTexture(new Rect(276, 10, 256, 256), mClearDepthTexture);
        }
    }
#endif

    private void OnDestroy()
    {
        lstRenderToShadow.Clear();
        mMainLight = null;
        mFollowTransform = null;
        cameraMain = null;
        if (mCamera != null)
        {
            mCamera.targetTexture = null;
            mCamera.RemoveAllCommandBuffers();
#if UNITY_EDITOR
            DestroyImmediate(mCamera);
#else
            Destroy(mCamera);
#endif
            mCamera = null;
            
        }

        if (mCameraClear != null)
        {
            mCameraClear.targetTexture = null;
            mCameraClear.RemoveAllCommandBuffers();
#if UNITY_EDITOR
            DestroyImmediate(mCameraClear);
#else
            Destroy(mCameraClear);
#endif
            mCameraClear = null;
        }

        if (mDepthTexture != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(mDepthTexture);
#else
            Object.Destroy(mDepthTexture);
#endif
        }
        mDepthTexture = null;

        if (mClearDepthTexture != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(mClearDepthTexture);
#else
            Object.Destroy(mClearDepthTexture);
#endif
        }
        mClearDepthTexture = null;
        if (clearShadowCaster != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(clearShadowCaster);
#else
            Destroy(clearShadowCaster);
#endif
            clearShadowCaster = null;
        }
        ShadowCaster = null;

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

        mainCamera = null;
    }

    public void AddRenderToShadow(Transform obj)
    {
        if (!lstRenderToShadow.Contains(obj))
            lstRenderToShadow.Add(obj);
    }

    public void RemoveRenderToShadow(Transform obj)
    {
        if (lstRenderToShadow.Contains(obj))
            lstRenderToShadow.Remove(obj);

        if (obj == mFollowTransform)
        {
            mFollowTransform = null;
        }
    }

    public Vector3 FollowCenterOffset
    {
        set
        {
            mCenterOffset = value;
        }
        get
        {
            return mCenterOffset;
        }
    }

    public Transform GetFollowTransform()
    {
        return mFollowTransform;
    }

    //private bool IsNameValid(string name)
    //{
    //    for (int i = 0; i < lstExcludeNames.Count; ++i)
    //    {
    //        if (name.Contains(lstExcludeNames[i]))
    //        {
    //            return false;
    //        }
    //    }

    //    return true;
    //}

    public void SetCameraCullingmask(int layer)
    {
        if (mCamera == null)
        {
            mCamera = GetComponent<Camera>();
        }
        mCamera.cullingMask |= layer;

        if (mCameraClear != null)
        {
            mCameraClear.cullingMask |= layer;
        }
    }

    public static void Initialize()
    {
        Shader.SetGlobalTexture("_ShadowmapTex", (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal) ? Texture2D.blackTexture : Texture2D.whiteTexture);
    }

    public static CascadeShadow CreateShadowCaster(Transform followTransform, Transform lightTransform, ShadowCreateParams shadowParam, Shader render2Shadow, GameObject shadowCaster)
    {
        if (!ShadowmapEnable)
        {
            //Shader.EnableKeyword("SHADOWMAP_OFF");
            //Shader.DisableKeyword("SHADOWMAP_ON");
            //return null;
        }

        if (render2Shadow == null)
        {
            return null;
        }

        //Shader.DisableKeyword("SHADOWMAP_OFF");
        //Shader.EnableKeyword("SHADOWMAP_ON");
        //GameObject shadowCaster = new GameObject(name);

        CascadeShadow.shadowMapSize = shadowParam.shadowMapSize;
        CascadeShadow renderToShadow = shadowCaster.AddComponent<CascadeShadow>();
        //renderToShadow.mUseCommandBuf = useCommandBuf;
        renderToShadow.mMainLight = lightTransform;
        renderToShadow.mFollowTransform = followTransform;

        Camera camera = shadowCaster.AddComponent<Camera>();
        camera.orthographic = true;
        //camera.cullingMask = 0;
        camera.farClipPlane = 15.0f;
        camera.orthographicSize = shadowParam.orthographicSize;
        camera.aspect = 1.0f;
        camera.pixelRect = new Rect(0, 0, shadowMapSize, shadowMapSize);
        camera.enabled = true;

        camera.depth = -1;
        camera.orthographic = true;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.useOcclusionCulling = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.rect = new Rect(0, 0, 1, 1);

        camera.backgroundColor = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal) ? new Color(0, 0, 0, 1) : Color.white;
        renderToShadow.mCamera = camera;

        renderToShadow.transform.forward = lightTransform.forward;
        renderToShadow.shadowMapTexelSize = new Vector4(1.0f / (float)shadowMapSize, 1.0f / (float)shadowMapSize, 0, 0);

        renderToShadow.mUseCascade = Shader.IsKeywordEnabled("_CASCADE_SHADOW");

        if (renderToShadow.mUseCascade)
        {
            CascadeShadow.clearShadowCaster = new GameObject("ShadowCasterClear");
            //clearCamera.transform.SetParent(shadowCaster.transform, false);
            camera = CascadeShadow.clearShadowCaster.AddComponent<Camera>();
            camera.orthographic = true;
            //amera.cullingMask = 0;
            camera.farClipPlane = 15.0f;
            camera.orthographicSize = shadowParam.orthographicSize * 0.2f;
            camera.aspect = 1.0f;
            camera.pixelRect = new Rect(0, 0, 1024, 1024);
            camera.enabled = true;

            camera.depth = -1.1f;
            camera.orthographic = true;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.rect = new Rect(0, 0, 1, 1);

            camera.backgroundColor = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal) ? new Color(0, 0, 0, 1) : Color.white;
            renderToShadow.mCameraClear = camera;
            renderToShadow.shadowMapTexelSizeClear = new Vector4(1.0f / 1024, 1.0f / 1024, 0, 0);


        }


        //Shader.Find("Omega/shadow/CascadeShadow");

        renderToShadow.mCamera.SetReplacementShader(render2Shadow, "Shadow");

        if (renderToShadow.mCameraClear != null)
        {
            renderToShadow.mCameraClear.SetReplacementShader(render2Shadow, "Shadow");
        }

        Shader.SetGlobalTexture("_ShadowmapTex", (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal) ? Texture2D.blackTexture : Texture2D.whiteTexture);

        renderToShadow.fDepthBias = shadowParam.fDepthBias;
        renderToShadow.fMaxDepthBias = shadowParam.fMaxDepthBias;
        renderToShadow.fDepthBiasFar = shadowParam.fDepthBiasFar;
        renderToShadow.fMaxDepthBiasFar = shadowParam.fMaxDepthBiasFar;

        return renderToShadow;
    }

    public static bool IsHardwareSupport()
    {
        return SystemInfo.SupportsRenderTextureFormat(shadowMapFormat);
    }

    public static bool ShadowmapEnable
    {
        get
        {
            return s_ShadowMapEnable;
        }
        set
        {
            if (s_ShadowMapEnable != value)
            {
                s_ShadowMapEnable = value;

                if (s_ShadowMapEnable)
                {
                    Shader.EnableKeyword("_RECEIVESHADOW");
                    Shader.EnableKeyword("_CASCADE_SHADOW");
     
   
                    if (CascadeShadow.ShadowCaster != null && !CascadeShadow.ShadowCaster.activeSelf)
                    {
                        CascadeShadow.ShadowCaster.SetActive(true);
                    }

                    if (clearShadowCaster != null && !clearShadowCaster.activeSelf)
                    {
                        clearShadowCaster.SetActive(true);
                    }
                }
                else
                {
                    Shader.DisableKeyword("_RECEIVESHADOW");
                    Shader.DisableKeyword("_CASCADE_SHADOW");

                    if (CascadeShadow.ShadowCaster != null && CascadeShadow.ShadowCaster.activeSelf)
                    {
                        CascadeShadow.ShadowCaster.SetActive(false);
                    }

                    if (clearShadowCaster != null && clearShadowCaster.activeSelf)
                    {
                        clearShadowCaster.SetActive(false);
                    }
                }
            }
        }
    }

    private void OnDisable()
    {
        if (mCamera != null)
        {
            mCamera.targetTexture = null;
        }
        if (mDepthTexture != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(mDepthTexture);
#else
            Object.Destroy(mDepthTexture);
#endif
        }
        mDepthTexture = null;

        if (mCameraClear != null)
        {
            mCameraClear.targetTexture = null;
        }

        if (mClearDepthTexture != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(mClearDepthTexture);
#else
            Object.Destroy(mClearDepthTexture);
#endif
        }
        mClearDepthTexture = null;
    }

    public void SetFollowTransform(Transform followTransform)
    {
        mFollowTransform = followTransform;
    }

    public void SetCameraFarPlane(float far)
    {
        if (mCamera != null)
        {
            mCamera.farClipPlane = far;
        }
        if (mUseCascade)
        {
            if (mCameraClear != null)
            {
                mCameraClear.farClipPlane = far;
            }
        }
    }

    public void SetFollowDistance(float distance)
    {
        distanceToFollow = distance;
    }

    public void SetMainCamera(Transform camera)
    {
        mainCamera = camera;
        CaculateCascadeCorners();

        float x = transform.eulerAngles.x * Mathf.Deg2Rad;
        float y = transform.eulerAngles.y * Mathf.Deg2Rad;
        float z = transform.eulerAngles.z * Mathf.Deg2Rad;
        Vector3 forwardCamera = new Vector3(Mathf.Cos(x) * Mathf.Sin(y) * Mathf.Cos(z) + Mathf.Sin(x) * Mathf.Sin(z), Mathf.Cos(x) * Mathf.Sin(y) * Mathf.Sin(z) - Mathf.Sin(x) * Mathf.Cos(z), Mathf.Cos(x) * Mathf.Cos(y));
        forwardCamera.Normalize();
        //Debug.Log(forwardCamera);
    }

    public Transform GetMainCamera()
    {
        return mainCamera;
    }

    private void CaculateCascadeCorners()
    {
        if (mainCamera == null)
            return;
        Camera mainCameraComponent = mainCamera.GetComponent<Camera>();
        if (null == mainCameraComponent)
            return;
        float y1InView = mainCameraComponent.nearClipPlane * Mathf.Tan(mainCameraComponent.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float y2InView = cascadeNear * Mathf.Tan(mainCameraComponent.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float y3InView = cascadeFar * Mathf.Tan(mainCameraComponent.fieldOfView * Mathf.Deg2Rad * 0.5f);

        float ratio = mainCameraComponent.aspect;
        float x1InView = y1InView * ratio;
        float x2InView = y2InView * ratio;
        float x3InView = y3InView * ratio;


        //Vector3 leftBottomCornerNear = mainCamera.localToWorldMatrix * new Vector3(-x1InView, -y1InView, mainCameraComponent.nearClipPlane);
        frustrumCornersLevel0[(int)CascadeCorner.bottomleft] = new Vector4(-x1InView, -y1InView, mainCameraComponent.nearClipPlane, 1);
        frustrumCornersLevel0[(int)CascadeCorner.topleft] = new Vector4(-x1InView, y1InView, mainCameraComponent.nearClipPlane, 1);
        frustrumCornersLevel0[(int)CascadeCorner.bottomright] = new Vector4(x1InView, -y1InView, mainCameraComponent.nearClipPlane, 1);
        frustrumCornersLevel0[(int)CascadeCorner.topright] = new Vector4(x1InView, y1InView, mainCameraComponent.nearClipPlane, 1);

        frustrumCornersLevel0[(int)CascadeCorner.bottomleft_far] = new Vector4(-x2InView, -y2InView, cascadeNear, 1);
        frustrumCornersLevel0[(int)CascadeCorner.topleft_far] = new Vector4(-x2InView, y2InView, cascadeNear, 1);
        frustrumCornersLevel0[(int)CascadeCorner.bottomright_far] = new Vector4(x2InView, -y2InView, cascadeNear, 1);
        frustrumCornersLevel0[(int)CascadeCorner.topright_far] = new Vector4(x2InView, y2InView, cascadeNear, 1);

        frustrumCornersLevel1[(int)CascadeCorner.bottomleft] = new Vector4(-x2InView, -y2InView, cascadeNear, 1);
        frustrumCornersLevel1[(int)CascadeCorner.topleft] = new Vector4(-x2InView, y2InView, cascadeNear, 1);
        frustrumCornersLevel1[(int)CascadeCorner.bottomright] = new Vector4(x2InView, -y2InView, cascadeNear, 1);
        frustrumCornersLevel1[(int)CascadeCorner.topright] = new Vector4(x2InView, y2InView, cascadeNear, 1);

        frustrumCornersLevel1[(int)CascadeCorner.bottomleft_far] = new Vector4(-x3InView, -y3InView, cascadeFar, 1);
        frustrumCornersLevel1[(int)CascadeCorner.topleft_far] = new Vector4(-x3InView, y3InView, cascadeFar, 1);
        frustrumCornersLevel1[(int)CascadeCorner.bottomright_far] = new Vector4(x3InView, -y3InView, cascadeFar, 1);
        frustrumCornersLevel1[(int)CascadeCorner.topright_far] = new Vector4(x3InView, y3InView, cascadeFar, 1);
    }

    private void CaculateCascadeBoundsInLight()
    {
        if (mainCamera == null)
            return;
        Matrix4x4 matViewToLightLevel0 = mCameraClear.worldToCameraMatrix * mainCamera.localToWorldMatrix;
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        NativeArray<Vector4> frustumCornerInLight_level0 = new NativeArray<Vector4>(8, Allocator.Temp);
        
        for (int i = 0; i < 8; ++i)
        {
            frustumCornerInLight_level0[i] = matViewToLightLevel0.MultiplyPoint(frustrumCornersLevel0[i]);
        }

        for (int i = 0; i < 8; ++i)
        {
            min = Vector3.Min(min, frustumCornerInLight_level0[i]);
            max = Vector3.Max(max, frustumCornerInLight_level0[i]);
        }
        

        float width = max.x - min.x;
        float height = max.y - min.y;
        //float depth = Mathf.Max(Mathf.Abs(maxZ), Mathf.Abs(minZ));

        //mCameraClear.rect = new Rect(0, 0, 1.0f, height / width);
        //shadowmapUpdateCount++;
        if (IsCameraCanMove(min.x, max.x, min.y, max.y, shadowMapTexelSizeClear.x, shadowMapTexelSizeClear.y))
        {
            Vector4 maxBound = new Vector4(max.x, max.y, max.z, 1);
            Vector4 minBound = new Vector4(min.x, min.y, min.z, 1);
            maxBound = mCameraClear.cameraToWorldMatrix * maxBound;
            minBound = mCameraClear.cameraToWorldMatrix * minBound;
            mCameraClear.farClipPlane = 100.0f;
            Vector3 lookAt = (maxBound + minBound) * 0.5f;
            //float distance = 70.0f;
            //mCameraClear.transform.position = lookAt - (mCameraClear.transform.forward * ((maxZ - minZ) * 0.5f - mCameraClear.nearClipPlane) * 5.0f);

            float distance = 100.0f;
            mCameraClear.farClipPlane = 200;
            mCameraClear.transform.position = lookAt - mCamera.transform.forward * distance;

            mCameraClear.orthographicSize = height * 0.5f;
            mCameraClear.aspect = width / height;
            //shadowmapUpdateCount = 0;
        }
        else
        {
            //Debug.Log("Camera do not move!");
        }

#if UNITY_EDITOR

        if (debugMode)
        {
            Vector4[] frustumCornerInWorld_level0 = new Vector4[8];
            for (int i = 0; i < 8; ++i)
            {
                frustumCornerInWorld_level0[i] = mainCamera.localToWorldMatrix.MultiplyPoint(frustrumCornersLevel0[i]);
            }

            min = Vector3.positiveInfinity;
            max = Vector3.negativeInfinity;

            for (int i = 0; i < 8; ++i)
            {
                min = Vector3.Min(min, frustumCornerInWorld_level0[i]);
                max = Vector3.Max(max, frustumCornerInWorld_level0[i]);
            }

            RenderDebug.DrawDebugBound(min.x, max.x, min.y, max.y, min.z, max.z);
        }

#endif // UNITY_EDITOR

        NativeArray<Vector4> frustumCornerInLight_level1 = new NativeArray<Vector4>(8, Allocator.Temp);
        Matrix4x4 matViewToLightLevel1 = mCamera.worldToCameraMatrix * mainCamera.localToWorldMatrix;
        for (int i = 0; i < 8; ++i)
        {
            frustumCornerInLight_level1[i] = matViewToLightLevel1.MultiplyPoint(frustrumCornersLevel1[i]);
        }

        min = Vector3.positiveInfinity;
        max = Vector3.negativeInfinity;

        for (int i = 0; i < 8; ++i)
        {
            min = Vector3.Min(min, frustumCornerInLight_level1[i]);
            max = Vector3.Max(max, frustumCornerInLight_level1[i]);
        }
        //maxX = Mathf.Abs(maxX);
        //maxY = Mathf.Abs(maxY);
        //shadowmapUpdateCountFar++;
        //if (IsCameraCanMove(min.x, max.x, min.y, max.y, shadowMapTexelSize.x, shadowMapTexelSize.y)/* && shadowmapUpdateCountFar > 60*/)
        {
            Vector4 maxBound = new Vector4(max.x, max.y, max.z, 1);
            Vector4 minBound = new Vector4(min.x, min.y, min.z, 1);
            maxBound = mCamera.cameraToWorldMatrix * maxBound;
            minBound = mCamera.cameraToWorldMatrix * minBound;

            Vector3 lookAt = (maxBound + minBound) * 0.5f;
            float distance = (max.z - min.z) * 0.5f - mCamera.nearClipPlane;
            if (distance < 70)
            {
                distance = 100;
            }
            //mCamera.farClipPlane = 140;//mCamera.nearClipPlane + maxZ - minZ; 
            distance = 100;
            mCamera.farClipPlane = 200;
            mCamera.transform.position = lookAt - mCamera.transform.forward * distance;

            width = max.x - min.x;
            height = max.y - min.y;
            mCamera.orthographicSize = height * 0.5f;
            mCamera.aspect = width / height;
            //shadowmapUpdateCountFar = 0;
        }


        //if (maxX > maxY)
        //{
        //    mCamera.rect = new Rect(0, 0, 1.0f, height / width);
        //}
        //else
        //{
        //    mCamera.rect = new Rect(0, 0, maxX / maxY, 1.0f);
        //}


#if UNITY_EDITOR

        if (debugMode)
        {
            Vector4[] frustumCornerInWorld_level1 = new Vector4[8];
            for (int i = 0; i < 8; ++i)
            {
                frustumCornerInWorld_level1[i] = mainCamera.localToWorldMatrix.MultiplyPoint(frustrumCornersLevel1[i]);
            }

            min = Vector3.positiveInfinity;
            max = Vector3.negativeInfinity;

            for (int i = 0; i < 8; ++i)
            {
                min = Vector3.Min(min, frustumCornerInWorld_level1[i]);
                max = Vector3.Max(max, frustumCornerInWorld_level1[i]);
            }

            RenderDebug.DrawDebugBound(min.x, max.x, min.y, max.y, min.z, max.z);
        }

#endif // UNITY_EDITOR

        frustumCornerInLight_level0.Dispose();
        frustumCornerInLight_level1.Dispose();
    }

    /*
#if UNITY_EDITOR

    private void DrawDebugBound(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
    {
        Debug.DrawLine(new Vector3(minX, minY, minZ), new Vector3(minX, minY, maxZ));
        Debug.DrawLine(new Vector3(minX, minY, minZ), new Vector3(maxX, minY, minZ));
        Debug.DrawLine(new Vector3(minX, minY, minZ), new Vector3(minX, maxY, minZ));
        Debug.DrawLine(new Vector3(maxX, maxY, maxZ), new Vector3(maxX, maxY, minZ));
        Debug.DrawLine(new Vector3(maxX, maxY, maxZ), new Vector3(minX, maxY, maxZ));
        Debug.DrawLine(new Vector3(maxX, maxY, maxZ), new Vector3(maxX, minY, maxZ));

        Debug.DrawLine(new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ));
        Debug.DrawLine(new Vector3(minX, maxY, minZ), new Vector3(minX, maxY, maxZ));

        Debug.DrawLine(new Vector3(maxX, minY, minZ), new Vector3(maxX, maxY, minZ));

        Debug.DrawLine(new Vector3(minX, maxY, maxZ), new Vector3(minX, maxY, minZ));
        Debug.DrawLine(new Vector3(minX, maxY, maxZ), new Vector3(minX, minY, maxZ));

        Debug.DrawLine(new Vector3(maxX, minY, minZ), new Vector3(maxX, minY, maxZ));
        Debug.DrawLine(new Vector3(minX, minY, maxZ), new Vector3(maxX, minY, maxZ));
    }

#endif // UNITY_EDITOR
    */

    bool IsCameraCanMove(float minX, float maxX, float minY, float maxY, float shadowMapTexelWidth, float shadowMapTexelHeight)
    {
        float xCenter = (minX + maxX) * 0.5f;
        float yCenter = (minY + maxY) * 0.5f;
        float width = maxX - minX;
        float height = maxY - minY;
        if (Mathf.Abs(xCenter) / width > shadowMapTexelWidth * 5.0f)
        {
            return true;
        }
        if (Mathf.Abs(yCenter) / height > shadowMapTexelHeight * 5.0f)
        {
            return true;
        }
        return false;
    }
}
