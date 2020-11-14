// Upgrade NOTE: replaced 'defined PCF_ON' with 'defined (PCF_ON)'
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
#pragma exclude_renderers d3d11 gles

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'
//#if SHADOW_NOSHADOW
//#define SHADOWMAP_OFF
//#else
//#define SHADOWMAP_ON
//#endif

#ifdef _SOFTSHADOW
#define PCF_ON
#define PCF_LERP
//#else
//#define PCF_OFF
#endif

uniform sampler2D _ShadowmapTex;


float3 lightPos;   //
float4 fDepthBias;
float3 shadowLightDirection;
//float4 shadowColor;

float4x4 LightProjectionMatrix;

//float shadowMin;
//float shadowAttentionDistance;   //衰减距离
//float followerToLight;            //跟随者到光源的距离

float4 shadowParams;

uniform float2 shadowMapTexel;
uniform float4 targetPositionAndDistance;
uniform half4 _ShadowColor;

#ifdef _CASCADE_SHADOW
uniform sampler2D _ClearShadowmapTex;
float4x4 LightProjectionMatrixNear;
uniform float2 shadowMapTexelClear;
uniform float2 cascadeEndClipSpace;
#endif


float4 getShadowmapTexel(sampler2D shadowmap, float4 uv)
{
	return tex2Dproj(shadowmap, uv);
}

//阴影比较函数，是一个单位阶跃，非0即1
//return 1-in shadowmap 0-not in shadow
float shadowCompare(sampler2D shadowmap, float4 uv, float2 offset, float currentDepth, float bias, float2 texel)
{
	float pcfDepth = getShadowmapTexel(shadowmap, float4(uv.xy + offset * texel, uv.zw)).r;
#if SHADER_API_D3D11 || SHADER_API_METAL
	return step(currentDepth + bias, pcfDepth);
#else
	return step(pcfDepth, currentDepth - bias);
#endif
}

float shadowCompareLerp(sampler2D shadowmap, float4 uv, float2 offset, float currentDepth, float bias, float2 texel)
{
	float2 texelSize = 1.0 / texel;
	float2 sampleUV = uv.xy + offset * texel;
	float4 uv00 = float4(floor(sampleUV * texelSize) * texel, uv.zw);
	float2 dudv = frac(sampleUV * texelSize);

	float c00 = shadowCompare(shadowmap, uv00, float2(0, 0), currentDepth, bias, texel);
	float c01 = shadowCompare(shadowmap, uv00, float2(0, 1), currentDepth, bias, texel);
	float c10 = shadowCompare(shadowmap, uv00, float2(1, 0), currentDepth, bias, texel);
	float c11 = shadowCompare(shadowmap, uv00, float2(1, 1), currentDepth, bias, texel);
	return (1.0 - dudv.x) * (1.0 - dudv.y) * c00 + (1.0 - dudv.x) * dudv.y * c01
		+ dudv.x * (1.0 - dudv.y) * c10 + dudv.x * dudv.y * c11;
}

float pcf_2x2filter(sampler2D shadowmap, float4 uvProj, float bias, float2 texel)
{
	float2 depth = uvProj.zw;
	float currentDepth = depth.x / depth.y;

	float sum = 0;

#ifdef PCF_LERP
	sum += shadowCompareLerp(shadowmap, uvProj, float2(-0.5, 0.5), currentDepth, bias, texel);
	sum += shadowCompareLerp(shadowmap, uvProj, float2(0.5, 0.5), currentDepth, bias, texel);
	sum += shadowCompareLerp(shadowmap, uvProj, float2(-0.5, -0.5), currentDepth, bias, texel);
	sum += shadowCompareLerp(shadowmap, uvProj, float2(0.5, -0.5), currentDepth, bias, texel);
#else
	sum += shadowCompare(shadowmap, uvProj, float2(-0.5, 0.5), currentDepth, bias, texel);
	sum += shadowCompare(shadowmap, uvProj, float2(0.5, 0.5), currentDepth, bias, texel);
	sum += shadowCompare(shadowmap, uvProj, float2(-0.5, -0.5), currentDepth, bias, texel);
	sum += shadowCompare(shadowmap, uvProj, float2(0.5, -0.5), currentDepth, bias, texel);
#endif

	return  sum * 0.25;
}

float pcf_5x5filter(sampler2D shadowmap, float4 uvProj, float bias, float2 texel)
{
	float2 depth = uvProj.zw;

	float currentDepth = depth.x / depth.y;
	//if (currentDepth > 1.0 || currentDepth < 0)
	//	return 1.0;

	float sum = 0;
	float x, y;	float nums = 0;	for (y = -2; y <= 2; y += 1)
	{
		for (x = -2; x <= 2; x += 1)
		{
#ifdef PCF_LERP
			sum += shadowCompareLerp(shadowmap, uvProj, float2(x, y), currentDepth, bias, texel);
#else
			sum += shadowCompare(shadowmap, uvProj, float2(x, y), currentDepth, bias, texel);
#endif
			nums++;
		}
	}
	return sum / nums;
}

float pcf_nofilter(sampler2D shadowmap, float4 uvProj, float bias, float2 texel)
{
	float2 depth = uvProj.zw;
	float currentDepth = depth.x / depth.y;
#ifdef PCF_LERP
	return shadowCompareLerp(shadowmap, uvProj, float2(0, 0), currentDepth, bias, texel);
#else
	return shadowCompare(shadowmap, uvProj, float2(0, 0), currentDepth, bias, texel);
#endif
}

static const half2 gaussianFilter[7] = 
{
	half2(-3.0, 0.015625),
	half2(-2.0, 0.09375),
	half2(-1.0, 0.234375),
	half2(0.0, 0.3125),
	half2(1.0, 0.234375),
	half2(2.0, 0.09375),
	half2(3.0, 0.015625)
};

float pcf_gaussian_filter(sampler2D shadowmap, float4 uvProj, float2 depth, float bias, float2 texel)
{
	float currentDepth = depth.x / depth.y;

	float sum = 0;
	float x, y;
	for (y = 0; y < 7; y += 1)
	{
		for (x = 0; x < 7; ++x)
		{
			half u = gaussianFilter[x].x;
			half v = gaussianFilter[y].x;
			sum += shadowCompare(shadowmap, uvProj, float2(u, v), currentDepth, bias, texel);
		}
	}

	return sum;
}

static const half2 poissonDisk[4] =
{
	half2(-0.94201624, -0.39906216),
	half2(0.94558609, -0.76890725),
	half2(-0.094184101, -0.92938870),
	half2(0.34495938, 0.29387760)
};

float pcf_poisson_filter(sampler2D shadowmap, float4 uvProj, float bias, float2 texel)
{
	float2 depth = uvProj.zw;
	float currentDepth = depth.x / depth.y;
	float sum = 0;

	//float visibility = 1.0;
	for (int i = 0; i < 4; i++)
	{
#ifdef PCF_LERP
		sum += shadowCompareLerp(shadowmap, uvProj, poissonDisk[i], currentDepth, bias, texel);
#else
		sum += shadowCompare(shadowmap, uvProj, poissonDisk[i], currentDepth, bias, texel);
#endif
	}
	return sum * 0.25;
}

float vsm_filter(sampler2D shadowmap, float4 uvProj, float receiverToLight, float bias)
{
	float currentDepth = uvProj.z / uvProj.w;
	if (currentDepth > 1.0)
		return 1.0;
	float lit = (float)0.0f;
	float2 moments = tex2D(shadowmap, UNITY_PROJ_COORD(uvProj));

	//if (moments.x == 0)
	//{
	//	moments.x = 1;
	//}

	float E_x2 = moments.y;
	float Ex_2 = moments.x * moments.x;
	float variance = E_x2 - Ex_2;
	float mD = (moments.x - receiverToLight);
	float mD_2 = mD * mD;
	float p = variance / (variance + mD_2);
	lit = max(p, receiverToLight <= moments.x);

	//lit = receiverToLight <= moments.x ? 1 : p;

	return lit;
}

float mapShadowToRange(float shadowScale)
{
	float shadowMin = shadowParams.x;
	return (1.0 - shadowMin) * shadowScale + shadowMin;
}

float getShadowAttention(float4 uv, float3 normalWorld, float3 posWorld)
{
	
	float4x4 lightSpaceMatrix = LightProjectionMatrix;
	float2 texel = shadowMapTexel; //float2(1.0 / 1024, 1.0 / 1024);
#ifdef _CASCADE_SHADOW
	//float view2posDistance = distance(posWorld, _WorldSpaceCameraPos);
	float clipPosZ = uv.z / uv.w;
#if SHADER_API_D3D11 || SHADER_API_METAL
	float cascade = step(cascadeEndClipSpace.x, clipPosZ);

#else
	float cascade = step(clipPosZ, cascadeEndClipSpace.x);
	
#endif
	if (cascade == 1)
	{
		lightSpaceMatrix = LightProjectionMatrixNear;
		texel = shadowMapTexelClear;
		//shadowmap = _ClearShadowmapTex;
	}
	uv = mul(lightSpaceMatrix, float4(posWorld, 1));
#endif
	
	float2 depth = uv.zw;
#if SHADER_API_D3D11 || SHADER_API_METAL
	if (depth.x / depth.y < 0.0)
		return 1.0;
#else
	if (depth.x / depth.y > 1.0)
		return 1.0;
#endif

	
	float shadowScale = 0;

	//float x = uv.x / uv.w;
	//float y = uv.y / uv.w;
	//clip(x + 1);
	//clip(1 - x);
	//clip(y + 1);
	//clip(1 - y);
	//if ((x >= 0 && x <= 1) && (y >= 0 && y <= 1))
	float2 xy = float2(uv.xy / uv.w);
	float2 clampXY = saturate(xy);
	if (xy.x == clampXY.x && xy.y == clampXY.y)
	{
#ifdef _CASCADE_SHADOW		
		//shadowScale = cascade == 1 ? pcf_2x2filter(_ClearShadowmapTex, uv, bias, texel) : pcf_nofilter(_ShadowmapTex, uv, bias);
		//shadowScale = cascade == 1 ? pcf_poisson_filter(_ClearShadowmapTex, uv, bias, shadowMapTexelClear) : pcf_nofilter(_ShadowmapTex, uv, bias, shadowMapTexel);
		if (cascade == 1)
		{
			float bias = max(fDepthBias.x * (1.0 - dot(normalize(normalWorld), shadowLightDirection)), fDepthBias.y);
			shadowScale = pcf_poisson_filter(_ClearShadowmapTex, uv, bias, shadowMapTexelClear);
		}
		else
		{
			float bias = max(fDepthBias.z * (1.0 - dot(normalize(normalWorld), shadowLightDirection)), fDepthBias.w);
			shadowScale = pcf_nofilter(_ShadowmapTex, uv, bias, shadowMapTexel);
		}

		//shadowScale = cascade == 1 ? pcf_nofilter(_ClearShadowmapTex, uv, bias) : pcf_nofilter(_ShadowmapTex, uv, bias);;
#else
		float bias = max(fDepthBias.x * (1.0 - dot(normalize(normalWorld), shadowLightDirection)), fDepthBias.y);
		//shadowScale = pcf_nofilter(_ShadowmapTex, uv, bias, shadowMapTexel);
		//shadowScale = pcf_5x5filter(_ShadowmapTex, uv, bias, shadowMapTexel);
		shadowScale = pcf_poisson_filter(_ShadowmapTex, uv, bias, shadowMapTexel);
#endif
		//shadowScale = view2posDis < 10.0 ? pcf_5x5filter(_ClearShadowmapTex, uv, depth.xy, bias, shadowMapTexelClear) : pcf_5x5filter(_ShadowmapTex, uv, depth.xy, bias, shadowMapTexel);
	}

	float shadow = mapShadowToRange(1.0 - shadowScale);
#ifdef _CASCADE_SHADOW		
	if (cascade == 0)
	{
		float cameraDistance = length(_WorldSpaceCameraPos.xyz - posWorld);
		float shadowStart = 65.0;
		float shadowFull = 50.0;
		float a = (1.0 - shadow) / (shadowStart - shadowFull);
		float b = 1.0 - (1.0 - shadow) * shadowStart / (shadowStart - shadowFull);
		shadow = a * cameraDistance + b;
		return saturate(shadow);
	}
#endif
	return shadow;
}

#if defined(_RECEIVESHADOW)
#if !defined(_CASCADE_SHADOW)
#define TRANSFER_MY_SHADOW(a, posWorld) \
float4x4 projMatrix = mul(LightProjectionMatrix, unity_ObjectToWorld); \
a._ShadowCoord = mul(projMatrix, v.vertex); 
#define MY_SHADOW_ATTENTION(a, normalWorld, posWorld) getShadowAttention(a._ShadowCoord, normalWorld, posWorld);
#define MY_SHADOW_COORDS(idx1) float4 _ShadowCoord : TEXCOORD##idx1;
#else
#define TRANSFER_MY_SHADOW(a, posWorld) a._ShadowCoord = a.pos;
#define MY_SHADOW_ATTENTION(a, normalWorld, posWorld) getShadowAttention(a._ShadowCoord, normalWorld, posWorld);
#define MY_SHADOW_COORDS(idx1) float4 _ShadowCoord : TEXCOORD##idx1;
#endif
#else
#define TRANSFER_MY_SHADOW(a, posWorld)
#define MY_SHADOW_ATTENTION(a, normalWorld, posWorld) 1.0;
#define MY_SHADOW_COORDS(idx1)
#endif


