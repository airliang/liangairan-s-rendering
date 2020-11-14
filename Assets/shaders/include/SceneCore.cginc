// Upgrade NOTE: replaced 'defined _Matcap' with 'defined (_Matcap)'

#ifndef ENVCORE_INCLUDED
#define ENVCORE_INCLUDED

#include "AutoLight.cginc"
#include "UnityCG.cginc"
#include "../Fog/FogCore.cginc"
#include "../Shadow/shadowmap.cginc"

#ifdef _USEROCKGRASS
float _GrassHeightBlend;
float _GrassThreshold;
sampler2D _GrassTex;
half4 _GrassTex_ST;
#endif

float4 Omega_Lightmap_HDR;

#define PI 3.14159265359
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
#endif

#ifdef _LIGHTMAP
sampler2D _Lightmap;
half _LightmapContrast;
half _LightmapBrightness;
half _DesaturateLightmap;
half4 _LightmapParams;
#endif

#ifdef _SPOTLIGHT
half4 _SpotLightParams;
half _SpotRad;
half4 _SpotLightColor;
#endif

#ifdef _MATCAP
sampler2D _MatcapTex;
#endif


#ifdef _USENORMAL

half _LightmapClamp;

sampler2D _BumpMap;
half4 _BumpMap_ST;

#endif

#ifdef _SPEC
sampler2D _SpecularMap;
half4 _SpecularMap_ST;
half _Shininess;
half4 _SpecColor;
#endif

#ifdef _CUSTOM_SPEC
half _Shininess;
half4 _SpecColor;
#endif

//texture
//sampler2D _MainTex;
sampler2D _MainTex;
half4 _MainTex_ST;
//half4 _Diffuse_ST;


//color			
float4 _LightColor0;



//in
struct CustomVertexInput
{
	float4 vertex   : POSITION;
	half3 normal    : NORMAL;
	//half4 color     : COLOR0;
	float2 uv0      : TEXCOORD0;
#ifdef _LIGHTMAP
	float2 uv1      : TEXCOORD1;  //lightmap
#endif
#ifdef _USENORMAL
	half4 tangent   : TANGENT;
#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
			
//out
struct CustomVertexOutputForwardBase
{
	UNITY_POSITION(pos);
	float4 uv                                : TEXCOORD0;    //VertexUV.xy | HighMask.z
	float4 tangentToWorldAndPackedData[3]   : TEXCOORD1;    // [3x3:tangentToWorld | 1x3:viewDirForParallax or worldPos]
	//half4 outdoor : TEXCOORD4;
	half4 fogCoord	: TEXCOORD5;
	MY_SHADOW_COORDS(6)
};

float4 CalculateContrast( float contrastValue, float4 colorTarget )
		{
			float t = 0.5 * ( 1.0 - contrastValue );
			return mul( float4x4( contrastValue,0,0,t, 0,contrastValue,0,t, 0,0,contrastValue,t, 0,0,0,1 ), colorTarget );
		}

//vs
CustomVertexOutputForwardBase CustomvertBase(CustomVertexInput v)
{
	UNITY_SETUP_INSTANCE_ID(v);
	CustomVertexOutputForwardBase o;
	UNITY_INITIALIZE_OUTPUT(CustomVertexOutputForwardBase, o);
	//UNITY_TRANSFER_INSTANCE_ID(v, o);

	float4 posWorld = mul(unity_ObjectToWorld, v.vertex);
	o.tangentToWorldAndPackedData[0].w = posWorld.x;
	o.tangentToWorldAndPackedData[1].w = posWorld.y;
	o.tangentToWorldAndPackedData[2].w = posWorld.z;
	o.pos = UnityObjectToClipPos(v.vertex);

	o.uv.xy = v.uv0.xy;
#ifdef _LIGHTMAP
	o.uv.zw = v.uv1.xy;
#endif
	//o.outdoor = v.color;
	half3 normalWorld = UnityObjectToWorldNormal(v.normal);
#ifdef _USENORMAL
	half4 tangentWorld = half4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
	half sign = tangentWorld.w * unity_WorldTransformParams.w;
	half3 binormal = cross(normalWorld, tangentWorld.xyz) * sign;
	half3x3 tangentToWorld = half3x3(tangentWorld.xyz, binormal, normalWorld);
#else
	half3x3 tangentToWorld = half3x3(normalWorld, normalWorld, normalWorld);
#endif
	o.tangentToWorldAndPackedData[0].xyz = tangentToWorld[0];
	o.tangentToWorldAndPackedData[1].xyz = tangentToWorld[1];
	o.tangentToWorldAndPackedData[2].xyz = tangentToWorld[2];

	TRANSFER_MY_SHADOW(o, posWorld.xyz)
	o.fogCoord = GetFogCoord(o.pos, posWorld);
	return o;
}


//Base
half4 CustomfragBase(CustomVertexOutputForwardBase i) : COLOR
{
	//UNITY_SETUP_INSTANCE_ID(i);
	float2 splatUV = i.uv.xy;
	float2 tex2UV = i.uv.xy;
//#ifdef _RECEIVESHADOW
//	return i._ShadowCoord.z / i._ShadowCoord.w;
//#endif
	float4 albedo = tex2D(_MainTex, i.uv.xy * _MainTex_ST.xy);
#ifdef _ALPHA
	clip(albedo.a - _Cutoff);
#endif
	float3 posWorld = float3(i.tangentToWorldAndPackedData[0].w, i.tangentToWorldAndPackedData[1].w, i.tangentToWorldAndPackedData[2].w);
	half3 norDir = i.tangentToWorldAndPackedData[2].xyz;

#ifdef _USEROCKGRASS
	float4 grassCol = tex2D(_GrassTex, i.uv.xy * _GrassTex_ST.xy + _GrassTex_ST.zw);
	half ler = smoothstep(_GrassHeightBlend - 1, _GrassHeightBlend + 1, posWorld.y);
	ler = smoothstep(0, 1 - _GrassThreshold, ler * norDir.y);
	albedo = lerp(albedo, grassCol, ler);
#endif

	float GNoL = saturate(dot(norDir, _WorldSpaceLightPos0.xyz));
#ifdef _USENORMAL
    float4 normaltex = tex2D(_BumpMap,i.uv.xy*_BumpMap_ST.xy);
	
	half3 tangent = normalize(i.tangentToWorldAndPackedData[0].xyz);
	half3 binormal = normalize(i.tangentToWorldAndPackedData[1].xyz);
	half3 worldNormal = normalize(i.tangentToWorldAndPackedData[2].xyz);
	half3x3 tangentToWorld = transpose(half3x3(tangent, binormal, worldNormal));
	half3 unpackNor = UnpackNormal(normaltex);
	//norDir = normalize(tangent * unpackNor.x + binormal * unpackNor.y + worldNormal * unpackNor.z);
	norDir = mul(tangentToWorld, unpackNor);
#endif

    //dir
    float NoL = saturate(dot(norDir, _WorldSpaceLightPos0.xyz));
	float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - posWorld);
	
	float ReceiveShadow = 1;
#if defined(_LIGHTMAP)
	if (GNoL > 0.2)
	    ReceiveShadow = MY_SHADOW_ATTENTION(i, norDir, posWorld.xyz);
#else
	ReceiveShadow = MY_SHADOW_ATTENTION(i, norDir, posWorld.xyz);
#endif

#ifdef _RECEIVESHADOW
#if !defined(_LIGHTMAP)
	ReceiveShadow *= saturate(NoL * 1.5);
#endif
#endif
    //ReceiveShadow = saturate(lerp(ReceiveShadow, 1.0, (distance(posWorld.xyz, _WorldSpaceCameraPos.xyz)- 8)));
    float3 ShadowColor = ReceiveShadow * float3(1, 1, 1) + (1-ReceiveShadow) * _ShadowColor.rgb;
	
    float3 sAlbedo = albedo * _TintColor.rgb;
#ifdef SHADER_API_METAL
	Omega_Lightmap_HDR = float4(2, 1, 0, 0);
#else
	Omega_Lightmap_HDR = float4(1, 1, 0, 0);
	#ifdef UNITY_COLORSPACE_GAMMA
	#ifdef UNITY_NO_RGBM
		Omega_Lightmap_HDR.x = 2.0;
	#else
		Omega_Lightmap_HDR.x = 5.0;
	#endif
	#else
	#ifdef UNITY_NO_RGBM
		Omega_Lightmap_HDR.x = GammaToLinearSpaceExact(2.0);
	#else
		Omega_Lightmap_HDR.x = pow(5.0, 2.2);
		Omega_Lightmap_HDR.y = 2.2;
	#endif
	#endif
#endif

#ifdef _LIGHTMAP
	half2 lmUV = i.uv.zw * unity_LightmapST.xy + unity_LightmapST.zw;
	//float4 lightmap = tex2D(_Lightmap, lmUV);
#ifdef _CUSTOM_LIGHTMAP
	float4 lightmap = tex2D(_Lightmap, lmUV) * float4(2.0, 2.0, 2.0, 1.0);//float4(DecodeLightmap(tex2D(_Lightmap, lmUV) * float4(2.0, 2.0, 2.0, 1.0)), 1);
#else
	float4 lightmap = float4(DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, lmUV), Omega_Lightmap_HDR), 1);
#endif
	
	float3 lmcolor = lightmap.rgb;

#ifdef _RECEIVESHADOW
	/*if (i.outdoor.r != 0)  //indoor
	{
		//ShadowColor = saturate(ShadowColor * 1.9);
		//lightcolor = lmcolor;
	}*/
	//if (ShadowColor.r < 1)
	//	lmcolor = min(lmcolor, ShadowColor);
	lmcolor *= ShadowColor;
#endif
	half3 lightcolor = lmcolor;
    sAlbedo *= lightcolor;
	float3 finalColor = sAlbedo;// *NoL + sAlbedo * UNITY_LIGHTMODEL_AMBIENT.rgb;
	//float3 finalColor = sAlbedo * max(lightcolor * ShadowColor, lmcolor);
#else
	sAlbedo *= ShadowColor;
	half3 lightcolor = (_LightColor0.rgb * NoL + UNITY_LIGHTMODEL_AMBIENT.rgb); // *NoL;
	float3 finalColor = lightcolor * sAlbedo;//sAlbedo * NoL + sAlbedo * UNITY_LIGHTMODEL_AMBIENT.rgb;
#endif	


#ifdef _Unlit
	finalColor = sAlbedo;
#endif	
	
#ifdef _SPEC
	float3 reflectDir = reflect(-_WorldSpaceLightPos0.xyz, norDir);
	float NoH = max(dot(viewDir, reflectDir), 0.0);
	//float4 specularMap = tex2D(_SpecularTex, i.uv.xy * _SpecularTex_ST.xy);
	float4 specTex = tex2D(_SpecularMap, i.uv.xy * _SpecularMap_ST.xy);
	float specular = specTex.r;
	half3 specularTerm = pow(NoH, 128 * _Shininess) * specular;
	
	finalColor += _SpecColor.rgb * specularTerm * sAlbedo;
#endif

#ifdef _CUSTOM_SPEC
	float3 reflectDir = reflect(-_WorldSpaceLightPos0.xyz, norDir);
	float NoH = max(dot(viewDir, reflectDir), 0.0);
	half3 specularTerm = pow(NoH, 48 * _Shininess);

	finalColor += _SpecColor.rgb * specularTerm * sAlbedo;
#endif

#ifdef _USEREFL
	//Fresnel
	float NoV = saturate(dot(norDir, viewDir));
	float Fresnel = pow((1.0 - NoV)*NoL, _FresnelRange);

	float3 reflDir = normalize(reflect(-viewDir, norDir));
	float perceptualRoughness = albedo.a + (1 - _MetalV) * floor(albedo.a + _MetalV);
	float3 reflspec = texCUBElod(_ReflTex, fixed4(reflDir.xyz, 0)).rgb;

	reflspec = pow(reflspec, 1.4);
	float Term = pow(perceptualRoughness, _MetalV) * Fresnel;
	finalColor = lerp(finalColor, reflspec, Term);
#endif
	
#ifdef _MATCAP
	float3 reflectDir = reflect(-_WorldSpaceLightPos0.xyz, norDir);
	half2 capUV = half2(dot(UNITY_MATRIX_V[0].xyz, reflectDir), dot(UNITY_MATRIX_V[1].xyz, reflectDir));
	half3 matcap = tex2D(_MatcapTex, capUV * 0.5 + 0.5);
	finalColor.rgb += matcap;
#endif

#ifdef _SPOTLIGHT
	half dis = saturate((length(posWorld.xz - _SpotLightParams.xy)) / _SpotRad);
	half spotStrenth = lerp(_SpotLightParams.z, _SpotLightParams.w, smoothstep(0, 1, 1 - dis));// sqrt(1 - dis * dis);
	finalColor = finalColor * spotStrenth * _SpotLightColor.rgb;
#endif

    float finalAlpha = albedo.a;

	finalColor.rgb = ApplySunFog(finalColor.rgb, i.fogCoord, viewDir);

#ifdef _RECEIVESHADOW
	//float zClip = i._ShadowCoord.z / i._ShadowCoord.w;
	//float cascade = step(cascadeEndClipSpace.x, zClip);
	//if (cascade == 1)
	//{
	//	float3 blendColor = float3(1, 0, 0);
	//	finalColor *= blendColor;
	//}
	//else
	//{
	//	cascade = step(cascadeEndClipSpace.y, zClip);
	//	if (cascade == 1)
	//	{
	//		float3 blendColor = float3(0, 0, 1);
	//		finalColor *= blendColor;
	//	}
	//}
#endif

    return saturate(half4(finalColor,finalAlpha));
}

#endif // ENVCORE_INCLUDED
