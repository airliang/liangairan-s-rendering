using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

[ExecuteAlways]
public class HierarchicalInstancedRenderer : MonoBehaviour
{
    public ComputeShader CullingCS;
    public static bool GPUDriven = false;
    Dictionary<Mesh, HierarchicalInstancedStaticMesh> m_InstancedStaticMeshes = new Dictionary<Mesh, HierarchicalInstancedStaticMesh>();
    [HideInInspector]
    public static HierarchicalInstancedRenderer instance;
    int cullingKernel;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        HierarchicalInstancedStaticMesh instancedMesh = null;

        var enumerator = m_InstancedStaticMeshes.GetEnumerator();
        while (enumerator.MoveNext())
        {
            instancedMesh = enumerator.Current.Value;
            instancedMesh.Update();
        }
    }

    private void OnEnable()
    {
        instance = this;
        if (CullingCS != null)
        {
            cullingKernel = CullingCS.FindKernel("CSMain");
        }
    }

    private void OnDisable()
    {
        instance = null;
    }

    private void OnDestroy()
    {
        Dictionary<Mesh, HierarchicalInstancedStaticMesh>.Enumerator enumerator = m_InstancedStaticMeshes.GetEnumerator();
        while (enumerator.MoveNext())
        {
            HierarchicalInstancedStaticMesh instancedMesh = enumerator.Current.Value;
            instancedMesh.Clear();
        }
        m_InstancedStaticMeshes.Clear();
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        Matrix4x4 VPMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;
        Dictionary<Mesh, HierarchicalInstancedStaticMesh>.Enumerator enumerator = m_InstancedStaticMeshes.GetEnumerator();
        while (enumerator.MoveNext())
        {
            HierarchicalInstancedStaticMesh instancedMesh = enumerator.Current.Value;
            DrawInstancedMesh(ref planes, VPMatrix, instancedMesh);
        }
    }

    public static HierarchicalInstancedStaticMesh CreateHierarchicalInstancedMesh(Mesh[] meshes, string name, Material material, int maxInstancePerNode, HierarchicalInstancedStaticMesh.OnDrawInstanced onDrawCallback,
            HierarchicalInstancedStaticMesh.OnBuildTree onBuildCallback)
    {
        if (instance == null)
        {
            GameObject instancedRenderer = new GameObject("Instanced Static Mesh Renderer");
            instance = instancedRenderer.AddComponent<HierarchicalInstancedRenderer>();
        }
        if (instance.m_InstancedStaticMeshes.ContainsKey(meshes[0]))
        {
            HierarchicalInstancedStaticMesh instancedMesh = instance.m_InstancedStaticMeshes[meshes[0]];
            return instancedMesh;
        }
        
        //GameObject gameObject = new GameObject(name);
        HierarchicalInstancedStaticMesh instancedStaticMesh = new HierarchicalInstancedStaticMesh();
        instancedStaticMesh.branchFactor = maxInstancePerNode;
        instancedStaticMesh.instanceMesh = new HierarchicalInstanceMesh(meshes);
        instancedStaticMesh.material = new Material(material);
        instancedStaticMesh.material.enableInstancing = true;
        instancedStaticMesh.OnDrawCallback = onDrawCallback;
        instancedStaticMesh.onBuildTreeFunc = onBuildCallback;
        instance.m_InstancedStaticMeshes.Add(meshes[0], instancedStaticMesh);
        return instancedStaticMesh;
    }
    void DrawInstancedMesh(ref Plane[] cameraPlanes, Matrix4x4 ViewPorjMatrix, HierarchicalInstancedStaticMesh instancedMesh)
    {
        if (GPUDriven && CullingCS != null)
        {
            //instancedMesh.GetCulledTransformBuffer(0).SetCounterValue(0);
            //instancedMesh.GetCulledInvTransformBuffer(0).SetCounterValue(0);
            instancedMesh.m_visibleBuffer.SetCounterValue(0);

            CullingCS.SetBuffer(cullingKernel, "_InstanceNodeBuffer", instancedMesh.m_instanceNodeBuffer);
            CullingCS.SetBuffer(cullingKernel, "_ObjectToWorldBuffer", instancedMesh.m_localToWorldBuffer);
            CullingCS.SetBuffer(cullingKernel, "_WorldToObjectBuffer", instancedMesh.m_worldToLocalBuffer);
            //cmd.SetComputeBufferParam(cullingCS, cullingKernel, "_TransformBuffer", instancedMesh.GetCulledTransformBuffer(0));
            //cmd.SetComputeBufferParam(cullingCS, cullingKernel, "_InverseTransformBuffer", instancedMesh.GetCulledInvTransformBuffer(0));
            CullingCS.SetBuffer(cullingKernel, "_ArgsBuffer", instancedMesh.m_instancesArgsBuffer);
            CullingCS.SetBuffer(cullingKernel, "_VisibleBuffer", instancedMesh.m_visibleBuffer);
            CullingCS.SetMatrix("_MATRIX_VP", ViewPorjMatrix);
            int threadGroupX = Mathf.CeilToInt(instancedMesh.InstancesNum / 64.0f);

            CullingCS.Dispatch(cullingKernel, threadGroupX, 1, 1);

            for (int i = 0; i < instancedMesh.LODs; ++i)
            {

                ComputeBuffer.CopyCount(instancedMesh.m_visibleBuffer, instancedMesh.m_instancesArgsBuffer, (int)i * 5 * sizeof(int) + 4);
                MaterialPropertyBlock propertyBlock = instancedMesh.materialPropertyBlock(i);
                if (instancedMesh.OnDrawCallback != null)
                {
                    instancedMesh.OnDrawCallback(propertyBlock, 0, instancedMesh.InstancesNum - 1);
                }

                propertyBlock.SetBuffer("_ObjectToWorldBuffer", instancedMesh.m_localToWorldBuffer);
                propertyBlock.SetBuffer("_WorldToObjectBuffer", instancedMesh.m_worldToLocalBuffer);
                propertyBlock.SetBuffer("_VisibleBuffer", instancedMesh.m_visibleBuffer);
                propertyBlock.SetBuffer("_ArgsBuffer", instancedMesh.m_instancesArgsBuffer);
                Mesh mesh = instancedMesh.GetMesh(i);
                Graphics.DrawMeshInstancedIndirect(mesh, 0, instancedMesh.material, instancedMesh.worldBound, instancedMesh.m_instancesArgsBuffer, 0, propertyBlock);
                //cmd.SetComputeBufferParam(cullingCS, cullingKernel, "_VisibleBuffer", null);
            }


            uint[] debugData = new uint[5];
            instancedMesh.m_instancesArgsBuffer.GetData(debugData);
            uint a = debugData[1];
        }
        else
        {
            if (instancedMesh.HISM)
            {

                HISMClusterTree tree = instancedMesh.ClusterTree;
                HISMClusterNode node = tree.Nodes[0];
                Stack<HISMClusterNode> stack = new Stack<HISMClusterNode>();
                stack.Push(node);
                while (stack.Count > 0)
                {
                    node = stack.Pop();
                    Bounds bounds = new Bounds();
                    bounds.SetMinMax(node.boundMin, node.boundMax);

                    if (GeometryUtility.TestPlanesAABB(cameraPlanes, bounds))
                    {
                        //À„lod
                        int lod = 0;
                        Mesh mesh = instancedMesh.GetMesh(lod);
                        //≈–∂œ «∑Ò «cluster
                        if (node.LastInstance - node.FirstInstance + 1 <= instancedMesh.branchFactor)
                        {
                            if (instancedMesh.OnDrawCallback != null)
                            {
                                instancedMesh.OnDrawCallback(instancedMesh.materialPropertyBlock(lod), node.FirstInstance, node.LastInstance);
                            }
                            NativeArray<Matrix4x4> matrices = instancedMesh.renderingMatrices.GetSubArray(node.FirstInstance, node.LastInstance - node.FirstInstance + 1); //new NativeArray<Matrix4x4>(node.LastInstance - node.FirstInstance + 1, Allocator.Temp);
                            Graphics.DrawMeshInstanced(mesh, 0, instancedMesh.material, matrices.ToArray(), matrices.Length, instancedMesh.materialPropertyBlock(lod));
                            RenderDebug.DrawDebugBound(node.boundMin, node.boundMax, Color.blue);
                        }
                        else
                        {
                            for (int child = node.FirstChild; child <= node.LastChild; ++child)
                            {
                                stack.Push(tree.Nodes[child]);
                            }
                        }
                    }

                }
            }
        }
    }
}
