// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/pbr/ltc" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Roughness ("Roughness", Range(0,1)) = 0
        //_Metallic("Metallicness",Range(0,1)) = 0
        _F0 ("Fresnel coefficient", Color) = (1,1,1,1)
        ltc_mat("ltc lookup matrix texture", 2D) = "white" {}
        ltc_mag("ltc lookup fresnel texture", 2D) = "gray" {}
        [Toggle(RADIANCE_TEXTURE)] _RADIANCE_TEXTURE("RADIANCE_TEXTURE?", Int) = 0
        _RadianceTexArray("radiance textures", 2DArray) = "white" {}
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
            #pragma shader_feature RADIANCE_TEXTURE
 

            sampler2D _MainTex;
            sampler2D ltc_mat;
            sampler2D ltc_mag;
            UNITY_DECLARE_TEX2DARRAY(_RadianceTexArray);

            float _Roughness;
            //float _Metallic;
			float _Ex;
			float _Ey;
            half4 _F0;
            half4 _Color;
            uniform half4 _RectPoints[4];
            uniform float3 _RectCenter;
            uniform half3 _RectDirX;
            uniform half3 _RectDirY;
            uniform half3 _RectSize;
            uniform float4  AreaLightColor;
            uniform matrix _RectWorldTransform;

            static const float LUT_SIZE = 64.0;
            static const float LUT_SCALE = (LUT_SIZE - 1.0) / LUT_SIZE;
            static const float LUT_BIAS = 0.5 / LUT_SIZE;

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

            struct Ray
            {
                half3 origin;
                half3 dir;
            };

            bool RayPlaneIntersect(Ray ray, half4 plane, out float t)
            {
                t = -dot(plane, half4(ray.origin, 1.0)) / dot(plane.xyz, ray.dir);
                return t > 0.0;
            }

            void InitRectPoints(float2 wh, float3 center, half3 dirx, half3 diry, out half3 points[4])
            {
                float halfx = wh.x * 0.5;
                float halfy = wh.y * 0.5;
                float3 ex = halfx * dirx;
                float3 ey = halfy * diry;

                points[0] = center - ex - ey;
                points[1] = center + ex - ey;
                points[2] = center + ex + ey;
                points[3] = center - ex + ey;

                points[0] = mul(_RectWorldTransform, half4(points[0], 1));
                points[1] = mul(_RectWorldTransform, half4(points[1], 1));
                points[2] = mul(_RectWorldTransform, half4(points[2], 1));
                points[3] = mul(_RectWorldTransform, half4(points[3], 1));
            }

            //reference：Geometric Derivation of the Irradiance of Polygonal Lights
            //fomular:
            //        1
            // Iij = ---cross(vi, vj)·z acos(θ)
            //        2π
            /*
            float IntegrateEdge(half3 v1, half3 v2)
            {
                float cosTheta = dot(v1, v2);
                float theta = acos(cosTheta);
                float res = cross(v1, v2).z * ((theta > 0.001) ? theta / sin(theta) : 1.0);

                return res;
            }
            */

            half3 IntegrateEdgeVec(half3 v1, half3 v2)
            {
                float x = dot(v1, v2);
                float y = abs(x);

                float a = 0.8543985 + (0.4965155 + 0.0145206*y)*y;
                float b = 3.4175940 + (4.1616724 + y)*y;
                float v = a / b;

                float theta_sintheta = (x > 0.0) ? v : 0.5 * rsqrt(max(1.0 - x*x, 1e-7)) - v;

                return cross(v1, v2)*theta_sintheta;
            }

            float IntegrateEdge(half3 v1, half3 v2)
            {
                return IntegrateEdgeVec(v1, v2).z;
            }

            half3 FetchDiffuseFilteredTexture(half3 p1, half3 p2, half3 p3, half3 p4, half3 dir)
            {
                //if (textured == false)
                //    return half3(1, 1, 1);

                // area light plane basis
                half3 V1 = p2 - p1;
                half3 V2 = p4 - p1;
                half3 planeOrtho = cross(V1, V2);
                float planeAreaSquared = dot(planeOrtho, planeOrtho);

                Ray ray;
                ray.origin = half3(0, 0, 0);
                ray.dir = dir;
                half4 plane = half4(planeOrtho, -dot(planeOrtho, p1));
                float planeDist;
                RayPlaneIntersect(ray, plane, planeDist);

                half3 P = planeDist * ray.dir - p1;

                // find tex coords of P
                float dot_V1_V2 = dot(V1, V2);
                float inv_dot_V1_V1 = 1.0 / dot(V1, V1);
                half3 V2_ = V2 - V1 * dot_V1_V2 * inv_dot_V1_V1;
                half2 Puv;
                Puv.y = dot(V2_, P) / dot(V2_, V2_);
                Puv.x = dot(V1, P) * inv_dot_V1_V1 - dot_V1_V2 * inv_dot_V1_V1 * Puv.y;

                // LOD
                float d = abs(planeDist) / pow(planeAreaSquared, 0.25);

                // Flip texture to match OpenGL conventions
                //Puv = Puv * half2(1, -1) + half2(0, 1);

                float lod = log(2048.0 * d) / log(3.0);
                lod = min(lod, 7.0);

                float lodA = floor(lod);
                float lodB = ceil(lod);
                float t = lod - lodA;

                half3 a = UNITY_SAMPLE_TEX2DARRAY(_RadianceTexArray, float3(Puv, lodA)).rgb;
                half3 b = UNITY_SAMPLE_TEX2DARRAY(_RadianceTexArray, float3(Puv, lodB)).rgb;

                return lerp(a, b, t);
            }

            
            half3 LTC_Evaluate(
                half3 N, half3 V, half3 P, float3x3 Minv, half4 points[4], bool twoSided)
            {
                // construct orthonormal basis around N
                half3 T1, T2;
                T1 = normalize(V - N * dot(V, N));
                T2 = cross(N, T1);

                // rotate area light in (T1, T2, N) basis
                Minv = mul(Minv, float3x3(T1, T2, N));
                //Minv = mul(transpose(float3x3(T1, T2, N)), Minv);

                // polygon (allocate 5 vertices for clipping)
                half3 L[5];
                L[0] = mul(Minv, points[0].xyz - P);
                L[1] = mul(Minv, points[1].xyz - P);
                L[2] = mul(Minv, points[2].xyz - P);
                L[3] = mul(Minv, points[3].xyz - P);
                L[4] = L[0];

                half3 LL[4];
                LL[0] = L[0];
                LL[1] = L[1];
                LL[2] = L[2];
                LL[3] = L[3];

                int n = 4;
                //下面这个是计算多少个顶点在平面下面，一般不需要做
                //ClipQuadToHorizon(L, n);

                //if (n == 0)
                //    return half3(0, 0, 0);

                // project onto sphere
                L[0] = normalize(L[0]);
                L[1] = normalize(L[1]);
                L[2] = normalize(L[2]);
                L[3] = normalize(L[3]);
                L[4] = normalize(L[4]);

                // integrate
                float sum = 0.0;
#ifdef RADIANCE_TEXTURE
                half3 vsum;

                // integrate
                vsum = IntegrateEdgeVec(L[0], L[1]);
                vsum += IntegrateEdgeVec(L[1], L[2]);
                vsum += IntegrateEdgeVec(L[2], L[3]);
                if (n >= 4)
                    vsum += IntegrateEdgeVec(L[3], L[4]);
                if (n == 5)
                    vsum += IntegrateEdgeVec(L[4], L[0]);

                sum = twoSided ? abs(vsum.z) : max(0.0, vsum.z);

                half3 fetchDir = normalize(vsum);
                half3 colorMap = FetchDiffuseFilteredTexture(LL[0], LL[1], LL[2], LL[3], fetchDir);
                half3 Lo_i = sum * colorMap;

                return Lo_i;
#else
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
#endif
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
                half3 points[4];
                InitRectPoints(_RectSize.xy, _RectCenter, _RectDirX, _RectDirY, points);
                half3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                half3 V = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                half3 N = normalize(i.normalWorld); //UnpackNormal(tex2D(_NormalTex, i.uv));
                //微表面法线
                half3 h = normalize(lightDirection + V);
                
                half4 albedo = tex2D(_MainTex, i.uv) * _Color;

                float theta = acos(dot(N, V));
                half2 uv = half2(_Roughness, theta / (0.5 * UNITY_PI));
                uv = uv * LUT_SCALE + LUT_BIAS;

                half4 t1 = tex2D(ltc_mat, uv);
                half4 t2 = tex2D(ltc_mag, uv);
                float3x3 Minv = float3x3(
                    float3(t1.x, 0, t1.z),
                    float3(0, 1, 0),
                    float3(t1.y, 0, t1.w)
                );


                bool twoSided = false;

                half3 spec = LTC_Evaluate(N, V, i.posWorld, Minv, _RectPoints, twoSided);
                // BRDF shadowing and Fresnel
                half3 scol = 1;
                spec *= scol * t2.x + (1.0 - scol) * t2.y;

                float3x3 identity = float3x3(
                    float3(1, 0, 0),
                    float3(0, 1, 0),
                    float3(0, 0, 1)
                    );
                half3 diff = LTC_Evaluate(N, V, i.posWorld, identity, _RectPoints, twoSided);

                half3 col = 0;
                col = AreaLightColor.rgb * (spec + albedo * diff);
                col /= 2.0 * UNITY_PI;
                //col = pow(col, 2.2);

                return half4(col, 1);
            }
            ENDCG
        }
	}
    FallBack "Diffuse"
}