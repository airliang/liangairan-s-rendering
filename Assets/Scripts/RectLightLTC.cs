using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class RectLightLTC : MonoBehaviour
{
    private List<Vector4> rectPoints = new List<Vector4>();
    private Vector4[] rectPointsInWorld = new Vector4[4];
    public Color lightColor = Color.white;
    public float intensity = 1;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    void UpdateRectPoints()
    {
        if (rectPoints.Count == 0)
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            for (int i = 0; i < meshFilter.sharedMesh.vertexCount; ++i)
            {
                rectPoints.Add(meshFilter.sharedMesh.vertices[i]);
            }
        }
        
        for (int i = 0; i < 4; ++i)
        {
            rectPointsInWorld[i] = transform.TransformPoint(rectPoints[i]);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnWillRenderObject()
    {
        UpdateRectPoints();
        //Shader.SetGlobalVectorArray("_RectPoints", rectPointsInWorld);
        Shader.SetGlobalVector("_RectCenter", transform.position);
        Shader.SetGlobalVector("_RectDirX", transform.right);
        Shader.SetGlobalVector("_RectDirY", transform.up);
        Shader.SetGlobalVector("_RectSize", transform.localScale);
        Shader.SetGlobalColor("AreaLightColor", lightColor * intensity);
    }
}
