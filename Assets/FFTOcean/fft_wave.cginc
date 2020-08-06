#include "UnityCG.cginc"
uniform sampler2D _HeightTex;
uniform sampler2D _NormalMap;

float FFTWavePos(float3 posWorld, out float3 outPos, out float3 normal)
{
	float4 heightMap = tex2Dlod(_HeightTex, float4(posWorld.xz / 128, 0, 0));

	float s = 50;
	float heightAttentionDis = 50;
	float heightScale = saturate(-(distance(_WorldSpaceCameraPos, posWorld) - s) / heightAttentionDis + 1);

	outPos = posWorld + heightMap.xyz * heightScale;
	normal = float3(0, 1, 0);
	//normal = tex2Dlod(_NormalMap, float4(posWorld.xz / 128, 0, 0));
	return heightScale;
}
