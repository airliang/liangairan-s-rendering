Shader "RayTracing/Disney"
{
    Properties
    {
        _baseColor("BaseColor", Color) = (1, 1, 1, 1)
        _metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _specular("Specular", Range(0.0, 1.0)) = 0.0
        _roughness("Roughness", Range(0.0, 1.0)) = 0
        _specularTint("SpecularTint", Range(0.0, 1.0)) = 0
        _anisotropy("Anisotropy", Range(0.0, 1.0)) = 0
        _sheen("Sheen", Range(0.0, 1.0)) = 0
        _sheenTint("SheenTint", Range(0.0, 1.0)) = 0
        _clearcoat("Clearcoat", Range(0.0, 1.0)) = 0
        _clearcoatGloss("ClearcoatGloss", Range(0.0, 1.0)) = 0
        _ior("IOR", Float) = 1.0
        _specularTransmission("SpecularTransmission", Range(0.0, 1.0)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            //sampler2D _MainTex;
            //float4 _MainTex_ST;
            float4 _baseColor;
            float  _metallic;
            float  _specular;
            float  _roughness;
            float  _specularTint;
            float  _anisotropy;
            float  _sheen;
            float  _sheenTint;
            float  _clearcoat;
            float  _clearcoatGloss;
            float  _ior;
            float  _specularTransmission;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                //o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = _baseColor;//tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
