// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/ocean/projectedgrid" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties{

	}
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			Tags { "LightMode" = "ForwardBase" }
			Cull off
		ZTest always
		ZWrite off
			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma exclude_renderers xbox360 flash	

			#define PI 3.14159265359

			//matrix viewProjInverse;
			uniform matrix screenToView;
			uniform matrix viewToWorld;
			uniform float3 cameraPosProj;  //cameraPos for projected grid in worldspace

			struct appdata
			{
				half4 vertex : POSITION;
			};

			struct VSOut
			{
				half4 pos		: SV_POSITION;
				half4 ndcPos    : TEXCOORD0;
				//half3 normalWorld : TEXCOORD0;
			};


			VSOut vert(appdata v)
			{
				VSOut o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.ndcPos = o.pos / o.pos.w;
				o.ndcPos.z = -1;
				return o;
			}

			half4 frag(VSOut i) : COLOR
			{
				half3 cameraDir = normalize(mul(screenToView, i.ndcPos).xyz);
				half3 worldDir = normalize(mul(viewToWorld, cameraDir).xyz);
				float t = -cameraPosProj.y / worldDir.y;
				float2 planePos = cameraPosProj.xz + t * worldDir.xz;
				//return half4(1, 0, 0, 1);
				return half4(planePos.x, 0, planePos.y, 1);
			}
			ENDCG
		}
		
        
	}
    FallBack "Diffuse"
}