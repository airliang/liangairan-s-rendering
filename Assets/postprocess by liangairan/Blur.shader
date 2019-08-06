// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/postprocess/Blur" {

	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

		CGINCLUDE
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
#pragma exclude_renderers d3d11 gles
#include "UnityCG.cginc"  

		//用于阈值提取高亮部分  
	struct v2f_threshold
	{
		float4 pos : SV_POSITION;
        half2 uv : TEXCOORD0;
        half2 uv21 : TEXCOORD1;
        half2 uv22 : TEXCOORD2;
        half2 uv23 : TEXCOORD3;
	};

	//用于blur  
	struct v2f_blur
	{
		float4 pos : SV_POSITION;
		float2 uv  : TEXCOORD0;
        float2 offs : TEXCOORD1;
		//float4 uv01 : TEXCOORD1;
		//float4 uv23 : TEXCOORD2;
		//float4 uv45 : TEXCOORD3;
	};

	//用于bloom  
	struct v2f_bloom
	{
		float4 pos : SV_POSITION;
		float2 uv  : TEXCOORD0;
		float2 uv1 : TEXCOORD1;
	};

	sampler2D _MainTex;
	float4 _MainTex_TexelSize;
    half4 _MainTex_ST;

	fixed4 _offsets;

    static const half4 curve4[7] = { half4(0.0205,0.0205,0.0205,0), half4(0.0855,0.0855,0.0855,0), half4(0.232,0.232,0.232,0),
            half4(0.324,0.324,0.324,1), half4(0.232,0.232,0.232,0), half4(0.0855,0.0855,0.0855,0), half4(0.0205,0.0205,0.0205,0) };


	//高斯模糊 vert shader
	v2f_blur vert_blur(appdata_img v)
	{
		v2f_blur o;
		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = v.texcoord.xy;
        o.offs = _MainTex_TexelSize.xyxy * _offsets;

		return o;
	}
    

	//高斯模糊 pixel shader（上一篇文章有详细注释）  
	fixed4 frag_blur(v2f_blur i) : SV_Target
	{
		fixed4 color = fixed4(0,0,0,0);
       
        
        half2 offs = i.offs;//_MainTex_TexelSize.xyxy * _offsets;
        half2 coords = i.uv - offs * 3.0;
        for (int j = 0; j < 7; ++j)
        {
            color += curve4[j] * tex2D(_MainTex, coords);
            coords += offs;
        }

        
	    return color;
	}


		ENDCG

		SubShader
	{

			//pass 0: 高斯模糊  
			Pass
		{
			ZTest Off
			Cull Off
			ZWrite Off
			Fog{ Mode Off }

			CGPROGRAM
#pragma vertex vert_blur  
#pragma fragment frag_blur  
			ENDCG
		}


	}

	FallBack "Diffuse"
}
