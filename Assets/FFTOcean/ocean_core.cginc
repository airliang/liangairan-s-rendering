#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "Lighting.cginc"
#include "ocean_projectedgrid.cginc"
#include "gerstner_wave.cginc"
#include "fft_wave.cginc"

sampler2D _MainTex;
samplerCUBE   _skyCube;
//sampler2D _NoiseMap;
//float4 _NoiseMap_ST;
sampler2D _SurfaceMap;
float4 _SurfaceMap_ST;
//uniform sampler2D _ProjectedGridMap;

half _BumpScale;
fixed4 _skyColor;

float _Transparent;


float4 _SeaBaseColor;
float4 _SeaWaterColor;
uniform float _camFarPlane;
uniform float _seaFogDistance;
uniform float4 _SeaFogColor;

struct appdata
{
    half4 vertex : POSITION;
    half2 uv : TEXCOORD0;
};

struct VSOut
{
    float4 pos		: SV_POSITION;
    //half4 color     : COLOR;
    half4 uv : TEXCOORD0;
    half3 normalWorld : TEXCOORD1;
    float3 posWorld : TEXCOORD2;
    //half3 tangentWorld : TEXCOORD3;
};

float fresnel(fixed3 I, fixed3 N)
{
    float cosi = clamp(-1, 1, dot(I, N));
    float etai = 1;
    float etat = 1.3;
    if (cosi > 0)
    {
        //swap(etai, etat);
        etat = 1;
        etai = 1.3;
    }
    // Compute sini using Snell's law
    float sint = etai / etat * sqrt(max(0, 1 - cosi * cosi));
    // Total internal reflection
    if (sint >= 1)
    {
        return 1;
    }
    else
    {
        float cost = sqrt(max(0, 1 - sint * sint));
        cosi = abs(cosi);
        float Rs = ((etat * cosi) - (etai * cost)) / ((etat * cosi) + (etai * cost));
        float Rp = ((etai * cosi) - (etat * cost)) / ((etai * cosi) + (etat * cost));
        return (Rs * Rs + Rp * Rp) / 2;
    }
}

half fresnelSchlick(float cosTheta, half F0)
{
    return saturate(F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0));
}

VSOut vert(appdata v)
{
    VSOut o;
    //TANGENT_SPACE_ROTATION;

    o.normalWorld = float3(0, 0, 0);//UnityObjectToWorldNormal(v.normal);

    float4 uv = float4(v.vertex.xy, v.uv.xy);
    float3 posWorld = oceanPos(uv);

    o.uv.zw = posWorld.xz * 0.1 + _Time.y * 0.05;
    o.uv.xy = posWorld.xz * 0.4 - _Time.y * 0.1;
    float opacity = 1 - _Transparent;
#if GERSTNER_WAVE
    float heightScale = GerstnerWaves3Composite(posWorld, o.posWorld, o.normalWorld);

    
    opacity *= heightScale;
    o.normalWorld *= float3(opacity, 1, opacity);
    //o.posWorld = posWorld;

    
#elif FFT_WAVE
    float heightScale = FFTWavePos(posWorld, o.posWorld, o.normalWorld);

    opacity *= heightScale;
    o.normalWorld *= float3(opacity, 1, opacity);
#else
    o.normalWorld = float3(0, 1, 0);
    o.posWorld = posWorld;
    
#endif
    o.pos = mul(UNITY_MATRIX_VP, float4(o.posWorld, 1));
    return o;
}

half4 frag(VSOut i) : COLOR
{
    fixed3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
    float  viewsDistance = length(_WorldSpaceCameraPos.xz - i.posWorld.xz);
    fixed3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);


    half2 detailBump1 = tex2D(_SurfaceMap, i.uv.zw * _SurfaceMap_ST.xy + _SurfaceMap_ST.zw).xy * 2 - 1;
    half2 detailBump2 = tex2D(_SurfaceMap, i.uv.xy * _SurfaceMap_ST.xy + _SurfaceMap_ST.zw).xy * 2 - 1;
    half2 detailBump = (detailBump1 + detailBump2 * 0.5) * 0.95;

#if FFT_WAVE
    i.normalWorld = tex2Dlod(_NormalMap, float4(i.posWorld.xz / 128, 0, 0));
#endif
    i.normalWorld += half3(detailBump.x, 0, detailBump.y) * _BumpScale;

    //i.normalWorld += half3(1-waterFX.y, 0.5h, 1-waterFX.z) - 0.5;

    float3 normalDirection = normalize(i.normalWorld);

    //return half4(normalDirection, 1);
    float NoL = max(0, dot(lightDirection, normalDirection));

    fixed3 reflectDir = normalize(reflect(-viewDirection, normalDirection));
    float NoR = max(0, dot(normalDirection, viewDirection));
    //float fr = fresnel(reflectDir, normalDirection);
    float R0 = (1 - 1.3) * (1 - 1.3) / ((1 + 1.3) * (1 + 1.3));
    half fr = fresnelSchlick(dot(normalDirection, viewDirection), R0);

    //float fresnel = clamp(1.0 - dot(normalDirection, viewDirection), 0.0, 1.0);
    //fresnel = pow(fresnel, 10.0) * 0.65;

    //float3 reflected = Sky(pos, reflect(rd, nor), lightDir);
    half3 reflectColor = _skyColor * fr; //texCUBE(_skyCube, reflectDir) * fr;
    //return float4(reflectColor, 1);
    half3 h = normalize(viewDirection + lightDirection);
    half3 spec = pow(max(dot(h, normalDirection), 0.0), 150.) * _LightColor0;

    float3 diff = pow(NoL * 0.4 + 0.6, 3.);
    float3 refracted = (_SeaBaseColor + diff * _SeaWaterColor * 0.12) * (1.0 - fr);
    float3 col = reflectColor + spec + refracted; //lerp(refracted, reflectColor + spec, fresnel);

    float t = saturate((_camFarPlane - viewsDistance) / _seaFogDistance + 1.0 - _camFarPlane / _seaFogDistance);
    t =  t * t;
    col = lerp(_SeaFogColor, col, t);

    col = pow(col, 0.6);
    //col.a = _Transparent;
    return float4(col, _Transparent);
}


float3 CalculateDistToCenter(float4 v0, float4 v1, float4 v2) {
	// points in screen space
	float2 ss0 = _ScreenParams.xy * v0.xy / v0.w;
	float2 ss1 = _ScreenParams.xy * v1.xy / v1.w;
	float2 ss2 = _ScreenParams.xy * v2.xy / v2.w;

	// edge vectors
	float2 e0 = ss2 - ss1;
	float2 e1 = ss2 - ss0;
	float2 e2 = ss1 - ss0;

	// area of the triangle
	float area = abs(e1.x * e2.y - e1.y * e2.x);

	// values based on distance to the center of the triangle
	float dist0 = area / length(e0);
	float dist1 = area / length(e1);
	float dist2 = area / length(e2);

	return float3(dist0, dist1, dist2);
}

// Computes the intensity of the wireframe at a point
// based on interpolated distances from center for the
// fragment, thickness, firmness, and perspective correction
// factor.
// w = 1 gives screen-space consistent wireframe thickness
float GetWireframeAlpha(float3 dist, float thickness, float firmness, float w = 1) {
	// find the smallest distance
	float val = min(dist.x, min(dist.y, dist.z));
	val *= w;

	// calculate power to 2 to thin the line
	val = exp2(-1 / thickness * val * val);
	val = min(val * firmness, 1);
	return val;
}

struct v2g
{
	float4  pos     : POSITION;     // vertex position
	float2  uv      : TEXCOORD0;    // vertex uv coordinate
};

struct g2f
{
	float4  pos     : POSITION;     // fragment position
	float2  uv      : TEXCOORD0;    // fragment uv coordinate
	float3  dist    : TEXCOORD1;    // distance to each edge of the triangle
};

float _Thickness = 1;       // Thickness of the wireframe line rendering
float _Firmness = 1;        // Thickness of the wireframe line rendering
float4 _Color = { 1,1,1,1 };  // Color of the line

v2g vert_wireframe(appdata v)
{
	v2g o;


    float4 uv = float4(v.vertex.xy, v.uv.xy);
    float3 posWorld = oceanPos(uv);
    float3 normalWorld = float3(0, 0, 0);
	o.uv.xy = posWorld.xz * 0.4 - _Time.y * 0.1;
    float heightScale = GerstnerWaves3Composite(posWorld, posWorld, normalWorld);

	float opacity = 1 - _Transparent;
	

	o.pos = mul(UNITY_MATRIX_VP, float4(posWorld, 1));

	return o;
}

[maxvertexcount(3)]
void geom(triangle v2g p[3], inout TriangleStream<g2f> triStream)
{
	float3 dist = CalculateDistToCenter(p[0].pos, p[1].pos, p[2].pos);

	g2f pIn;

	// add the first point
	pIn.pos = p[0].pos;
	pIn.uv = p[0].uv;
	pIn.dist = float3(dist.x, 0, 0);
	triStream.Append(pIn);

	// add the second point
	pIn.pos = p[1].pos;
	pIn.uv = p[1].uv;
	pIn.dist = float3(0, dist.y, 0);
	triStream.Append(pIn);

	// add the third point
	pIn.pos = p[2].pos;
	pIn.uv = p[2].uv;
	pIn.dist = float3(0, 0, dist.z);
	triStream.Append(pIn);
}

float4 frag_wireframe(g2f input) : COLOR
{
	float w = input.pos.w;
	#if UCLAGL_DISTANCE_AGNOSTIC
	w = 1;
	#endif

	float alpha = GetWireframeAlpha(input.dist, _Thickness, _Firmness, w);
	float4 col = _Color * tex2D(_MainTex, input.uv);
	col.a *= alpha;

	#if UCLAGL_CUTOUT
	if (col.a < 0.5f) discard;
	col.a = 1.0f;
	#endif

	return col;
}

