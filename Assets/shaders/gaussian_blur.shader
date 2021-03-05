// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/postprocess/gaussian_blur" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		
	}

		CGINCLUDE
		// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
//#pragma exclude_renderers d3d11 gles
#include "UnityCG.cginc"  

		sampler2D _MainTex;
	float4 _MainTex_TexelSize;
	half4 _MainTex_ST;
		uniform float sigma;
		//uniform float radius;

	//用于blur  
	struct v2f_blur
	{
		float4 pos : SV_POSITION;
		float2 uv  : TEXCOORD0;
	};

	//高斯模糊 vert shader
	v2f_blur vert(appdata_img v)
	{
		v2f_blur o;
		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = v.texcoord.xy;
#if UNITY_UV_STARTS_AT_TOP
		//o.uv.y = 1.0 - o.uv.y;
#endif
		//o.offs = _MainTex_TexelSize.xyxy * _offsets;

		return o;
	}

	float normpdf(float x, float sigma)
	{
		return 0.39894 * exp(-0.5 * x * x / (sigma * sigma)) / sigma;
	}

	//高斯模糊 pixel shader（上一篇文章有详细注释）  
	half4 frag(v2f_blur input) : SV_Target
	{
		half3 c = tex2D(_MainTex, input.uv).rgb;
		//return half4(c, 1.0);
		//declare stuff
		const int mSize = 61;
		int kSize = (mSize - 1) / 2;
		float kernel[mSize];
		half3 final_colour = half3(0.0, 0.0, 0.0);

		//create the 1-D kernel
		
		float Z = 0.0;
		for (int j = 0; j <= kSize; ++j)
		{
			kernel[kSize + j] = kernel[kSize - j] = normpdf(float(j), 7.0);
		}

		//get the normalization factor (as the gaussian has been clamped)
		for (j = 0; j < mSize; ++j)
		{
			Z += kernel[j];
		}

		//read out the texels
		for (int i = -kSize; i <= kSize; ++i)
		{
			for (j = -kSize; j <= kSize; ++j)
			{
				final_colour += kernel[kSize + j] * kernel[kSize + i] * tex2D(_MainTex, input.uv + half2(float(i),float(j)) * _MainTex_TexelSize.xy).rgb;

			}
		}
		
		return half4(final_colour / (Z * Z), 1.0);
		
	}


		ENDCG

	SubShader
	{

		//pass 0: 高斯模糊  
		Pass
		{
			ZTest Off
			//Cull Off
			ZWrite Off
			Fog{ Mode Off }

			CGPROGRAM
#pragma vertex vert 
#pragma fragment frag
			ENDCG
		}
	}
	//FallBack "Diffuse"
}