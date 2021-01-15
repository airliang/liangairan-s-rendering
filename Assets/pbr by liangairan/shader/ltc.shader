// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/pbr/ltc" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Roughness ("Roughness", Range(0,1)) = 0
        _Metallic("Metallicness",Range(0,1)) = 0
        _F0 ("Fresnel coefficient", Color) = (1,1,1,1)
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
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers xbox360 flash	
            #pragma multi_compile_fwdbase 
            #define PI 3.14159265359

            sampler2D _MainTex;
            uniform sampler2D ltc_mat;
            uniform sampler2D ltc_mag;

            float _Roughness;
            float _Metallic;
			float _Ex;
			float _Ey;
            fixed4 _F0;
			fixed4 _Color;
            float4 _RectPoints[4];
            matrix _RectWorldTransform;

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

            //reference：Geometric Derivation of the Irradiance of Polygonal Lights
            //fomular:
            //        1
            // Iij = ---cross(vi, vj)·z acos(θ)
            //        2π
            float IntegrateEdge(half3 v1, half3 v2)
            {
                float cosTheta = dot(v1, v2);
                float theta = acos(cosTheta);
                float res = cross(v1, v2).z * ((theta > 0.001) ? theta / sin(theta) : 1.0);

                return res;
            }

            half3 LTC_Evaluate(
                half3 N, half3 V, half3 P, float3x3 Minv, half3 points[4], bool twoSided)
            {
                // construct orthonormal basis around N
                half3 T1, T2;
                T1 = normalize(V - N * dot(V, N));
                T2 = cross(N, T1);

                // rotate area light in (T1, T2, N) basis
                Minv = mul(Minv, transpose(float3x3(T1, T2, N)));

                // polygon (allocate 5 vertices for clipping)
                half3 L[5];
                L[0] = mul(Minv, points[0] - P);
                L[1] = mul(Minv, points[1] - P);
                L[2] = mul(Minv, points[2] - P);
                L[3] = mul(Minv, points[3] - P);

                int n = 5;
                //下面这个是计算多少个顶点在平面下面，一般不需要做
                //ClipQuadToHorizon(L, n);

                if (n == 0)
                    return half3(0, 0, 0);

                // project onto sphere
                L[0] = normalize(L[0]);
                L[1] = normalize(L[1]);
                L[2] = normalize(L[2]);
                L[3] = normalize(L[3]);
                L[4] = normalize(L[4]);

                // integrate
                float sum = 0.0;

                sum += IntegrateEdge(L[0], L[1]);
                sum += IntegrateEdge(L[1], L[2]);
                sum += IntegrateEdge(L[2], L[3]);
                if (n >= 4)
                    sum += IntegrateEdge(L[3], L[4]);
                if (n == 5)
                    sum += IntegrateEdge(L[4], L[0]);

                sum = twoSided ? abs(sum) : max(0.0, sum);

                half3 Lo_i = half3(sum, sum, sum);

                return Lo_i;
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
                
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                

                return albedo;
            }
            ENDCG
        }
	}
    FallBack "Diffuse"
}