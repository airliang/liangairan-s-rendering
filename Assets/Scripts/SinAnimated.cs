using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class SinAnimated : MonoBehaviour
{
    public float maxHeight = 1.0f;
    public float speed = 1.0f;
    private Vector3 position;
    // Start is called before the first frame update
    void Start()
    {
        position = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        position.y = Mathf.Sin(Time.time * speed) * maxHeight;
        transform.position = position;
    }
}
