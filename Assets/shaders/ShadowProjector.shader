// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/shadow/ShadowProjector" 
{

	Properties{
        //_ShadowmapTex("ShadowMap", 2D) = "gray" {}
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
            #include "shadowmap.cginc"
            #pragma vertex vert  
            #pragma fragment frag
            //sampler2D _ShadowmapTex;
            uniform float4x4 unity_Projector;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 uvShadow : TEXCOORD0;
                //float2 depth : TEXCOORD0;
            };


            v2f vert(appdata_img v)
            {
                v2f o = (v2f)0;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uvShadow = mul(unity_Projector, v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 shadow = tex2Dproj(_ShadowmapTex, UNITY_PROJ_COORD(i.uvShadow));
                return shadow;
            }
			ENDCG
		}

		

	}
}
