// Upgrade NOTE: replaced 'defined PCF_ON' with 'defined (PCF_ON)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


uniform sampler2D _ShadowmapTex;

float3 lightPos;   //
float fDepthBias;
float3 shadowLightDirection;

float4x4 LightProjectionMatrix;

uniform float2 shadowMapTexel;


float4 getShadowmapPixel(sampler2D shadowmap, float4 uv, float2 offset)
{
	return tex2Dproj(shadowmap, UNITY_PROJ_COORD(float4(uv.xy + offset * shadowMapTexel, uv.z, uv.w)));
}



float pcf_filter(sampler2D shadowmap, float4 uvProj, float2 depth, float bias)
{
	float4 shadow = tex2Dproj(shadowmap, UNITY_PROJ_COORD(uvProj));
	float closestDepth = shadow.x;//DecodeFloatRGBA(shadow);
	float d = 0; // DecodeFloatRGBA(shadow);
	float currentDepth = depth.x / depth.y;
	if (currentDepth > 1.0)
		return 1.0;
#ifdef PCF_ON
	float sum = 0;
	float x, y;	float nums = 0;	for (y = -2; y <= 2; y += 1)
	{
		for (x = -2; x <= 2; x += 1)
		{
			float4 temp = getShadowmapPixel(shadowmap, uvProj, float2(x, y));
			sum += (currentDepth - bias) > temp.x ? 0.2 : 0.0;
			//d += temp.x;
			//d = saturate(min(d, DecodeFloatRGBA(temp)));
			nums++;
		}
	}
	shadow.x = sum / nums;
				
#else
	shadow = tex2Dproj(shadowmap, UNITY_PROJ_COORD(uvProj));
	if (currentDepth - bias > closestDepth)
	{
		return 0.4;
	}
	//d = DecodeFloatRGBA(shadow);
#endif
	//d = DecodeFloatRGBA(shadow);
	d = shadow.x;
				
				
	float shadowScale = 1;
#if SHADER_API_D3D11
	//if (currentDepth <= d)
#else
	if (currentDepth - bias > d)
#endif
	{
		return saturate(1.0 - shadow.x);
		//shadowScale = 0.5;
	}

	return shadowScale;
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

float getShadowAttention(float4 uv, float2 depth, float3 normalWorld, float3 posWorld)
{
	if (uv.z / uv.w > 1.0)
		return 1.0;
	float bias = max(0.0005 * (1.0 - dot(normalize(normalWorld), shadowLightDirection)), fDepthBias);
	float shadowScale = 1;
#if VSM_ON
	float x = uv.x / uv.w;
	float y = uv.y / uv.w;
	if ((x >= 0 && x <= 1) && (y >= 0 && y <= 1))
		shadowScale = vsm_filter(_ShadowmapTex, uv, length(posWorld - lightPos), bias);
#else
	shadowScale = pcf_filter(_ShadowmapTex, uv, depth.xy, bias);
#endif

	return shadowScale;
}


#define TRANSFER_MY_SHADOW(a) float4x4 projMatrix = mul(LightProjectionMatrix, unity_ObjectToWorld); a.uvProj = mul(projMatrix, v.vertex); a.depth.xy = a.uvProj.zw;


