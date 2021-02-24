using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class RectLightLTC : MonoBehaviour
{
    //private List<Vector4> rectPoints = new List<Vector4>();
    private Vector4[] rectPointsInWorld = new Vector4[4];
    public Color lightColor = Color.white;
    public float intensity = 1;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    void UpdateRectPoints()
    {
        float halfX = transform.localScale.x * 0.5f;
        float halfY = transform.localScale.y * 0.5f;
        rectPointsInWorld[0] = transform.position - transform.right * halfX
            - transform.up * halfY;

        rectPointsInWorld[1] = transform.position + transform.right * halfX
            - transform.up * halfY;

        rectPointsInWorld[2] = transform.position + transform.right * halfX
            + transform.up * halfY;

        rectPointsInWorld[3] = transform.position - transform.right * halfX
            + transform.up * halfY;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnWillRenderObject()
    {
        UpdateRectPoints();
        Shader.SetGlobalVectorArray("_RectPoints", rectPointsInWorld);
        Shader.SetGlobalVector("_RectCenter", Vector3.zero);
        Shader.SetGlobalVector("_RectDirX", Vector3.right);
        Shader.SetGlobalVector("_RectDirY", Vector3.up);
        Shader.SetGlobalVector("_RectSize", transform.localScale);
        Shader.SetGlobalMatrix("_RectWorldTransform", transform.localToWorldMatrix);
        Shader.SetGlobalColor("AreaLightColor", lightColor * intensity);
    }
}
