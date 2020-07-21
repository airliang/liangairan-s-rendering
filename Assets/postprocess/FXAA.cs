using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class FXAA : MonoBehaviour
{
    [Range(0.0312f, 0.0833f)]
    public float contrastThreshold = 0.0312f;
    [Range(0.063f, 0.333f)]
    public float relativeThreshold = 0.063f;
    [Range(0f, 1f)]
    public float subpixelBlending = 1f;

    public bool luminanceGreen = true;

    Material m_FXAAMat = null;
    const int luminancePass = 0;
    const int fxaaPass = 1;
    // Start is called before the first frame update
    void Start()
    {
        if (m_FXAAMat == null)
        {
            m_FXAAMat = new Material(Shader.Find("liangairan/postprocess/FXAA"));
            m_FXAAMat.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        if (m_FXAAMat != null)
        {
            DestroyImmediate(m_FXAAMat);
        }
        m_FXAAMat = null;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (m_FXAAMat != null)
        {
            m_FXAAMat.SetFloat("_ContrastThreshold", contrastThreshold);
            m_FXAAMat.SetFloat("_RelativeThreshold", relativeThreshold);
            m_FXAAMat.SetFloat("_SubpixelBlending", subpixelBlending);

            if (luminanceGreen)
            {
                m_FXAAMat.EnableKeyword("LUMINANCE_GREEN");
            }
            else
            {
                m_FXAAMat.DisableKeyword("LUMINANCE_GREEN");
            }

            Graphics.Blit(source, destination, m_FXAAMat, fxaaPass);
        }
    }
}
