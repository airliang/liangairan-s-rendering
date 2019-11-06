// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/postprocess/Bloom" {

	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
	_BlurTex("Blur", 2D) = "white"{}
	}

		CGINCLUDE

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
	sampler2D _BlurTex;
	float4 _BlurTex_TexelSize;
	fixed4 _offsets;
	fixed4 _colorThreshold;
	fixed4 _bloomColor;
	fixed _bloomFactor;
    uniform float _thresholdIntensity;
    float  _weightCount;

    static const half4 curve4[7] = { half4(0.0205,0.0205,0.0205,0), half4(0.0855,0.0855,0.0855,0), half4(0.232,0.232,0.232,0),
            half4(0.324,0.324,0.324,1), half4(0.232,0.232,0.232,0), half4(0.0855,0.0855,0.0855,0), half4(0.0205,0.0205,0.0205,0) };


	//高亮部分提取shader  
	v2f_threshold vert_threshold(appdata_img v)
	{
		v2f_threshold o;
		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = v.texcoord.xy;
#if UNITY_UV_STARTS_AT_TOP
		o.uv.y = 1.0 - o.uv.y;
#endif
        o.uv21 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy * half2(-0.5h, -0.5h), _MainTex_ST);
        o.uv22 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy * half2(0.5h, -0.5h), _MainTex_ST);
        o.uv23 = UnityStereoScreenSpaceUVAdjust(v.texcoord + _MainTex_TexelSize.xy * half2(-0.5h, 0.5h), _MainTex_ST);
		//dx中纹理从左上角为初始坐标，需要反向  
//#if UNITY_UV_STARTS_AT_TOP  
//		if (_MainTex_TexelSize.y < 0)
//			o.uv.y = 1 - o.uv.y;
//#endif    
		return o;
	}

	fixed4 frag_threshold(v2f_threshold i) : SV_Target
	{
		fixed4 color = tex2D(_MainTex, i.uv);
        color += tex2D(_MainTex, i.uv21);
        color += tex2D(_MainTex, i.uv22);
        color += tex2D(_MainTex, i.uv23);
	    //仅当color大于设置的阈值的时候才输出  
	    return max(color / 4 - _colorThreshold, 0) * _thresholdIntensity;
	}

	//高斯模糊 vert shader
	v2f_blur vert_blur(appdata_img v)
	{
		v2f_blur o;
		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = v.texcoord.xy;
        o.offs = _MainTex_TexelSize.xyxy * _offsets;
#if UNITY_UV_STARTS_AT_TOP
		o.uv.y = 1.0 - o.uv.y;
#endif
		//o.uv01 = v.texcoord.xyxy + _offsets.xyxy * float4(1, 1, -1, -1);
		//o.uv23 = v.texcoord.xyxy + _offsets.xyxy * float4(1, 1, -1, -1) *2.0;
		//o.uv45 = v.texcoord.xyxy + _offsets.xyxy * float4(1, 1, -1, -1) *3.0;

		return o;
	}
    

	//高斯模糊 pixel shader（上一篇文章有详细注释）  
	fixed4 frag_blur(v2f_blur i) : SV_Target
	{
		fixed4 color = fixed4(0,0,0,0);

        half2 offs = i.offs;//_MainTex_TexelSize.xyxy * _offsets;
        half2 coords = i.uv - offs * 3.0;
        //color += weight[0] * tex2D(_MainTex, i.uv);
        for (int j = 0; j < 7; ++j)
        {
            color += curve4[j] * tex2D(_MainTex, coords);
            coords += offs;
        }
        
	    return color;
	}

		//Bloom效果 vertex shader  
		v2f_bloom vert_bloom(appdata_img v)
	{
		v2f_bloom o;
		//mvp矩阵变换  
		o.pos = UnityObjectToClipPos(v.vertex);
		//uv坐标传递  
		o.uv.xy = v.texcoord.xy;
		o.uv1.xy = o.uv.xy;
#if UNITY_UV_STARTS_AT_TOP  
		//if (_MainTex_TexelSize.y < 0)
		o.uv.y = 1 - o.uv.y;
#endif    
		return o;
	}

	fixed4 frag_bloom(v2f_bloom i) : SV_Target
	{
		//取原始清晰图片进行uv采样  
		fixed4 ori = tex2D(_MainTex, i.uv1);
	//取模糊普片进行uv采样  
	fixed4 blur = tex2D(_BlurTex, i.uv);
	//输出= 原始图像，叠加bloom权值*bloom颜色*泛光颜色  
	fixed4 final = ori + _bloomFactor * blur * _bloomColor;
	return final;
	}

		ENDCG

		SubShader
	{
		//pass 0: 提取高亮部分  
		Pass
		{
			ZTest Off
			Cull Off
			ZWrite Off
			Fog{ Mode Off }

			CGPROGRAM
#pragma vertex vert_threshold  
#pragma fragment frag_threshold  
			ENDCG
		}

			//pass 1: 高斯模糊  
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

			//pass 2: Bloom效果  
			Pass
		{

			ZTest Off
			Cull Off
			ZWrite Off
			Fog{ Mode Off }

			CGPROGRAM
#pragma vertex vert_bloom  
#pragma fragment frag_bloom  
			ENDCG
		}

	}
}
