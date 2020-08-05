#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "Lighting.cginc"

//第一行是bottomleft
//第二行是bottomright
//3 topleft
//4 topright
uniform matrix frustumInterpolation;

//screenpos从0-1
float4 oceanPos(float4 screenPos)
{
	screenPos.xy = saturate(screenPos.xy);

	//Interpolate between frustums world space projection points.
	float4 p = lerp(lerp(frustumInterpolation[0], frustumInterpolation[1], screenPos.x), lerp(frustumInterpolation[2], frustumInterpolation[3], screenPos.x), screenPos.y);
	p = p / p.w;

	//Find the world position of the screens center position.
	float4 c = lerp(lerp(frustumInterpolation[0], frustumInterpolation[1], 0.5), lerp(frustumInterpolation[2], frustumInterpolation[3], 0.5), 0.5);
	c = c / c.w;

	//平面上的方向
	float3 worldDir = normalize(p.xyz - c.xyz);


	//if p and c are the same value the normalized
	//results in a nan on ATI cards. Clamp fixes this.
	worldDir = clamp(worldDir, -1, 1);

	//Apply edge border by pushing those verts in the border 
	//in the direction away from the center.
	float mask = saturate(screenPos.z + screenPos.w);
	p.xz += worldDir.xz * mask * 100;

	return p;
}

