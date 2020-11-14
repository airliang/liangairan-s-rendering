Shader "liangairan/shadow/RenderToShadow" 
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Shadow" = "Character"}
        Pass
        {
            Name "AlphaTest"
            ColorMask 0
            Cull Off
            ZClip Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed _Cutoff;

            v2f_img vert(appdata_img v)
            {
                v2f_img o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                clip(tex2D(_MainTex, i.uv).a - _Cutoff);
                return 0;
            }
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Shadow" = "Character"}
        Pass
        {
            Name "Opaque"
            ColorMask 0
            Offset 1, 1
            ZClip Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert (float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }
            
            fixed4 frag () : SV_Target { return 0; }
            ENDCG
        }
    }
}