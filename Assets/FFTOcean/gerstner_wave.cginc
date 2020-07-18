
//K = (xk, zk)
//s = steepness
//f(x,z) = (xk, zk)A((sin(w * K·pos.xz - wt + φ) + 1) / 2)^s
//H(y) = A((cos(w * K·pos.xz - wt + φ) + 1)/2)^s

//函数对x求偏导
// |∂fx/∂x|   |0.5 * w * xk² * A * cos(w * K·pos.xz - wt + φ) * ((sin(w * K·pos.xz - wt + φ) + 1) / 2)^(s - 1) * s|
// |∂H/∂x | = |-0.5 * w * xk * A * sin(w * K·pos.xz - wt + φ) * ((cos(w * K·pos.xz - wt + φ) + 1)/2)^(s - 1) * s|
// |∂fz/∂x|	  |0.5 * w * zk * xk * A * cos(w * K·pos.xz - wt + φ) * ((sin(w * K·pos.xz - wt + φ) + 1) / 2)^(s - 1) * s|

//函数对z求偏导
// |∂fx/∂z|   |0.5 * w * xk * zk * A * cos(w * K·pos.xz - wt + φ) * ((sin(w * K·pos.xz - wt + φ) + 1) / 2)^(s - 1) * s|
// |∂H/∂z | = |-0.5 * w * zk* A * sin(w * K·pos.xz - wt + φ) * ((cos(w * K·pos.xz - wt + φ) + 1) / 2)^(s - 1) * s|
// |∂fz/∂z|	  |0.5 * w * zk² * A * cos(w * K·pos.xz - wt + φ) * ((sin(w * K·pos.xz - wt + φ) + 1) / 2)^(s - 1) * s|

//pos orig position in world
//waveLength crest-to-crest length in meters
//amplitude  in meters
//steepness  discript the choppy
//t time
//waveDir wave moving direction
float3 GerstnerWave(float3 pos, float waveLength, float amplitude, float steepness, 
	float t, float2 waveDir, float phase, out float3 normal)
{
	float w = 2.0 * UNITY_PI / waveLength;
	float waveDotPos = dot(pos.xz, waveDir);
	float x = w * waveDotPos - t + phase;
	float sinX = sin(x);
	float cosX = cos(x);
	float sinTerm = (sinX + 1) / 2;
	float dsinTeram = cosX;
	//sinTerm = pow(sinTerm, steepness);
	float cosTerm = (cosX + 1) / 2;
	float dcosTerm = -sinX;
	//cosTerm = pow(cosTerm, steepness);
	float2 xz = -waveDir * amplitude * pow(sinTerm, steepness);
	float height = amplitude * pow(cosTerm, steepness);

	float sinPow_subOne = steepness == 1 ? 1 : pow(sinTerm, steepness - 1);
	float cosPow_subOne = steepness == 1 ? 1 : pow(cosTerm, steepness - 1);

	//float pow2_s = steepness == 1 ? 0.5 : pow(0.5, steepness);
	//对x求偏导
	//由于坐标是 xz - f(x,z)
	//所以对x求导的结果是：
	//∂(xz - f(x,z))/∂x = 1 - |∂fx/∂x|
	float3 tangent = float3(1 - 0.5 * w * waveDir.x * waveDir.x * amplitude * dsinTeram * sinPow_subOne * steepness,
		0.5 * w * waveDir.x * amplitude * dcosTerm * cosPow_subOne * steepness,
		-0.5 * w * waveDir.y * waveDir.x * amplitude * dsinTeram * sinPow_subOne * steepness
		);

	//对z求偏导
	//∂(xz - f(x,z))/∂z = 1 - |∂fx/∂z|
	float3 b = float3(-0.5 * w * waveDir.y * waveDir.x * amplitude * dsinTeram * sinPow_subOne * steepness,
		0.5 * w * waveDir.y * amplitude * dcosTerm * cosPow_subOne * steepness,
		1 - 0.5 * w * waveDir.y * waveDir.y * amplitude * dsinTeram * sinPow_subOne * steepness
		);

	normal = normalize(cross(b, tangent));

	//normal
	/*
	waveDotPos = dot(pos.xz + float2(1, 0), waveDir);
	x = w * waveDotPos - t + phase;
	cosX = cos(x);
	cosTerm = (cosX + 1) / 2;
	float height_x = amplitude * pow(cosTerm, steepness);
	tangent = float3(1, height_x - height, 0);

	waveDotPos = dot(pos.xz + float2(0, 1), waveDir);
	x = w * waveDotPos - t + phase;
	cosX = cos(x);
	cosTerm = (cosX + 1) / 2;
	float height_z = amplitude * pow(cosTerm, steepness);
	b = float3(0, height_x - height, 1);
	normal = normalize(cross(b, tangent));
	*/

	//return float3(0, height, 0);
	return float3(xz.x, height, xz.y);
}

float3 GerstnerWaveCircle(float dis2center, float waveLength, float amplitude, float steepness,
	float t, float2 waveDir, float phase, out float3 normal)
{
	float w = 2.0 * UNITY_PI / waveLength;
	float sinTerm = sin(w * dis2center - t + phase);
	float cosTerm = cos(w * dis2center - t + phase);
	float2 xz = -waveDir * amplitude * sinTerm * steepness;
	float height = amplitude * cosTerm;

	//对x求偏导
	//由于坐标是 xz - f(x,z)
	//所以对x求导的结果是：
	//∂(xz - f(x,z))/∂x = 1 - |∂fx/∂x|
	float3 tangent = float3(1 - w * waveDir.x * waveDir.x * amplitude * cosTerm * steepness,
		-w * waveDir.x * amplitude * sinTerm,
		-w * waveDir.y * waveDir.x * amplitude * cosTerm * steepness
		);

	//对z求偏导
	//∂(xz - f(x,z))/∂z = 1 - |∂fx/∂z|
	float3 b = float3(-w * waveDir.y * waveDir.x * amplitude * cosTerm * steepness,
		-w * waveDir.y * amplitude * sinTerm,
		1 - w * waveDir.y * waveDir.y * amplitude * cosTerm * steepness
		);

	normal = cross(b, tangent);

	return float3(xz.x, height, xz.y);
}


// |∂fx/∂x|   |w * xk²Acos(w * K·pos.xz - wt + φ)|
// |∂H/∂x | = |-w * xk * Asin(w * K·pos.xz - wt + φ)|
// |∂fz/∂x|	  |w * zk * xk * Acos(w * K·pos.xz - wt + φ)|
float3 GerstnerT(float3 pos, float waveLength, float amplitude, float steepness, float t, float2 waveDir, float phase)
{
	float w = 2.0 * UNITY_PI / waveLength;
	return float3(waveDir.x * waveDir.x * amplitude * cos(w * dot(waveDir, pos.xz) - t + phase),
		-waveDir.x * amplitude * sin(w * dot(waveDir, pos.xz) - t + phase),
		waveDir.y * waveDir.x * amplitude * cos(w * dot(waveDir, pos.xz) - t + phase));
}

// |∂fx/∂z|   |w * xk * zk * Acos(w * K·pos.xz - wt + φ)|
// |∂H/∂z | = |-w * zk*Asin(w * K·pos.xz - wt + φ)|
// |∂fz/∂z|	  |w * zk²Acos(w * K·pos.xz - wt + φ)|
float3 GerstnerB(float3 pos, float waveLength, float amplitude, float steepness, float t, float2 waveDir, float phase)
{
	float w = 2.0 * UNITY_PI / waveLength;
	return float3(waveDir.y * waveDir.x * amplitude * cos(w * dot(waveDir, pos.xz) - t + phase),
		-waveDir.y * amplitude * sin(w * dot(waveDir, pos.xz) - t + phase),
		waveDir.y * waveDir.y * amplitude * cos(w * dot(waveDir, pos.xz) - t + phase));
}

float3 CircleWave(float3 pos, float waveLength, float amplitude, float steepness, float t, float2 center, out float3 normal)
{
	float2 waveDir = normalize(pos.xz - center);
	
	return GerstnerWaveCircle(length(pos.xz - center), waveLength, amplitude, steepness, t, waveDir, 0, normal);
}
