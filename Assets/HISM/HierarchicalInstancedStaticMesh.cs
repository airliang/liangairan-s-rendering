using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System.Runtime.InteropServices;


public class InstanceData
{
    public Matrix4x4 worldMatrix;

    public InstanceData(ref Matrix4x4 mat)
    {
        worldMatrix = mat;
    }
}

struct InstanceBound
{
    Vector3 min;
    Vector3 max;

    public InstanceBound(Vector3 boundMin, Vector3 boundMax)
    {
        min = boundMin;
        max = boundMax;
    }
}

public class HierarchicalInstanceMesh
{
    public Mesh[] m_meshes;
    public MaterialPropertyBlock[] m_materialPropertyBlocks;
    public HierarchicalInstanceMesh(Mesh[] meshes)
    {
        m_meshes = meshes;
        if (meshes.Length > 0)
        {
            m_materialPropertyBlocks = new MaterialPropertyBlock[meshes.Length];
            for (int i = 0; i < m_materialPropertyBlocks.Length; ++i)
            {
                m_materialPropertyBlocks[i] = new MaterialPropertyBlock();
            }
        }
    }
}

public class HierarchicalInstancedStaticMesh
{
    List<InstanceData> instanceDatas = new List<InstanceData>();
    //public List<InstancedDrawCall> instanceDrawcalls = new List<InstancedDrawCall>();
    List<Matrix4x4> worldMatrices = new List<Matrix4x4>();

    
    public NativeArray<Matrix4x4> renderingMatrices;
    public NativeArray<Matrix4x4> renderingInvMatrices;
    private bool m_bDirty = false;
        
    public bool HISM = true;
    public int branchFactor = 32;
    private bool m_bCasterShadows = true;
    public HierarchicalInstanceMesh instanceMesh;
    HISMClusterTree clusterTree = null;

    private Material m_material;
    //private MaterialPropertyBlock m_materialPropertyBlock;
    //private Mesh m_mesh;

    //Dictionary<ShaderTagId, int> shaderTagPass = new Dictionary<ShaderTagId, int>();

    public delegate void OnDrawInstanced(MaterialPropertyBlock materialPropertyBlock, int FirstInstance, int LastInstance);
    public delegate void OnBuildTree(List<int> sortedIndex);

    public OnDrawInstanced  onDrawInstancedFunc = null;
    public OnBuildTree      onBuildTreeFunc = null;

    public Bounds worldBound;
    // Start is called before the first frame update

    public ComputeBuffer m_instancesArgsBuffer;
    public ComputeBuffer m_instanceNodeBuffer;
    public ComputeBuffer m_localToWorldBuffer;
    public ComputeBuffer m_worldToLocalBuffer;
    public ComputeBuffer m_visibleBuffer;
    //private List<ComputeBuffer> m_instanceTranformBuffers = new List<ComputeBuffer>();
    //private List<ComputeBuffer> m_instanceInverseTransformBuffers = new List<ComputeBuffer>();
    private uint[] m_args;

    // Update is called once per frame
    public void Update()
    {

        if (m_bDirty)
        {
            if (!renderingMatrices.IsCreated)
            {
                renderingMatrices = new NativeArray<Matrix4x4>(worldMatrices.Count, Allocator.Persistent);
            }
            else
            {
                if (renderingMatrices.Length != worldMatrices.Count)
                {
                    if (renderingMatrices.IsCreated)
                        renderingMatrices.Dispose();
                    renderingMatrices = new NativeArray<Matrix4x4>(worldMatrices.Count, Allocator.Persistent);
                }
            }

            if (!renderingInvMatrices.IsCreated || renderingInvMatrices.Length != renderingMatrices.Length)
            {
                if (renderingInvMatrices.IsCreated)
                    renderingInvMatrices.Dispose();
                renderingInvMatrices = new NativeArray<Matrix4x4>(renderingMatrices.Length, Allocator.Persistent);
            }

            //if (HierarchicalInstancedData.instance.IsGPUDriven)
            //{
            //    for (int i = 0; i < worldMatrices.Count; ++i)
            //    {
            //        renderingMatrices[i] = worldMatrices[i];
            //        renderingInvMatrices[i] = worldMatrices[i].inverse;
            //    }

            //    SetupIndirectBuffers();
            //}
            //else //if (HISM)
            {
                    
                HierarchicalInstancedClusterBuilder builder = new HierarchicalInstancedClusterBuilder(worldMatrices, instanceMesh.m_meshes[0].bounds, branchFactor);
                builder.BuildTree(ref renderingMatrices);
                clusterTree = builder.GetClusterTree();
                worldBound.SetMinMax(clusterTree.Nodes[0].boundMin, clusterTree.Nodes[0].boundMax);
                if (onBuildTreeFunc != null)
                    onBuildTreeFunc(clusterTree.SortedInstances);

                for (int i = 0; i < renderingMatrices.Length; ++i)
                {
                    renderingInvMatrices[i] = renderingMatrices[i].inverse;
                }

                SetupIndirectBuffers();
            }

            m_bDirty = false;
        }
    }

    private void SetupIndirectBuffers()
    {
        if (instanceMesh.m_meshes == null)
            return;

        ReleaseIndirectArgumentBuffers();

        m_args = new uint[instanceMesh.m_meshes.Length * 5];

        m_instanceNodeBuffer = new ComputeBuffer(instanceDatas.Count, Marshal.SizeOf<InstanceBound>(), ComputeBufferType.Default);
            
        m_localToWorldBuffer = new ComputeBuffer(instanceDatas.Count, Marshal.SizeOf<Matrix4x4>(), ComputeBufferType.Default);
        m_localToWorldBuffer.SetData(renderingMatrices);

        m_worldToLocalBuffer = new ComputeBuffer(instanceDatas.Count, Marshal.SizeOf<Matrix4x4>(), ComputeBufferType.Default);
        m_worldToLocalBuffer.SetData(renderingInvMatrices);

        NativeArray<InstanceBound> instanceBounds = new NativeArray<InstanceBound>(instanceDatas.Count, Allocator.Temp);
        int index = 0;
        if (clusterTree != null)
        {
            for (int i = clusterTree.BottomLevelStart; i < clusterTree.Nodes.Count; ++i)
            {
                instanceBounds[index++] = new InstanceBound(clusterTree.Nodes[i].boundMin, clusterTree.Nodes[i].boundMax);
            }
        }
        else
        {
            for (int i = 0; i < instanceBounds.Length; ++i)
            {
                Bounds meshBound = instanceMesh.m_meshes[0].bounds;
                Bounds bound = BoundingUtils.TransformBounds(ref instanceDatas[i].worldMatrix, ref meshBound);
                instanceBounds[i] = new InstanceBound(bound.min, bound.max);
            }
        }
            
        m_instanceNodeBuffer.SetData(instanceBounds);

        m_instancesArgsBuffer = new ComputeBuffer(1, sizeof(uint) * m_args.Length, ComputeBufferType.IndirectArguments);
        //uint lastIndexLocation = 0;
        for (int i = 0; i < instanceMesh.m_meshes.Length; ++i)
        {
            m_args[0 + i * 5] = instanceMesh.m_meshes[i].GetIndexCount(0);      // 0 - index count per instance,
            m_args[1 + i * 5] = 0;                                              // 1 - instance count
            m_args[2 + i * 5] = instanceMesh.m_meshes[i].GetIndexStart(0);                              // 2 - start index location
            m_args[3 + i * 5] = instanceMesh.m_meshes[i].GetBaseVertex(0);                                              // 3 - base vertex location
            m_args[4 + i * 5] = 0;                                              // 4 - start instance location

            //lastIndexLocation += m_args[0 + i * 5];
                
            //m_instanceTranformBuffers.Add(new ComputeBuffer(instanceDatas.Count, 64, ComputeBufferType.Append));
            //m_instanceInverseTransformBuffers.Add(new ComputeBuffer(instanceDatas.Count, 64, ComputeBufferType.Append));
        }

        m_instancesArgsBuffer.SetData(m_args);

        m_visibleBuffer = new ComputeBuffer(instanceDatas.Count, sizeof(uint), ComputeBufferType.Append);

        material.EnableKeyword("_LOWPOLY_GPU_DRIVEN");
        material.enableInstancing = true;
    }

    private void ReleaseIndirectArgumentBuffers()
    {
        if (m_instanceNodeBuffer != null)
        {
            m_instanceNodeBuffer.Release();
        }
        m_instanceNodeBuffer = null;

        if (m_localToWorldBuffer != null)
        {
            m_localToWorldBuffer.Release();
        }
        m_localToWorldBuffer = null;

        if (m_worldToLocalBuffer != null)
        {
            m_worldToLocalBuffer.Release();
        }
        m_worldToLocalBuffer = null;

        if (m_instancesArgsBuffer != null)
        {
            m_instancesArgsBuffer.Release();
        }
        m_instancesArgsBuffer = null;

        if (m_visibleBuffer != null)
        {
            m_visibleBuffer.Release();
        }
        m_visibleBuffer = null;

        //for (int i = 0; i < m_instanceTranformBuffers.Count; ++i)
        //{
        //    m_instanceTranformBuffers[i].Release();
        //}
        //m_instanceTranformBuffers.Clear();

        //for (int i = 0; i < m_instanceInverseTransformBuffers.Count; ++i)
        //{
        //    m_instanceInverseTransformBuffers[i].Release();
        //}
        //m_instanceInverseTransformBuffers.Clear();
    }

    public ComputeBuffer GetArgumentBuffer()
    {
        return m_instancesArgsBuffer;
    }

    //public ComputeBuffer GetCulledTransformBuffer(int lod)
    //{
    //    return m_instanceTranformBuffers[lod];
    //}

    //public ComputeBuffer GetCulledInvTransformBuffer(int lod)
    //{
    //    return m_instanceInverseTransformBuffers[lod];
    //}

    public HISMClusterTree ClusterTree
    {
        get
        {
            return clusterTree;
        }
    }

    public int InstancesNum
    {
        get
        {
            return instanceDatas.Count;
        }
    }
    public Mesh GetMesh(int lod)
    {
        return instanceMesh.m_meshes[lod];
    }

    public int LODs
    {
        get
        {
            return instanceMesh.m_meshes.Length;
        }
    }

    public int AddInstance(InstanceData instance)
    {
        int instanceIndex = instanceDatas.Count;
        instanceDatas.Add(instance);
        worldMatrices.Add(instance.worldMatrix);
        m_bDirty = true;
        return instanceIndex;
    }

    public void RemoveInstance(InstanceData instance)
    {
        instanceDatas.Remove(instance);
        worldMatrices.Remove(instance.worldMatrix);
        m_bDirty = true;
    }

    public void Clear()
    {
        //for (int i = 0; i < instanceDrawcalls.Count; ++i)
        //    instanceDrawcalls[i].Dispose();
        //instanceDatas.Clear();
        //instanceDrawcalls.Clear();
        worldMatrices.Clear();
        if (renderingMatrices != null)
            renderingMatrices.Dispose();

        if (renderingInvMatrices != null)
            renderingInvMatrices.Dispose();

        ReleaseIndirectArgumentBuffers();

        //HierarchicalInstancedData.instance.RemoveInstancedStaticMesh(this);
    }

    public Material material
    {
        get
        {
            return m_material;
        }
        set
        {
            m_material = value;
        }
    }

    public MaterialPropertyBlock materialPropertyBlock(int lod)
    {
        return instanceMesh.m_materialPropertyBlocks[lod];
    }

    public OnDrawInstanced OnDrawCallback
    {
        get
        {
            return onDrawInstancedFunc;
        }
        set
        {
            onDrawInstancedFunc = value;
        }
    }

    public bool CasterShadow
    {
        get
        {
            return m_bCasterShadows;
        }
        set
        {
            m_bCasterShadows = value;
        }
    }
}

