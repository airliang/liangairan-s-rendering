using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderDebug
{
#if UNITY_EDITOR
    public static void DrawDebugBound(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
    {
        Debug.DrawLine(new Vector3(minX, minY, minZ), new Vector3(minX, minY, maxZ));
        Debug.DrawLine(new Vector3(minX, minY, minZ), new Vector3(maxX, minY, minZ));
        Debug.DrawLine(new Vector3(minX, minY, minZ), new Vector3(minX, maxY, minZ));
        Debug.DrawLine(new Vector3(maxX, maxY, maxZ), new Vector3(maxX, maxY, minZ));
        Debug.DrawLine(new Vector3(maxX, maxY, maxZ), new Vector3(minX, maxY, maxZ));
        Debug.DrawLine(new Vector3(maxX, maxY, maxZ), new Vector3(maxX, minY, maxZ));

        Debug.DrawLine(new Vector3(minX, maxY, minZ), new Vector3(maxX, maxY, minZ));
        Debug.DrawLine(new Vector3(minX, maxY, minZ), new Vector3(minX, maxY, maxZ));

        Debug.DrawLine(new Vector3(maxX, minY, minZ), new Vector3(maxX, maxY, minZ));

        Debug.DrawLine(new Vector3(minX, maxY, maxZ), new Vector3(minX, maxY, minZ));
        Debug.DrawLine(new Vector3(minX, maxY, maxZ), new Vector3(minX, minY, maxZ));

        Debug.DrawLine(new Vector3(maxX, minY, minZ), new Vector3(maxX, minY, maxZ));
        Debug.DrawLine(new Vector3(minX, minY, maxZ), new Vector3(maxX, minY, maxZ));
    }
#endif
}
