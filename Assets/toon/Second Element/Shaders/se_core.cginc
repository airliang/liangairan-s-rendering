// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

#include "AutoLight.cginc"
sampler2D _MainTex;
float4 _MainTex_ST;

#ifdef _USE_RAM
sampler2D _RampTex;
float4 _RampTex_ST;
#else
float4 _DiffuseSegment;
#endif
float4 _ShadowColor;
float4 _LightColor0;
float ks;
float _shininess;
float4 _scaleTranslate;  //xy-scale, zw-translate
float2 _splitUV;
float _SpecularScale;
float _SquareN;
float _SquareTau;
float _RotationZ;

#define DegreeToRadian 0.0174533

struct CustomVertexInput
{
	half4 vertex   : POSITION;
	half3 normal    : NORMAL;
	half4 color     : COLOR0;
	float4 tangent : TANGENT;
	float2 uv0      : TEXCOORD0;
	float2 uv1      : TEXCOORD1; 
};

struct CustomVertexOutput
{
	half4 pos		: SV_POSITION;
	half4 color     : COLOR;
	half4 uv : TEXCOORD0;
	half3 posWorld : TEXCOORD1;
	half3 normalWorld : TEXCOORD2;
	half3 normalTangent : TEXCOORD3;
	half3 viewTangent : TEXCOORD4;
	half3 lightTangent : TEXCOORD5;
};

float rim_fresnel(float NdV)
{
	float f = 1.0 - NdV;
	return f * f * f * f;
}

CustomVertexOutput vert(CustomVertexInput v)
{
	CustomVertexOutput o;
	o.color = v.color;
	o.pos = UnityObjectToClipPos(v.vertex);

	o.uv.xy = v.uv0;
	o.uv.zw = v.uv1;

	o.normalWorld = UnityObjectToWorldNormal(v.normal);
	o.posWorld = mul(unity_ObjectToWorld, v.vertex);
	//o.posWorld = mul(unity_ObjectToWorld, v.vertex);

	TANGENT_SPACE_ROTATION;
	//tengent space's normal is (0, 0, 1).
	o.normalTangent = mul(rotation, v.normal);
	o.viewTangent = mul(rotation, ObjSpaceViewDir(v.vertex));
	o.lightTangent = mul(rotation, ObjSpaceLightDir(v.vertex));

	return o;
}

half4 frag_skin(CustomVertexOutput i) : COLOR
{
	half4 col;
	half4 mainTex = tex2D(_MainTex, TRANSFORM_TEX(i.uv.xy, _MainTex));
	half3 normalWorld = normalize(i.normalWorld);
	half3 lightDir = normalize(_WorldSpaceLightPos0);
	half ndl = dot(normalWorld, lightDir);
	half halfLambert = ndl * 0.5 + 0.5;
	half3 diffuse = mainTex.rgb * _LightColor0.rgb;
#ifdef _USE_RAM
	half4 rampColor = tex2D(_RampTex, float2(halfLambert, 0));
	diffuse *= rampColor.rgb;
#else
	fixed w = fwidth(halfLambert) * 2.0;
	if (halfLambert < _DiffuseSegment.x + w) {
		halfLambert = lerp(_DiffuseSegment.x, _DiffuseSegment.y, smoothstep(_DiffuseSegment.x - w, _DiffuseSegment.x + w, halfLambert));
		//  diff = lerp(_DiffuseSegment.x, _DiffuseSegment.y, clamp(0.5 * (diff - _DiffuseSegment.x) / w, 0, 1));
	}
	//else if (halfLambert < _DiffuseSegment.y + w) {
	//	halfLambert = lerp(_DiffuseSegment.y, _DiffuseSegment.z, smoothstep(_DiffuseSegment.y - w, _DiffuseSegment.y + w, halfLambert));
	//	//  diff = lerp(_DiffuseSegment.y, _DiffuseSegment.z, clamp(0.5 * (diff - _DiffuseSegment.y) / w, 0, 1));
	//}
	//else if (halfLambert < _DiffuseSegment.z + w) {
	//	halfLambert = lerp(_DiffuseSegment.z, _DiffuseSegment.w, smoothstep(_DiffuseSegment.z - w, _DiffuseSegment.z + w, halfLambert));
	//	//  diff = lerp(_DiffuseSegment.z, _DiffuseSegment.w, clamp(0.5 * (diff - _DiffuseSegment.z) / w, 0, 1));
	//}
	else {
		halfLambert = _DiffuseSegment.y;
	}

	diffuse *= halfLambert;
#endif

	//if (halfLambert < 0.3)
	//{
	//	mainTex *= _ShadowColor;
	//}
	half3 normalTangent = normalize(i.normalTangent);
	half3 viewTangent = normalize(i.viewTangent);
	half3 lightTangent = normalize(i.lightTangent);
	half3 halfTangent = normalize(lightTangent + viewTangent);
	//rotation
	//z axis rotation
	float zRad = _RotationZ * DegreeToRadian;
	float3x3 zRotation = float3x3(cos(zRad), sin(zRad), 0,
		-sin(zRad), cos(zRad), 0,
		0, 0, 1);
	halfTangent = mul(zRotation, halfTangent);
	
	// Scale
	halfTangent = halfTangent - _scaleTranslate.x * halfTangent.x * half3(1, 0, 0);
	halfTangent = normalize(halfTangent);
	halfTangent = halfTangent - _scaleTranslate.y * halfTangent.y * half3(0, 1, 0);
	halfTangent = normalize(halfTangent);

	

	// Translation
	// inside the tangent space
	// tangent = (1, 0, 0) bnormal = (0, 1, 0)
	//H' = h + alpha * tangent + beta * bnormal = h + (alpha, beta, 0);
	halfTangent = halfTangent + half3(_scaleTranslate.z, _scaleTranslate.w, 0);
	halfTangent = normalize(halfTangent);

	//split
	halfTangent = halfTangent - float3(_splitUV.x * sign(halfTangent.x), _splitUV.y * sign(halfTangent.y), 0);
	halfTangent = normalize(halfTangent);
	
	// Square
	float theta = min(acos(abs(halfTangent.x)), acos(abs(halfTangent.y)));
	float sqrnorm = sin(pow(2 * theta, floor(_SquareN)));
	halfTangent = halfTangent - _SquareTau * sqrnorm * float3(halfTangent.x, halfTangent.y, 0);
	halfTangent = normalize(halfTangent);
	
	//float sqrThetaX = acos(halfTangent.x);
	//float sqrThetaY = acos(halfTangent.y);
	//fixed sqrnormX = sin(pow(2 * sqrThetaX, _SquareN));
	//fixed sqrnormY = sin(pow(2 * sqrThetaY, _SquareN));
	//halfTangent = halfTangent - _SquareTau * (sqrnormX * halfTangent.x * fixed3(1, 0, 0) + sqrnormY * halfTangent.y * fixed3(0, 1, 0));
	//halfTangent = normalize(halfTangent);
	

	half spec = max(pow(max(dot(halfTangent, normalTangent), 0), _shininess), 0);
#ifdef _USE_RAM
	half4 rampSpecColor = tex2D(_RampSpecTex, float2(spec, 0));
	half3 specular = _LightColor0.rgb * spec * ks * rampSpecColor.rgb;
#else
	w = fwidth(spec) * 3.0;
	half3 specular = lerp(half3(0, 0, 0), _LightColor0.rgb * ks, smoothstep(-w, w, spec + _SpecularScale - 1));
#endif
	
	//fixed3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
	//float3 H = normalize(lightDir + viewDirection);
	//float specularLight = max(pow(max(dot(H, normalWorld), 0), _shininess), 0);
 //   float3 specular = _LightColor0.rgb * specularLight * ks;

	col.rgb = diffuse + specular;
	col.a = mainTex.a;
	return col;
}



float4 _OutlineColor;
float _OutlineWidth;

struct appdata_outline
{
    half4 vertex : POSITION;
    half4 color  : COLOR;
    half3 normal : NORMAL;
};

struct VSOut
{
    half4 pos		: SV_POSITION;
    //half3 normalWorld : TEXCOORD0;
};


VSOut vert_outline(appdata_outline v)
{
    VSOut o;
    float4 clipPosition = UnityObjectToClipPos(v.vertex);
    float3 worldNormal = UnityObjectToWorldNormal(v.normal);
    float3 clipNormal = mul((float3x3) UNITY_MATRIX_VP, worldNormal);
    float2 normalOffset = normalize(clipNormal.xy) / _ScreenParams.xy * _OutlineWidth * clipPosition.w * v.color.rg;
    clipPosition.xy += normalOffset;

    o.pos = clipPosition;
    //o.pos = UnityObjectToClipPos(v.vertex);

    return o;
}

half4 frag_outline(VSOut i) : COLOR
{
    return _OutlineColor;
}



