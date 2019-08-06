using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class RenderToShadow : MonoBehaviour {

    private Camera mCamera;
    //public Matrix4x4 matTest;
    private Matrix4x4 mLightProjMatrix;     //camera view matrix X camera projmatrix
    private RenderTexture mDepthTexture;
    private Material mRenderToShadowMtl;
    private GameObject mPlane;
    private Matrix4x4 mMatR = Matrix4x4.identity;

    private int lightMatrixID = -1;
    private int shadowmapTexelID = -1;
    private Vector4 shadowMapTexelSize;
    private static readonly int shadowMapSize = 512;
    //private GameObject mGameObject;
    // Use this for initialization
    void Start () {
        mPlane = GameObject.Find("Plane");

        mMatR.m00 = 0.5f;
        mMatR.m11 = 0.5f;
        mMatR.m22 = 1;// 0.5f;
        mMatR.m03 = 0.5f;
        mMatR.m13 = 0.5f;
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2 || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
            mMatR.m23 = 0;// 0.5f;
        else
        {
            mMatR.m23 = 0.5f;
        }
        mMatR.m33 = 1;

        //matTest = mPlane.transform.worldToLocalMatrix;
        lightMatrixID = Shader.PropertyToID("LightProjectionMatrix");
        shadowmapTexelID = Shader.PropertyToID("shadowMapTexel");
        shadowMapTexelSize = new Vector4(1.0f / shadowMapSize, 1.0f / shadowMapSize, 0, 0);
    }
	
	// Update is called once per frame
	void Update () {
        if (mDepthTexture != null)
        {
            if (lightMatrixID < 0)
            {
                lightMatrixID = Shader.PropertyToID("LightProjectionMatrix");
            }

            if (shadowmapTexelID < 0)
            {
                shadowmapTexelID = Shader.PropertyToID("shadowMapTexel");
            }

            if (lightMatrixID >= 0)
                Shader.SetGlobalMatrix(lightMatrixID, mLightProjMatrix);

            if (shadowmapTexelID >= 0)
                Shader.SetGlobalVector(shadowmapTexelID, shadowMapTexelSize);
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
        //if (mCamera == null)
        {
            mCamera = GetComponent<Camera>();
            if (mCamera == null)
            {
                mCamera = GetComponentInChildren<Camera>();
            }

            if (mCamera == null)
            {
                return;
            }
            //mCamera.cullingMask = 1 << LayerMask.NameToLayer("RenderToShadow");
            mCamera.clearFlags = CameraClearFlags.SolidColor;
            mCamera.backgroundColor = Color.white;//new Color(1, 1, 1, 1);
            mCamera.depth = 0;
            mCamera.orthographic = true;
            
            //mCamera.orthographicSize = 0.5f;

            if (mDepthTexture == null)
            {
                //mDepthTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.Depth);
                mDepthTexture = new RenderTexture(shadowMapSize, shadowMapSize, 24/*, RenderTextureFormat.Depth*/);
                mDepthTexture.wrapMode = TextureWrapMode.Clamp;
                mDepthTexture.filterMode = FilterMode.Bilinear;
                mDepthTexture.autoGenerateMips = false;
                mDepthTexture.useMipMap = false;
                mDepthTexture.mipMapBias = 0.0f;
            }
            mCamera.targetTexture = mDepthTexture;
            Shader shader = Shader.Find("liangairan/shadow/RenderToShadow");
            mCamera.SetReplacementShader(shader, "Shadow");
            //Shader.SetGlobalTexture("_ShadowmapTex", mDepthTexture);
            if (mPlane != null)
                mPlane.GetComponent<Renderer>().sharedMaterial.SetTexture("_ShadowmapTex", mDepthTexture);

            //gameObject.GetComponent<Renderer>().sharedMaterial.SetTexture("_ShadowmapTex", mDepthTexture);
        }

        mMatR.m00 = 0.5f;
        mMatR.m11 = 0.5f;
        mMatR.m22 = 0.5f;
        mMatR.m03 = 0.5f;
        mMatR.m13 = 0.5f;
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2 || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
            mMatR.m23 = 0.5f;// 0.5f;
        else
        {
            mMatR.m23 = 0.5f;
        }
        mMatR.m33 = 1;

        Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(mCamera.projectionMatrix, false);
        //Matrix4x4 projectionMatrix = mCamera.projectionMatrix;
        mLightProjMatrix = mMatR * projectionMatrix * mCamera.worldToCameraMatrix;

        //Matrix4x4 mat = mPlane.transform.localToWorldMatrix;
        //mat = mat * mLightProjMatrix;
        //Vector4 pos = new Vector4(0, 0, 0, 1);
        //pos = pos * mat;

        mCamera.Render();
    }

    private void RenderToShadowmap()
    {
        //mCamera.TryGetCullingParameters();
    }

    private void OnDestroy()
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

        if (mRenderToShadowMtl != null)
        {
            Object.Destroy(mRenderToShadowMtl);
            mRenderToShadowMtl = null;
        }
    }
}
