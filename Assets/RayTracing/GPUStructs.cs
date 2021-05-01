using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct GPUPrimitive
{
    public int vertexOffset;      //mesh vertex offset in the whole scene vertexbuffer
    public int triangleOffset;    //triangle offset in the whole scene trianglebuffer
    public int transformId; //the primitive belong to the transform
    public int faceIndex;   //mesh triangle indice start
}

public struct GPULight
{
    public int type;  //0 - deltadistance 1 - delta point 2 - area
    public Color color;
    public float intensity;
    public float pointRadius;
}

public struct GPURay
{
    public Vector4 orig;
    public Vector4 direction; //w is t
}

public struct GPUInteraction
{
    Vector3 intinteractPoint;
    float time;
    Vector4 pError; //floating point error
    Vector4 wo;   //output direction
    Vector4 normal;
}

public struct GPUSampler
{
    int dim;
}
