// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/shadow/RenderToShadow" 
{

	Properties{
	}

	SubShader
	{
        Tags { "RenderType" = "Opaque" "Shadow" = "Character"}
		Pass
		{
            ZWrite On
			Cull Front
			CGPROGRAM
            #include "UnityCG.cginc" 
			#pragma shader_feature VSM_OFF VSM_ON


		#pragma vertex vert_shadow  
		#pragma fragment frag_shadow  

            struct v2f
            {
                float4 pos : SV_POSITION;
#if VSM_ON
				float4 posWorld : TEXCOORD0;
#else
                float2 depth : TEXCOORD0;
#endif
            };

			float3 lightPos;

            v2f vert_shadow(appdata_img v)
            {
                v2f o = (v2f)0;
                o.pos = UnityObjectToClipPos(v.vertex);
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
