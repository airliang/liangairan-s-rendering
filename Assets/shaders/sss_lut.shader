// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/sss/sss_lut" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
        //_Cube("Environment Map", Cube) = "_Skybox" {}
        //_NormalTex("NormalMap (RGB)", 2D) = "bump" {}
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
        Cull Off

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
            //#pragma multi_compile_fwdbase 
            #define PI 3.14159265359

            //samplerCUBE _Cube;
            //sampler2D _NormalTex;

            struct appdata
            {
                half4 vertex : POSITION;
                half2 uv : TEXCOORD0;
            };

            struct VSOut
            {
                half4 pos		: SV_POSITION;
                half2 uv : TEXCOORD0;
            };

			float PHBeckmann(float ndoth, float m)
			{
				float alpha = acos(ndoth);
				float ta = tan(alpha);
				float val = 1.0 / (m * m * pow(ndoth, 4.0)) * exp(-(ta * ta) / (m * m));
				return val;
			}

			//高斯分布函数
			float Gaussian(float v, float r)
			{
				return 1.0 / sqrt(2.0 * PI * v) * exp(-(r * r) / (2 * v));
			}

			float3 Scatter(float r)
			{
				// Values from GPU Gems 3 "Advanced Skin Rendering"
  // Originally taken from real life samples
				return Gaussian(0.0064 * 1.414, r) * float3(0.233, 0.455, 0.649)
					+ Gaussian(0.0484 * 1.414, r) * float3(0.100, 0.336, 0.344)
					+ Gaussian(0.1870 * 1.414, r) * float3(0.118, 0.198, 0.000)
					+ Gaussian(0.5670 * 1.414, r) * float3(0.113, 0.007, 0.007)
					+ Gaussian(1.9900 * 1.414, r) * float3(0.358, 0.004, 0.00001)
					+ Gaussian(7.4100 * 1.414, r) * float3(0.078, 0.00001, 0.00001);
			}

			float3 integrateDiffuseScatteringOnRing(float cosTheta, float skinRadius)
			{
				//Angle from lightdirection
				float theta = acos(cosTheta);
				float3 totalWeights = 0;
				float3 totalLight = 0;

				float a = -PI / 2;

				float inc = 0.05f;

				while (a <= PI / 2.0f)
				{
					float sampleAngle = theta + a;
					float diffuse = saturate(cos(sampleAngle));

					// Distance
					float sampleDist = abs(2.0f * skinRadius * sin(a * 0.5f));

					// Profile Weight
					float3 weights = Scatter(sampleDist);

					totalWeights += weights;
					totalLight += diffuse * weights;
					a += inc;
				}

				float3 result = float3(totalLight.x / totalWeights.x, totalLight.y / totalWeights.y, totalLight.z / totalWeights.z);
				return result;
			}

            VSOut vert(appdata v)
            {
                VSOut o;
                o.pos = v.vertex;
#if UNITY_UV_STARTS_AT_TOP
				v.uv.y = 1.0 - v.uv.y;
#endif
                o.uv = v.uv;
                return o;
            }

            half4 frag(VSOut i) : COLOR
            {
                fixed4 colorOut;
				float textureHeight = 512.0;
				float r = 1.0 / (i.uv.y + 0.0001);
                colorOut.rgb = integrateDiffuseScatteringOnRing(lerp(-1, 1, i.uv.x), r);
				colorOut.rgb *= 2.2;
				float spec = 0.5 * pow(PHBeckmann(i.uv.x, i.uv.y), 0.1);
				colorOut.a = spec;
                return colorOut;
            }
            ENDCG
        }
	}
    //FallBack "Diffuse"
}