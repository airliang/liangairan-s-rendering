using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[ExecuteInEditMode]
public class FFTWave : WaterWave
{


    public RenderTexture mh0;
    public RenderTexture mHeightTexture;
    public RenderTexture mGaussionRandom;
    public RenderTexture mButterflyTexture;
    public RenderTexture mPingpong0;
    public RenderTexture mPingpong1;

    public RenderTexture mPingpongChoppy0;
    public RenderTexture mPingpongChoppy1;
    public RenderTexture mNormalMap;

    private RenderTexture[] mPingpongs = new RenderTexture[2];
    private RenderTexture[] mPingpongChoppys = new RenderTexture[2];

    public Vector2 mWindDirection = new Vector2(1.0f, 1.0f);
    public float A = 1.5f;
    public float mWindSpeed = 10.0f;

    int mResolution = 256;
    float length = 0.5f;
    private float mPatchSize;

    public ComputeShader mComputeShader;

    public Color waterColor = new Color(9, 45, 103);
    public Color SkyColor = new Color(148, 245, 245);
    public Color foamColor = Color.white;
    public float mHeightScale = 1.0f;

    public Vector2 showTexturePos;
    public float screenTextureWidth;

    private ScreenQuad screenQuad;
    public Transform mMainCamera;

    void Start()
    {
        mPatchSize = mResolution * length;

        if (mComputeShader == null)
            mComputeShader = Resources.Load<ComputeShader>("FFTOcean");
        RunComputeShader();        
    }

    // Update is called once per frame
    void Update()
    {
        if (mComputeShader != null)
        {
            int kGenerateHeight = mComputeShader.FindKernel("GenerateHeight");

            mComputeShader.SetTexture(kGenerateHeight, "H0InputTexture", mh0);
            mComputeShader.SetTexture(kGenerateHeight, "HeightTexture", mPingpong0);   //这个时候pingpong0作为第一个输入
            mComputeShader.SetTexture(kGenerateHeight, "ChoppyTexture", mPingpongChoppy0);   //这个时候pingpong0作为第一个输入
            mComputeShader.SetFloat("time", Time.time);

            mComputeShader.Dispatch(kGenerateHeight, 256 / 8, 256 / 8, 1);


            int maxStage = (int)(Mathf.Log(256) / Mathf.Log(2));

            int kIFFTHorizontalHeight = mComputeShader.FindKernel("IFFTHorizontalHeight");
            mComputeShader.SetTexture(kIFFTHorizontalHeight, "ButterflyInput", mButterflyTexture);
            int inputPingPong = 0;
            int outputPingPong = 1;
            int pingpongIndex = 0;
            
            for (int i = 0; i < maxStage; ++i)
            {
                mComputeShader.SetInt("stage", i);
                mComputeShader.SetTexture(kIFFTHorizontalHeight, "PingpongInput", mPingpongs[inputPingPong]);
                mComputeShader.SetTexture(kIFFTHorizontalHeight, "PingpongOutput", mPingpongs[outputPingPong]);
                //mComputeShader.SetTexture(kIFFTHorizontalHeight, "PingpongChoppyInput", mPingpongChoppys[inputPingPong]);
                //mComputeShader.SetTexture(kIFFTHorizontalHeight, "PingpongChoppyOutput", mPingpongChoppys[outputPingPong]);
                pingpongIndex++;
                inputPingPong = pingpongIndex % 2;
                outputPingPong = (pingpongIndex + 1) % 2;

                mComputeShader.Dispatch(kIFFTHorizontalHeight, 256 / 8, 256 / 8, 1);
            }
            
            int kIFFTVerticalHeight = mComputeShader.FindKernel("IFFTVerticalHeight");
            mComputeShader.SetTexture(kIFFTVerticalHeight, "ButterflyInput", mButterflyTexture);
            for (int i = 0; i < maxStage; ++i)
            {
                mComputeShader.SetInt("stage", i);
                mComputeShader.SetTexture(kIFFTVerticalHeight, "PingpongInput", mPingpongs[inputPingPong]);
                mComputeShader.SetTexture(kIFFTVerticalHeight, "PingpongOutput", mPingpongs[outputPingPong]);
                //mComputeShader.SetTexture(kIFFTVerticalHeight, "PingpongChoppyInput", mPingpongChoppys[inputPingPong]);
                //mComputeShader.SetTexture(kIFFTVerticalHeight, "PingpongChoppyOutput", mPingpongChoppys[outputPingPong]);
                pingpongIndex++;
                inputPingPong = pingpongIndex % 2;
                outputPingPong = (pingpongIndex + 1) % 2;

                mComputeShader.Dispatch(kIFFTVerticalHeight, 256 / 8, 256 / 8, 1);
            }

            //处理choppy
            int inputPingPongChoppy = 0;
            int outputPingPongChoppy = 1;
            pingpongIndex = 0;

            int kIFFTHorizontalChoppy = mComputeShader.FindKernel("IFFTHorizontalChoppy");
            mComputeShader.SetTexture(kIFFTHorizontalChoppy, "ButterflyInput", mButterflyTexture);
            for (int i = 0; i < maxStage; ++i)
            {
                mComputeShader.SetInt("stage", i);
                mComputeShader.SetTexture(kIFFTHorizontalChoppy, "PingpongChoppyInput", mPingpongChoppys[inputPingPongChoppy]);
                mComputeShader.SetTexture(kIFFTHorizontalChoppy, "PingpongChoppyOutput", mPingpongChoppys[outputPingPongChoppy]);
                pingpongIndex++;
                inputPingPongChoppy = pingpongIndex % 2;
                outputPingPongChoppy = (pingpongIndex + 1) % 2;

                mComputeShader.Dispatch(kIFFTHorizontalChoppy, 256 / 8, 256 / 8, 1);
            }

            int kIFFTVerticalChoppy = mComputeShader.FindKernel("IFFTVerticalChoppy");
            mComputeShader.SetTexture(kIFFTVerticalChoppy, "ButterflyInput", mButterflyTexture);
            for (int i = 0; i < maxStage; ++i)
            {
                mComputeShader.SetInt("stage", i);
                mComputeShader.SetTexture(kIFFTVerticalChoppy, "PingpongChoppyInput", mPingpongChoppys[inputPingPongChoppy]);
                mComputeShader.SetTexture(kIFFTVerticalChoppy, "PingpongChoppyOutput", mPingpongChoppys[outputPingPongChoppy]);
                pingpongIndex++;
                inputPingPongChoppy = pingpongIndex % 2;
                outputPingPongChoppy = (pingpongIndex + 1) % 2;

                mComputeShader.Dispatch(kIFFTVerticalChoppy, 256 / 8, 256 / 8, 1);
            }


            int kFinalHeight = mComputeShader.FindKernel("FinalHeight");
            mComputeShader.SetTexture(kFinalHeight, "PingpongInput", mPingpongs[inputPingPong]);
            mComputeShader.SetTexture(kFinalHeight, "PingpongChoppyInput", mPingpongChoppys[inputPingPongChoppy]);
            mComputeShader.SetTexture(kFinalHeight, "HeightTexture", mHeightTexture);
            mComputeShader.SetFloat("heightScale", mHeightScale);
            mComputeShader.Dispatch(kFinalHeight, 256 / 8, 256 / 8, 1);

            int kNormalMap = mComputeShader.FindKernel("GenerateNormalMap");
            mComputeShader.SetTexture(kNormalMap, "DisplacementMap", mHeightTexture);
            mComputeShader.SetTexture(kNormalMap, "NormalMapTex", mNormalMap);

            mComputeShader.Dispatch(kNormalMap, 256 / 8, 256 / 8, 1);
        }
    }

    private void OnDestroy()
    {

        if (mh0 != null)
        {
            mh0.Release();
            DestroyImmediate(mh0);
            mh0 = null;
        }

        if (mHeightTexture != null)
        {
            mHeightTexture.Release();
            DestroyImmediate(mHeightTexture);
            mHeightTexture = null;
        }

        if (mGaussionRandom != null)
        {
            mGaussionRandom.Release();
            DestroyImmediate(mGaussionRandom);
            mGaussionRandom = null;
        }

        if (mButterflyTexture != null)
        {
            mButterflyTexture.Release();
            DestroyImmediate(mButterflyTexture);
            mButterflyTexture = null;
        }

        mPingpongs[0] = null;
        mPingpongs[1] = null;

        if (mPingpong0 != null)
        {
            mPingpong0.Release();
            DestroyImmediate(mPingpong0);
            mPingpong0 = null;
        }

        if (mPingpong1 == null)
        {
            mPingpong1.Release();
            DestroyImmediate(mPingpong1);
            mPingpong1 = null;
        }

        mPingpongChoppys[0] = null;
        mPingpongChoppys[1] = null;

        if (mPingpongChoppy0 != null)
        {
            mPingpongChoppy0.Release();
            DestroyImmediate(mPingpongChoppy0);
            mPingpongChoppy0 = null;
        }

        if (mPingpongChoppy1 == null)
        {
            mPingpongChoppy1.Release();
            DestroyImmediate(mPingpongChoppy1);
            mPingpongChoppy1 = null;
        }

        if (mComputeShader != null)
        {
            Resources.UnloadAsset(mComputeShader);
            mComputeShader = null;
            //Object.DestroyImmediate(mComputeShader);
            //mComputeShader = null;
        }
        
    }

    private void RunComputeShader()
    {
        if (mh0 != null)
        {
            mh0.Release();
            mh0.enableRandomWrite = true;
            mh0.Create();
        }
        if (mh0 == default)
        {
            mh0 = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat, 0);
            mh0.enableRandomWrite = true;
            mh0.Create();
        }

        if (mHeightTexture != null)
        {
            mHeightTexture.Release();
            mHeightTexture.enableRandomWrite = true;
            mHeightTexture.Create();
        }
        if (mHeightTexture == default)
        {
            mHeightTexture = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat, 0);
            mHeightTexture.enableRandomWrite = true;
            mHeightTexture.wrapMode = TextureWrapMode.Repeat;
            mHeightTexture.useMipMap = false;
            mHeightTexture.Create();
        }

        if (mGaussionRandom != null)
        {
            mGaussionRandom.Release();
            mGaussionRandom.enableRandomWrite = true;
            mGaussionRandom.Create();
        }
        if (mGaussionRandom == default)
        {
            mGaussionRandom = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat, 0);
            mGaussionRandom.enableRandomWrite = true;
            mGaussionRandom.Create();
        }

        if (mNormalMap != null)
        {
            mNormalMap.Release();
            mNormalMap.enableRandomWrite = true;
            mNormalMap.Create();
        }
        if (mNormalMap == default)
        {
            mNormalMap = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32, 0);
            mNormalMap.enableRandomWrite = true;
            mNormalMap.wrapMode = TextureWrapMode.Repeat;
            mNormalMap.useMipMap = false;
            mNormalMap.Create();
        }

        if (mComputeShader != null)
        {
            ComputeBuffer randomBuffer = new ComputeBuffer(256 * 256, 8);
            float[] randomArray = new float[256 * 256 * 2];
            for (int i = 0; i < 256 * 256 * 2; ++i)
            {
                randomArray[i] = Random.Range(0.00001f, 1.0f);
            }
            randomBuffer.SetData(randomArray);


            ComputeBuffer randomBuffer2 = new ComputeBuffer(256 * 256, 8);
            float[] randomArray2 = new float[256 * 256 * 2];
            for (int i = 0; i < 256 * 256 * 2; ++i)
            {
                randomArray2[i] = Random.Range(0.00001f, 1.0f);
            }
            randomBuffer2.SetData(randomArray2);

            int generateRandom = mComputeShader.FindKernel("GenerateGaussianMap");

            mComputeShader.SetBuffer(generateRandom, "randomData1", randomBuffer);
            mComputeShader.SetBuffer(generateRandom, "randomData2", randomBuffer2);
            mComputeShader.SetTexture(generateRandom, "GaussianRandom", mGaussionRandom);
            mComputeShader.Dispatch(generateRandom, 256 / 8, 256 / 8, 1);

            int generateHeight0 = mComputeShader.FindKernel("GenerateHeight0");
            mComputeShader.SetBuffer(generateHeight0, "randomData1", randomBuffer);
            mComputeShader.SetBuffer(generateHeight0, "randomData2", randomBuffer2);
            mComputeShader.SetTexture(generateHeight0, "H0Texture", mh0);
            //mComputeShader.SetTexture(generateHeight0, "GaussianRandom", mGaussionRandom);
            mComputeShader.SetInt("N", 256);
            mComputeShader.SetFloat("A", A);
            mComputeShader.SetVector("windDirection", mWindDirection);
            mComputeShader.SetFloat("windSpeed", mWindSpeed);
            mComputeShader.SetFloat("patchSize", mPatchSize);
            mComputeShader.Dispatch(generateHeight0, 256 / 8, 256 / 8, 1);
            randomBuffer.Release();
            randomBuffer2.Release();


            //生成butterfly纹理
            int widthButterfly = 0;
            if (mButterflyTexture != null)
            {
                mButterflyTexture.Release();
                mButterflyTexture.enableRandomWrite = true;
                mButterflyTexture.Create();
            }
            if (mButterflyTexture == null)
            {
                
                mButterflyTexture = new RenderTexture(widthButterfly, 256, 0, RenderTextureFormat.ARGBFloat, 0);
                mButterflyTexture.enableRandomWrite = true;
                mButterflyTexture.Create();
            }
            widthButterfly = (int)(Mathf.Log(256) / Mathf.Log(2));

            int generateButterfly = mComputeShader.FindKernel("GenerateButterfly");

            int[] test = new int[256];
            for (int i = 0; i < test.Length; ++i)
            {
                test[i] = i;
            }

            ReserveBit(test, 256);

            ComputeBuffer reserveBit = new ComputeBuffer(256, 4);
            reserveBit.SetData(test);
            mComputeShader.SetBuffer(generateButterfly, "reserveBit", reserveBit);
            mComputeShader.SetTexture(generateButterfly, "ButterflyTex", mButterflyTexture);
            mComputeShader.Dispatch(generateButterfly, widthButterfly, 256 / 8, 1);

            reserveBit.Release();
        }

        if (mPingpong0 != null)
        {
            mPingpong0.Release();
            mPingpong0.enableRandomWrite = true;
            mPingpong0.Create();
        }
        if (mPingpong0 == null)
        {
            mPingpong0 = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat, 0);
            mPingpong0.enableRandomWrite = true;
            mPingpong0.Create();
        }

        if (mPingpong1 != null)
        {
            mPingpong1.Release();
            mPingpong1.enableRandomWrite = true;
            mPingpong1.Create();
        }
        if (mPingpong1 == null)
        {
            mPingpong1 = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat, 0);
            mPingpong1.enableRandomWrite = true;
            mPingpong1.Create();
        }

        mPingpongs[0] = mPingpong0;
        mPingpongs[1] = mPingpong1;

        if (mPingpongChoppy0 != null)
        {
            mPingpongChoppy0.Release();
            mPingpongChoppy0.enableRandomWrite = true;
            mPingpongChoppy0.Create();
        }
        if (mPingpongChoppy0 == null)
        {
            mPingpongChoppy0 = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat, 0);
            mPingpongChoppy0.enableRandomWrite = true;
            mPingpongChoppy0.Create();
        }

        if (mPingpongChoppy1 != null)
        {
            mPingpongChoppy1.Release();
            mPingpongChoppy1.enableRandomWrite = true;
            mPingpongChoppy1.Create();
        }
        if (mPingpongChoppy1 == null)
        {
            mPingpongChoppy1 = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat, 0);
            mPingpongChoppy1.enableRandomWrite = true;
            mPingpongChoppy1.Create();
        }

        mPingpongChoppys[0] = mPingpongChoppy0;
        mPingpongChoppys[1] = mPingpongChoppy1;
    }

    void ReserveBit(int[] x, int N)
    {
        //float2 temp;
        int i = 0, j = 0, k = 0;
        int t;
        int temp = 0;
        for (i = 0; i < N; i++)
        {
            k = i; j = 0;
            t = (int)(Mathf.Log((float)N) / Mathf.Log(2.0f));
            while ((t--) > 0)    //利用按位与以及循环实现码位颠倒  
            {
                j = j << 1;
                j |= (k & 1);
                k = k >> 1;
            }
            if (j > i)    //将x(n)的码位互换  
            {
                temp = x[i];
                x[i] = x[j];
                x[j] = temp;
            }
        }
    }

    public void SaveRenderTexture(RenderTexture renderT, string fileName)
    {
        if (renderT == null)
            return;

        int width = renderT.width;
        int height = renderT.height;
        Texture2D tex2d = new Texture2D(width, height, TextureFormat.ARGB32, false);
        RenderTexture.active = renderT;
        tex2d.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex2d.Apply();

        byte[] b = tex2d.EncodeToTGA();
        Destroy(tex2d); 

        File.WriteAllBytes(Application.dataPath + "/" + fileName, b); 
    }

    public override void ApplyMaterial(Material waterMaterial)
    {
        waterMaterial.SetTexture("_HeightTex", mHeightTexture);
        waterMaterial.SetTexture("_NormalMap", mNormalMap);
    }
}
