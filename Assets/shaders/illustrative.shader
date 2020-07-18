// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/toon/illustrative" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
	_WrapingTex("Typical diffuse light warping function", 2D) = "white" {}
	_IrradianceMap("IrradianceMap", Cube) = "_Skybox" {}
	alpha_beta_gama("alpha beta gama", Vector) = (0.5, 0.5, 2)
		_kexponent("exponent,x=kspec, y=krim", Vector) = (5, 5, 0)
		_artist_fresnel("artist-tuned Fresnel term", Color) = (1,1,1,1)
		_Ks("specular mask color", Color) = (1,1,1,1)
		_Kr("rim mask color", Color) = (1,1,1,1)
		_OutlineColor("Outline Color", Color) = (0,0,0,1)
		_OutlineWidth("outline width", Range(0,5)) = 0
		_OutlineTex("Outline Texture", 2D) = "black"
	}
	SubShader {
		Tags { "RenderType" = "Opaque" }
		LOD 200

		
		
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers xbox360 flash	
            #pragma multi_compile_fwdbase 
            #define PI 3.14159265359

#include "include/illustrative_core.cginc"

			sampler2D _MainTex;
			
			fixed4 _Color;


            struct appdata
            {
                half4 vertex : POSITION;
                half4 color : COLOR;
                half2 uv : TEXCOORD0;
                half3 normal : NORMAL;
				//half3 tangent: TANGENT;
            };

            struct VSOut
            {
                half4 pos		: SV_POSITION;
                half4 color     : COLOR;
                half2 uv : TEXCOORD0;
                half3 normalWorld : TEXCOORD1;
				half3 posWorld : TEXCOORD2;
            };


            VSOut vert(appdata v)
            {
                VSOut o;
                o.color = v.color;
                o.pos = UnityObjectToClipPos(v.vertex);
                //TANGENT_SPACE_ROTATION;
                o.uv = v.uv;
				
				o.normalWorld = UnityObjectToWorldNormal(v.normal);
				o.posWorld = mul(unity_ObjectToWorld, v.vertex);

                return o;
            }

            half4 frag(VSOut i) : COLOR
            {
				
                fixed3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
				fixed3 normalDirection = normalize(i.normalWorld);
				fixed3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
				float4 albedo = tex2D(_MainTex, i.uv) * i.color;
				float NdL = dot(normalDirection, lightDirection);
				float4 outcolor = albedo * (direction_ambient(normalDirection) + wrapfunction(NdL, _LightColor0));
				fixed3 reflectLight = reflect(-viewDirection, normalDirection);
				outcolor = outcolor + view_dependent(normalDirection, viewDirection, reflectLight, _LightColor0);
				return outcolor;
            }
            ENDCG
        }

		Pass
		{
			Tags { "LightMode" = "ForwardBase" }
			Cull Front
			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma exclude_renderers xbox360 flash	

			#define PI 3.14159265359

			float4 _OutlineColor;
			float _OutlineWidth;

			struct appdata_outline
			{
				half4 vertex : POSITION;
				half4 color  : COLOR;
				half3 normal : NORMAL;
			};

			struct VSOut
			{
				half4 pos		: SV_POSITION;
				//half3 normalWorld : TEXCOORD0;
			};


			VSOut vert(appdata_outline v)
			{
				VSOut o;
				float4 clipPosition = UnityObjectToClipPos(v.vertex);
				float3 worldNormal = UnityObjectToWorldNormal(v.normal);
				float3 clipNormal = mul((float3x3) UNITY_MATRIX_VP, worldNormal);
				float2 normalOffset = normalize(clipNormal.xy) / _ScreenParams.xy * _OutlineWidth * clipPosition.w;
				clipPosition.xy += normalOffset;

				o.pos = clipPosition;
				//o.pos = UnityObjectToClipPos(v.vertex);

				return o;
			}

			half4 frag(VSOut i) : COLOR
			{
				return _OutlineColor;
			}
			ENDCG
		}
	}

}