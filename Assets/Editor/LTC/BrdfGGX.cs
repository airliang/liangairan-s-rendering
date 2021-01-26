using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrdfGGX : IBrdf
{
    public float Eval(Vector3 V, Vector3 L, float alpha, ref float pdf)
	{
		if(V.z <= 0)
		{
			pdf = 0;
			return 0;
		}

		// masking
		float a_V = 1.0f / alpha / Mathf.Tan(Mathf.Acos(V.z));
		float LambdaV = (V.z < 1.0f) ? 0.5f * (-1.0f + Mathf.Sqrt(1.0f + 1.0f / a_V / a_V)) : 0.0f;
		float G1 = 1.0f / (1.0f + LambdaV);

		// shadowing
		float G2;
		if (L.z <= 0.0f)
			G2 = 0;
		else
		{
			float a_L = 1.0f / alpha / Mathf.Tan(Mathf.Acos(L.z));
			float LambdaL = (L.z < 1.0f) ? 0.5f * (-1.0f + Mathf.Sqrt(1.0f + 1.0f / a_L / a_L)) : 0.0f;
			G2 = 1.0f / (1.0f + LambdaV + LambdaL);
		}

		// D
		Vector3 H = Vector3.Normalize(V + L);
		float slopex = H.x / H.z;
		float slopey = H.y / H.z;
		float D = 1.0f / (1.0f + (slopex * slopex + slopey * slopey) / alpha / alpha);
		D = D * D;
		D = D / (3.14159f * alpha * alpha * H.z * H.z * H.z * H.z);

		pdf = Mathf.Acos(D * H.z / 4.0f / Vector3.Dot(V, H));
		float res = D * G2 / 4.0f / V.z;

		return res;
	}

	// todo: how does this sampling function work exactly?
	public Vector3 Sample(Vector3 V, float alpha, float U1, float U2)
	{
		float phi = 2.0f * 3.14159f * U1;
		float r = alpha * Mathf.Sqrt(U2 / (1.0f - U2));
		Vector3 N = Vector3.Normalize(new Vector3(r * Mathf.Acos(phi), r * Mathf.Sin(phi), 1.0f));
		Vector3 L = -V + 2.0f * N * Vector3.Dot(N, V);
		return L;
	}
}
