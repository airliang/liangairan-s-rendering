#ifndef TERRAIN_CORE_INCLUDED
#define TERRAIN_CORE_INCLUDED

#include "AutoLight.cginc"
#include "UnityCG.cginc"
#include "../Fog/FogCore.cginc"
#include "../Shadow/shadowmap.cginc"

#define COL 4
#define ROW 4
#define COLROW float2(COL,ROW)
#define INV_COLROW float2(0.25, 0.25)
#define SIZE 16

#define MIPMAP_COUNT(tex) (log2( max(tex##_TexelSize.z, tex##_TexelSize.w) ))

float MIPMAP_LEVEL(float2 uv)
{
	float2 dx = ddx(uv);
	float2 dy = ddy(uv);
	float max_dxdy_sqr = max(dot(dx, dx), dot(dy, dy));
	return 0.5 * log2(max_dxdy_sqr); // <==> log2(sqrt(max_dxdy_sqr));
}

	//Variants
	half4 _TintColor;
	fixed _Shadow;

#ifdef _ALPHA
	fixed _Cutoff;
#endif

#ifdef _USEREFL
	samplerCUBE _ReflTex;
	half4 _ReflTex_ST;
	fixed _MetalV;
	fixed _FresnelRange;
#endif //_USEREFL

#ifdef _LIGHTMAP
	sampler2D _Lightmap;
	half _LightmapContrast;
	half _LightmapBrightness;
	half _DesaturateLightmap;
#endif //_LIGHTMAP

//texture
#ifdef USE_2DARRAY
UNITY_DECLARE_TEX2DARRAY(_MainTexArray);
#else
sampler2D _MainTex;
float4 _MainTex_TexelSize;
#endif //USE_2DARRAY

#ifdef USE_NORMAL

	half _Shininess;
	half4 _SpecColor;
	half _LightmapClamp;

	#ifdef USE_2DARRAY
	UNITY_DECLARE_TEX2DARRAY(_BumpMapArray);
	#else //Atlas
	sampler2D _BumpMap;
	#endif //USE_2DARRAY

#endif //USE_NORMAL

#ifdef USE_VERTEXCOLOR
fixed4 _Index;
#else
sampler2D _Splat;
float4 _Splat_TexelSize;
#endif

//color
float4 _LightColor0;
//half _ShadowDistance,_ShadowFade;

//in
struct CustomVertexInput
{
	float4 vertex	: POSITION;
	half3 normal	: NORMAL;
	float2 uv0		: TEXCOORD0;
	float2 uv1		: TEXCOORD1;
#if _MULTIUVTYPE_B
	half2 uv2		: TEXCOORD2;
#endif
	half4 tangent	: TANGENT;
	UNITY_VERTEX_INPUT_INSTANCE_ID
#ifdef USE_VERTEXCOLOR
	fixed4 color	: COLOR;
#endif
};

//out
struct CustomVertexOutputForwardBase
{
	UNITY_POSITION(pos);
	float4 uv							  : TEXCOORD0;	  //VertexUV.xy | HighMask.z
	float4 tangentToWorldAndPackedData[3] : TEXCOORD1;	  // [3x3:tangentToWorld | 1x3:viewDirForParallax or worldPos]
	half4 fogCoord						  : TEXCOORD4;
	MY_SHADOW_COORDS(5)
#if _MULTIUVTYPE_B
	half2 uv2							  : TEXCOORD7;
#endif //_MULTIUVTYPE_B
	UNITY_VERTEX_INPUT_INSTANCE_ID
#ifdef USE_VERTEXCOLOR
	fixed4 color						  : COLOR0;
#endif //USE_VERTEXCOLOR
};

#ifndef USE_2DARRAY
#if _SPLAT_X4
	void RemapUV(float2 uv, uint4 index, out float4 uv1, out float4 uv2, out half4 lod)
	{
		uint4 row = index / ROW;
		uint4 col = index % ROW;
		uv1.xy = uv.xy * _Scale[row.x][col.x];
		uv1.zw = uv.xy * _Scale[row.y][col.y];
		uv2.xy = uv.xy * _Scale[row.z][col.z];
		uv2.zw = uv.xy * _Scale[row.w][col.w];

		float mipCount = MIPMAP_COUNT(_MainTex);
		float4 uv1_continuous = uv1 * INV_COLROW.xyxy;
		float4 uv2_continuous = uv2 * INV_COLROW.xyxy;
		lod = half4(
			MIPMAP_LEVEL(uv1_continuous.xy), 
			MIPMAP_LEVEL(uv1_continuous.zw),
			MIPMAP_LEVEL(uv2_continuous.xy),
			MIPMAP_LEVEL(uv2_continuous.zw));
		lod += mipCount;
		lod = clamp(lod, 0, mipCount - 4);

		uv1 = frac(uv1) * INV_COLROW.xyxy;
		uv2 = frac(uv2) * INV_COLROW.xyxy;

		float4 texelSizeLod = pow(2, lod);
		float4 texelSizeLod1 = _MainTex_TexelSize.xyxy * texelSizeLod.xxyy;
		float4 texelSizeLod2 = _MainTex_TexelSize.xyxy * texelSizeLod.zzww;

		float4 offset1 = texelSizeLod1 * 0.5;
		float4 offset2 = texelSizeLod2 * 0.5;
		uv1 = clamp(uv1, offset1, INV_COLROW.xyxy - offset1);
		uv2 = clamp(uv2, offset2, INV_COLROW.xyxy - offset2);
		uv1.xzyw += float4(col.xy, row.xy) * INV_COLROW.xxyy;
		uv2.xzyw += float4(col.zw, row.zw) * INV_COLROW.xxyy;
		#if SHADER_API_METAL
		uv1.yw = 1 - uv1.yw;
		uv2.yw = 1 - uv2.yw;
		#endif //SHADER_API_METAL
	}
#else // Splat2
	void RemapUV(float2 uv, uint2 index, out float4 uv1, out half2 lod)
	{
		uint2 row = index / ROW;
		uint2 col = index % ROW;
		uv1.xy = uv.xy * _Scale[row.x][col.x];
		uv1.zw = uv.xy * _Scale[row.y][col.y];

		float mipCount = MIPMAP_COUNT(_MainTex);
		float4 uv1_continuous = uv1 * INV_COLROW.xyxy;
		lod = half2(
			MIPMAP_LEVEL(uv1_continuous.xy), 
			MIPMAP_LEVEL(uv1_continuous.zw));
		lod += mipCount;
		lod = clamp(lod, 0, mipCount - 4);
		lod = floor(lod);

		uv1 = frac(uv1) * INV_COLROW.xyxy;

		float4 texelSizeLod = _MainTex_TexelSize.xyxy * pow(2, lod).xxyy;

		float4 offset = texelSizeLod * 0.5;
		uv1 = clamp(uv1, offset, INV_COLROW.xyxy - offset);
		uv1.xzyw += float4(col, row) * INV_COLROW.xxyy;
		#if SHADER_API_METAL
		uv1.yw = 1 - uv1.yw;
		#endif //SHADER_API_METAL
	}
#endif //_SPLAT_X4
#endif //!USE_2DARRAY

//vs
CustomVertexOutputForwardBase CustomvertBase(CustomVertexInput v)
{
	UNITY_SETUP_INSTANCE_ID(v);
	CustomVertexOutputForwardBase o;
	UNITY_INITIALIZE_OUTPUT(CustomVertexOutputForwardBase, o);
	UNITY_TRANSFER_INSTANCE_ID(v, o);

	float4 posWorld = mul(unity_ObjectToWorld, v.vertex);
	o.tangentToWorldAndPackedData[0].w = posWorld.x;
	o.tangentToWorldAndPackedData[1].w = posWorld.y;
	o.tangentToWorldAndPackedData[2].w = posWorld.z;
	o.pos = UnityObjectToClipPos(v.vertex);

	o.uv.xy = v.uv0.xy;
	o.uv.zw = v.uv1.xy;

#if _MULTIUVTYPE_B
	o.uv2 = v.uv2.xy;
#endif
   
	half3 normalWorld = UnityObjectToWorldNormal(v.normal);
	half4 tangentWorld = half4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
	half sign = tangentWorld.w * unity_WorldTransformParams.w;
	half3 binormal = cross(normalWorld, tangentWorld.xyz) * sign;
	half3x3 tangentToWorld = half3x3(tangentWorld.xyz, binormal, normalWorld);
	o.tangentToWorldAndPackedData[0].xyz = tangentToWorld[0];
	o.tangentToWorldAndPackedData[1].xyz = tangentToWorld[1];
	o.tangentToWorldAndPackedData[2].xyz = tangentToWorld[2];
	
#ifdef USE_VERTEXCOLOR
	o.color = v.color;
#endif
	TRANSFER_MY_SHADOW(o, posWorld)
	o.fogCoord = GetFogCoord(o.pos, posWorld);
	return o;
}

//Base
half4 CustomfragBase(CustomVertexOutputForwardBase i) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(i);

	float2 splatUV = i.uv.xy;
	float2 tex2UV = i.uv.xy;

	fixed4 albedo = 0;
#ifdef USE_NORMAL
	float4 bump = 0;
#endif //USE_NORMAL
	fixed4 splat = 0;
	uint4 idx = 0;

#if _MULTIUVTYPE_B
	splatUV = i.uv2.xy;
	tex2UV = i.uv.zw;
#endif

half3 norDir = i.tangentToWorldAndPackedData[2].xyz;

#ifdef USE_VERTEXCOLOR
	idx = _Index;
	#if _SPLAT_X4
	splat = i.color;
	#else //_SPLAT_X2
	splat = i.color.r;
	#endif //_SPLAT_X4
#else
	float2 pointSplatUV = (floor(splatUV * _Splat_TexelSize.zw) + 0.5) * _Splat_TexelSize.xy;
	fixed3 pointSplat = tex2D(_Splat, pointSplatUV, 0, 0).rgb;
	fixed3 filteredSplat = tex2D(_Splat, splatUV, 0, 0).rgb;
	splat.rg = pointSplat.rg;
	splat.b = lerp(pointSplat.b, filteredSplat.b, step(length(pointSplat.rg - filteredSplat.rg), 0));
	idx.xy = splat.rg * SIZE;
#endif //USE_VERTEXCOLOR

#if _SPLAT_X4
	fixed4x4 albedotex;
#ifdef USE_2DARRAY
	albedotex[0] = UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, float3(i.uv.xy * _Scale[idx.x / 4][idx.x % 4], idx.x));
	albedotex[1] = UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, float3(i.uv.xy * _Scale[idx.y / 4][idx.y % 4], idx.y));
	albedotex[2] = UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, float3(i.uv.xy * _Scale[idx.z / 4][idx.z % 4], idx.z));
	albedotex[3] = UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, float3(i.uv.xy * _Scale[idx.w / 4][idx.w % 4], idx.w));
#else //!USE_2DARRAY
	float4 uv1, uv2;
	half4 lod;
	RemapUV(i.uv, idx, uv1, uv2, lod);
	albedotex[0] = tex2Dlod(_MainTex, float4(uv1.xy, lod.rr));
	albedotex[1] = tex2Dlod(_MainTex, float4(uv1.zw, lod.gg));
	albedotex[2] = tex2Dlod(_MainTex, float4(uv2.xy, lod.bb));
	albedotex[3] = tex2Dlod(_MainTex, float4(uv2.zw, lod.aa));
#endif //USE_2DARRAY
	albedo = lerp(albedotex[0], albedotex[3], splat.a);
	albedo = lerp(albedo, albedotex[2], splat.b);
	albedo = lerp(albedo, albedotex[1], splat.g);
	albedo = lerp(albedo, albedotex[0], splat.r);

#ifdef USE_NORMAL
	fixed4x4 bumptex;
#ifdef USE_2DARRAY
	bumptex[0] = UNITY_SAMPLE_TEX2DARRAY(_BumpMapArray, float3(i.uv.xy * _Scale[idx.x / 4][idx.x % 4], idx.x));
	bumptex[1] = UNITY_SAMPLE_TEX2DARRAY(_BumpMapArray, float3(i.uv.xy * _Scale[idx.y / 4][idx.y % 4], idx.y));
	bumptex[2] = UNITY_SAMPLE_TEX2DARRAY(_BumpMapArray, float3(i.uv.xy * _Scale[idx.z / 4][idx.z % 4], idx.z));
	bumptex[3] = UNITY_SAMPLE_TEX2DARRAY(_BumpMapArray, float3(i.uv.xy * _Scale[idx.w / 4][idx.w % 4], idx.w));
#else //!USE_2DARRAY
	bumptex[0] = tex2Dlod(_BumpMap, float4(uv1.xy, lod.rr));
	bumptex[1] = tex2Dlod(_BumpMap, float4(uv1.zw, lod.gg));
	bumptex[2] = tex2Dlod(_BumpMap, float4(uv2.xy, lod.bb));
	bumptex[3] = tex2Dlod(_BumpMap, float4(uv2.zw, lod.aa));
#endif //USE_2DARRAY
	bump = lerp(bumptex[0], bumptex[3], splat.a);
	bump = lerp(bump, bumptex[2], splat.b);
	bump = lerp(bump, bumptex[1], splat.g);
	bump = lerp(bump, bumptex[0], splat.r);
#endif // USE_NORMAL

#else // Splat2

#ifdef USE_2DARRAY
	albedo = lerp(
		UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, float3(i.uv.xy * _Scale[idx.x / 4][idx.x % 4], idx.x)),
		UNITY_SAMPLE_TEX2DARRAY(_MainTexArray, float3(i.uv.xy * _Scale[idx.y / 4][idx.y % 4], idx.y)),
		splat.b);
#else //!USE_2DARRAY
	float4 uv1;
	half2 lod;
	RemapUV(i.uv, idx.rg, uv1, lod);

	albedo = lerp(
		tex2Dlod(_MainTex, float4(uv1.xy, lod.rr)), 
		tex2Dlod(_MainTex, float4(uv1.zw, lod.gg)),
		splat.b);
#endif //USE_2DARRAY

#ifdef USE_NORMAL
#ifdef USE_2DARRAY
	bump = lerp(
		UNITY_SAMPLE_TEX2DARRAY(_BumpMapArray, float3(i.uv.xy * _Scale[idx.x / 4][idx.x % 4], idx.x)),
		UNITY_SAMPLE_TEX2DARRAY(_BumpMapArray, float3(i.uv.xy * _Scale[idx.y / 4][idx.y % 4], idx.y)),
		splat.b);
#else //!USE_2DARRAY
	bump = lerp(
		tex2Dlod(_BumpMap, float4(uv1.xy, lod.rr)), 
		tex2Dlod(_BumpMap, float4(uv1.zw, lod.gg)),
		splat.b);
#endif //USE_2DARRAY
	half3 tangent = i.tangentToWorldAndPackedData[0].xyz;
	half3 binormal = i.tangentToWorldAndPackedData[1].xyz;
	half3 unpackNor = bump.rgb * 2 - 1;//UnpackNormal(bump);
	norDir = normalize(tangent * unpackNor.x + binormal * unpackNor.y + i.tangentToWorldAndPackedData[2].xyz * unpackNor.z);
#endif // USE_NORMAL

#endif //_SPLAT_X4

#ifdef _LIGHTMAP
	float4 lightmap = tex2D(_Lightmap,i.uv.zw);
#endif	

	//dir
	float3 posWorld = float3(i.tangentToWorldAndPackedData[0].w, i.tangentToWorldAndPackedData[1].w, i.tangentToWorldAndPackedData[2].w);
	float NoL = saturate(dot(norDir, _WorldSpaceLightPos0.xyz));
	float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - posWorld);

#ifdef _USEREFL
	//Fresnel
	float NoV = saturate(dot(norDir, viewDir));
	float Fresnel = pow((1.0 - NoV)*NoL, _FresnelRange);

	float3 reflDir = normalize(reflect(-viewDir, norDir));
	float perceptualRoughness = albedo.a + (1 - _MetalV)*floor(albedo.a + _MetalV);
	float3 reflspec = texCUBElod(_ReflTex, fixed4(reflDir.xyz, 6)).rgb;

	reflspec = pow(reflspec, 1.4);
#endif

	//ambient SH9
	float3 GIindirectDiff = ShadeSH9 (float4(norDir,1.0)); 
	float3 lmcolor = 1;

	//shadow
#ifdef _RECEIVESHADOW
	float ReceiveShadow = MY_SHADOW_ATTENTION(i, norDir, posWorld.xyz)//getShadowAttention(i.uvProj, i.depth.xy, norDir, posWorld.xyz);
	ReceiveShadow *= NoL;
	
	float3 ShadowColor = ReceiveShadow + (1-ReceiveShadow)*_ShadowColor.rgb;
#endif
	
	//fianl
	float3 sAlbedo = albedo*_TintColor.rgb;
#ifdef _LIGHTMAP
#ifdef _RECEIVESHADOW
#endif
	sAlbedo *= lmcolor;
#endif	
   
	half3 lightcolor = _LightColor0.rgb * NoL + UNITY_LIGHTMODEL_AMBIENT.rgb;
	float3 finalColor = sAlbedo * lightcolor;

#ifdef _RECEIVESHADOW
	finalColor *= ShadowColor;
#endif

#ifdef USE_NORMAL
	float3 LoV = normalize(viewDir + _WorldSpaceLightPos0.xyz);
	float NoH = saturate(dot(norDir, LoV));
	half3 specularTerm = pow(NoH, 128 * _Shininess) * bump.a;
	finalColor += _SpecColor.rgb * specularTerm;
#endif

#ifdef _USEREFL
	float Term = pow(perceptualRoughness, _MetalV)*Fresnel;
#ifdef _LIGHTMAP
	Term = saturate(Term*lmshadow);
#endif
	finalColor = lerp(finalColor, reflspec, Term);
#endif

	float finalAlpha = albedo.a;

#ifdef _ALPHA
	clip(finalAlpha - _Cutoff);
#endif

	finalColor.rgb = ApplySunFog(finalColor.rgb, i.fogCoord, viewDir);

	return saturate(half4(finalColor,finalAlpha));
}

#endif // TERRAIN_CORE_INCLUDED
