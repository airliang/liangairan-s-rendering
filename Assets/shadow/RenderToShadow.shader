// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/shadow/RenderToShadow" 
{

	Properties{
		_MainTex("Albedo (RGB)", 2D) = "white" {}
	}

	SubShader
	{
        Tags { "RenderType" = "Opaque" "Shadow" = "Character"}
		Pass
		{
            ZWrite On
			//Cull front
			CGPROGRAM
            #include "UnityCG.cginc" 
			#pragma shader_feature VSM_OFF VSM_ON


		#pragma vertex vert_shadow  
		#pragma fragment frag_shadow  

			struct appdata
			{
				half4 vertex : POSITION;
				half2 uv : TEXCOORD0;
			};

            struct v2f
            {
                float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
#if VSM_ON
				float4 posWorld : TEXCOORD1;
#else
                float2 depth : TEXCOORD1;
#endif
            };

			sampler2D _MainTex;
			float3 lightPos;

            v2f vert_shadow(appdata v)
            {
                v2f o = (v2f)0;
                o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
#if VSM_ON
				o.posWorld = mul(unity_ObjectToWorld, v.vertex);
#else
                o.depth = o.pos.zw;
#endif
                //UNITY_TRANSFER_DEPTH(o.depth);
                return o;
            }

            float4 frag_shadow(v2f i) : SV_Target
            {
                //return 0;
                //UNITY_OUTPUT_DEPTH(i.depth);
				fixed4 col = tex2D(_MainTex, i.uv);
				clip(col.a - 0.6);
#if VSM_ON
				float depth = length(lightPos - i.posWorld.xyz) + 0.01;

				return float4(depth, depth * depth, 0, 1);
#else
				float depth = i.depth.x / i.depth.y * 0.5 + 0.5;
                //float4 color = EncodeFloatRGBA(depth);
                //return color;
                return float4(depth, depth, depth, 1);
#endif
            }
			ENDCG
		}

		

	}
}
