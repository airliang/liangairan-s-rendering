using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LODObject
{
    public Transform transform;
    public Material[] opaqueMaterials;
    public Material[] transparentMaterials;
}

[ExecuteInEditMode]
public class CustomLOD : MonoBehaviour
{
    public Transform[] lods;
    private int lastLOD = -1;
    private int curLOD = 0;
    private float[] transparents = new float[3];
    private Bounds bounds;
    private float transparentSpeed = 1.0f;
    private bool transparentAdd = false;
    private int appearLOD = -1;
    private int dissapearLOD = -1;
    private List<Material[]> lodMaterials = new List<Material[]>();

    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < 3; ++i)
        {
            transparents[i] = 0;
            MeshRenderer renderer = lods[i].gameObject.GetComponent<MeshRenderer>();
            if (i == 0)
            {
                transparents[0] = 1;
                bounds = renderer.bounds;
            }
            lodMaterials.Add(renderer.sharedMaterials);
            for (int j = 0; j < renderer.sharedMaterials.Length; ++j)
                renderer.sharedMaterials[j].SetFloat("_Transparency", transparents[i]);
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private int CaculateLOD(Camera camera)
    {
        Vector3 centerWorld = transform.localToWorldMatrix.MultiplyPoint(bounds.center);
        Vector3 centerScreen = camera.WorldToScreenPoint(centerWorld);
        Vector3 extendWorld = transform.localToWorldMatrix.MultiplyVector(bounds.extents);
        Vector3 extendScreenMax = camera.WorldToScreenPoint(centerWorld + extendWorld);
        Vector3 extendScreenMin = camera.WorldToScreenPoint(centerWorld - extendWorld);
        float Area = Mathf.Abs((extendScreenMax.x - extendScreenMin.x) * (extendScreenMax.y - extendScreenMin.y));
        float ratio = Area / (Screen.width * Screen.height);
        //Debug.Log("Screen Area is:" + Area + ". ratio = " + ratio);
        //float dis =  Vector3.Distance(Camera.current.transform.position, lods[curLOD].position);
        if (ratio > 0.15)
            return 0;
        if (ratio > 0.01)
            return 1;

        return 2;
    }

    private void OnWillRenderObject()
    {
        Camera cam = Camera.current;
        if (cam == null || cam.cameraType == CameraType.Preview || cam.name == "ShadowCaster")
        {
            return;
        }
        int lod = CaculateLOD(cam);
        if (lod != curLOD)
        {
            //for (int i = 0; i < 3; ++i)
            //{
            //    MeshRenderer renderer = lods[i].gameObject.GetComponent<MeshRenderer>();
                
            //    transparents[i] = renderer.sharedMaterial.GetFloat("_Transparency");
            //}
            //MeshRenderer renderer = lods[lod].transform.gameObject.GetComponent<MeshRenderer>();

            
            //transparents[lod] = 1;
            //for (int i = 0; i < mtls.Length; ++i)
            //    mtls[i].SetFloat("_Transparency", transparents[lod]);

            //MeshRenderer[] curRenderer = lods[curLOD].gameObject.GetComponents<MeshRenderer>();

            lastLOD = curLOD;
            curLOD = lod;
            if (!lods[curLOD].gameObject.activeSelf)
            {
                lods[curLOD].gameObject.SetActive(true);
            }
            //出现时要设置成opaque材质
            /*
            renderer.materials = lods[lod].opaqueMaterials;
            MeshRenderer[] curRenderer = lods[curLOD].transform.gameObject.GetComponents<MeshRenderer>();
            for (int i = 0; i < curRenderer.Length; ++i)
            {
                //curRenderer[i].materials = lods[curLOD].transparentMaterials[i];
                //curRenderer[i].sharedMaterial.SetFloat("_Transparency", transparents[curLOD]);
            }
            //disapearLodMat = curRenderer[0].sharedMaterials;
            curRenderer[0].materials = lods[curLOD].transparentMaterials;
            disapearLodMat = curRenderer[0].materials;
            disapearLOD = curLOD;
            curLOD = lod;
            if (!lods[curLOD].transform.gameObject.activeSelf)
            {
                lods[curLOD].transform.gameObject.SetActive(true);
            }
            */
            Debug.Log("lod change!");
        }

        UpdateLODTransparency();
    }

    private void UpdateLODTransparency()
    {
        if (lastLOD > -1)
        {
            /*
            if (lastLOD < curLOD)
            {
                
                if (transparents[curLOD] < 1.0f)
                {
                    appearLOD = curLOD;
                }
            }
            else
            {
                if (transparents[lastLOD] < 1.0f)
                    appearLOD = lastLOD;
            }
            */
            if (transparents[curLOD] < 1.0f)
            {
                appearLOD = curLOD;
            }

            if (appearLOD > -1)
            {
                float transparency = UpdateAppearLOD();
                if (transparency >= 1.0f)
                {
                    appearLOD = -1;
                    dissapearLOD = lastLOD;
                }
            }

            if (dissapearLOD > -1)
            {
                float transparency = UpdateDissapearLOD();
                if (transparency <= 0.0f)
                {
                    //整个lod切换流程完毕
                    lods[dissapearLOD].gameObject.SetActive(false);
                    dissapearLOD = -1;
                    lastLOD = -1;
                }
            }
            /*
            if (lastLOD < curLOD)
            {
                //curlod transparency add
                //当前lod渐渐出现
                transparents[curLOD] += Time.deltaTime * transparentSpeed;
                transparents[curLOD] = Mathf.Min(1, transparents[curLOD]);
                for (int i = 0; i < lodMaterials[curLOD].Length; ++i)
                {
                    lodMaterials[curLOD][i].SetFloat("_Transparency", transparents[curLOD]);
                }

                if (transparents[curLOD] == 1)
                {
                    lods[lastLOD].gameObject.SetActive(false);
                    lastLOD = -1;
                    //这个时候可以让last lod渐渐消失
                }
            }

            if (lastLOD > curLOD)
            {
                //lastlod transparency sub
                transparents[lastLOD] -= Time.deltaTime * transparentSpeed;
                transparents[lastLOD] = Mathf.Max(0, transparents[lastLOD]);
                for (int i = 0; i < lodMaterials[lastLOD].Length; ++i)
                {
                    lodMaterials[lastLOD][i].SetFloat("_Transparency", transparents[lastLOD]);
                }

                if (transparents[lastLOD] == 0)
                {
                    lods[lastLOD].gameObject.SetActive(false);
                    lastLOD = -1;
                }
            }
            */
        }
    }

    private float UpdateAppearLOD()
    {
        transparents[appearLOD] += Time.deltaTime * transparentSpeed;
        transparents[appearLOD] = Mathf.Min(1, transparents[appearLOD]);
        for (int i = 0; i < lodMaterials[appearLOD].Length; ++i)
        {
            lodMaterials[appearLOD][i].SetFloat("_Transparency", transparents[appearLOD]);
        }

        return transparents[appearLOD];
    }

    private float UpdateDissapearLOD()
    {
        transparents[dissapearLOD] -= Time.deltaTime * transparentSpeed;
        transparents[dissapearLOD] = Mathf.Max(0, transparents[dissapearLOD]);
        for (int i = 0; i < lodMaterials[dissapearLOD].Length; ++i)
        {
            lodMaterials[dissapearLOD][i].SetFloat("_Transparency", transparents[dissapearLOD]);
        }

        return transparents[dissapearLOD];
    }

    void OnDestroy()
    {
        lodMaterials.Clear();
    }
}
