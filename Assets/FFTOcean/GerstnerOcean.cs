using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GerstnerWave
{
    //波的方向
    public Vector2 direction;
    //圆形波的中心位置(0, 1)
    public Vector2 circle;
    //波长
    public float crest2crest;
    public float amplitude;
    public float speed;
    public float steepness;
}

[ExecuteInEditMode]
public class GerstnerOcean : MonoBehaviour
{
    public int resolution = 512;
    public float gridLength = 1.0f;

    public GerstnerWave wave1;
    public GerstnerWave wave2;
    public GerstnerWave wave3;

    private Material oceanMaterial;
    private WaterMesh waterMesh = null;

    bool materialDirty = false;
    // Start is called before the first frame update
    void Start()
    {
        waterMesh = new WaterMesh();
        Mesh oceanMesh = waterMesh.CreateWaterMesh(gridLength, resolution);
        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            gameObject.AddComponent<MeshRenderer>();
        }

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }
        meshFilter.mesh = oceanMesh;

        if (oceanMaterial == null)
        {
            oceanMaterial = new Material(Shader.Find("liangairan/ocean/ocean"));

            gameObject.GetComponent<Renderer>().sharedMaterial = oceanMaterial;
            
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
