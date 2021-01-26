// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/pbr/anisotropic pbr simple" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Roughness ("Roughness", Range(0,1)) = 0
		//_RoughnessY ("RoughnessY", Range(0,1)) = 0
        _Metallic("Metallicness",Range(0,1)) = 0
        _F0 ("Fresnel coefficient", Color) = (1,1,1,1)
		_Ex("Ex", Range(0,1)) = 1
		_Ey("Ey",Range(0,1)) = 1
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #include "pbrInclude.cginc"
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers xbox360 flash	
            #pragma multi_compile_fwdbase 
            #define PI 3.14159265359

            sampler2D _MainTex;
            float _Roughness;
            float _Metallic;
			float _Ex;
			float _Ey;
            fixed4 _F0;
			fixed4 _Color;

            struct appdata
            {
                half4 vertex : POSITION;
                half4 color : COLOR;
                half2 uv : TEXCOORD0;
                half3 normal : NORMAL;
				half3 tangent: TANGENT;
            };

            struct VSOut
            {
                half4 pos		: SV_POSITION;
                half4 color     : COLOR;
                half2 uv : TEXCOORD0;
                half3 normalWorld : TEXCOORD1;
                half3 posWorld : TEXCOORD2;
				half3 tangentWorld : TEXCOORD3;
                SHADOW_COORDS(4)
            };

			half3 wardBrdf(half3 fresnel, float NdotH, float ndv, float ndl, float3 H, float3 X, float3 Y)
			{
				float ax = _Ex;
				float ay = _Ey;
				float alphaX = dot(H, X) / ax;
				float alphaY = dot(H, Y) / ay;
				float exponent = -2.0 * (alphaX * alphaX + alphaY * alphaY) / (1.0 + NdotH);

				float spec = sqrt(ndl / ndv) * exp(exponent);
				return fresnel * spec;
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
                TRANSFER_SHADOW(o);
                return o;
            }

            half4 frag(VSOut i) : COLOR
            {
                fixed3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                fixed3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                fixed3 normalDirection = normalize(i.normalWorld); //UnpackNormal(tex2D(_NormalTex, i.uv));
				
                //微表面法线
                fixed3 h = normalize(lightDirection + viewDirection);

                fixed3 attenColor = _LightColor0.xyz;
                

                float NdL = max(dot(normalDirection, lightDirection), 0);
                float NdV = max(dot(normalDirection, viewDirection), 0);
                float VdH = max(dot(viewDirection, h), 0);
                float NdH = max(dot(normalDirection, h), 0);
                float LdH = max(dot(lightDirection, h), 0);

                
                fixed4 albedo = i.color * tex2D(_MainTex, i.uv) * _Color;
                //fixed3 lambert = max(0.0, NdL) * albedo.rgb;
                //radiance
                fixed3 totalLightColor = UNITY_LIGHTMODEL_AMBIENT.xyz + attenColor;
                fixed3 specularColor = lerp(fixed3(0.04, 0.04, 0.04), _F0.rgb, _Metallic);
                half3 F = fresnelSchlick(VdH, specularColor.rgb);
                half3 kS = F;
                half3 kD = (half3(1, 1, 1) - kS) * (1.0 - _Metallic);

                
				//h要变换到切线空间里
				half3 tangent = i.tangentWorld;//normalize(cross(normalDirection, viewDirection));
				half3 bNormal = normalize(cross(normalDirection, tangent));
				//float D = BeckmannNormalDistribution(_Roughness, NdH);
				float D = D_GGXaniso(_Ex, _Ey, NdH, h, tangent, bNormal);
				float G = smith_schilck(_Roughness, NdV, NdH);
				fixed3 specular = brdf(F, D, G, NdV, NdL);
				//fixed3 specular = wardBrdf(F, NdH, NdV, NdL, h, tangent, bNormal) * G;
                fixed4 lightOut;
                fixed3 directDiffuse = (albedo.rgb / PI) * kD * totalLightColor * NdL;
                lightOut.rgb = directDiffuse + specular * totalLightColor * NdL;

				//lightOut.rgb = i.tangentWorld;

                return lightOut;
            }
            ENDCG
        }
	}
    FallBack "Diffuse"
}