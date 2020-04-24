using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BSDFMaterial : MonoBehaviour
{
    public enum BSDFType
    {
        Matte,
        Plastic,
        Mirror,
    }

    [Range(0.0f, 1.0f)]
    public float roughness;

    public Color kd = Color.white;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
