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
#endif
}
