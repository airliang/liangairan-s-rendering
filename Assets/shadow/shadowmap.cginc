// Upgrade NOTE: replaced 'defined PCF_ON' with 'defined (PCF_ON)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


uniform sampler2D _ShadowmapTex;

float3 lightPos;   //
float2 fDepthBias;
float3 shadowLightDirection;
//float4 shadowColor;

float4x4 LightProjectionMatrix;
//float shadowMin;
//float shadowAttentionDistance;   //衰减距离
//float followerToLight;            //跟随者到光源的距离

float4 shadowParams;

uniform float2 shadowMapTexel;


float4 getShadowmapPixel(sampler2D shadowmap, float4 uv, float2 offset)
{
	return tex2Dproj(shadowmap, UNITY_PROJ_COORD(float4(uv.xy + offset * shadowMapTexel, uv.z, uv.w)));
}

float pcf_2x2filter(sampler2D shadowmap, float4 uvProj, float bias)
{
	float2 depth = uvProj.zw;
	float4 shadow = tex2Dproj(shadowmap, UNITY_PROJ_COORD(uvProj));
	float closestDepth = shadow.x;
	float d = 0; 
	float currentDepth = depth.x / depth.y;


    float sum = 0;


	float pcfDepth = getShadowmapPixel(shadowmap, uvProj, float2(-0.25, 0.25)).r;
#if SHADER_API_D3D11 || SHADER_API_METAL
	sum += step(currentDepth + bias, pcfDepth);
#else
	sum += step(pcfDepth, currentDepth - bias);
#endif
	pcfDepth = getShadowmapPixel(shadowmap, uvProj, float2(0.25, 0.25)).r;
#if SHADER_API_D3D11 || SHADER_API_METAL
	sum += step(currentDepth + bias, pcfDepth);
#else
	sum += step(pcfDepth, currentDepth - bias);
#endif
	pcfDepth = getShadowmapPixel(shadowmap, uvProj, float2(-0.25, -0.25)).r;
#if SHADER_API_D3D11 || SHADER_API_METAL
	sum += step(currentDepth + bias, pcfDepth);
#else
	sum += step(pcfDepth, currentDepth - bias);
#endif
	pcfDepth = getShadowmapPixel(shadowmap, uvProj, float2(0.25, -0.25)).r;
#if SHADER_API_D3D11 || SHADER_API_METAL
	sum += step(currentDepth + bias, pcfDepth);
#else
	sum += step(pcfDepth, currentDepth - bias);
#endif
	shadow.x = sum * 0.25;
	return shadow.x;
}

float pcf_5x5filter(sampler2D shadowmap, float4 uvProj, float bias)
{
    float2 depth = uvProj.zw;
	float4 shadow = tex2Dproj(shadowmap, UNITY_PROJ_COORD(uvProj));
	float closestDepth = shadow.x;//DecodeFloatRGBA(shadow);
	float d = 0; // DecodeFloatRGBA(shadow);
	float currentDepth = depth.x / depth.y;
	//if (currentDepth > 1.0 || currentDepth < 0)
	//	return 1.0;
#ifdef PCF_ON
	float sum = 0;
	float x, y;	float nums = 0;	for (y = -2; y <= 2; y += 1)
	{
		for (x = -2; x <= 2; x += 1)
		{
			float pcfDepth = getShadowmapPixel(shadowmap, uvProj, float2(x * 0.5, y * 0.5)).r;
#if SHADER_API_D3D11 || SHADER_API_METAL
			sum += step(currentDepth + bias, pcfDepth);
			//sum = nums;
#else
			sum += step(pcfDepth, currentDepth - bias);
			//sum = nums;
#endif
			//d += temp.x;
			//d = saturate(min(d, DecodeFloatRGBA(temp)));
			nums++;
		}
	}
	shadow.x = sum / nums;
				
#else
	//shadow = tex2Dproj(shadowmap, UNITY_PROJ_COORD(uvProj));
#if SHADER_API_D3D11 || SHADER_API_METAL
	if (currentDepth + bias < closestDepth)
#else
	if (currentDepth - bias > closestDepth)
#endif
	{
		return 1;
	}
	return 0;
#endif
				

	return shadow.x;
}

float shadow_filter(sampler2D shadowmap, float4 uvProj, float bias)
{
    float2 depth = uvProj.zw;
    float4 shadow = tex2Dproj(shadowmap, UNITY_PROJ_COORD(uvProj));
    float closestDepth = shadow.x;//DecodeFloatRGBA(shadow);
    float d = 0; // DecodeFloatRGBA(shadow);
    float currentDepth = depth.x; // / depth.y;

#if SHADER_API_D3D11 || SHADER_API_METAL
    if (currentDepth + bias < closestDepth)
#else
    if (currentDepth - bias > closestDepth)
#endif
    {
        return 1;
    }
    return 0;
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

float pcf_gaussian_filter(sampler2D shadowmap, float4 uvProj, float bias)
{
	float2 depth = uvProj.zw;
	float4 shadow = tex2Dproj(shadowmap, UNITY_PROJ_COORD(uvProj));
	float closestDepth = shadow.x;//DecodeFloatRGBA(shadow);
	float currentDepth = depth.x / depth.y;

	float sum = 0;
	float x, y;
	for (y = 0; y < 7; y += 1)
	{
		for (x = 0; x < 7; ++x)
		{
			half u = gaussianFilter[x].x;
			half v = gaussianFilter[y].x;
			float pcfDepth = getShadowmapPixel(shadowmap, uvProj, float2(u, v)).r;
#if SHADER_API_D3D11 || SHADER_API_METAL
			sum += step(currentDepth + bias, pcfDepth) * gaussianFilter[x].y * gaussianFilter[y].y;
#else
			sum += step(pcfDepth, currentDepth - bias) * gaussianFilter[x].y * gaussianFilter[y].y;
#endif
		}
	}

	return sum;
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
	float mD = (moments.x - currentDepth);
	float mD_2 = mD * mD;
	float p = variance / (variance + mD_2);
	lit = max(p, step(receiverToLight, moments.x));


	return lit;
}

float mapShadowToRange(float shadowScale)
{
	float shadowMin = shadowParams.x;
	return (1.0 - shadowMin) * shadowScale + shadowMin;
}

float getShadowAttention(float4 uv, float3 normalWorld, float3 posWorld)
{
	float2 depth = uv.zw;
#if SHADER_API_D3D11 || SHADER_API_METAL
	if (depth.x / depth.y < 0.0)
		return 1.0;
#else
	if (depth.x / depth.y > 1.0)
		return 1.0;
#endif

	float bias = max(fDepthBias.x * (1.0 - dot(normalize(normalWorld), shadowLightDirection)), fDepthBias.y);
	float shadowScale = 0;
#if VSM_ON
	float x = uv.x / uv.w;
	float y = uv.y / uv.w;
	if ((x >= 0 && x <= 1) && (y >= 0 && y <= 1))
		shadowScale = vsm_filter(_ShadowmapTex, uv, length(posWorld - lightPos), bias);
#else
	float x = uv.x / uv.w;
	float y = uv.y / uv.w;
	//clip(x);
	//clip(1 - x);
	//clip(y);
	//clip(1 - y);
	if ((x >= 0 && x <= 1) && (y >= 0 && y <= 1))
	{
#if PCF_ON
        shadowScale = pcf_5x5filter(_ShadowmapTex, uv, bias);
#else
		shadowScale = pcf_2x2filter(_ShadowmapTex, uv, bias);
#endif
        //shadowScale = shadow_filter(_ShadowmapTex, uv, bias);
    }
#endif

	
	float shadow = mapShadowToRange(1.0 - shadowScale);
	return shadow;
	float disReceiverToLight = length(posWorld - lightPos);
	float shadowAttentionDistance = shadowParams.y;
	float followerToLight = shadowParams.z;
	float colorScale = shadowParams.w;
	float shadowDownStart = 1.5f;
	float shadowFinal = 1.0 / (shadowAttentionDistance - shadowDownStart) - shadowDownStart / (shadowAttentionDistance - shadowDownStart);
	//return saturate(saturate(shadowFinal) + shadow);
	return saturate(saturate((1.0 / shadowAttentionDistance * disReceiverToLight - 1.0/* - (followerToLight + shadowAttentionDistance) / shadowAttentionDistance*/)) + shadow);
}

float getShadowAttentionEx(float4 uv, float3 normalWorld, float3 posWorld)
{
	float2 depth = uv.zw;
#if SHADER_API_D3D11 || SHADER_API_METAL
	if (depth.x / depth.y < 0.0)
		return 1.0;
#else
	if (depth.x / depth.y > 1.0)
		return 1.0;
#endif

	float bias = max(fDepthBias.x * (1.0 - dot(normalize(normalWorld), shadowLightDirection)), fDepthBias.y);
	float shadowScale = 1;

	float x = uv.x / uv.w;
	float y = uv.y / uv.w;
	//clip(x);
	//clip(1 - x);
	//clip(y);
	//clip(1 - y);
	if ((x >= 0 && x <= 1) && (y >= 0 && y <= 1))
	{
#if PCF_ON
		shadowScale = 0;
		for (y = 0; y < 7; y += 1)
		{
			for (x = 0; x < 7; ++x)
			{
				half u = gaussianFilter[x].x;
				half v = gaussianFilter[y].x;
				shadowScale += getShadowmapPixel(_ShadowmapTex, uv, float2(u, v)).r * gaussianFilter[x].y * gaussianFilter[y].y;

			}
		}
#endif
		//shadowScale = shadow_filter(_ShadowmapTex, uv, bias);
	}


	return shadowScale;
	float shadow = mapShadowToRange(1.0 - shadowScale);
	return shadow;
}


#define TRANSFER_MY_SHADOW(a, input) \
float4x4 projMatrix = mul(LightProjectionMatrix, unity_ObjectToWorld); \
a._ShadowCoord = mul(projMatrix, input.vertex);
#define MY_SHADOW_COORDS(idx1) float4 _ShadowCoord : TEXCOORD##idx1;
#define MY_SHADOW_ATTENTION(a, normalWorld, posWorld) getShadowAttention(a._ShadowCoord, normalWorld, posWorld);
