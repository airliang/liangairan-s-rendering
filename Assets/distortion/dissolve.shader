// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Omega/Actors/Dissolve" {
    Properties {
		//<- 溶解
		_DissolveMap("Dissolve Map", 2D) = "white" {}
		_DissolveColorMap("Dissolve Color Map", 2D) = "white" {}
		_DissolveColor("Dissolve Color", Color) = (1,1,1,1)
		_DissolveEdgeColor("Dissolve Edge Color", Color) = (1,1,1,1)
		_DissolveThreshold("DissolveThreshold", Range(0, 1)) = 0.17
			_ColorFactor("Color Factor", Range(0, 1)) = 0.7
		_DissolveEdge("DissolveEdge", Range(0, 1)) = 0.8
		_DissolveSize("Ex-Dissolve Size", Range(0, 1)) = 0.01

		_ColorE("Color+E", 2D) = "white" {}
		_Normal("Normal", 2D) = "bump" {}
		_NormalIntensity("Normal Intensity", Range(0, 2)) = 1
		_SMMS("SMMS", 2D) = "white" {}
		_EmissiveColor("Emissive Color", Color) = (0.5,0.5,0.5,1)
		_SkinColor("SkinColor", Color) = (1,1,1,1)

		_EnvMin("enviroment min",Range(0,0.5)) = 0.1
			_ColorfulMetal("color metal", Range(0.0, 2.0)) = 1

		_Highlight("specular light", Range(0,2)) = 1
		_AmbientLight("Ambient light", Range(0,2)) = 1

		[Space(15)][Header(Glass Properties)]
		[Space(25)]_Refraction("Refraction", Range(0, 2)) = 0.1
		[Space(10)]_Transparency("Transparency", Range(0, 1)) = 0.9
		[HideInInspector]_Cutoff("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader {
        Tags {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        GrabPass{ "_GrabTexture" }
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
			Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            //#define UNITY_PASS_FORWARDBASE
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            //#include "UnityPBSLighting.cginc"
            //#include "UnityStandardBRDF.cginc"
            #pragma multi_compile_fwdbase_fullshadows
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles gles3 metal d3d11_9x xboxone ps4 psp2 n3ds wiiu
            #pragma target 3.0

			uniform sampler2D _ColorE; 
			uniform float4 _ColorE_ST;
			uniform sampler2D _DissolveMap; 
			uniform float4 _DissolveMap_ST;
			uniform sampler2D _DissolveColorMap; 
			uniform float4 _DissolveColorMap_ST;
			uniform float4 _DissolveColor;
			uniform float4 _DissolveEdgeColor;
			uniform float _DissolveThreshold;
			uniform float _ColorFactor;
			uniform float _DissolveEdge;

			uniform sampler2D _Environment; 
			uniform float4 _Environment_ST;
			uniform float _CubeIntensity;
			//uniform sampler2D _ColorE; uniform float4 _ColorE_ST;
			uniform sampler2D _Normal; 
			uniform float4 _Normal_ST;
			uniform sampler2D _SMMS; 
			uniform float4 _SMMS_ST;
			uniform float4 _EmissiveColor;
			uniform float4 _SkinColor;

			float _Gloss, _Metal, _Spec, _Highlight; 
			float _EnvMin;
			float _AmbientLight;
			float _ColorfulMetal;

			uniform sampler2D _GrabTexture;
			//uniform sampler2D _BumpMap;
			//uniform float4 _BumpMap_ST;
			uniform float _NormalIntensity;
			uniform float _Refraction;
			uniform float _ReflectionIntensity;
			//uniform float _BlurReflection;
			uniform float _Transparency;

            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
                float3 tangentDir : TEXCOORD5;
                float3 bitangentDir : TEXCOORD6;
                float4 screenPos : TEXCOORD7;
                UNITY_FOG_COORDS(8)
                #if defined(LIGHTMAP_ON) || defined(UNITY_SHOULD_SAMPLE_SH)
                    float4 ambientOrLightmapUV : TEXCOORD11;
                #endif
            };

			half GetSpecTerm(half perceptualRoughness, half nh, half nl, half nv)
			{
				half roughness = max((perceptualRoughness * perceptualRoughness), 0.002);
				half lambdaV = nl * (nv * (1 - roughness) + roughness);
				half lambdaL = nv * (nl * (1 - roughness) + roughness);
				half GGVTerm = 0.5f / (lambdaV + lambdaL + 1e-5f);

				half roughness2 = roughness * roughness;
				half nhroughness = (nh * roughness2 - nh) * nh + 1.0f;
				half GGXTerm = UNITY_INV_PI * roughness2 / (nhroughness * nhroughness + 1e-7f);

				half specularTerm = GGVTerm * GGXTerm * UNITY_PI;
				specularTerm = sqrt(max(1e-4h, specularTerm));
				specularTerm = max(0, specularTerm * nl);
				return specularTerm;
			}

			half3 CustomUnity_GlossyEnvironment(UNITY_ARGS_TEXCUBE(tex), half4 hdr, half roughness, half3 reflUVW)
			{
				half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(tex, reflUVW, roughness);
				return DecodeHDR(rgbm, hdr);
			}

			half3 GetEnvCube(half glossness, half3 reflDir)
			{
				half3 env0 = CustomUnity_GlossyEnvironment(UNITY_PASS_TEXCUBE(unity_SpecCube0), unity_SpecCube0_HDR, glossness, reflDir);

				half3 envCUBE;
				if (unity_SpecCube0_BoxMin.w < 0.99999)
				{
					half3 env1 = CustomUnity_GlossyEnvironment(UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1, unity_SpecCube0), unity_SpecCube1_HDR, glossness, reflDir);
					envCUBE = lerp(env1, env0, unity_SpecCube0_BoxMin.w);
				}
				else {
					envCUBE = env0;
				}
				return envCUBE;
			}

            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                #ifdef LIGHTMAP_ON
                    o.ambientOrLightmapUV.xy = v.texcoord0.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                    o.ambientOrLightmapUV.zw = 0;
                #endif
                #ifdef DYNAMICLIGHTMAP_ON
                    o.ambientOrLightmapUV.zw = v.texcoord0.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos(v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                o.screenPos = o.pos;
#if UNITY_UV_STARTS_AT_TOP
				float scale = -1.0;
#else
				float scale = 1.0;
#endif
				// pos的范围是【-1,1】+1为【0,2】，乘以0.5变成uv的范围【0,1】
				o.screenPos.xy = (float2(o.pos.x / o.pos.w, o.pos.y / o.pos.w * scale) + float2(1.0, 1.0)) * 0.5;
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }

			float4 fragTransparent(VertexOutput i) : COLOR{
				//#if UNITY_UV_STARTS_AT_TOP
				//    float grabSign = -_ProjectionParams.x;
				//#else
				//    float grabSign = _ProjectionParams.x;
				//#endif
				i.normalDir = normalize(i.normalDir);
				//i.screenPos = float4( i.screenPos.xy / i.screenPos.w, 0, 0 );
				//i.screenPos.y *= _ProjectionParams.x;
				float3x3 tangentTransform = float3x3(i.tangentDir, i.bitangentDir, i.normalDir);
				float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
				float3 _BumpMap_var = UnpackNormal(tex2D(_Normal, TRANSFORM_TEX(i.uv0, _Normal)));
				float3 node_7657 = lerp(float3(0,0,1) ,_BumpMap_var.rgb, _NormalIntensity);
				float3 Normalmap = node_7657;
				float3 normalLocal = Normalmap;
				float3 normalDirection = normalize(mul(normalLocal, tangentTransform)); // Perturbed normals
				float3 viewReflectDirection = reflect(-viewDirection, normalDirection);
				float2 Refractionmap = (node_7657.rg * _Refraction);
				//float2 sceneUVs = float2(1,grabSign) * i.screenPos.xy * 0.5 + 0.5 + Refractionmap;
				float2 sceneUVs = i.screenPos.xy + Refractionmap;
				float4 sceneColor = tex2D(_GrabTexture, sceneUVs);
				float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
				float3 lightColor = _LightColor0.rgb;
				float3 halfDirection = normalize(viewDirection + lightDirection);
				////// Lighting:
				float attenuation = LIGHT_ATTENUATION(i);
				float3 attenColor = attenuation * _LightColor0.xyz;
				float Pi = 3.141592654;
				float InvPi = 0.31830988618;
				///////// Gloss:
				float gloss = 0;

				float LdotH = saturate(dot(lightDirection, halfDirection));
				float NdotV = abs(dot(normalDirection, viewDirection));
				float NdotL = max(0.0,dot(normalDirection, lightDirection));
				half fd90 = 0.5 + 2 * LdotH * LdotH * (1 - gloss);
				float nlPow5 = Pow5(1 - NdotL);
				float nvPow5 = Pow5(1 - NdotV);
				float3 directDiffuse = ((1 + (fd90 - 1)*nlPow5) * (1 + (fd90 - 1)*nvPow5) * NdotL) * attenColor;
				float Transparency = 1.0 - _Transparency; // lerp(1, 0, _Transparency);
				fixed4 finalRGBA = fixed4(lerp(sceneColor.rgb, directDiffuse,Transparency),1);
				//fixed4 finalRGBA = fixed4(directDiffuse, 1);
				UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
				return finalRGBA;
			}

            float4 frag(VertexOutput i) : COLOR {

				fixed4 dissolveValue = tex2D(_DissolveMap, TRANSFORM_TEX(i.uv0, _DissolveMap));
				if (dissolveValue.r < _DissolveThreshold)
				{
					return fragTransparent(i);
				}
				//input tex
				float4 albedo = tex2D(_ColorE,TRANSFORM_TEX(i.uv0, _ColorE));
				float3 normaltex = UnpackNormal(tex2D(_Normal,TRANSFORM_TEX(i.uv0, _Normal)));
				float4 smms = tex2D(_SMMS, TRANSFORM_TEX(i.uv0, _SMMS));

				//NormalSetup
				half3 tangent = i.tangentDir.xyz;
				half3 binormal = i.bitangentDir.xyz;
				half3 normal = i.normalDir.xyz;
				float3 norDir = normalize(tangent * normaltex.x + binormal * normaltex.y + normal * normaltex.z);

				//dir
				float3 posWorld = i.posWorld;
				float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - posWorld);
				float3 reflDir = normalize(reflect(-viewDir, norDir));
				float3 floatDir = normalize(viewDir + _WorldSpaceLightPos0.xyz);
				float NoH = saturate(dot(norDir, floatDir));
				float3 lightDir = normalize(_WorldSpaceLightPos0.xyz - posWorld.xyz);
				half NoL = saturate(dot(norDir,lightDir));
				float NoV = saturate(dot(norDir, viewDir));

				float smoothness = saturate(smms.r*_Gloss);
				float glossness = saturate(smms.r / _Gloss);

				//Refl
				float occ = 1;
				float perceptualRoughness = glossness + (1 - _Metal)*smms.g;
				float3 reflspec = GetEnvCube(perceptualRoughness * 6, reflDir);
				reflspec = pow(reflspec, 1.4);
				reflspec *= _CubeIntensity * (occ * 0.5 + 0.5);
				reflspec = max(_EnvMin, reflspec);

				float Fresnel = pow(1.0 - NoV, 2.8)*0.8 + 0.2;
				float ReceiveShadow = 1;

				float3 IBLDiffuse = GetEnvCube(6, norDir);
				float3 IBLColor = lerp(IBLDiffuse, min(0.7, IBLDiffuse * 1.5), Fresnel);
				float3 IBLShadows = lerp(IBLColor * _AmbientLight, 1.0, ReceiveShadow);
				float3 albedocolor = (IBLColor.rgb + UNITY_LIGHTMODEL_AMBIENT.xyz) * albedo.rgb;

				albedocolor *= IBLShadows;

				
				//spec
				half3 specularTerm = GetSpecTerm(smoothness, NoH, ReceiveShadow, NoV) * _Highlight;
				half3 specColor = lerp(unity_ColorSpaceDielectricSpec.rgb, albedo, smms.g);
				//specularTerm *= any(specColor) ? 1.0 : 0.0;
				specColor *= specularTerm;
				specColor = lerp(specColor, specColor * albedocolor * _ColorfulMetal, smms.g * _ColorfulMetal);
				fixed4 finalColor = albedo + fixed4(specColor, 0);

				
				float percentage = _DissolveThreshold / dissolveValue.r;
				float lerpEdge = sign(percentage - _ColorFactor - _DissolveEdge);

				fixed4 edgeColor = lerp(_DissolveEdgeColor.rgba, _DissolveColor.rgba, saturate(lerpEdge));
				fixed4 destColorGet = tex2D(_DissolveColorMap, TRANSFORM_TEX(i.uv0, _DissolveColorMap));
				fixed4 colorOut = fixed4((destColorGet.rgb + _DissolveColor.rgb * _DissolveColor.a).rgb, destColorGet.a);
				colorOut = lerp(_DissolveColor, destColorGet, destColorGet.a);
				colorOut.rgb = colorOut.rgb + edgeColor.rgb * edgeColor.a;
				float lerpOut = sign(percentage - _ColorFactor);

				//colorOut.rgb = finalColor.rgb + edgeColor.rgb * edgeColor.a;
				//colorOut.a = 1.0;
				colorOut = lerp(finalColor, colorOut, saturate(lerpOut));

				return colorOut;
            }
            ENDCG
        }
        
    }
    FallBack "Diffuse"
}
