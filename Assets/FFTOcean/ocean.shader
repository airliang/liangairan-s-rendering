// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/ocean/ocean" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
        _skyColor("Sky Color", Color) = (1,1,1,1)
        _skyCube("sky Cubemap", Cube) = ""{}
        _NoiseMap("Noise", 2D) = "bump" {}
        _SurfaceMap("SurfaceMap", 2D) = "white" {}
        _BumpScale("Bump Scale", Range(0, 2)) = 0.2
		_Wave1("Wave1 wavelength y-amplitude z-speed w-waveNum",Vector) = (2, 1, 10, 1)
		_Wave2("Wave2",Vector) = (20,0.3,30,0.1)
		_Wave3("Wave3",Vector) = (15,1.2,20,0.5)

		_D1("Wave1 xy-horizontal direction of wave", Vector) = (1,0,1,1)
		_D2("Wave2 direction", Vector) = (-0.3,0.8,1,1)
		_D3("Wave3 direction", Vector) = (0.3,0.9,1,1)

        _Circle1("Circle point1", Vector) = (0.2, 0.2, 1, 1)
        _Circle2("Circle point2", Vector) = (0.82, 0.3, 1, 1)
        _Circle3("Circle point3", Vector) = (0.6, 0.75, 1, 1)
        _WaterSize("Water resolution", Float) = 1024
		_Transparent("Transparent", Float) = 0.7
        _SeaBaseColor("Sea base color", Color) = (0.1, 0.19, 0.22, 1)
        _SeaWaterColor("Sea diffuse color", Color) = (0.8, 0.9, 0.6, 1)
        _Tess("Tessellation", Range(1,32)) = 4
	}
	SubShader {
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent"}
		LOD 200
		
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
			//Blend SrcAlpha OneMinusSrcAlpha
			//ZTest Off
			//ZWrite Off

            CGPROGRAM
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
#include "gerstner_wave.cginc"
            #pragma target 3.0
            //#pragma tessellate:tessFixed
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers xbox360 flash	
            #pragma multi_compile_fwdbase 
            
            #pragma multi_compile _ CIRCLE_WAVE

//#define CIRCLE_WAVE
            sampler2D _MainTex;
            samplerCUBE   _skyCube;
            sampler2D _NoiseMap;
            float4 _NoiseMap_ST;
            sampler2D _SurfaceMap;
            float4 _SurfaceMap_ST;
            half _BumpScale;
			fixed4 _Color;
            fixed4 _skyColor;
			float4 _Wave1;
			float4 _Wave2;
			float4 _Wave3;
			float4 _D1;
			float4 _D2;
			float4 _D3;
            float4 _Circle1;
            float4 _Circle2;
            float4 _Circle3;
            float _WaterSize;
			float _Transparent;

            float4 _SeaBaseColor;
            float4 _SeaWaterColor;

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
                half4 uv : TEXCOORD0;
                half3 normalWorld : TEXCOORD1;
                half3 posWorld : TEXCOORD2;
				half3 tangentWorld : TEXCOORD3;
            };

            

            float _Tess;

            float4 tessFixed()
            {
                return _Tess;
            }

            float fresnel(fixed3 I, fixed3 N)
            {
                float cosi = clamp(-1, 1, dot(I, N));
                float etai = 1;
                float etat = 1.3;
                if (cosi > 0) 
                { 
                    //swap(etai, etat);
                    etat = 1;
                    etai = 1.3;
                }
                // Compute sini using Snell's law
                float sint = etai / etat * sqrt(max(0, 1 - cosi * cosi));
                // Total internal reflection
                if (sint >= 1) 
                {
                    return 1;
                }
                else 
                {
                    float cost = sqrt(max(0, 1 - sint * sint));
                    cosi = abs(cosi);
                    float Rs = ((etat * cosi) - (etai * cost)) / ((etat * cosi) + (etai * cost));
                    float Rp = ((etai * cosi) - (etat * cost)) / ((etai * cosi) + (etat * cost));
                    return (Rs * Rs + Rp * Rp) / 2;
                }
            }

            half3 fresnelSchlick(float cosTheta, half3 F0)
            {
                return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
            }

            VSOut vert(appdata v)
            {
                VSOut o;
                o.color = v.color;

                
                //TANGENT_SPACE_ROTATION;
                
				
                o.normalWorld = float3(0, 0, 0);//UnityObjectToWorldNormal(v.normal);
                float3 posWorld = mul(unity_ObjectToWorld, v.vertex);

                o.uv.zw = posWorld.xz * 0.1 + _Time.y * 0.05;
                o.uv.xy = posWorld.xz * 0.4 - _Time.y * 0.1;

                float3 origPosWorld = posWorld;
                float3 gersterner = float3(0, 0, 0);
                float3 normal = float3(0, -1, 0);

                float speed1 = _Wave1.z;
                float speed2 = _Wave2.z;
                float speed3 = _Wave3.z;
#ifdef CIRCLE_WAVE
                float waveNum = 3;
                float2 circle = (_Circle1.xy - 0.5) * _WaterSize;
                gersterner += CircleWave(o.posWorld, _Wave1.x, _Wave1.y, _Wave1.w, _Time.y * _Wave1.z, circle, normal);
                o.normalWorld += normal;
                circle = (_Circle2.xy - 0.5) * _WaterSize;
                gersterner += CircleWave(o.posWorld, _Wave2.x, _Wave2.y, _Wave2.w, _Time.y * _Wave2.z, circle, normal);
                o.normalWorld += normal;
                circle = (_Circle3.xy - 0.5) * _WaterSize;
                gersterner += CircleWave(o.posWorld, _Wave3.x, _Wave3.y, _Wave3.w, _Time.y * _Wave3.z, circle, normal);
                o.normalWorld += normal;
                
#else
                /*
                float2 noiseUV = v.uv.xy * _NoiseMap_ST.xy;
#if !defined(SHADER_API_OPENGL)
                float4 noiseTex = tex2Dlod(_NoiseMap, float4(noiseUV + frac(_Time.x * _Wave1.z * 0.1), 0, 0)) * 0.2;
#else
                float4 noiseTex = tex2D(_NoiseMap, noiseUV + frac(_Time.x * _Wave1.z));
#endif
                float speed1 = sqrt(9.8 * 2 * UNITY_PI / _Wave1.x);
                gersterner += GerstnerWave(o.posWorld, _Wave1.x, _Wave1.y, _Wave1.w, _Time.y * speed1, normalize(_D1.xy), _D1.z, normal);
                o.normalWorld += normal; 
#if !defined(SHADER_API_OPENGL)
                noiseTex = tex2Dlod(_NoiseMap, float4(noiseUV + frac(_Time.x * _Wave2.z * 0.2), 0, 0)) * 0.2;
#else
                noiseTex = tex2D(_NoiseMap, noiseUV + frac(_Time.x * _Wave2.z));
#endif
                float speed2 = sqrt(9.8 * 2 * UNITY_PI / _Wave2.x);
                gersterner += GerstnerWave(o.posWorld, _Wave2.x, _Wave2.y, _Wave2.w, _Time.y * speed2, normalize(_D2.xy), _D2.z, normal);
                o.normalWorld += normal;
#if !defined(SHADER_API_OPENGL)
                noiseTex = tex2Dlod(_NoiseMap, float4(noiseUV + frac(_Time.x * _Wave3.z * 0.2), 0, 0)) * 0.2;
#else
                noiseTex = tex2D(_NoiseMap, noiseUV + frac(_Time.x * _Wave3.z));
#endif
                float speed3 = sqrt(9.8 * 2 * UNITY_PI / _Wave3.x);
                gersterner += GerstnerWave(o.posWorld, _Wave3.x, _Wave3.y, _Wave3.w, _Time.y * speed3, normalize(_D3.xy), _D3.z, normal);
                o.normalWorld += normal;
                */
                float waveNum = 3;
                gersterner += GerstnerWave2(posWorld, _Wave1.x, _Wave1.y, waveNum, normalize(_D1.xy), speed1);

                gersterner += GerstnerWave2(posWorld, _Wave2.x, _Wave2.y, waveNum, normalize(_D2.xy), speed2);

                gersterner += GerstnerWave2(posWorld, _Wave3.x, _Wave3.y, waveNum, normalize(_D3.xy), speed3);
#endif
               
                posWorld.xz += gersterner.xz;
                posWorld.y = gersterner.y;

                normal += GerstnerWaveNormal(posWorld, _Wave1.x, _Wave1.y, waveNum, normalize(_D1.xy), speed1);
                normal += GerstnerWaveNormal(posWorld, _Wave2.x, _Wave2.y, waveNum, normalize(_D2.xy), speed2);
                normal += GerstnerWaveNormal(posWorld, _Wave3.x, _Wave3.y, waveNum, normalize(_D3.xy), speed3);

                o.normalWorld = -normal;
                float opacity = 1 - _Transparent;
                o.normalWorld *= float3(opacity, 1, opacity);
                o.posWorld = posWorld;

                o.pos = mul(UNITY_MATRIX_VP, float4(o.posWorld, 1));
				o.tangentWorld = UnityObjectToWorldDir(v.tangent.xyz);

                return o;
            }

            half4 frag(VSOut i) : COLOR
            {
                fixed3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                fixed3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);

  
                half2 detailBump1 = tex2D(_SurfaceMap, i.uv.zw * _SurfaceMap_ST.xy + _SurfaceMap_ST.zw).xy * 2 - 1;
                half2 detailBump2 = tex2D(_SurfaceMap, i.uv.xy * _SurfaceMap_ST.xy + _SurfaceMap_ST.zw).xy * 2 - 1;
                half2 detailBump = (detailBump1 + detailBump2 * 0.5) * 0.25;

                i.normalWorld += half3(detailBump.x, 0, detailBump.y) * _BumpScale;
                //i.normalWorld += half3(1-waterFX.y, 0.5h, 1-waterFX.z) - 0.5;
                
                float3 normalDirection = normalize(i.normalWorld); //UnpackNormal(tex2D(_NormalTex, i.uv));
                float3 b = ddx(i.posWorld);
                float3 t = ddy(i.posWorld);
                //normalDirection = normalize(cross(t, b));
                //return half4(normalDirection, 1);
                float NoL = max(0, dot(lightDirection, normalDirection));

                fixed3 reflectDir = normalize(reflect(-viewDirection, normalDirection));
                float NoR = max(0, dot(normalDirection, reflectDir));
                //float fr = fresnel(reflectDir, normalDirection);
                float R0 = (1 - 1.3) * (1 - 1.3) / ((1 + 1.3) * (1 + 1.3));
                half3 fr = fresnelSchlick(NoR, R0);
                
                float fresnel = clamp(1.0 - dot(normalDirection, viewDirection), 0.0, 1.0);
                fresnel = pow(fresnel, 3.0) * 0.65;

                //float3 reflected = Sky(pos, reflect(rd, nor), lightDir);
                fixed4 reflectColor = _skyColor;//texCUBE(_skyCube, reflectDir);
                float4 diff = pow(NoL * 0.4 + 0.6, 3.);
                float4 refracted = _SeaBaseColor + diff * _SeaWaterColor * 0.12;
                float4 col = lerp(refracted, reflectColor, fresnel);

                half3 h = normalize(viewDirection + lightDirection);
                float spec = pow(max(dot(h, normalDirection), 0.0), 150.);
                col += spec;
                col = pow(col, 0.6);
                return col;
                //return fixed4(normalDirection, 1);
				//float3 vDir = normalize(_WorldSpaceCameraPos - i.posWorld);
				//float fr = fresnel(viewDirection, i.normalWorld);
                fixed4 albedo = (1 - half4(fr, 0)) * tex2D(_MainTex, i.uv) * _Color * NoL + /*reflectColor + */spec;
				albedo.a = _Transparent;
                
                return albedo;// *fr;
            }
            ENDCG
        }
	}
    FallBack "Diffuse"
}