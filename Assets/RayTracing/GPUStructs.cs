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
    public int meshInstanceID;
    public int distributeAddress;   //triangle area distribution
    public int trianglesNum;
    public float intensity;
    public float pointRadius;
    public Vector3 radiance;
}

public struct GPURay
{
    public Vector3 orig;      //w is tMax，代表ray能到达的最远距离
    public Vector3 direction; //w is time, 代表当前位置
    public float tmax;
    public float tmin;

    public static GPURay TransformRay(ref Matrix4x4 matrix, ref GPURay ray)
    {
        GPURay output = new GPURay();
        output.orig = matrix.MultiplyPoint(new Vector3(ray.orig.x, ray.orig.y, ray.orig.z));
        output.tmax = ray.tmax;
        output.direction = matrix.MultiplyVector(ray.direction);
        output.tmin = ray.tmin;
        return output;
    }

    public float tMax
    {
        get
        {
            return tmax;
        }
        set
        {
            tmax = value;
        }
    }

    public float tMin
    {
        get
        {
            return tmin;
        }
        set
        {
            tmin = value;
        }
    }
}

public struct GPURandomSampler
{
    uint state;
    uint s1;
}

public struct GPUInteraction
{
    public Vector4 p;   //w is hitT
    //float time;
    //Vector4 pError; //floating point error
    public Vector4 wo;   //output direction
    //Vector4 primitive;
    public Vector4 uv;
    //public Vector4 row1;
    //public Vector4 row2;
    //public Vector4 row3;
    //Vector4 ns;  //shading normal
    //Vector4 dpdu;
    //Vector4 dpdv;
    public Vector3 normal; //geometry normal
    public Vector3 tangent;
    public Vector3 bitangent;
    public uint materialID;
    public uint meshInstanceID;

    public Vector3 WorldToLocal(Vector3 v)
    {
        return new Vector3(Vector3.Dot(tangent, v), Vector3.Dot(bitangent, v), Vector3.Dot(normal, v));
    }

    public Vector3 LocalToWorld(Vector3 v)
    {
        return new Vector3(tangent.x * v.x + bitangent.x * v.y + normal.x * v.z,
            tangent.y * v.x + bitangent.y * v.y + normal.y * v.z,
            tangent.z * v.x + bitangent.z * v.y + normal.z * v.z);
    }

    public bool IsHit()
    {
        return p.w > 0;
    }
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
        Vector3 extend = Extend;
		if (max.x > min.x) 
            o.x /= extend.x;
		if (max.y > min.y) 
            o.y /= extend.y;
		if (max.z > min.z) 
            o.z /= extend.z;
		return o;
	}

    public Vector3 Extend
    {
        get
        {
            return max - min;
        }
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

    public static GPUBounds Intersection(GPUBounds a, GPUBounds b)
    {
        GPUBounds bounds;
        bounds.max = Vector3.Min(a.max, b.max);
        bounds.min = Vector3.Max(a.min, b.min);
        return bounds;
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

    public static GPUBounds TransformBounds(ref Matrix4x4 matrix, ref GPUBounds bounds)
    {
        GPUBounds result = new GPUBounds();
        Vector3 xa = matrix.GetColumn(0) * bounds.min.x;
        Vector3 xb = matrix.GetColumn(0) * bounds.max.x;

        Vector3 ya = matrix.GetColumn(1) * bounds.min.y;
        Vector3 yb = matrix.GetColumn(1) * bounds.max.y;

        Vector3 za = matrix.GetColumn(2) * bounds.min.z;
        Vector3 zb = matrix.GetColumn(2) * bounds.max.z;

        Vector3 column3 = matrix.GetColumn(3);
        result.min = Vector3.Min(xa, xb) + Vector3.Min(ya, yb) + Vector3.Min(za, zb) + column3;
        result.max = Vector3.Max(xa, xb) + Vector3.Max(ya, yb) + Vector3.Max(za, zb) + column3;
        return result;
    }

    public static void TransformBounds(ref Matrix4x4 matrix, Vector3 min, Vector3 max, out Vector3 minResult, out Vector3 maxResult)
    {
        Vector3 column0 = matrix.GetColumn(0);
        Vector3 xa = column0 * min.x;
        Vector3 xb = column0 * max.x;

        Vector3 column1 = matrix.GetColumn(1);
        Vector3 ya = column1 * min.y;
        Vector3 yb = column1 * max.y;

        Vector3 column2 = matrix.GetColumn(2);
        Vector3 za = column2 * min.z;
        Vector3 zb = column2 * max.z;

        Vector3 column3 = matrix.GetColumn(3);
        minResult = Vector3.Min(xa, xb) + Vector3.Min(ya, yb) + Vector3.Min(za, zb) + column3;
        maxResult = Vector3.Max(xa, xb) + Vector3.Max(ya, yb) + Vector3.Max(za, zb) + column3;
        //Vector3 minT = matrix.MultiplyPoint(min);
        //Vector3 maxT = matrix.MultiplyPoint(max);
        //minResult = Vector3.Min(minT, maxT);
        //maxResult = Vector3.Max(minT, maxT);
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

public struct GPUVertex
{
    public Vector4 position;
    public Vector4 uv;

    public GPUVertex(Vector4 _position, Vector4 _uv)
    {
        position = _position;
        uv = _uv;
    }
}
public struct GPUBVHNode
{
    //public GPUBounds b1;
    //public GPUBounds b2;
    //public int idx1;
    //public int idx2;
    //int c0;    //extend to 64bytes
    //int c1;
    //public Vector4 b0xy;  //bounding box b0 min.x, max.x min.y max.y
    //public Vector4 b1xy;  //bounding box b1 min.x, max.x min.y max.y
    //public Vector4 b01z;  //bounding box b0.min.z, b0.max.z b1.min.z b1.max.z
    public Vector3 b0min;
    public Vector3 b0max;
    public Vector3 b1min;
    public Vector3 b1max;
    //x left node array index, y right node array index, z left node primitive's num if it is leaf, w right node primitive's num if leaf
    //if z and w < 0, is a meshinstance node, zw is the meshinstance id.
    public Vector4 cids;  
}

//do not support textures
public struct GPUMaterial
{
    //public BSDFMaterial.BSDFType materialType;
    public Vector4 materialParams;  //x-materialtype, y-sigma, z-roughness
    public Color kd;
    public Color ks;
    public Color kr;

    public GPUMaterial(int type, float sigma, float roughness, Color kd, Color ks, Color kr)
    {
        this.kd = kd.linear;
        this.ks = ks.linear;
        this.kr = kr.linear;
        materialParams.x = type;
        materialParams.y = sigma;
        materialParams.z = roughness;
        materialParams.w = 0;
    }
}

public struct GPUPathRadiance
{
    public Vector3 li;
    public Vector3 beta;
}

public struct GPUShadowRay
{
    public Vector3 p0;   //isect position
    public Vector3 p1;   //light sample point position
    public Vector3 radiance;  //light radiance
    //float3 lightNormal;  //light sample point normal
    //mis weight
    //public float weight;
    public float lightSourcePdf;        //Light Radiance pdf
    public float lightPdf;   //light sampling pdf
    public float visibility; //1 is visible, 0 invisible
    public float lightIndex;
}

public struct GPUDistributionDiscript
{
    public int start;
    public int num;
    public int unum; //2D distribution
    public int c;
}



