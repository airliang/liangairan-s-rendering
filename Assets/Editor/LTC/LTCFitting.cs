using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.EditorCoroutines.Editor;
using System.IO;
using System.Runtime.InteropServices;
using System;
using System.Runtime.Serialization.Formatters.Binary;

public class FitLTC 
{
    IBrdf brdf;
    LTC ltc;
    bool isotropic = true;
    Vector3 V;
    float alpha;
    public FitLTC(LTC ltc_, IBrdf brdf_, bool isotropic_, Vector3 V_, float alpha_)
    {
        ltc = ltc_;
        brdf = brdf_;
        isotropic = isotropic_;
        V = V_;
        alpha = alpha_;
    }

    public void Update(float[] fparams)
	{

        float m00 = Mathf.Max(fparams[0], LTCFitting.MIN_ALPHA);
        float m11 = Mathf.Max(fparams[1], LTCFitting.MIN_ALPHA);
        float m02 = fparams[2];

		if(isotropic)
		{
			ltc.m00 = m00;
			ltc.m11 = m00;
			ltc.m02 = 0.0f;
		}

        else
        {
            ltc.m00 = m00;
            ltc.m11 = m11;
            ltc.m02 = m02;
        }
        ltc.Update();
    }

	float Callback(float[] fparams)
	{
        Update(fparams);
        return LTCFitting.ComputeError(ltc, brdf, V, alpha);
    }

    

    class Point
    {
        public float[] points = new float[4];
    }

    void mov(ref float[] r, float[] v, int dim)
    {
        for (int i = 0; i < dim; ++i)
            r[i] = v[i];
    }

    void set(ref float[] r, float v, int dim)
    {
        for (int i = 0; i<dim; ++i)
            r[i] = v;
    }

    void add(ref float[] r, float[] v, int dim)
    {
        for (int i = 0; i < dim; ++i)
            r[i] += v[i];
    }

    public float NelderMead(
        ref float[] pmin, float[] start, float delta, float tolerance, int maxIters)
    {
        int DIM = 3;
        
        // standard coefficients from Nelder-Mead
        float reflect = 1.0f;
        float expand = 2.0f;
        float contract = 0.5f;
        float shrink = 0.5f;

        int NB_POINTS = DIM + 1;

        Point[] s = new Point[NB_POINTS];
        for (int i = 0; i < NB_POINTS; ++i)
        {
            s[i] = new Point();
        }
        float[] f = new float[NB_POINTS];

        // initialise simplex
        mov(ref s[0].points, start, DIM);
        for (int i = 1; i < NB_POINTS; i++)
        {
            mov(ref s[i].points, start, DIM);
            s[i].points[i - 1] += delta;
        }

        // evaluate function at each point on simplex
        for (int i = 0; i < NB_POINTS; i++)
            f[i] = Callback(s[i].points);

        int lo = 0, hi, nh;

        for (int j = 0; j < maxIters; j++)
        {
            // find lowest, highest and next highest
            lo = hi = nh = 0;
            for (int i = 1; i < NB_POINTS; i++)
            {
                if (f[i] < f[lo])
                    lo = i;
                if (f[i] > f[hi])
                {
                    nh = hi;
                    hi = i;
                }
                else if (f[i] > f[nh])
                    nh = i;
            }

            // stop if we've reached the required tolerance level
            float a = Mathf.Abs(f[lo]);
            float b = Mathf.Abs(f[hi]);
            if (2.0f * Mathf.Abs(a - b) < (a + b) * tolerance)
                break;

            // compute centroid (excluding the worst point)
            Point o = new Point();
            set(ref o.points, 0.0f, DIM);
            for (int i = 0; i < NB_POINTS; i++)
            {
                if (i == hi) continue;
                add(ref o.points, s[i].points, DIM);
            }

            for (int i = 0; i < DIM; i++)
                o.points[i] /= DIM;

            // reflection
            Point r = new Point();
            for (int i = 0; i < DIM; i++)
                r.points[i] = o.points[i] + reflect * (o.points[i] - s[hi].points[i]);

            float fr = Callback(r.points);
            if (fr < f[nh])
            {
                if (fr < f[lo])
                {
                    // expansion
                    Point e = new Point();
                    for (int i = 0; i < DIM; i++)
                        e.points[i] = o.points[i] + expand * (o.points[i] - s[hi].points[i]);

                    float fe = Callback(e.points);
                    if (fe < fr)
                    {
                        mov(ref s[hi].points, e.points, DIM);
                        f[hi] = fe;
                        continue;
                    }
                }

                mov(ref s[hi].points, r.points, DIM);
                f[hi] = fr;
                continue;
            }

            // contraction
            Point c = new Point();
            for (int i = 0; i < DIM; i++)
                c.points[i] = o.points[i] - contract * (o.points[i] - s[hi].points[i]);

            float fc = Callback(c.points);
            if (fc < f[hi])
            {
                mov(ref s[hi].points, c.points, DIM);
                f[hi] = fc;
                continue;
            }

            // reduction
            for (int k = 0; k < NB_POINTS; k++)
            {
                if (k == lo) continue;
                for (int i = 0; i < DIM; i++)
                    s[k].points[i] = s[lo].points[i] + shrink * (s[k].points[i] - s[lo].points[i]);
                f[k] = Callback(s[k].points);
            }
        }

        // return best point and its value
        mov(ref pmin, s[lo].points, DIM);
        return f[lo];
    }
};


public class LTCFitting : EditorWindow
{
    static int N = 64;
    static int Nsample = 50;
    public static float MIN_ALPHA = 0.0001f;
    EditorCoroutine generateLTC = null;
    private bool tableGenerated = false;
    private bool tabSphereGenerated = false;
    float[] tabSphere;
    Matrix4x4[] tables;
    Vector2[] tabMagFresnel;
    [MenuItem("Tools/LTC Generate", false, 0)]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(LTCFitting));
    }

    void OnGUI()
    {
        if (GUILayout.Button("Generate LTC"))
        {
            tableGenerated = false;
            tabSphereGenerated = false;
            generateLTC = this.StartCoroutine(GenerateLTC());
        }

        if (GUILayout.Button("Stop Generate"))
        {
            if (generateLTC != null)
                this.StopCoroutine(generateLTC);
        }

        if (GUILayout.Button("Generate from files"))
        {
            GenerateLTCFromFiles();
        }
    }

    void OnDestroy()
    {
        generateLTC = null;
    }

    void GenerateLTCFromFiles()
    {
        Color[] tex1 = new Color[N * N];
        BinaryReader reader = new BinaryReader(File.Open("Assets/Editor/LTC/ltc_1.data", FileMode.Open));
        for (int i = 0; i < N * N; ++i)
        {
            tex1[i].r = reader.ReadSingle();
            tex1[i].g = reader.ReadSingle();
            tex1[i].b = reader.ReadSingle();
            tex1[i].a = reader.ReadSingle();
        }
        reader.Close();

        Color[] tex2 = new Color[N * N];
        reader = new BinaryReader(File.Open("Assets/Editor/LTC/ltc_2.data", FileMode.Open));
        for (int i = 0; i < N * N; ++i)
        {
            tex2[i].r = reader.ReadSingle();
            tex2[i].g = reader.ReadSingle();
            tex2[i].b = reader.ReadSingle();
            tex2[i].a = reader.ReadSingle();
        }
        reader.Close();

        Texture2D tab = new Texture2D(N, N, TextureFormat.RGBAHalf, false);
        Texture2D tabAmplitudes = new Texture2D(N, N, TextureFormat.RGBAHalf, false);
        tab.SetPixels(tex1);
        tabAmplitudes.SetPixels(tex2);

        byte[] bytes = tab.EncodeToPNG();
        File.WriteAllBytes("Assets/pbr by liangairan/textures/ltc_mat.tga", bytes);
        bytes = tabAmplitudes.EncodeToPNG();
        File.WriteAllBytes("Assets/pbr by liangairan/textures/ltc_mag.tga", bytes);
        DestroyImmediate(tab);
        DestroyImmediate(tabAmplitudes);

        //AssetDatabase.CreateAsset(tab, "Assets/pbr by liangairan/textures/ltc_mat.tga");
        //AssetDatabase.CreateAsset(tabAmplitudes, "Assets/pbr by liangairan/textures/ltc_mag.tga");
    }

    IEnumerator GenerateLTC()
    {
        tables = new Matrix4x4[N * N];
        tabMagFresnel = new Vector2[N * N];
        tabSphere = new float[N * N];

        Debug.Log("Starting FitTable......");
        yield return FitTable();

        Debug.Log("Starting GenSphereTab......");
        yield return GenSphereTab();

        yield return new WaitUntil(() => (tableGenerated && tabSphereGenerated));
        Color[] tex1 = new Color[N * N];
        Color[] tex2 = new Color[N * N];

        PackTab(ref tex1, ref tex2, tables, tabMagFresnel, tabSphere);

        Texture2D tab = new Texture2D(N, N, TextureFormat.RGBAHalf, false);
        Texture2D tabAmplitudes = new Texture2D(N, N, TextureFormat.RGBAHalf, false);
        tab.SetPixels(tex1);
        tabAmplitudes.SetPixels(tex2);
        /*
        for (int i = 0; i < N; ++i)
        {
            for (int j = 0; j < N; ++j)
            {
                int index = j + i * N;
                Matrix4x4 m = tables[index];
                Color color = new Color(m.m00, m.m02, m.m11, m.m20);
                tab.SetPixel(i, j, color);
                color.r = amplitudes[index].x;
                color.g = amplitudes[index].y;
                tabAmplitudes.SetPixel(i, j, color);
            }
            
        }
        */
        Debug.Log("Writing the texture to database......");
        AssetDatabase.CreateAsset(tab, "Assets/pbr by liangairan/textures/ltc_mat.tga");
        AssetDatabase.CreateAsset(tabAmplitudes, "Assets/pbr by liangairan/textures/ltc_mag.tga");

        yield return null;
    }

    IEnumerator FitTable(/*ref Matrix4x4[] tables, ref Vector2[] tabMagFresnel*/)
    {
        IBrdf brdf = new BrdfGGX();
        LTC ltc = new LTC();
        int num = 0;

        // loop over theta and alpha
        for (int a = N - 1; a >= 0; --a)
        {    
            for (int t = 0; t <= N - 1; ++t)
            {
                num++;
                float theta = Mathf.Min(1.57f, t / (N - 1) * 1.57079f);
                Vector3 V = new Vector3(Mathf.Sin(theta), 0, Mathf.Cos(theta));

                // alpha = roughness^2
                float roughness = a / (N - 1);
                float alpha = Mathf.Max(roughness * roughness, MIN_ALPHA);

                //ltc.magnitude = ComputeNorm(brdf, V, alpha);
                Vector3 averageDir = ComputeAvgTerms(brdf, V, alpha, ref ltc.magnitude, ref ltc.fresnel);
                bool isotropic = true;

                // 1. first guess for the fit, downhill simplex method(NelderMead need the first guest)
                // init the hemisphere in which the distribution is fitted
                // if theta == 0 the lobe is rotationally symmetric and aligned with Z = (0 0 1)
                if (t == 0)
                {
                    ltc.X = Vector3.right;
                    ltc.Y = Vector3.up;
                    ltc.Z = Vector3.forward;

                    if (a == N - 1) // roughness = 1
                    {
                        ltc.m00 = 1.0f;
                        ltc.m11 = 1.0f;
                    }
                    else // init with roughness of previous fit
                    {
                        ltc.m00 = Mathf.Max(tables[a + 1 + t * N].m00, MIN_ALPHA);
                        ltc.m11 = Mathf.Max(tables[a + 1 + t * N].m11, MIN_ALPHA);
                    }

                    ltc.m02 = 0;
                    
                    ltc.Update();

                    isotropic = true;
                }
                // otherwise use previous configuration as first guess
                else
                {
                    Vector3 L = Vector3.Normalize(averageDir);
                    Vector3 T1 = new Vector3(L.z,0,-L.x);
                    Vector3 T2 = Vector3.up;
                    ltc.X = T1;
                    ltc.Y = T2;
                    ltc.Z = L;

                    ltc.Update();

                    isotropic = false;
                }

                // 2. fit (explore parameter space and refine first guess)
                // use the downhill simplex of the target function computeError
                // minize the 
                float epsilon = 0.05f;
                Fit(ltc, brdf, V, alpha, epsilon, isotropic);

                // copy data
                tables[a + t * N] = ltc.M;
                tabMagFresnel[a + t * N].x = ltc.magnitude;
                tabMagFresnel[a + t * N].y = ltc.fresnel;

		        // kill useless coefs in matrix and normalize
		        tables[a + t * N].m01 = 0;
		        tables[a + t * N].m10 = 0;
		        tables[a + t * N].m21 = 0;
		        tables[a + t * N].m12 = 0;
                //Vector4 row0 = tables[a + t * N].GetRow(0) * (1.0f / tables[a + t * N].m22);
                //Vector4 row1 = tables[a + t * N].GetRow(1) * (1.0f / tables[a + t * N].m22);
                //Vector4 row2 = tables[a + t * N].GetRow(2) * (1.0f / tables[a + t * N].m22);
                //tables[a + t * N].SetRow(0, row0);
                //tables[a + t * N].SetRow(1, row1);
                //tables[a + t * N].SetRow(2, row2);
                Debug.Log("a=" + a + " t=" + t);
                yield return null;
            }
        }

        if (num == N * N)
        {
            tableGenerated = true;
        }
        yield return null;
    }

    static void Fit(LTC ltc, IBrdf brdf, Vector3 V, float alpha, float epsilon = 0.05f, bool isotropic = false)
    {

        float[] startFit = { ltc.m00, ltc.m11, ltc.m02 };

        float[] resultFit = new float[3];

        FitLTC fitter = new FitLTC(ltc, brdf, isotropic, V, alpha);

        // Find best-fit LTC lobe (scale, alphax, alphay)
        float error = fitter.NelderMead(ref resultFit, startFit, epsilon, 1e-5f, 100);

        // Update LTC with best fitting values
        fitter.Update(resultFit);
    }

    /*
    static float ComputeNorm(IBrdf brdf, Vector3 V, float alpha)
    {
        float norm = 0.0f;

        for (int j = 0; j < Nsample; ++j)
            for (int i = 0; i < Nsample; ++i)
            {
                float U1 = ((float)i + 0.5f) / (float)Nsample;
                float U2 = (j + 0.5f) / (float)Nsample;

                // sample
                Vector3 L = brdf.Sample(V, alpha, U1, U2);

                // eval
                float pdf = 0;
                float eval = brdf.Eval(V, L, alpha, ref pdf);

                // accumulate
                norm += (pdf > 0) ? eval / pdf : 0.0f;
            }

        return norm / (float)(Nsample * Nsample);
    }
    */

    // * the norm (albedo) of the BRDF
    // * the average Schlick Fresnel value
    // * the average direction of the BRDF
    static Vector3 ComputeAvgTerms(IBrdf brdf, Vector3 V, float alpha,
        ref float norm, ref float fresnel)
    {
        Vector3 averageDir = Vector3.zero;

        for (int j = 0; j < Nsample; ++j)
            for (int i = 0; i < Nsample; ++i)
            {
                float U1 = (i + 0.5f) / (float)Nsample;
                float U2 = (j + 0.5f) / (float)Nsample;

                // sample
                Vector3 L = brdf.Sample(V, alpha, U1, U2);

                // eval
                float pdf = 0;
                float eval = brdf.Eval(V, L, alpha, ref pdf);

                // accumulate
                float weight = eval / pdf;

                Vector3 H = Vector3.Normalize(V + L);

                // accumulate
                norm += weight;
                fresnel += weight * Mathf.Pow(1.0f - Mathf.Max(Vector3.Dot(V, H), 0.0f), 5.0f);
                averageDir += weight * L;
            }

        norm /= (float)(Nsample * Nsample);
        fresnel /= (float)(Nsample * Nsample);
        // clear y component, which should be zero with isotropic BRDFs
        averageDir.y = 0.0f;

        return averageDir.normalized;
    }

    // compute the error between the BRDF and the LTC
    // using Multiple Importance Sampling
    public static float ComputeError(LTC ltc, IBrdf brdf, Vector3 V, float alpha)
    {

        double error = 0.0;

        for (int j = 0; j < Nsample; ++j)
        {
            for (int i = 0; i < Nsample; ++i)
            {
                float U1 = (i + 0.5f) / (float)Nsample;
                float U2 = (j + 0.5f) / (float)Nsample;

                // importance sample LTC
                {
                    // sample
                    Vector3 L = ltc.Sample(U1, U2);

                    // error with MIS weight
                    float pdf_brdf = 0;
                    float eval_brdf = brdf.Eval(V, L, alpha, ref pdf_brdf);
                    float eval_ltc = ltc.Eval(L);
                    float pdf_ltc = eval_ltc / ltc.magnitude;
                    double error_ = Mathf.Abs(eval_brdf - eval_ltc);
                    error_ = error_ * error_ * error_;
                    error += error_ / (pdf_ltc + pdf_brdf);
                }

                // importance sample BRDF
                {
                    // sample
                    Vector3 L = brdf.Sample(V, alpha, U1, U2);

                    // error with MIS weight
                    float pdf_brdf = 0;
                    float eval_brdf = brdf.Eval(V, L, alpha, ref pdf_brdf);
                    float eval_ltc = ltc.Eval(L);
                    float pdf_ltc = eval_ltc / ltc.magnitude;
                    double error_ = Mathf.Abs(eval_brdf - eval_ltc);
                    error_ = error_ * error_ * error_;
                    error += error_ / (pdf_ltc + pdf_brdf);
                }
            }
        }

	    return (float)error / (float)(Nsample * Nsample);
    }

    static float sqr(float x)
    {
        return x * x;
    }

    static float G(float w, float s, float g)
    {
        return -2.0f * Mathf.Sin(w) * Mathf.Cos(s) * Mathf.Cos(g) + Mathf.PI / 2.0f - g + Mathf.Sin(g) * Mathf.Cos(g);
    }

    static float H(float w, float s, float g)
    {
        float sinsSq = sqr(Mathf.Sin(s));
        float cosgSq = sqr(Mathf.Cos(g));

        return Mathf.Cos(w) * (Mathf.Cos(g) * Mathf.Sqrt(sinsSq - cosgSq) + sinsSq * Mathf.Asin(Mathf.Cos(g) / Mathf.Sin(s)));
    }

    static float ihemi(float w, float s)
    {
        float g = Mathf.Asin(Mathf.Cos(s) / Mathf.Sin(w));
        float sinsSq = sqr(Mathf.Sin(s));

        if (w >= 0.0f && w <= (Mathf.PI / 2.0f - s))
            return Mathf.PI * Mathf.Cos(w) * sinsSq;

        if (w >= (Mathf.PI / 2.0f - s) && w < Mathf.PI / 2.0f)
            return Mathf.PI * Mathf.Cos(w) * sinsSq + G(w, s, g) - H(w, s, g);

        if (w >= Mathf.PI / 2.0f && w < (Mathf.PI / 2.0f + s))
            return G(w, s, g) + H(w, s, g);

        return 0.0f;
    }

    IEnumerator GenSphereTab()
    {
        int num = 0;
        for (int j = 0; j < N; ++j)
        {
            for (int i = 0; i < N; ++i)
            {
                num++;
                float U1 = (float)i / (N - 1);
                float U2 = (float)j / (N - 1);

                // z = cos(elevation angle)
                float z = 2.0f * U1 - 1.0f;

                // length of average dir., proportional to sin(sigma)^2
                float len = U2;

                float sigma = Mathf.Asin(Mathf.Sqrt(len));
                float omega = Mathf.Acos(z);

                // compute projected (cosine-weighted) solid angle of spherical cap
                float value = 0.0f;

                if (sigma > 0.0f)
                    value = ihemi(omega, sigma) / (Mathf.PI * len);
                else
                    value = Mathf.Max(z, 0.0f);

                //if (value != value)
                //    printf("nan!\n");

                tabSphere[i + j * N] = value;

                yield return null;
            }
        }

        if (num == N * N)
        {
            tableGenerated = true;
        }
    }

    static void PackTab(
    ref Color[] tex1, ref Color[] tex2,
    Matrix4x4[] tab,
    Vector2[] tabMagFresnel,
    float[] tabSphere)
    {
        for (int i = 0; i< N * N; ++i)
        {
            Matrix4x4 m = tab[i];

            Matrix4x4 invM = m.inverse;

            // normalize by the middle element
            for (int k = 0; k < 12; ++k)
            {
                invM[k] /= invM.m11;
            }
            

            // store the variable terms
            tex1[i].r = invM.m00;
            tex1[i].g = invM.m02;
            tex1[i].b = invM.m20;
            tex1[i].a = invM.m22;
            tex2[i].r = tabMagFresnel[i][0];
            tex2[i].g = tabMagFresnel[i][1];
            tex2[i].b = 0.0f; // unused
            tex2[i].a = tabSphere[i];
        }
    }
}
