using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ShadowCasterManager : MonoBehaviour
{
    public Transform mainCamera;
    public Transform mainLight;
    public Transform followTransform;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnEnable()
    {
        CascadeShadow cascade = GetComponent<CascadeShadow>();
        if (cascade != null && !cascade.enabled)
            cascade.enabled = true;
        if (cascade == null && followTransform != null && mainLight != null)
        {
            Shader.EnableKeyword("_CASCADE_SHADOW");
            cascade = CascadeShadow.CreateShadowCaster(followTransform, mainLight, new ShadowCreateParams(), Shader.Find("liangairan/shadow/RenderToShadow"), gameObject);
            cascade.SetMainCamera(mainCamera);
        }
        
    }

    private void OnDisable()
    {
        CascadeShadow cascade = GetComponent<CascadeShadow>();
        if (cascade != null)
        {
            cascade.enabled = false;
        }
    }
}
