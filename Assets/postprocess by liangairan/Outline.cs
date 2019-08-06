using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
[ExecuteInEditMode]
public class Outline : MonoBehaviour {

    Material mOutlineMaterial = null;
    Camera mCamera;
    CommandBuffer mOutlineCommand;

    public List<Transform> mOutlineObjects = new List<Transform>();
    public float mOutlineFactor = 0.2f;
    public Color mOutlineColor = Color.green;
    bool mCommandBufferDirty = false;
    private RenderTexture mRT;
    private Material mImageAddMaterial;
	// Use this for initialization
	void Start () {
        mOutlineCommand = new CommandBuffer();
        mCommandBufferDirty = true;
    }

    private void OnEnable()
    {
        if (mOutlineMaterial == null)
        {
            mOutlineMaterial = new Material(Shader.Find("liangairan/postprocess/outline"));
        }
        
        if (mCamera == null)
        {
            mCamera = GetComponent<Camera>();
        }
        
        //renderer.materials.f
    }

    private void OnDisable()
    {
        if (mCamera != null && mOutlineCommand != null)
            mCamera.RemoveCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, mOutlineCommand);
        mCommandBufferDirty = true;
    }

    private void OnDestroy()
    {
        if (mOutlineMaterial != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(mOutlineMaterial);
#else
            Object.Destroy(mOutlineMaterial);
#endif
            mOutlineMaterial = null;
        }

        mOutlineObjects.Clear();
        if (mOutlineCommand != null)
        {
            mOutlineCommand.Clear();
            mOutlineCommand = null;
        }

        if (mRT != null)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(mRT);
#else
            Object.Destroy(mRT);
#endif
            mRT = null;
        }
    }

    // Update is called once per frame
    void Update ()
    {
        if (mOutlineMaterial != null)
        {
            mOutlineMaterial.SetFloat("_NormalScale", mOutlineFactor);
            mOutlineMaterial.SetColor("_OutlineColor", mOutlineColor);
        }
		if (mCommandBufferDirty)
        {
            UpdateCommandBuffer();
        }
	}

    void UpdateCommandBuffer()
    {
        if (mOutlineCommand == null)
        {
            mOutlineCommand = new CommandBuffer();
        }
        mCamera.RemoveCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, mOutlineCommand);

        mOutlineCommand.Clear();
        if (mRT == null)
        {
            mRT = new RenderTexture(Screen.width, Screen.height, 0);
        }
        else
        {
            if (mRT.width != Screen.width || mRT.height != Screen.height)
            {
                Object.DestroyImmediate(mRT);
                mRT = new RenderTexture(Screen.width, Screen.height, 0);
            }
        }
        RenderTargetIdentifier renderTargetIdentifier = new RenderTargetIdentifier(mRT);
        
        mOutlineCommand.SetRenderTarget(renderTargetIdentifier, 0);
        mOutlineCommand.ClearRenderTarget(false, true, new Color(0, 0, 0, 0));

        for (int i = 0; i < mOutlineObjects.Count; ++i)
        {
            Renderer renderer = mOutlineObjects[i].gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int j = 0; j < materials.Length; ++j)
                {
                    mOutlineCommand.DrawRenderer(renderer, mOutlineMaterial, j);
                }
            }

            Renderer[] childRenderers = mOutlineObjects[i].gameObject.GetComponentsInChildren<Renderer>();
            for (int j = 0; j < childRenderers.Length; ++j)
            {
                Material[] materials = childRenderers[j].sharedMaterials;
                for (int k = 0; k < materials.Length; ++k)
                {
                    mOutlineCommand.DrawRenderer(childRenderers[j], mOutlineMaterial, k);
                }
            }
        }

        mCamera.AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, mOutlineCommand);
        mCommandBufferDirty = false;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (mRT != null)
        {
            if (mImageAddMaterial == null)
            {
                Shader shader = Shader.Find("liangairan/postprocess/ImageAlphaLerp");
                mImageAddMaterial = new Material(shader);
                //mImageAddMaterial.SetTexture("_SourceTex", source);
                mImageAddMaterial.SetTexture("_OutlineTex", mRT);
            }

            
            Graphics.Blit(source, destination, mImageAddMaterial);
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }

    public void AddOutlineObject(Transform transform)
    {
        if (!mOutlineObjects.Contains(transform))
        {
            mOutlineObjects.Add(transform);
            mCommandBufferDirty = true;
        }
    }

    public void RemoveOutlineObject(Transform transform)
    {
        if (mOutlineObjects.Contains(transform))
        {
            mOutlineObjects.Remove(transform);
            mCommandBufferDirty = true;
        }
    }
}

public class OutlineManager
{
    public Outline mOutline = null;
    private static OutlineManager s_instance = null;
    public List<Transform> mTransformCaches = new List<Transform>();

    private OutlineManager()
    {

    }

    public static OutlineManager GetInstance()
    {
        if (s_instance == null)
        {
            s_instance = new OutlineManager();
        }

        return s_instance;
    }

    public void AddOutlineObject(Transform transform)
    {
        if (mOutline == null)
        {
            GameObject mainCamera = GameObject.Find("Main Camera");
            if (mainCamera != null)
            {
                mOutline = mainCamera.GetComponent<Outline>();
            }
        }

        if (mOutline != null)
        {
            mOutline.AddOutlineObject(transform);
        }
        else
        {
            mTransformCaches.Add(transform);
        }
    }

    public void RemoveOutlineObject(Transform transform)
    {
        if (mOutline != null)
        {
            mOutline.RemoveOutlineObject(transform);
        }
    }
}
