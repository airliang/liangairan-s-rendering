using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//we can use as a triangle
public class Primitive
{
    //public int vertexOffset;      //mesh vertex offset in the whole scene vertexbuffer
    //public int triangleOffset;    //triangle offset in the whole scene trianglebuffer
    public Vector3Int triIndices;
    //public int transformId; //the primitive belong to the transform
    //public int faceIndex;   //mesh triangle indice start
    public Bounds worldBound = new Bounds();

    Bounds BuildBounds(Vector3 p0, Vector3 p1, Vector3 p2)
    {
        Bounds bounds = new Bounds();
        //bounds.min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        //bounds.max = new Vector3(float.MinValue, float.MinValue, float.MinValue); ;
        Vector3 min = new Vector3(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Min(p0.z, p1.z));
        Vector3 max = new Vector3(Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y), Mathf.Max(p0.z, p1.z));
        bounds.min = Vector3.Min(min, p2);
        bounds.max = Vector3.Max(max, p2);

        return bounds;
    }
    public Primitive(int tri0, int tri1, int tri2, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        //vertexOffset = vOffset;
        //triangleOffset = tOffset;
        //transformId = tId;
        //faceIndex = fId;
        triIndices.x = tri0;
        triIndices.y = tri1;
        triIndices.z = tri2;

        //Vector3 p0 = transform.TransformPoint(mesh.vertices[mesh.triangles[fId * 3]]);
        //Vector3 p1 = transform.TransformPoint(mesh.vertices[mesh.triangles[fId * 3 + 1]]);
        //Vector3 p2 = transform.TransformPoint(mesh.vertices[mesh.triangles[fId * 3 + 2]]);

        worldBound = BuildBounds(p0, p1, p2);
    }
}


