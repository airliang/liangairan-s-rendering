#define PI 3.14159265359

//F(v,h)公式 cosTheta = v dot h
half3 fresnelSchlick(float cosTheta, half3 F0)
{
	return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

half3 fresnelSchlickRoughness(float cosTheta, half3 F0, float roughness)
{
	float oneminusroughness = 1.0 - roughness;
	return F0 + (max(half3(oneminusroughness, oneminusroughness, oneminusroughness), F0) - F0) * pow(1.0 - cosTheta, 5.0);
}

half3 DiffuseLambert(half3 diffuse)
{
	return diffuse / PI;
}

//D(h)GGX公式，计算法线分布
//alpha = roughness * roughness
float normalDistribution_GGX(float alpha, float ndh)
{
	if (ndh == 0)
		return 0;
	float alphaPow = alpha * alpha;
	float t = ndh * ndh * (alphaPow - 1) + 1;
	return alphaPow / (PI * t * t);
	//float ndhPow2 = ndh * ndh;
	//float tanSita_pow = (1 - ndhPow2) / (ndh * ndh + 0.00001);
	//float t = alphaPow + tanSita_pow;
	//float D = alphaPow * ndh / (ndhPow2 * ndhPow2 * PI * t * t);
	//return D;
}

float GGX_GSF(float roughness, float ndv, float ndl)
{
	//float tan_ndv_pow = (1 - ndv * ndv) / (ndv * ndv + 0.00001);

	//return (ndl / ndv) * 2 / (1 + sqrt(1 + roughness * roughness * tan_ndv_pow));
	float k = roughness / 2;


	float SmithL = (ndl) / (ndl * (1 - k) + k);
	float SmithV = (ndv) / (ndv * (1 - k) + k);


	float Gs = (SmithL * SmithV);
	return Gs;
}

float RoughnessToAlpha(float roughness) 
{
	roughness = max(roughness, 0.0001);
	float x = log(roughness);
	return 1.62142f + 0.819955f * x + 0.1734f * x * x +
		0.0171201f * x * x * x + 0.000640711f * x * x * x * x;
}

float BeckmannNormalDistribution(float roughness, float NdotH)
{
	float roughnessSqr = roughness * roughness;
	float NdotHSqr = NdotH * NdotH;
	return max(0.000001, (1.0 / (3.1415926535 * roughnessSqr * NdotHSqr*NdotHSqr)) * exp((NdotHSqr - 1) / (roughnessSqr*NdotHSqr)));
}

float BeckmannDistribution(float roughnessX, float roughnessY, float NdotH, float cosPhi)
{
	if (NdotH <= 0)
		return 0.0001;
	float cosTheta = NdotH;
	float sinTheta = sqrt(1.0 - cosTheta);
	float tanTheta = sinTheta / cosTheta;
	float tan2Theta = tanTheta * tanTheta;

	float cos4Theta = cosTheta * cosTheta * cosTheta * cosTheta;
	float alphax = RoughnessToAlpha(roughnessX);
	float alphay = RoughnessToAlpha(roughnessY);
	float cos2Phi = cosPhi * cosPhi;
	float sin2Phi = 1.0 - cos2Phi;
	return exp(-tan2Theta * (cos2Phi / (alphax * alphax) +
		sin2Phi / (alphay * alphay))) /
		(PI * alphax * alphay * cos4Theta);
}

//G(l,v,h)，计算微表面遮挡
float smith_schilck(float roughness, float ndv, float ndl)
{
	float k = (roughness + 1) * (roughness + 1) / 8;
	float Gv = ndv / (ndv * (1 - k) + k);
	float Gl = ndl / (ndl * (1 - k) + k);
	return Gv * Gl;
}

float Schilck_GSF(float roughness, float ndv, float ndl)
{
	float roughnessSqr = roughness * roughness;
	float Gv = ndv / (ndv * (1 - roughnessSqr) + roughnessSqr);
	float Gl = ndl / (ndl * (1 - roughnessSqr) + roughnessSqr);
	return Gv * Gl;
}

float MixFunction(float i, float j, float x) 
{
	return  j * x + i * (1.0 - x);
}

float SchlickFresnel(float i) {
	float x = clamp(1.0 - i, 0.0, 1.0);
	float x2 = x * x;
	return x2 * x2*x;
}

float F0(float NdotL, float NdotV, float LdotH, float roughness) {
	float FresnelLight = SchlickFresnel(NdotL);
	float FresnelView = SchlickFresnel(NdotV);
	float FresnelDiffuse90 = 0.5 + 2.0 * LdotH*LdotH * roughness;
	return  MixFunction(1, FresnelDiffuse90, FresnelLight) * MixFunction(1, FresnelDiffuse90, FresnelView);
}

half3 brdf(half3 fresnel, float D, float G, float ndv, float ndl)
{
	return fresnel * D * G / (4 * ndv * ndl + 0.0001);
}

//roughnessX roughnessY X和Y切线方向的粗糙度
//NoH 宏观法线和微表面法线的点乘
//H 微表面法线
//X 切线向量
//Y 切线向量
float D_GGXaniso(float RoughnessX, float RoughnessY, float NoH, float3 H, float3 X, float3 Y)
{
	float ax = RoughnessX * RoughnessX;
	float ay = RoughnessY * RoughnessY;
	float XoH = dot(X, H);
	float YoH = dot(Y, H);
	float d = XoH * XoH / (ax * ax) + YoH * YoH / (ay * ay) + NoH * NoH;
	return 1 / (PI * ax * ay * d * d);
}

float aniso_smith_schilck(float ax, float ay, float ndv, float ndl)
{
	float k = (ax + 1) * (ay + 1) / 8;
	float Gv = ndv / (ndv * (1 - k) + k);
	float Gl = ndl / (ndl * (1 - k) + k);
	return Gv * Gl;
}


half3 rough_sss(half3 l, half3 v, half3 h, half3 n, float roughness, float3 rou_ss, float3 ks)
{
	half ndl = dot(n, l);
	half ndv = dot(n, v);
	float alhpa = roughness * roughness;
	half hdl = dot(h, l);
	float F_SS90 = roughness * hdl * hdl;
	float F_D90 = 0.5 + 2 * F_SS90;
	float one_minus_ndl_5 = (1.0 - ndl) * (1.0 - ndl);
	one_minus_ndl_5 *= one_minus_ndl_5;
	one_minus_ndl_5 *= (1.0 - ndl);

	float one_minus_ndv_5 = (1.0 - ndv) * (1.0 - ndv);
	one_minus_ndv_5 *= one_minus_ndv_5;
	one_minus_ndv_5 *= (1.0 - ndv);

	float F_SS = (1 + (F_SS90 - 1) * one_minus_ndl_5) * (1 + (F_SS90 - 1) * one_minus_ndv_5);

	float f_ss = (1.0 / ((ndl * ndv) + 0.001) - 0.5) * F_SS + 0.5;
	float f_d = (1 + (F_D90 - 1) * one_minus_ndl_5) * (1 + (F_D90 - 1) * one_minus_ndv_5);

	return max(ndl, 0) * max(ndv, 0) * rou_ss / PI * ((1 - ks) * f_d + 1.25 * ks * f_ss);
}

