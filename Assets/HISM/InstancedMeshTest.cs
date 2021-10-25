using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

public class InstancedMeshTest : MonoBehaviour
{
    // Start is called before the first frame update
    public Material instanceMaterial;
    List<Color> colors = new List<Color>();
    NativeArray<Vector4> colorsRender;
    public int MAX_INSTACE_NUM = 8;
    void Start()
    {
        if (instanceMaterial != null)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            MeshFilter filter = sphere.GetComponent<MeshFilter>();
            Mesh[] meshes = new Mesh[1];
            meshes[0] = filter.mesh;

            HierarchicalInstancedStaticMesh instancedMesh = HierarchicalInstancedRenderer.CreateHierarchicalInstancedMesh(meshes, "sphere", instanceMaterial, 32, OnDrawInstance, OnBuildTree);
            

            float volumeSize = MAX_INSTACE_NUM * 4.0f;
            float startPosX = -volumeSize / 2.0f;
            float startPosY = 1.0f;
            float startPosZ = -volumeSize / 2.0f;
            int totalSize = MAX_INSTACE_NUM * MAX_INSTACE_NUM * MAX_INSTACE_NUM;
            colorsRender = new NativeArray<Vector4>(totalSize, Allocator.Persistent);

            for (int i = 0; i < MAX_INSTACE_NUM; ++i)
                for (int j = 0; j < MAX_INSTACE_NUM; ++j)
                    for (int k = 0; k < MAX_INSTACE_NUM; ++k)
                    {
                        Vector3 position = Vector3.zero;
                        position.y = startPosY + i * 4.0f;
                        position.z = startPosZ + j * 4.0f;
                        position.x = startPosX + k * 4.0f;
                        
                        Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                        instancedMesh.AddInstance(new InstanceData(ref matrix));

                        //int index = i * MAX_INSTACE_NUM + j * MAX_INSTACE_NUM + k;
                        if (i == 0 && j == 0)
                        {
                            if (k == 0)
                                colors.Add(Color.red);
                            else
                                colors.Add(Color.blue);
                        }
                        else
                            colors.Add(Color.white);
                    }

            sphere.SetActive(false);
        }
    }

    void OnDrawInstance(MaterialPropertyBlock materialPropertyBlock, int FirstInstance, int LastInstance)
    {
        NativeArray<Vector4> colorArray = colorsRender.GetSubArray(FirstInstance, LastInstance - FirstInstance + 1);
        materialPropertyBlock.SetVectorArray("_MainColor", colorArray.ToArray());
    }

    void OnBuildTree(List<int> sortedIndex)
    {
        for (int i = 0; i < sortedIndex.Count; ++i)
        {
            colorsRender[i] = colors[sortedIndex[i]];
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        if (colorsRender.IsCreated)
            colorsRender.Dispose();
    }
}
