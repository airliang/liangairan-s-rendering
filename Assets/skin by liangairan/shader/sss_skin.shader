Shader "liangairan/sss/Skin" {
	// 　　　　　　D(h) F(v,h) G(l,v,h)
	//f(l,v) = ---------------------------
	// 　　　　　　4(n·l)(n·v)
	Properties{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
	_NormalTex("NormalMap (RGB)", 2D) = "bump" {}
	_BlurNormalTex("Blur NormalMap (RGB)", 2D) = "bump" {}
	_SSSLUTTex("Brdf lut map", 2D) = "white" {}
	_CurveFactor("CurveFactor",Range(1,3000)) = 800
		_Roughness("Roughness", Range(0,1)) = 0.1
		_Specular("Specular", Range(0,5)) = 1
	}
		SubShader{
			Tags { "RenderType" = "Opaque" "Shadow"="Character"}
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

				sampler2D _MainTex;
			sampler2D _BlurNormalTex;
			sampler2D _NormalTex;
			sampler2D _SSSLUTTex;
			//float _Roughness;
			//float _Metallic;
			fixed4 _Color;
			float _CurveFactor;
			float _Roughness;
			float _Specular;

			half3 fresnelSchlick(float cosTheta, half3 F0)
			{
				return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
			}

			struct appdata
			{
				half4 vertex : POSITION;
				half4 color : COLOR;
				half2 uv : TEXCOORD0;
				half3 normal : NORMAL;
				float4 tangent	: TANGENT;
			};

			struct VSOut
			{
				half4 pos		: SV_POSITION;
				half4 color     : COLOR;
				half2 uv : TEXCOORD0;
				half3 normalWorld : TEXCOORD1;
				half3 posWorld : TEXCOORD2;
				half3 tangentWorld : TEXCOORD3;
				half3 binormalWorld : TEXCOORD4;
				SHADOW_COORDS(5)
					//    half4 proj : TEXCOORD6;
					//half2 depth : TEXCOORD7;
				};

			float fresnelReflectance(float VdH, float F0)
			{
				float base = 1.0 - VdH;
				float exponential = pow(base, 5.0);
				return exponential + F0 * (1.0 - exponential);
			}

			VSOut vert(appdata v)
			{
				VSOut o;
				o.color = v.color;
				o.pos = UnityObjectToClipPos(v.vertex);
				//TANGENT_SPACE_ROTATION;
				o.uv = v.uv;
				o.normalWorld = UnityObjectToWorldNormal(v.normal);
				o.posWorld = mul(unity_ObjectToWorld, v.vertex);
				//TRANSFER_VERTEX_TO_FRAGMENT(o);
				o.tangentWorld = UnityObjectToWorldDir(v.tangent.xyz);
				o.binormalWorld = cross(normalize(o.normalWorld), normalize(o.tangentWorld.xyz)) * v.tangent.w;
				TRANSFER_SHADOW(o);

				//float4x4 matWLP = mul(LightProjectionMatrix, unity_ObjectToWorld);
				//o.proj = mul(matWLP, v.vertex);
				//o.depth = o.proj.zw;
				return o;
			}

			half4 frag(VSOut i) : COLOR
			{
				fixed3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
				fixed3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);

				fixed3 tangentNormal = UnpackNormal(tex2D(_NormalTex, i.uv));
				float3x3 mTangentToWorld = transpose(float3x3(i.tangentWorld, i.binormalWorld, i.normalWorld));
				fixed3 normalDirection = normalize(mul(mTangentToWorld, tangentNormal));  //法线贴图的世界坐标
				
				fixed3 blurNormal = UnpackNormal(tex2D(_BlurNormalTex, i.uv));
				fixed3 blurNormalDirection = normalize(mul(mTangentToWorld, blurNormal));

				//微表面法线
				fixed3 h = normalize(lightDirection + viewDirection);

				float NdL = max(dot(normalDirection, lightDirection), 0);
				float NdV = max(dot(normalDirection, viewDirection), 0);
				float VdH = max(dot(viewDirection, h), 0);
				float NdH = max(dot(normalDirection, h), 0);

				fixed curve = length(fwidth(blurNormalDirection)) / (length(fwidth(i.posWorld)) * _CurveFactor);
				//fixed3 curveVec = fwidth(blurNormalDirection) / (fwidth(i.posWorld) * _CurveFactor);
				//fixed curve = curveVec.x * dot(_LightColor0.rgb, fixed3(0.22, 0.707, 0.071));

				fixed NDotL = dot(blurNormalDirection, lightDirection);
				fixed4 sssColor = tex2D(_SSSLUTTex, float2(NDotL * 0.5 + 0.5, curve)) * _LightColor0;
				fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;

				fixed4 diffuse = (sssColor + UNITY_LIGHTMODEL_AMBIENT) * albedo;

				float PH = pow(2.0 * tex2D(_SSSLUTTex, float2(NdH, _Roughness)).a, 10.0);

				float F = fresnelReflectance(VdH, 0.028);
				float frSpec = max(PH * F / dot(h, h), 0) * _Specular * NdL;

				return diffuse +_LightColor0 * frSpec;
			}
			ENDCG
		}
	}
		FallBack "Diffuse"
}