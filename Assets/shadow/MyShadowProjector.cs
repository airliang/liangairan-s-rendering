using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MyShadowProjector : MonoBehaviour {
    Projector mProjector;
    private Camera m_camera;
    private RenderTexture mShadowTexture;
    public Transform target;
    private CommandBuffer m_commandBuffer;
    private Material mRenderToShadowMaterial;
    // Use this for initialization
    void Start () {
        mProjector = GetComponent<Projector>();
        m_camera = GetComponentInChildren<Camera>();
        if (mShadowTexture == null)
        {
            mShadowTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            mShadowTexture.wrapMode = TextureWrapMode.Clamp;
            mShadowTexture.filterMode = FilterMode.Bilinear;
            mShadowTexture.autoGenerateMips = false;
            mShadowTexture.useMipMap = false;
            mShadowTexture.mipMapBias = 0.0f;
        }

        mProjector.material.SetTexture("_ShadowmapTex", mShadowTexture);
        m_camera.targetTexture = mShadowTexture;
        m_camera.cullingMask = 1 << LayerMask.NameToLayer("RenderToShadow");
        m_camera.clearFlags = CameraClearFlags.SolidColor;
        m_camera.backgroundColor = new Color(1, 1, 1, 1);
        m_camera.depth = 0;
        //m_camera.SetReplacementShader(shader, "RenderType");
        Shader shader = Shader.Find("liangairan/shadow/RenderToShadow");
        mRenderToShadowMaterial = new Material(shader);

        CreateCommandBuffer();

        if (m_camera != null && m_commandBuffer != null)
        {
            m_camera.AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, m_commandBuffer);
        }
    }
	
	// Update is called once per frame
	void Update () {

    }

    private void CreateCommandBuffer()
    {
        if (m_commandBuffer == null)
        {
            m_commandBuffer = new CommandBuffer();
            Renderer renderer = target.gameObject.GetComponent<Renderer>();
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; ++i)
            {
                m_commandBuffer.DrawRenderer(renderer, mRenderToShadowMaterial, i);
            }
        }
    }

    private void OnEnable()
    {
        
        if (m_camera != null && m_commandBuffer != null)
        {
            m_camera.AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, m_commandBuffer);
        }
    }

    private void OnDisable()
    {
        if (m_commandBuffer != null)
        {
            if (m_camera != null)
            {
                m_camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, m_commandBuffer);
            }
            m_commandBuffer.Clear();
            m_commandBuffer = null;
        }
    }

    private void OnValidate()
    {
        
    }

    private void OnPreCull()
    {
        if (m_camera != null)
        {
            int layer = 0;
            if (target != null)
            {
                layer = target.gameObject.layer;
                target.gameObject.layer = LayerMask.NameToLayer("RenderToShadow");
            }
            //m_camera.Render();

            if (target != null)
            {
                target.gameObject.layer = layer;
            }
        }
    }

    private void OnDestroy()
    {
        if (m_camera != null)
        {
            m_camera.targetTexture = null;
        }

        if (mShadowTexture != null)
        {
            DestroyImmediate(mShadowTexture);
            mShadowTexture = null;
        }

        if (mRenderToShadowMaterial != null)
        {
            Object.Destroy(mRenderToShadowMaterial);
            mRenderToShadowMaterial = null;
        }
    }
}
