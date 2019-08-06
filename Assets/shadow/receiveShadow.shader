// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/shadow/receiveShadow" 
{

	Properties{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
        _ShadowmapTex("ShadowMap", 2D) = "gray" {}
		[Toggle] PCF("percentage closer filter enable", Float) = 0
	}

		//CGINCLUDE
    


	SubShader
	{
        Tags { "RenderType" = "Opaque" }
		Pass
		{
            ZWrite On
			CGPROGRAM
            #include "UnityCG.cginc" 
            
#pragma vertex vert  
#pragma fragment frag
		#pragma multi_compile PCF_OFF PCF_ON
            sampler2D _ShadowmapTex;
			sampler2D _MainTex;
			float4    _Color;
			float4    _LightPosDistance;    //xyz lightpos; w:light to occullder's distance 

			float4x4 LightProjectionMatrix;

			uniform float2 shadowMapTexel;

			struct appdata
			{
				half4 vertex : POSITION;
				half4 color : COLOR;
				half2 uv : TEXCOORD0;
				half3 normal : NORMAL;
				float4 tangent	: TANGENT;
			};

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
				float4 uvProj : TEXCOORD1;
				float2 depth : TEXCOORD2;
				float3 normalWorld : TEXCOORD3;
				//SHADOW_COORDS(1);
                //float2 depth : TEXCOORD0;
            };

			float4 getShadowmapPixel(sampler2D shadowmap, float4 uv, float2 offset)
			{
				return tex2Dproj(shadowmap, UNITY_PROJ_COORD(float4(uv.xy + offset * shadowMapTexel, uv.z, uv.w)));
			}

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				float4x4 projMatrix = mul(LightProjectionMatrix, unity_ObjectToWorld);
				o.uvProj = mul(projMatrix, v.vertex);
				o.depth = o.uvProj.zw;
				o.normalWorld = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {

				fixed4 col = _Color * tex2D(_MainTex, i.uv);
				fixed3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
				float LDotN = dot(lightDirection, normalize(i.normalWorld));
				if (LDotN <= 0)
					return col;

				float4 shadow = tex2Dproj(_ShadowmapTex, UNITY_PROJ_COORD(i.uvProj));
				float d = DecodeFloatRGBA(shadow);
#ifdef PCF_ON
				float4 sum = 0;
				float x, y;
				for (y = -2.0; y <= 2.0; y += 1.0)
				{
					for (x = -2.0; x <= 2.0; x += 1.0)
					{
						float4 temp = getShadowmapPixel(_ShadowmapTex, i.uvProj, float2(x, y));
						sum += temp;
						d = min(d, DecodeFloatRGBA(temp));
					}
				}
				shadow = sum / 25.0;
#endif
				return shadow.x;
				

				float depth = saturate(i.depth.x / i.depth.y);
				
				float shadowScale = 1;
				if (depth >= d)
				{
					//return shadow.x;
					shadowScale = shadow.x;
				}
				return col * shadowScale;

            }
			ENDCG
		}

		

	}
}
