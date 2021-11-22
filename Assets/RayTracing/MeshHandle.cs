using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct MeshHandle
{
    public int vertexOffset;
    public int triangleOffset;
    public int vertexCount;
    public int triangleCount;
    public int bvhOffset;
    public GPUBounds localBounds;

    public MeshHandle(int _vertexOff, 
        int _triangleOff,
        int _vertexCount,
        int _triangleCount,
        Bounds bounds)
    {
        vertexOffset = _vertexOff;
        triangleOffset = _triangleOff;
        vertexCount = _vertexCount;
        triangleCount = _triangleCount;
        bvhOffset = -1;
        localBounds = GPUBounds.ConvertUnityBounds(bounds);
    }
}

public struct MeshInstance
{
    public Matrix4x4 localToWorld;
    public Matrix4x4 worldToLocal;
    public int meshHandleIndex;
    public int materialIndex;
    public int lightIndex;
    public int bvhOffset;
    public int triangleStartOffset;  //triangle index start in trianglebuffer
    public int trianglesNum;

    public MeshInstance(Matrix4x4 _local2world, Matrix4x4 _world2local, int _meshHandleIndex, int _materialIndex, int _lightIndex, int _triangleOffset, int _trianglesNum)
    {
        localToWorld = _local2world;
        worldToLocal = _world2local;
        meshHandleIndex = _meshHandleIndex;
        materialIndex = _materialIndex;
        lightIndex = _lightIndex;
        bvhOffset = -1;
        triangleStartOffset = _triangleOffset;
        trianglesNum = _trianglesNum;
    }
}
