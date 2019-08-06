// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/postprocess/ImageAlphaLerp" 
{

	Properties{
        _MainTex("Base (RGB)", 2D) = "white" {}
        _OutlineTex("OutlineTex", 2D) = "black" {}
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
            sampler2D _MainTex;
            sampler2D _OutlineTex;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                //float2 depth : TEXCOORD0;
            };


            v2f vert(appdata_img v)
            {
                v2f o = (v2f)0;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex1 = tex2D(_MainTex, i.uv);
                fixed4 tex2 = tex2D(_OutlineTex, i.uv);
                return lerp(tex1, tex2, tex2.a);
            }
			ENDCG
		}

		

	}
}
