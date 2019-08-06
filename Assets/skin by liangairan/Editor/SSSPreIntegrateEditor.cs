using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
public class SSSPreIntegrateEditor : EditorWindow
{
    static SSSPreIntegrateEditor _windowInstance;

    Material mPrefilterSSSLutMaterial;
    Material mBlurNormalmapMaterial;
    Texture2D mSourceNormalMap;
    string mOutputPath;
    Camera mCamera;
    GameObject mCameraObject;
    bool genSSSLutByCPU = false;

    [Range(0.25f, 5.0f)]
    public float blurSize = 0.5f;

    [Range(0.0f, 2.5f)]
    public float intensity = 0.75f;
    [Range(1, 4)]
    public int blurIterator = 1;

    [MenuItem("Tools/PreIntegrate SSS Skin", false, 0)]
    static void ShowIBLWindow()
    {
        if (_windowInstance == null)
        {
            _windowInstance = EditorWindow.GetWindow(typeof(SSSPreIntegrateEditor), true, "PreIntegrate SSS Skin editor") as SSSPreIntegrateEditor;
            //SceneView.onSceneGUIDelegate += OnSceneGUI;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("output path：");
        EditorGUILayout.EndHorizontal();
        Scene scene = SceneManager.GetActiveScene();
        mOutputPath = scene.path;
        int lastSlash = mOutputPath.LastIndexOf("/");
        if (lastSlash >= 0)
        {
            mOutputPath = mOutputPath.Substring(0, lastSlash + 1);
        }
        EditorGUILayout.BeginHorizontal();
        mOutputPath = EditorGUILayout.TextField(mOutputPath);

        EditorGUILayout.EndHorizontal();


        EditorGUILayout.BeginHorizontal();
        //EditorGUILayout.LabelField("Generate SSS Lut map by CPU?");
        genSSSLutByCPU = EditorGUILayout.Toggle("Generate by CPU?", genSSSLutByCPU);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        //GUILayout.Label("Render to cubemap");
        if (GUILayout.Button("Gen SSS Lut"))
        {
            PrefilterSSSLut();

        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Blur a Normal map");
        
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        mSourceNormalMap = EditorGUILayout.ObjectField(mSourceNormalMap, typeof(Texture2D), false) as Texture2D;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("blurIterator");
        blurIterator = EditorGUILayout.IntSlider(blurIterator, 1, 5);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("blurSize");
        blurSize = EditorGUILayout.Slider(blurSize, 0.25f, 5.0f);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("intensity");
        intensity = EditorGUILayout.Slider(intensity, 0, 2.5f);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        //GUILayout.Label("Render to cubemap");
        if (GUILayout.Button("blur normalmap"))
        {
            if (mSourceNormalMap != null)
                BlurNormalMap();

        }
        EditorGUILayout.EndHorizontal();
    }

    void PrefilterSSSLut()
    {
        if (File.Exists(mOutputPath + "sssLUT.png"))
        {
            return;
        }

        if (genSSSLutByCPU)
        {
            GenSSSLutByCPU();
            return;
        }

        if (mCameraObject == null)
        {
            mCameraObject = new GameObject("RenderToCubemap");
            mCameraObject.transform.position = Vector3.zero;
            mCameraObject.transform.rotation = Quaternion.identity;
        }

        mCamera = mCameraObject.GetComponent<Camera>();
        if (mCamera == null)
        {
            mCamera = mCameraObject.AddComponent<Camera>();
            mCamera.clearFlags = CameraClearFlags.SolidColor;
            mCamera.backgroundColor = Color.black;
        }
        //mCamera.targetTexture = mCubeMapSpecular;
        mCamera.cullingMask = 1 << LayerMask.NameToLayer("RenderToCubemap");

        RenderTexture mSSSLutMap = new RenderTexture(512, 512, 0, RenderTextureFormat.RGHalf);


        if (mPrefilterSSSLutMaterial == null)
        {
            Shader shader = Shader.Find("liangairan/sss/sss_lut");
            mPrefilterSSSLutMaterial = new Material(shader);
        }


        FullScreenQuad mQuad = new FullScreenQuad();
        GameObject quadObj = mQuad.GetGameObject();
        quadObj.GetComponent<MeshRenderer>().sharedMaterial = mPrefilterSSSLutMaterial;
        quadObj.layer = LayerMask.NameToLayer("RenderToCubemap");
        quadObj.transform.position = mCamera.transform.position + mCamera.transform.forward * 5.0f;


        mCamera.targetTexture = mSSSLutMap;
        mCamera.Render();
        RenderTexture.active = mSSSLutMap;
        Texture2D brdfLUT = new Texture2D(mSSSLutMap.width, mSSSLutMap.height, TextureFormat.RG16, false);
        brdfLUT.ReadPixels(new Rect(0, 0, mSSSLutMap.width, mSSSLutMap.height), 0, 0);
        brdfLUT.Apply();
        byte[] bytes = brdfLUT.EncodeToPNG();
        //AssetDatabase.CreateAsset(brdfLUT, Application.dataPath + "/pbr by liangairan/brdfLUT.png");
        File.WriteAllBytes(mOutputPath + "sssLUT.png", bytes);
        DestroyImmediate(brdfLUT);
        brdfLUT = null;
        mCamera.targetTexture = null;
        DestroyImmediate(mSSSLutMap);
        mQuad.Clear();
        mQuad = null;

        AssetDatabase.Refresh();

        TextureImporter textureImporter = AssetImporter.GetAtPath(mOutputPath + "sssLUT.png") as TextureImporter;
        textureImporter.textureType = TextureImporterType.Default;
        textureImporter.mipmapEnabled = false;
        textureImporter.sRGBTexture = true;
        textureImporter.filterMode = FilterMode.Bilinear;
        textureImporter.wrapMode = TextureWrapMode.Clamp;
        textureImporter.maxTextureSize = Mathf.Max(512, 512);
        AssetDatabase.ImportAsset(mOutputPath + "sssLUT.png", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    float PHBeckmann(float ndoth, float m)
    {
        float alpha = Mathf.Acos(ndoth);
        float ta = Mathf.Tan(alpha);
        float val = 1f / (m * m * Mathf.Pow(ndoth, 4f)) * Mathf.Exp(-(ta * ta) / (m * m));
        return val;
    }

    Vector3 IntegrateDiffuseScatteringOnRing(float cosTheta, float skinRadius)
    {
        // Angle from lighting direction
        float theta = Mathf.Acos(cosTheta);
        Vector3 totalWeights = Vector3.zero;
        Vector3 totalLight = Vector3.zero;

        float a = -(Mathf.PI / 2.0f);

        const float inc = 0.05f;

        while (a <= (Mathf.PI / 2.0f))
  {
            float sampleAngle = theta + a;
            float diffuse = Mathf.Clamp01(Mathf.Cos(sampleAngle));

            // Distance
            float sampleDist = Mathf.Abs(2.0f * skinRadius * Mathf.Sin(a * 0.5f));

            // Profile Weight
            Vector3 weights = Scatter(sampleDist);

            totalWeights += weights;
            totalLight += diffuse * weights;
            a += inc;
        }

        Vector3 result = new Vector3(totalLight.x / totalWeights.x, totalLight.y / totalWeights.y, totalLight.z / totalWeights.z);
        return result;
    }

    float Gaussian(float v, float r)
    {
        return 1.0f / Mathf.Sqrt(2.0f * Mathf.PI * v) * Mathf.Exp(-(r * r) / (2 * v));
    }

    Vector3 Scatter(float r)
    {
        // Values from GPU Gems 3 "Advanced Skin Rendering"
        // Originally taken from real life samples
        return Gaussian(0.0064f * 1.414f, r) * new Vector3(0.233f, 0.455f, 0.649f)
         + Gaussian(0.0484f * 1.414f, r) * new Vector3(0.100f, 0.336f, 0.344f)
         + Gaussian(0.1870f * 1.414f, r) * new Vector3(0.118f, 0.198f, 0.000f)
         + Gaussian(0.5670f * 1.414f, r) * new Vector3(0.113f, 0.007f, 0.007f)
         + Gaussian(1.9900f * 1.414f, r) * new Vector3(0.358f, 0.004f, 0.00001f)
         + Gaussian(7.4100f * 1.414f, r) * new Vector3(0.078f, 0.00001f, 0.00001f);
    }

    void GenSSSLutByCPU()
    {
        // Diffuse Scattering
        int width = 512;
        int height = 512;
        Texture2D diffuseScattering = new Texture2D(width, height, TextureFormat.ARGB32, false);
        for (int j = 0; j < height; ++j)
        {
            for (int i = 0; i < width; ++i)
            {
                // Lookup by:
                // x: NDotL
                // y: 1 / r
                float y = 2.0f * 1f / ((j + 1) / (float)height);
                Vector3 val = IntegrateDiffuseScatteringOnRing(Mathf.Lerp(-1f, 1f, i / (float)width), y);
                diffuseScattering.SetPixel(i, j, new Color(val.x, val.y, val.z, 1f));
            }
        }
        diffuseScattering.Apply();

        byte[] bytes = diffuseScattering.EncodeToPNG();
        DestroyImmediate(diffuseScattering);
        File.WriteAllBytes(mOutputPath + "SSSLut.png", bytes);
    }

    void BlurNormalMap()
    {
        /*
        if (mCameraObject == null)
        {
            mCameraObject = new GameObject("RenderToCubemap");
            mCameraObject.transform.position = Vector3.zero;
            mCameraObject.transform.rotation = Quaternion.identity;
        }

        mCamera = mCameraObject.GetComponent<Camera>();
        if (mCamera == null)
        {
            mCamera = mCameraObject.AddComponent<Camera>();
            mCamera.clearFlags = CameraClearFlags.SolidColor;
            mCamera.backgroundColor = Color.black;
        }
        //mCamera.targetTexture = mCubeMapSpecular;
        mCamera.cullingMask = 1 << LayerMask.NameToLayer("RenderToCubemap");
        */

        if (mBlurNormalmapMaterial == null)
        {
            Shader shader = Shader.Find("liangairan/postprocess/Blur");
            mBlurNormalmapMaterial = new Material(shader);
        }

        RenderTexture rt = RenderTexture.GetTemporary(mSourceNormalMap.width >> 1, mSourceNormalMap.height >> 1, 0, RenderTextureFormat.ARGB32);

        rt.filterMode = FilterMode.Bilinear;
        //temp2.filterMode = FilterMode.Bilinear;
        //直接将场景图拷贝到低分辨率的RT上达到降分辨率的效果  
        Graphics.Blit(mSourceNormalMap, rt);

        for (int i = 0; i < blurIterator; ++i)
        {
            RenderTexture temp1 = RenderTexture.GetTemporary(mSourceNormalMap.width >> 1, mSourceNormalMap.height >> 1, 0, RenderTextureFormat.ARGB32);
            //RenderTexture temp2 = RenderTexture.GetTemporary(source.width >> downSample, source.height >> downSample, 0, source.format);
            //高斯模糊，两次模糊，横向纵向，使用pass1进行高斯模糊  
            mBlurNormalmapMaterial.SetVector("_offsets", new Vector4(0, blurSize * 0.5f + i * 1.0f, 0, 0));
            //_Material.SetFloatArray("weight", weights);
            Graphics.Blit(rt, temp1, mBlurNormalmapMaterial, 0);
            RenderTexture.ReleaseTemporary(rt);
            rt = temp1;

            mBlurNormalmapMaterial.SetVector("_offsets", new Vector4(blurSize * 0.5f + i * 1.0f, 0, 0, 0));
            temp1 = RenderTexture.GetTemporary(mSourceNormalMap.width >> 1, mSourceNormalMap.height >> 1, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(rt, temp1, mBlurNormalmapMaterial, 0);
            RenderTexture.ReleaseTemporary(rt);
            rt = temp1;
        }

        RenderTexture.active = rt;
        Texture2D blurNormalMap = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
        blurNormalMap.ReadPixels(new Rect(0, 0, rt.width, rt.width), 0, 0);
        blurNormalMap.Apply();
        
        byte[] bytes = blurNormalMap.EncodeToPNG();

        File.WriteAllBytes(mOutputPath + mSourceNormalMap.name + ".png", bytes);
        DestroyImmediate(blurNormalMap);
        blurNormalMap = null;

        RenderTexture.ReleaseTemporary(rt);

        AssetDatabase.Refresh();
    }

    private void OnDestroy()
    {
        if (mPrefilterSSSLutMaterial != null)
        {
            DestroyImmediate(mPrefilterSSSLutMaterial);
        }

        if (mBlurNormalmapMaterial != null)
        {
            DestroyImmediate(mBlurNormalmapMaterial);
        }

        if (mCamera != null)
        {
            mCamera.targetTexture = null;
        }

        if (mCameraObject != null)
        {
            Object.DestroyImmediate(mCameraObject);
            mCameraObject = null;
            
        }

        mCamera = null;
    }
}
#endif
