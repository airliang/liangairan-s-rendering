//https://developer.amd.com/wordpress/media/2012/10/Mitchell-IllustrativeRenderingInTF2(Siggraph07).pdf
samplerCUBE _IrradianceMap;
sampler2D _WrapingTex;
float3 alpha_beta_gama;
float3 _kexponent;
float4 _artist_fresnel;
float4 _Ks;
float4 _Kr;

float rim_fresnel(float NdV)
{
	float f = 1.0 - NdV;
	return f * f * f * f;
}

half3 DiffuseLambert(half3 diffuse)
{
	return diffuse / PI;
}

fixed4 direction_ambient(half3 normal)
{
	fixed4 irradianceColor = texCUBE(_IrradianceMap, normal);
	return irradianceColor;
}

fixed4 wrapfunction(float NdL, fixed4 lightColor)
{
	float alpha = alpha_beta_gama.x;
	float beta = alpha_beta_gama.y;
	float gama = alpha_beta_gama.y;
	float u = pow(alpha * NdL + beta, gama);
	return tex2D(_WrapingTex, float2(u, 0.0)) * lightColor;
}

fixed4 view_dependent(half3 normal, half3 view, half3 reflectlight, fixed4 lightColor)
{
	half VdR = max(0, dot(view, reflectlight));
	half NdV = max(0, dot(normal, view));

	float4 ks = _Ks;    //specular mask texture
	float4 kr = _Kr;   //rim mask texture

	float kspec = _kexponent.x;  //exponent(constant or texture)
	float krim = _kexponent.y;        //broad, constant exponent
	
	float4 fs = _artist_fresnel;    //artist tuned Fresnel term

	float fr = rim_fresnel(NdV);       //rim fresnel

	half3 up = half3(0, 1, 0);
	fixed4 output = lightColor * ks * max(fs * pow(VdR, kspec), fr * kr * pow(fr, krim)) + dot(normal, up) * fr * kr * direction_ambient(view);
	return output;
}
