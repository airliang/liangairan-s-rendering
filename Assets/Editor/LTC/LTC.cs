using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LTC
{
    // lobe magnitude
    public float magnitude;

    // Average Schlick Fresnel term
    public float fresnel;

    // parametric representation
    public float m00, m11, m02;
    public Vector3 X, Y, Z;

    // matrix representation
    public Matrix4x4 M;
    public Matrix4x4 invM;
    public float detM;

    public LTC()
    {
        magnitude = 1;
        fresnel = 1;
        m00 = 1;
        m11 = 1;
        m02 = 0;
        //m12 = 0;
        X = Vector3.right;
        Y = Vector3.up;
        Z = Vector3.forward;
    }

    public void Update()
    {
        Matrix4x4 mRotate = Matrix4x4.identity;
        mRotate.SetRow(0, X);
        mRotate.SetRow(1, Y);
        mRotate.SetRow(2, Z);

        Matrix4x4 tmp = Matrix4x4.identity;
        tmp.m00 = m00;
        tmp.m11 = m11;
        tmp.m02 = m02;
        

        M = mRotate * tmp;
        invM = M.inverse;
        detM = M.determinant;
    }

    //Evaluate the new Distribution by the original distribution
    //
    //             ∂Lo         M^(-1)L     M^(-1)L
    //D(L) = Do(Lo)---- = Do(-----------)------------
    //              ∂L       ||M^(-1)L|| ||M^(-1)L||³
    //Do is the cosine distribution
    //          1
    //Do(Lo) = ---max(Lo.z, 0)    because Lo.z = cosθ
    //          π
    public float Eval(Vector3 L)
	{

        Vector3 Loriginal = Vector3.Normalize(invM * L);
        Vector3 L_ = M * Loriginal;

        float l = L_.magnitude;
        float Jacobian = detM / (l * l * l);

        float D = 1.0f / 3.14159f * Mathf.Max(0.0f, Loriginal.z);

        float res = magnitude * D / Jacobian;
        return res;
	}

    //important sampling
    //∫Do(ωo)dωo = 1
    //∫∫p(θ, φ)sinθdθdφ = 1
    //∫∫cosθsinθ/πdθdφ = 1
    //so the probability density function p(θ, φ) is:
    //p(θ, φ) = cosθsinθ/π
    //according to the margin probability function:
    //p(θ) = ∫[0, 2π]p(θ, φ)dφ = 2cosθsinθ
    //conditional probability for the p(φ)
    //p(φ) = p(θ, φ) / p(θ) = 1/2π
    //cdf of the p(φ)
    //∫[0, φ]1/2πdφ = φ/2π = u2
    //cdf of p(θ)
    //∫[0, θ]2cosθsinθdθ = -cosθ = u1
    // u1 = 1 - u1
    //θ = acos(u1)
    public Vector3 Sample(float U1, float U2)
	{

        float theta = Mathf.Acos(Mathf.Sqrt(U1));
        float phi = 2.0f * 3.14159f * U2;
        Vector3 L = Vector3.Normalize(M * new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta)));
		return L;
	}
}
