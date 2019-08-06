// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/distortion/distortion" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_NormalTex("NormalMap (RGB)", 2D) = "bump" {}
		_FlowTex("Flow (RG A-noise)", 2D) = "black" {}
		_UJump("U jump per phase", Range(-0.25, 0.25)) = 0.25
		_VJump("V jump per phase", Range(-0.25, 0.25)) = 0.25
			_Tiling("Tiling", Float) = 1
			_Speed("Speed", Float) = 1
			_FlowStrength("Flow Strength", Float) = 1
			_FlowOffset("Flow Offset", Float) = 0
			_Specular("Specular", Range(0.01, 1)) = 0.5
			_Gloss("specular Gloss", Float) = 1
	}
	SubShader {
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent"}
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
			sampler2D _NormalTex;
			sampler2D _FlowTex;
			fixed4 _Color;
			float _UJump, _VJump;
			float _Tiling;
			float _Speed;
			float _FlowStrength, _FlowOffset;
			float _Gloss, _Specular;

            struct appdata
            {
                half4 vertex : POSITION;
                half4 color : COLOR;
                half2 uv : TEXCOORD0;
                half3 normal : NORMAL;
				float4 tangent: TANGENT;
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
            };

			float3 FlowUVW(float2 uv, float2 flowVector, float2 jump, float flowOffset, float tiling, float time, bool flowB) {
				float phaseOffset = flowB ? 0.5 : 0;
				float progress = frac(time + phaseOffset);
				float3 uvw;
				uvw.xy = uv - flowVector * (progress + flowOffset);
				uvw.xy *= tiling;
				uvw.xy += phaseOffset;
				uvw.xy += (time - progress) * jump;
				uvw.z = 1 - abs(1 - 2 * progress);
				return uvw;
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
				o.tangentWorld = UnityObjectToWorldDir(v.tangent.xyz);
				o.binormalWorld = cross(normalize(o.normalWorld), normalize(o.tangentWorld.xyz)) * v.tangent.w;
                TRANSFER_SHADOW(o);
                return o;
            }

            half4 frag(VSOut i) : COLOR
            {
                fixed3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                fixed3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
				//fixed3 tangentNormal = UnpackNormal(tex2D(_NormalTex, i.uv));
				float3x3 mTangentToWorld = transpose(float3x3(i.tangentWorld, i.binormalWorld, i.normalWorld));
				//float3 normalDirection = normalize(mul(mTangentToWorld, tangentNormal));  //法线贴图的世界坐标

				float3 vDir = normalize(_WorldSpaceCameraPos - i.posWorld);

				float2 flowVector = tex2D(_FlowTex, i.uv).rg * 2 - 1;
				flowVector *= _FlowStrength;
				float noise = tex2D(_FlowTex, i.uv).a;
				float time = _Time.y * _Speed + noise;
				float2 jump = float2(_UJump, _VJump);
				float3 uvw1 = FlowUVW(i.uv, flowVector, jump, _FlowOffset, _Tiling, time, false);
				float3 uvw2 = FlowUVW(i.uv, flowVector, jump, _FlowOffset, _Tiling, time, true);

				float3 normalA = UnpackNormal(tex2D(_NormalTex, uvw1.xy)) * uvw1.z;
				float3 normalB = UnpackNormal(tex2D(_NormalTex, uvw2.xy)) * uvw2.z;
				float3 normalDirection = normalize(mul(mTangentToWorld, normalize(normalA + normalB)));

				half3 h = normalize(lightDirection + viewDirection);
				fixed diff = max(0, dot(normalDirection, lightDirection));

				float nh = max(0, dot(normalDirection, h));

				float spec = pow(nh, _Specular * 128.0) * _Gloss * _LightColor0;

				fixed4 c1 = tex2D(_MainTex, uvw1.xy) * uvw1.z;
				fixed4 c2 = tex2D(_MainTex, uvw2.xy) * uvw2.z;
				fixed4 c = (c1 + c2) * _Color * diff + spec;
                
				return c;
            }
            ENDCG
        }
	}
    FallBack "Diffuse"
}