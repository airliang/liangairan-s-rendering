using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenQuad
{
    private Rect screenRect;

    private Mesh mQuadMesh;
    private Vector3[] mVertices;
    private Vector2[] mUVs;
    private int[] mTriangles;
    private GameObject mScreenQuad;

    public ScreenQuad(Rect rect)
    {
        screenRect = rect;
        mVertices = new Vector3[4];
        mVertices[0] = new Vector3(screenRect.xMin, screenRect.yMin, 0.0f);
        mVertices[1] = new Vector3(screenRect.xMax, screenRect.yMin, 0.0f);
        mVertices[2] = new Vector3(screenRect.xMin, screenRect.yMax, 0.0f);
        mVertices[3] = new Vector3(screenRect.xMax, screenRect.yMax, 0.0f);

        mUVs = new Vector2[4];
        mUVs[0] = new Vector2(0.0f, 0.0f);
        mUVs[1] = new Vector2(1.0f, 0.0f);
        mUVs[2] = new Vector2(0.0f, 1.0f);
        mUVs[3] = new Vector2(1.0f, 1.0f);

        mTriangles = new int[6];
        mTriangles[0] = 0;
        mTriangles[1] = 1;
        mTriangles[2] = 2;
        mTriangles[3] = 1;
        mTriangles[4] = 3;
        mTriangles[5] = 2;

        mQuadMesh = new Mesh();
        mQuadMesh.vertices = mVertices;
        mQuadMesh.uv = mUVs;
        mQuadMesh.triangles = mTriangles;

        mScreenQuad = new GameObject("ScreenQuad");
        mScreenQuad.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = mScreenQuad.AddComponent<MeshFilter>();
        meshFilter.mesh = mQuadMesh;

        //mScreenQuad.transform.SetParent(Camera.current.transform);
        //mScreenQuad.transform.localPosition = new Vector3(0, 0, 10);
    }

    public void Clear()
    {
        mQuadMesh.Clear();
#if UNITY_EDITOR
        Object.DestroyImmediate(mScreenQuad);
#else
        Object.Destroy(mScreenQuad);
#endif
        mScreenQuad = null;
    }

    public void SetMaterial(Material material)
    {
        if (mScreenQuad != null)
        {
            MeshRenderer meshRenderer = mScreenQuad.GetComponent<MeshRenderer>();
            meshRenderer.material = material;
        }
        
    }

    public void SetParent(Transform parent, Vector3 offset)
    {
        if (mScreenQuad)
        {
            mScreenQuad.transform.localPosition = offset;
            mScreenQuad.transform.SetParent(parent, false);
            //mScreenQuad.transform.position = parent.position + parent.forward * 10;
        }
    }
}
