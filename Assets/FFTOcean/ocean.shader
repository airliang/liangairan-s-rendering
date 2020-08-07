// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/ocean/ocean" {
// 　　　　　　D(h) F(v,h) G(l,v,h)
//f(l,v) = ---------------------------
// 　　　　　　4(n·l)(n·v)
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
        _skyColor("Sky Color", Color) = (1,1,1,1)
        _skyCube("sky Cubemap", Cube) = ""{}
        //_NoiseMap("Noise", 2D) = "bump" {}
        _SurfaceMap("SurfaceMap", 2D) = "white" {}
        _NoiseMap("Flow (RG A-noise)", 2D) = "black" {}
        _BumpScale("Bump Scale", Range(0, 2)) = 0.35
		_Wave1("Wave1 wavelength y-amplitude z-speed w-waveNum",Vector) = (2, 1, 10, 1)
		_Wave2("Wave2",Vector) = (20,0.3,30,0.1)
		_Wave3("Wave3",Vector) = (15,1.2,20,0.5)

        _Circle1("Circle point1", Vector) = (0.2, 0.2, 1, 1)
        _Circle2("Circle point2", Vector) = (0.82, 0.3, 1, 1)
        _Circle3("Circle point3", Vector) = (0.6, 0.75, 1, 1)
        _WaterSize("Water resolution", Float) = 1024
		_Transparent("Transparent", Float) = 0.7
        _SeaBaseColor("Sea base color", Color) = (0.1, 0.19, 0.22, 1)
        _SeaWaterColor("Sea diffuse color", Color) = (0.8, 0.9, 0.6, 1)
        _SeaFogColor("Sea fog color", Color) = (0.8, 0.9, 0.6, 1)
            _seaFogDistance("Fog Start Distance", Float) = 300
        //_Tess("Tessellation", Range(1,32)) = 4
	}
	SubShader {
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent"}
		LOD 200
		

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
			//Blend SrcAlpha OneMinusSrcAlpha
			//ZTest Off
			//ZWrite Off
            Cull off

            CGPROGRAM
            
#include "ocean_core.cginc"
            #pragma target 3.0
            //#pragma tessellate:tessFixed
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers xbox360 flash	
            #pragma multi_compile_fwdbase 
            //#pragma multi_compile _ INFINITE_OCEAN
            
            #pragma multi_compile _ CIRCLE_WAVE
            #pragma multi_compile _ GERSTNER_WAVE FFT_WAVE

//#define CIRCLE_WAVE
            
            ENDCG
        }
	}
    FallBack "Diffuse"
}