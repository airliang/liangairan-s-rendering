// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/postprocess/ScreenTexture" {

	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

		CGINCLUDE
#include "UnityCG.cginc"  

	struct appdata
	{
		float4 vertex : POSITION;
        half2 uv : TEXCOORD0;

	};

	struct VSOut
	{
		float4 pos : SV_POSITION;
		float2 uv  : TEXCOORD0;
	};



	sampler2D _MainTex;

	VSOut vert(appdata v)
	{
		VSOut o;
		o.pos = v.vertex;
		o.uv = v.uv;

		return o;
	}
    
	fixed4 frag(VSOut i) : SV_Target
	{
		fixed4 color = tex2D(_MainTex, i.uv);
        
	    return color;
	}


		ENDCG

		SubShader
	{
		Tags{ "Queue" = "Overlay" }
			Pass
		{
			ZTest Off
			ZWrite Off
			Fog{ Mode Off }

			CGPROGRAM
#pragma vertex vert  
#pragma fragment frag 
			ENDCG
		}


	}
}
