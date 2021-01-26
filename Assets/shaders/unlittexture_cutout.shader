// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/enviroment/unlittexture_cutout" 
{

	Properties{
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Header(Blend State)]
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 1 //"One"
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DestBlend", Float) = 0 //"Zero"
		[Toggle(STIPPLE_ENABLE)] _Stipple_enable("Stipple transparency?", Int) = 0
		_Transparency("Transparency", Range(0.0, 1.0)) = 1.0
		
	}

	SubShader
	{
        Tags { "RenderType" = "Opaque" "Shadow" = "Character"}
		Pass
		{
            ZWrite On
			Cull off
			CGPROGRAM
            #include "UnityCG.cginc" 
			#include "shadowmap.cginc"


			#pragma vertex vert  
			#pragma fragment frag
			#pragma shader_feature STIPPLE_ENABLE

			sampler2D _MainTex;
			half _Cutoff;
			half _Transparency;

			struct appdata
			{
				half4 vertex : POSITION;
				half4 color : COLOR;
				half2 uv : TEXCOORD0;
				half3 normal : NORMAL;
				float4 tangent	: TANGENT;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 uvProj : TEXCOORD1;
				float3 depth : TEXCOORD2;        //z-length of receiver to light in worldspace
				float3 normalWorld : TEXCOORD3;
				float3 posWorld : TEXCOORD4;
				float4 screenPos : TEXCOORD5;
				//SHADOW_COORDS(1);
				//float2 depth : TEXCOORD0;
			};


			v2f vert(appdata v)
			{
				v2f o = (v2f)0;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				float4x4 projMatrix = mul(LightProjectionMatrix, unity_ObjectToWorld);
				o.uvProj = mul(projMatrix, v.vertex);
				o.depth.xy = o.uvProj.zw;
				float4 posWorld = mul(unity_ObjectToWorld, v.vertex);
				o.depth.z = length(lightPos - posWorld.xyz);
				o.normalWorld = UnityObjectToWorldNormal(v.normal);
				o.posWorld = posWorld.xyz;
				o.screenPos = o.pos;
				return o;
			}


			float4 frag(v2f i) : SV_Target
			{

				fixed4 col = tex2D(_MainTex, i.uv);
				clip(col.a - _Cutoff);
				//float shadowAttention = getShadowAttention(i.uvProj, i.normalWorld, i.posWorld);
#if STIPPLE_ENABLE
				float2 screenpos = i.screenPos.xy / i.screenPos.w * 0.5 + 0.5;
				screenpos *= _ScreenParams.xy;   // pixel position
				//return half4(screenpos, 0, 1);
				StippleClip(screenpos, _Transparency);
#endif
				return col; //*shadowAttention;

			}
			ENDCG
		}

		

	}
}
