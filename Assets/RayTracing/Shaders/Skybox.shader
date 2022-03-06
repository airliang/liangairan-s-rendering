Shader "RayTracing/SkyboxHDR"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [Gamma] _Exposure("Exposure", Range(0, 8)) = 1.0

    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            //#pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float3 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            half4 _MainTex_HDR;
            half _Exposure;
            

            inline float2 DirectionToPolar(float3 direction)
            {
                float3 normalizedCoords = normalize(direction);
                float latitude = acos(normalizedCoords.y);
                float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
                float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                return float2(0.5, 1.0) - sphereCoords;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = o.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // sample the texture
                float2 uv = DirectionToPolar(i.uv);
                half4 col = tex2D(_MainTex, uv);
                half3 c = DecodeHDR(col, _MainTex_HDR);
                c *= _Exposure;
                return half4(c, 1);
            }
            ENDCG
        }
    }
}
