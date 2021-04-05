using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class Raytracing : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log(BVHInterface.Add(5, 6));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
