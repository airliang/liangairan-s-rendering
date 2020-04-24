using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shape : MonoBehaviour
{
    public enum ShapeType
    {
        sphere,
        disk,
        triangleMesh,
    }

    public ShapeType shapeType;
    public bool isAreaLight = false;
    public Color lightColor = Color.white;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
