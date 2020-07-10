using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterMesh
{
    Mesh mMesh;
    //GameObject mOceanObject;
    private Vector3[] mVertices;
    private Vector3[] mNormals;
    private Vector2[] mUVs;
    private int[] mTriangles;

    public Mesh CreateWaterMesh(float vertexGridLength, int resolution)
    {
        mVertices = new Vector3[resolution * resolution];
        mNormals = new Vector3[resolution * resolution];
        mUVs = new Vector2[resolution * resolution];

        mTriangles = new int[(resolution - 1) * (resolution - 1) * 6];

        int nIndex = 0;
        for (int i = 0; i < resolution; ++i)
        {
            for (int j = 0; j < resolution; ++j)
            {
                nIndex = i * resolution + j;

                mVertices[nIndex] = new Vector3(i * vertexGridLength, 0, j * vertexGridLength);

                mUVs[nIndex] = new Vector2((float)i / (resolution - 1), (float)j / (resolution - 1));

                mNormals[nIndex] = new Vector3(0, 1, 0);
            }
        }

        nIndex = 0;
        for (int i = 0; i < resolution - 1; ++i)
        {
            for (int j = 0; j < resolution - 1; ++j)
            {
                mTriangles[nIndex++] = i * resolution + j;
                mTriangles[nIndex++] = i * resolution + j + 1;
                mTriangles[nIndex++] = (i + 1) * resolution + j;
                mTriangles[nIndex++] = i * resolution + j + 1;
                mTriangles[nIndex++] = (i + 1) * resolution + j + 1;
                mTriangles[nIndex++] = (i + 1) * resolution + j;
            }
        }

        if (mMesh != null)
        {
            mMesh.Clear();
        }
        mMesh = new Mesh();
        mMesh.vertices = mVertices;
        mMesh.uv = mUVs;
        mMesh.triangles = mTriangles;

        return mMesh;
    }

    public void Clear()
    {
        if (mMesh != null)
        {
            mMesh.Clear();
            mMesh = null;
        }
    }
}
