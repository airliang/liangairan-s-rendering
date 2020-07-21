// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/postprocess/outline" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1,1,1,1)
        _NormalScale("Normal Scale", Range(0,2)) = 0
        _OutlineTex("Outline Texture", 2D) = "black"
        _SubTex("Subtract Texture", 2D) = "black"
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        //pass 1
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Front
            ZWrite Off
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers xbox360 flash	

            #define PI 3.14159265359

            float4 _OutlineColor;
            float _NormalScale;

            struct appdata_outline
            {
                half4 vertex : POSITION;
                half3 normal : NORMAL;
            };

            struct VSOut
            {
                half4 pos		: SV_POSITION;
                //half3 normalWorld : TEXCOORD0;
            };


            VSOut vert(appdata_outline v)
            {
                VSOut o;
                float4 view_vertex = mul(UNITY_MATRIX_MV, v.vertex);
                float3 view_normal = mul(UNITY_MATRIX_IT_MV, v.normal);
                view_vertex.xyz += normalize(view_normal) * _NormalScale; //记得normalize
                o.pos = mul(UNITY_MATRIX_P, view_vertex);
                //o.pos = UnityObjectToClipPos(v.vertex);
           
                return o;
            }

            half4 frag(VSOut i) : COLOR
            {
                return _OutlineColor;
            }
            ENDCG
        }
        //pass 2
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Front
            ZWrite Off
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers xbox360 flash	

            #define PI 3.14159265359

            float4 _OutlineColor;

            struct appdata
            {
                half4 vertex : POSITION;
            };

            struct VSOut
            {
                half4 pos		: SV_POSITION;
            };


            VSOut vert(appdata v)
            {
                VSOut o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(VSOut i) : COLOR
            {
                return _OutlineColor;
            }
                ENDCG
        }


        //pass 3
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Front
            ZWrite Off
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers xbox360 flash	

            #define PI 3.14159265359

            sampler2D _OutlineTex;
            sampler2D _SubTex;

            struct appdata
            {
                half4 vertex : POSITION;
                half2 uv : TEXCOORD0;
            };

            struct VSOut
            {
                half4 pos		: SV_POSITION;
                half2 uv : TEXCOORD0;
            };


            VSOut vert(appdata v)
            {
                VSOut o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag(VSOut i) : COLOR
            {
                float4 outlineColor = tex2D(_OutlineTex, i.uv);
                float4 noOutlineColor = tex2D(_SubTex, i.uv);
                return outlineColor - noOutlineColor;
            }
            ENDCG
        }
	}
    FallBack "Diffuse"
}