Shader "liangairan/2th_element/se_skin_transparent"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _RampTex("Ramp Tex", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue" = "Transparent"}
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_skin
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "include/se_core.cginc"

            
            ENDCG
        }
    }
}
