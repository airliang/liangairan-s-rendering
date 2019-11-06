// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/ocean/checkerboard" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
		_UVScale("UV Scale", Vector) = (1, 1, 1, 1)
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


			float2 _UVScale;

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
            };


            VSOut vert(appdata v)
            {
                VSOut o;
                o.color = v.color;

                o.pos = UnityObjectToClipPos(v.vertex);
                //TANGENT_SPACE_ROTATION;
                o.uv = v.uv * _UVScale;
				
                o.normalWorld = UnityObjectToWorldNormal(v.normal);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
				o.tangentWorld = UnityObjectToWorldDir(v.tangent.xyz);
                return o;
            }

			int bumpInt(float x) 
			{
				return floor(x * 0.5) + 2.0 * max(x * 0.5 - floor(x * 0.5) - 0.5, 0); 
			}

            half4 frag(VSOut i) : COLOR
            {
				float2 dstdx = ddx(i.uv);
				float2 dstdy = ddy(i.uv);
				float ds = max(dstdx.x, dstdy.x);
				float dt = max(dstdx.y, dstdy.y);
				float s0 = i.uv.x - ds;
				float s1 = i.uv.x + ds;
				float t0 = i.uv.y - dt;
				float t1 = i.uv.y + dt;
				float4 result = 0;
				if (floor(s0) == floor(s1) && floor(t0) == floor(t1))
				{
					result = (floor(i.uv.x) + floor(i.uv.y)) % 2 == 0 ? float4(1, 1, 1, 1) : float4(0, 0, 0, 1);
				}
				else
				{
					float sint = (bumpInt(s1) - bumpInt(s0)) / (2.0 * ds);
					float tint = (bumpInt(t1) - bumpInt(t0)) / (2.0 * dt);
					float area2 = sint + tint - 2 * sint * tint;
					if (ds > 1 || dt > 1)
						area2 = 0.5;
					result.rgb = (1 - area2) * float3(1, 1, 1) +
						area2 * float3(0, 0, 0);

					result.a = 1.0;
				}
				return result;
            }
            ENDCG
        }
	}
    //FallBack "Diffuse"
}