// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/shadow/RenderToShadow" 
{

	Properties{
	}

		//CGINCLUDE
 


	SubShader
	{
        Tags { "RenderType" = "Opaque" "Shadow" = "Character"}
		Pass
		{
            ZWrite On
			CGPROGRAM
            #include "UnityCG.cginc" 
            
#pragma vertex vert_shadow  
#pragma fragment frag_shadow  

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 depth : TEXCOORD0;
            };


            v2f vert_shadow(appdata_img v)
            {
                v2f o = (v2f)0;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.depth = o.pos.zw;
                //UNITY_TRANSFER_DEPTH(o.depth);
                return o;
            }

            float4 frag_shadow(v2f i) : SV_Target
            {
                //return 0;
                //UNITY_OUTPUT_DEPTH(i.depth);
            
				float depth = i.depth.x / i.depth.y * 0.5 + 0.5;
                float4 color = EncodeFloatRGBA(depth);
                return color;
                //return float4(depth, depth, depth, 1);
                
            }
			ENDCG
		}

		

	}
}
