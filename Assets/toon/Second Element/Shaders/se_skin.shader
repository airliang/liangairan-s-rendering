Shader "liangairan/2th_element/se_skin"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _RampTex("Ramp Tex", 2D) = "white" {}
        _DiffuseSegment("Diffuse Segment", Vector) = (0.3,1.0, 1.0,1.0)
        _ShadowColor("Shadow Color", Color) = (1,1,1,1)
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("outline width", Range(0,5)) = 2
        ks("specular ks", Range(0,1)) = 0.5
        _shininess("specular shininess", Range(0, 64)) = 10
            _scaleTranslate("xy scale, zw-translate xy", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_skin
            // make fog work
            #pragma multi_compile_fog

//#define _USE_RAM

            #include "UnityCG.cginc"
            #include "se_core.cginc"

            
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Front
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma target 3.0
            #pragma vertex vert_outline
            #pragma fragment frag_outline
            #pragma exclude_renderers xbox360 flash	

            #include "se_core.cginc"
            ENDCG
        }
    }
}
