// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/shadow/BlurShadow" 
{

	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

	CGINCLUDE
#include "UnityCG.cginc" 

	sampler2D _MainTex;
	float4 _MainTex_TexelSize;
	float fFilterWidth;
	sampler2D _BlurShadowTex;

	struct v2f
	{
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	

	v2f vert(appdata_img v)
	{
		v2f o = (v2f)0;
		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = v.texcoord.xy;
#if UNITY_UV_STARTS_AT_TOP
		o.uv.y = 1.0 - o.uv.y;
#endif
		return o;
	}

	float4 frag(v2f i) : SV_Target
	{
		float fStartOffset = (fFilterWidth - 1.0) * 0.5;

		float4 sum = 0;
		float nums = 0;
		for (float y = -fStartOffset; y <= fStartOffset; y += 1.0)
		{
			for (float x = -fStartOffset; x <= fStartOffset; x += 1.0)
			{
				float4 temp = tex2D(_MainTex, i.uv.xy + float2(x, y) * _MainTex_TexelSize.xy);
				sum += temp;
				nums += 1.0;
			}
		}
		return sum / nums;
	}

	float4 fragCopy(v2f i) : SV_Target
	{
		return tex2D(_BlurShadowTex, i.uv.xy);
	}
		ENDCG
 
	SubShader
	{
        Tags { "RenderType" = "Opaque" }
		Pass
		{
            ZWrite Off
			ZTest Off
			CGPROGRAM
            
            
#pragma vertex vert
#pragma fragment frag 

            
			ENDCG
		}

		Pass
		{
			ZWrite Off
			ZTest Off
			CGPROGRAM


#pragma vertex vert
#pragma fragment fragCopy 


			ENDCG
		}
	}
}
