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

public struct GPUBounds
{
    public Vector3 min;
    public Vector3 max;

    public static GPUBounds DefaultBounds()
    {
        GPUBounds bounds;
        bounds.min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        bounds.max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        return bounds;
    }

    public Vector3 Diagonal
	{
        get
        {
            return max - min;
        }
	}

    public float SurfaceArea() 
    {
		Vector3 d = Diagonal;
		return 2 * (d.x* d.y + d.x* d.z + d.y* d.z);
	}

    public int MaximunExtent()
	{
        Vector3 d = Diagonal;
		if (d.x > d.y && d.x > d.z)
			return 0;
		else if (d.y > d.z)
			return 1;
		else
			return 2;
	}

    //返回p点到min点的距离和max到min的比例
    //例如p = max，那么返回的就是1
    //
    public Vector3 Offset(Vector3 p)
	{
        Vector3 o = p - min;
		if (max.x > min.x) o.x /= max.x - min.x;
		if (max.y > min.y) o.y /= max.y - min.y;
		if (max.z > min.z) o.z /= max.z - min.z;
		return o;
	}

    public Vector3 centroid
    {
        get
        {
            return (max + min) * 0.5f;
        }
    }

    public void Intersect(GPUBounds b)
    {
        max = Vector3.Min(max, b.max);
        min = Vector3.Max(min, b.min);
    }
    public static GPUBounds Union(GPUBounds a, GPUBounds b)
    {
        GPUBounds bounds = a;
        bounds.min = Vector3.Min(a.min, b.min);
        bounds.max = Vector3.Max(a.max, b.max);
        return bounds;
    }

    public static GPUBounds Union(GPUBounds a, Vector3 p)
    {
        GPUBounds bounds = a;
        bounds.min = Vector3.Min(a.min, p);
        bounds.max = Vector3.Max(a.max, p);
        return bounds;
    }

    public static GPUBounds ConvertUnityBounds(Bounds b)
    {
        GPUBounds bounds = new GPUBounds();
        bounds.max = b.max;
        bounds.min = b.min;
        return bounds;
    }

    public float MinSize()
    {
        Vector3 size = Diagonal;
        float min = size.x;
        min = Mathf.Min(size.y, min);
        min = Mathf.Min(size.z, min);
        return min;
    }

    public float MaxSize()
    {
        Vector3 size = Diagonal;
        float max = size.x;
        max = Mathf.Max(size.y, max);
        max = Mathf.Max(size.z, max);
        return max;
    }
}


public struct GPUSampler
{
    int dim;
}

public struct Vector4Int
{
    public int x;
    public int y;
    public int z;
    public int w;
}
public struct GPUBVHNode
{
    //public GPUBounds b1;
    //public GPUBounds b2;
    //public int idx1;
    //public int idx2;
    //int c0;    //extend to 64bytes
    //int c1;
    public Vector4 b0xy;
    public Vector4 b1xy;
    public Vector4 b01z;
    public Vector4 cids;
}

