// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "liangairan/postprocess/FXAA" 
{

	Properties{
        _MainTex("Base (RGB)", 2D) = "white" {}
	}

	CGINCLUDE
#include "UnityCG.cginc" 

#define EDGE_STEP_COUNT 4
#define EDGE_STEPS 1, 1.5, 2, 4
#define EDGE_GUESS 12
		static const float edgeSteps[EDGE_STEP_COUNT] = { EDGE_STEPS };

	sampler2D _MainTex;
	float4 _MainTex_TexelSize;

	float _ContrastThreshold;
	float _RelativeThreshold;
	float _SubpixelBlending;

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

	fixed4 luminance(v2f i) : SV_Target
	{
		fixed4 tex = tex2D(_MainTex, i.uv);

		return tex.ggga;
	}

	float SampleLuminance(float2 uv, float uOffset, float vOffset) 
	{
		uv += _MainTex_TexelSize.xy * float2(uOffset, vOffset);
		return tex2D(_MainTex, uv).g;
	}

	float SampleG(float2 uv)
	{
		return tex2D(_MainTex, uv).g;
	}

	struct LuminanceData 
	{
		float m, n, e, s, w;
#ifndef LUMINANCE_GREEN
		float ne, nw, se, sw;
#endif
		float highest, lowest, contrast;
	};

	LuminanceData SampleLuminanceNeighborhood(float2 uv) {
		LuminanceData l;
		l.m = SampleLuminance(uv, 0, 0);
		l.n = SampleLuminance(uv, 0, 1);
		l.e = SampleLuminance(uv, 1, 0);
		l.s = SampleLuminance(uv, 0, -1);
		l.w = SampleLuminance(uv, -1, 0);
#ifndef LUMINANCE_GREEN
		l.ne = SampleLuminance(uv, 1, 1);
		l.nw = SampleLuminance(uv, -1, 1);
		l.se = SampleLuminance(uv, 1, -1);
		l.sw = SampleLuminance(uv, -1, -1);
#endif
		l.highest = max(max(max(max(l.n, l.e), l.s), l.w), l.m);
		l.lowest = min(min(min(min(l.n, l.e), l.s), l.w), l.m);
		l.contrast = l.highest - l.lowest;
		return l;
	}

	bool ShouldSkipPixel(LuminanceData l) {
		float threshold =
			max(_ContrastThreshold, _RelativeThreshold * l.highest);
		return l.contrast < threshold;
	}

	float DeterminePixelBlendFactor(LuminanceData l) {
#if	LUMINANCE_GREEN
		float filter = (l.n + l.e + l.s + l.w);
		filter *= 1.0 / 4;
#else
		float filter = 2 * (l.n + l.e + l.s + l.w);
		filter += l.ne + l.nw + l.se + l.sw;
		filter *= 1.0 / 12;
#endif
		filter = abs(filter - l.m);
		filter = saturate(filter / l.contrast);
		float blendFactor = smoothstep(0, 1, filter);
		//float blendFactor = filter;
		return blendFactor * blendFactor * _SubpixelBlending;
	}

	struct EdgeData {
		bool isHorizontal;
		float pixelStep;
		float gradient;
		float oppositeLuminance;
	};

	float DetermineEdgeBlendFactor(LuminanceData l, EdgeData e, float2 uv) 
	{
		float2 uvEdge = uv;
		float2 edgeStep;
		if (e.isHorizontal) {
			uvEdge.y += e.pixelStep * 0.5;
			edgeStep = float2(_MainTex_TexelSize.x, 0);
		}
		else {
			uvEdge.x += e.pixelStep * 0.5;
			edgeStep = float2(0, _MainTex_TexelSize.y);
		}

		float edgeLuminance = (l.m + e.oppositeLuminance) * 0.5;
		float gradientThreshold = e.gradient * 0.25;

		float2 puv = uvEdge + edgeStep * edgeSteps[0];
		float pLuminanceDelta = SampleG(puv) - edgeLuminance;
		bool pAtEnd = abs(pLuminanceDelta) >= gradientThreshold;

		UNITY_UNROLL
			for (int i = 1; i < EDGE_STEP_COUNT && !pAtEnd; i++) {
				puv += edgeStep * edgeSteps[i];
				pLuminanceDelta = SampleG(puv) - edgeLuminance;
				pAtEnd = abs(pLuminanceDelta) >= gradientThreshold;
			}
		if (!pAtEnd) {
			puv += edgeStep * EDGE_GUESS;
		}

		float2 nuv = uvEdge - edgeStep * edgeSteps[0];
		float nLuminanceDelta = SampleG(nuv) - edgeLuminance;
		bool nAtEnd = abs(nLuminanceDelta) >= gradientThreshold;

		UNITY_UNROLL
			for (int i = 1; i < EDGE_STEP_COUNT && !nAtEnd; i++) {
				nuv -= edgeStep * edgeSteps[i];
				nLuminanceDelta = SampleG(nuv) - edgeLuminance;
				nAtEnd = abs(nLuminanceDelta) >= gradientThreshold;
			}
		if (!nAtEnd) {
			nuv -= edgeStep * EDGE_GUESS;
		}

		float pDistance, nDistance;
		if (e.isHorizontal) {
			pDistance = puv.x - uv.x;
			nDistance = uv.x - nuv.x;
		}
		else {
			pDistance = puv.y - uv.y;
			nDistance = uv.y - nuv.y;
		}

		float shortestDistance;
		bool deltaSign;
		if (pDistance <= nDistance) {
			shortestDistance = pDistance;
			deltaSign = pLuminanceDelta >= 0;
		}
		else {
			shortestDistance = nDistance;
			deltaSign = nLuminanceDelta >= 0;
		}

		if (deltaSign == (l.m - edgeLuminance >= 0)) {
			return 0;
		}
		return 0.5 - shortestDistance / (pDistance + nDistance);
	}

#if	LUMINANCE_GREEN	
	EdgeData DetermineEdge(LuminanceData l) {
		EdgeData e;
		float horizontal = abs(l.n + l.s - 2 * l.m) * 2;
		float vertical = abs(l.e + l.w - 2 * l.m) * 2;
		e.isHorizontal = horizontal >= vertical;
		e.isHorizontal = horizontal >= vertical;

		float pLuminance = e.isHorizontal ? l.n : l.e;
		float nLuminance = e.isHorizontal ? l.s : l.w;
		float pGradient = abs(pLuminance - l.m);
		float nGradient = abs(nLuminance - l.m);

		e.pixelStep = e.isHorizontal ? _MainTex_TexelSize.y : _MainTex_TexelSize.x;

		if (pGradient < nGradient) {
			e.pixelStep = -e.pixelStep;
			e.oppositeLuminance = nLuminance;
			e.gradient = nGradient;
		}
		else
		{
			e.oppositeLuminance = pLuminance;
			e.gradient = pGradient;
		}

		return e;
	}

#else	
	EdgeData DetermineEdge(LuminanceData l) {
		EdgeData e;
		float horizontal =
			abs(l.n + l.s - 2 * l.m) * 2 +
			abs(l.ne + l.se - 2 * l.e) +
			abs(l.nw + l.sw - 2 * l.w);
		float vertical =
			abs(l.e + l.w - 2 * l.m) * 2 +
			abs(l.ne + l.nw - 2 * l.n) +
			abs(l.se + l.sw - 2 * l.s);
		e.isHorizontal = horizontal >= vertical;

		float pLuminance = e.isHorizontal ? l.n : l.e;
		float nLuminance = e.isHorizontal ? l.s : l.w;
		float pGradient = abs(pLuminance - l.m);
		float nGradient = abs(nLuminance - l.m);

		e.pixelStep =
			e.isHorizontal ? _MainTex_TexelSize.y : _MainTex_TexelSize.x;

		if (pGradient < nGradient) {
			e.pixelStep = -e.pixelStep;
			e.oppositeLuminance = nLuminance;
			e.gradient = nGradient;
		}
		else
		{
			e.oppositeLuminance = pLuminance;
			e.gradient = pGradient;
		}

		return e;
	}
#endif

	fixed4 fxaa(v2f i) : SV_Target
	{
		

		LuminanceData l = SampleLuminanceNeighborhood(i.uv);
		if (ShouldSkipPixel(l)) 
		{
			fixed4 tex = tex2D(_MainTex, i.uv);
			return tex;
		}
		float pixelBlend = DeterminePixelBlendFactor(l);
		EdgeData e = DetermineEdge(l);
		float edgeBlend = DetermineEdgeBlendFactor(l, e, i.uv);
		float finalBlend = max(pixelBlend, edgeBlend);

		if (e.isHorizontal) 
		{
			i.uv.y += e.pixelStep * finalBlend;
		}
		else 
		{
			i.uv.x += e.pixelStep * finalBlend;
		}
		fixed4 tex = tex2D(_MainTex, i.uv);
		return float4(tex.rgb, l.m);
	}
		ENDCG
	SubShader
	{
        Tags { "RenderType" = "Opaque" }
		Pass
		{
            ZWrite On
			CGPROGRAM
            
            
#pragma vertex vert  
#pragma fragment luminance
            
			ENDCG
		}

		Pass
		{
			ZWrite On
			CGPROGRAM
			#pragma multi_compile _ LUMINANCE_GREEN

#pragma vertex vert  
#pragma fragment fxaa

			ENDCG
		}

	}
	
}
