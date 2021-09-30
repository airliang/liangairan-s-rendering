using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderDebug
{
#if UNITY_EDITOR
    public static void DrawDebugBound(float minX, float maxX, float minY, float maxY, float minZ, float maxZ, Color color)
    {
        Debug.DrawLine(new Vector3(minX, minY, minZ), new Vector3(minX, minY, maxZ), color);
        Debug.DrawLine(new Vector3(minX, minY, minZ), new Vector3(maxX, minY, minZ), color);
        Debug.DrawLine(new Vector3(minX, minY, minZ), new Vector3(minX, maxY, minZ), color);
        Debug.DrawLine(new Vector3(maxX, maxY, maxZ), new Vector3(maxX, maxY, minZ), color);
        Debug.DrawLine(new Vector3(maxX, maxY, maxZ), new Vector3(minX, maxY, maxZ), color);
        Debug.DrawLine(new Vector3(maxX, maxY, maxZ), new Vector3(maxX, minY, maxZ), color);

        Debug.DrawLine(new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ), color);
        Debug.DrawLine(new Vector3(minX, maxY, minZ), new Vector3(minX, maxY, maxZ), color);

        Debug.DrawLine(new Vector3(maxX, minY, minZ), new Vector3(maxX, maxY, minZ), color);

        Debug.DrawLine(new Vector3(minX, maxY, maxZ), new Vector3(minX, maxY, minZ), color);
        Debug.DrawLine(new Vector3(minX, maxY, maxZ), new Vector3(minX, minY, maxZ), color);

        Debug.DrawLine(new Vector3(maxX, minY, minZ), new Vector3(maxX, minY, maxZ), color);
        Debug.DrawLine(new Vector3(minX, minY, maxZ), new Vector3(maxX, minY, maxZ), color);
    }

    public static void DrawDebugBound(Vector3 min, Vector3 max, Color color)
    {
        Debug.DrawLine(min, new Vector3(min.x, min.y, max.z), color);
        Debug.DrawLine(min, new Vector3(max.x, min.y, min.z), color);
        Debug.DrawLine(min, new Vector3(min.x, max.y, min.z), color);
        Debug.DrawLine(max, new Vector3(max.x, max.y, min.z), color);
        Debug.DrawLine(max, new Vector3(min.x, max.y, max.z), color);
        Debug.DrawLine(max, new Vector3(max.x, min.y, max.z), color);

        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), color);

        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), color);

        Debug.DrawLine(new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), color);
        Debug.DrawLine(new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z), color);

        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), color);
        Debug.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), color);
    }

    public static void DrawNormal(Vector3 p0, Vector3 p1, Vector3 p2, float u, float v)
    {
        Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
        //if (normal.magnitude < 1)
        normal.Normalize();
        Vector3 barycenter = p0 * (1.0f - u - v) + p1 * u + p2 * v;
        Debug.DrawLine(barycenter, barycenter + normal, Color.green);
    }

    public static void DrawTriangle(Vector3 p0, Vector3 p1, Vector3 p2, Color color)
    {
        Debug.DrawLine(p0, p1, color);
        Debug.DrawLine(p0, p2, color);
        Debug.DrawLine(p2, p1, color);
    }
#endif
}
