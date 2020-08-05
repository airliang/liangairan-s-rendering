// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/ocean/wireframe" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
		_Color ("Color", Color) = (0,0,0,1)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
		//_Wave1("Wave1 wavelength y-amplitude z-speed w-waveNum",Vector) = (2, 1, 10, 1)
		//_Wave2("Wave2",Vector) = (20,0.3,30,0.1)
		//_Wave3("Wave3",Vector) = (15,1.2,20,0.5)
        _Thickness("Thickness", Range(0,1000)) = 255
        _Firmness("Firmness", Range(0,1000)) = 255
	}
	SubShader {
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent"}
		LOD 200

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert_wireframe
            #pragma geometry geom
            #pragma fragment frag_wireframe
            #pragma exclude_renderers xbox360 flash	

			#pragma multi_compile _ INFINITE_OCEAN
			#pragma multi_compile _ CIRCLE_WAVE
            #include "gerstner_wave.cginc"


            ENDCG
        }
	}
    FallBack "Diffuse"
}