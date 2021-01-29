

void StippleClip(float2 screenpos, half transparency)
{
    float4x4 thresholdMatrix =
    {
        1.0 / 17.0, 9.0 / 17.0, 3.0 / 17.0, 11.0 / 17.0,
      13.0 / 17.0, 5.0 / 17.0, 15.0 / 17.0, 7.0 / 17.0,
       4.0 / 17.0, 12.0 / 17.0, 2.0 / 17.0, 10.0 / 17.0,
      16.0 / 17.0, 8.0 / 17.0, 14.0 / 17.0, 6.0 / 17.0
    };
    float4x4 _RowAccess = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
 
    //float2 pos = IN.screenPos.xy / IN.screenPos.w;
    //pos *= _ScreenParams.xy; // pixel position
    clip(transparency - thresholdMatrix[fmod(screenpos.x, 4)] * _RowAccess[fmod(screenpos.y, 4)]);
 
}

void StippleClipFast(in float2 screenpos, half transparency)
{
    int2 sp = (int2) screenpos;
    sp -= (sp / 4) * 4; //3 slots
    clip(transparency - (frac(((float) sp.x * 0.61414 + 0.84242) + (float) sp.y * 1.691148549999)));
}