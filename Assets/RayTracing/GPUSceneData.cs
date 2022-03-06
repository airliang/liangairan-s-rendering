using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public class GPUSceneData
{
    public ComputeBuffer woodTriBuffer;
    public ComputeBuffer woodTriIndexBuffer;
    public ComputeBuffer verticesBuffer;
    public ComputeBuffer triangleBuffer;
    public ComputeBuffer meshInstanceBuffer;
    public ComputeBuffer BVHBuffer;
    public ComputeBuffer intersectBuffer;
    public ComputeBuffer lightBuffer;
    public ComputeBuffer materialBuffer;

    List<Primitive> primitives = new List<Primitive>();
    List<MeshHandle> meshHandles = new List<MeshHandle>();
    List<MeshInstance> meshInstances = new List<MeshInstance>();
    List<int> triangles = new List<int>();
    List<GPUVertex> gpuVertices = new List<GPUVertex>();
    List<GPULight> gpuLights = new List<GPULight>();
    List<GPUMaterial> gpuMaterials = new List<GPUMaterial>();
    Dictionary<Material, int> materialIds = new Dictionary<Material, int>();
    Dictionary<Mesh, AreaLightResource> meshDistributions = new Dictionary<Mesh, AreaLightResource>();
    List<LightInstance> areaLightInstances = new List<LightInstance>();

    Bounds worldBound;
    BVHAccel bvhAccel = new BVHAccel();
    int instBVHNodeAddr = -1;
    EnviromentLight envLight = new EnviromentLight();

    public int InstanceBVHNodeAddr
    {
        get
        {
            return instBVHNodeAddr;
        }
    }

    public void Setup(bool useInstanceBVH, MeshRenderer[] meshRenderers)
    {
        if (meshRenderers.Length == 0)
            return;

        int renderObjectsNum = 0;

        Profiler.BeginSample("Scene Mesh Data Process");
        if (useInstanceBVH)
        {
            //Dictionary<Mesh, int> sharedMeshes = new Dictionary<Mesh, int>();

            List<Mesh> sharedMeshes = new List<Mesh>();
            Dictionary<Mesh, List<int>> meshHandlesDict = new Dictionary<Mesh, List<int>>();
            int lightObjectsNum = 0;

            //先生成MeshHandle
            int meshHandleIndex = 0;

            for (int i = 0; i < meshRenderers.Length; ++i)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                //worldMatrices[i] = shapes[i].transform.localToWorldMatrix;

                BSDFMaterial bsdfMaterial = meshRenderers[i].GetComponent<BSDFMaterial>();
                //if ((meshRenderers[i].shapeType == Shape.ShapeType.triangleMesh || meshRenderers[i].shapeType == Shape.ShapeType.rectangle) && bsdfMaterial != null)

                //MeshRenderer meshRenderer = meshRenderers[i];//shapes[i].GetComponent<MeshFilter>();
                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter.sharedMesh;
                if (!mesh.isReadable)
                {
                    continue;
                }

                Light lightComponent = meshRenderer.GetComponent<Light>();
                if (lightComponent != null && lightComponent.type == LightType.Area)
                {
                    lightObjectsNum++;
                }


                if (sharedMeshes.Contains(mesh))
                {
                    continue;
                }
                //if (mesh.normals == null || mesh.normals.Length == 0)
                //    mesh.RecalculateNormals();
                sharedMeshes.Add(mesh);
                List<int> meshHandleIndices = new List<int>();
                meshHandlesDict.Add(mesh, meshHandleIndices);


                //int meshId = i;
                Profiler.BeginSample("Getting mesh orig datas");
                int vertexOffset = gpuVertices.Count;
                List<Vector2> meshUVs = new List<Vector2>();
                List<Vector3> meshVertices = new List<Vector3>();
                List<Vector3> meshNormals = new List<Vector3>();

                mesh.GetUVs(0, meshUVs);
                //if (meshUVs.Count == 0)
                //{
                //    Vector2[] uvs = new Vector2[mesh.vertexCount];
                //    mesh.SetUVs(0, uvs);
                //}
                mesh.GetVertices(meshVertices);
                mesh.GetNormals(meshNormals);
                if (meshNormals.Count == 0)
                {
                    mesh.RecalculateNormals();
                    mesh.GetNormals(meshNormals);
                }
                Profiler.EndSample();
                GPUVertex vertexTmp = new GPUVertex();

                Profiler.BeginSample("Geometry Vertex data fetching");
                for (int sm = 0; sm < mesh.subMeshCount; ++sm)
                {
                    int subMeshVertexOffset = gpuVertices.Count;
                    int triangleOffset = triangles.Count;
                    //int primitiveTriangleOffset = gpuVertices.Count;
                    //int primitiveVertexOffset = triangles.Count;
                    List<int> meshTriangles = new List<int>();
                    mesh.GetTriangles(meshTriangles, sm);

                    SubMeshDescriptor subMeshDescriptor = mesh.GetSubMesh(sm);
                    //MeshHandle meshHandle = new MeshHandle(vertexOffset, triangleOffset, mesh.vertexCount, mesh.triangles.Length, mesh.bounds);
                    MeshHandle meshHandle = new MeshHandle(subMeshVertexOffset, triangleOffset, subMeshDescriptor.vertexCount,
                        subMeshDescriptor.indexCount, subMeshDescriptor.bounds);
                    meshHandleIndices.Add(meshHandleIndex);
                    meshHandleIndex++;
                    meshHandles.Add(meshHandle);
                    //创建该meshHandle的bvh


                    for (int j = 0; j < subMeshDescriptor.vertexCount; ++j)
                    {
                        vertexTmp.position = meshVertices[subMeshDescriptor.firstVertex + j];//mesh.vertices[j];
                        vertexTmp.uv = meshUVs.Count == 0 ? Vector2.zero : meshUVs[subMeshDescriptor.firstVertex + j];
                        vertexTmp.normal = meshNormals[subMeshDescriptor.firstVertex + j];
                        gpuVertices.Add(vertexTmp);
                    }
                    for (int j = 0; j < subMeshDescriptor.indexCount; ++j)
                    {
                        triangles.Add(meshTriangles[j + subMeshDescriptor.indexStart] + vertexOffset);
                    }
                }
                Profiler.EndSample();
            }


            //生成meshinstance和对应的material
            meshHandleIndex = 0;
            for (int i = 0; i < meshRenderers.Length; ++i)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                if (i == 0)
                {
                    worldBound = meshRenderer.bounds;
                }
                else
                {
                    worldBound.Encapsulate(meshRenderer.bounds);
                }
                //BSDFMaterial bsdfMaterial = shapes[i].GetComponent<BSDFMaterial>();
                Transform transform = meshRenderer.transform;
                int lightIndex = -1;
                //if ((shapes[i].shapeType == Shape.ShapeType.triangleMesh || shapes[i].shapeType == Shape.ShapeType.rectangle) && bsdfMaterial != null)

                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter.sharedMesh;

                Light lightComponent = meshRenderer.GetComponent<Light>();
                if (lightComponent != null && lightComponent.type == LightType.Area)
                {
                    Profiler.BeginSample("Lights data fetching");
                    AreaLightResource areaLight = null;
                    List<Vector3> lightMeshVertices = new List<Vector3>();
                    if (!meshDistributions.TryGetValue(mesh, out areaLight))
                    {
                        areaLight = new AreaLightResource();
                        //compute the mesh triangle distribution
                        //for (int t = 0; t < mesh.triangles.Length; t += 3)
                        //{
                        //    Vector3 p0 = mesh.vertices[mesh.triangles[t]];
                        //    Vector3 p1 = mesh.vertices[mesh.triangles[t + 1]];
                        //    Vector3 p2 = mesh.vertices[mesh.triangles[t + 2]];
                        //    float triangleArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                        //    areaLight.triangleAreas.Add(triangleArea);
                        //}

                        mesh.GetVertices(lightMeshVertices);

                        for (int sm = 0; sm < mesh.subMeshCount; ++sm)
                        {
                            List<int> meshTriangles = new List<int>();
                            mesh.GetTriangles(meshTriangles, sm);
                            for (int t = 0; t < meshTriangles.Count; t += 3)
                            {
                                Vector3 p0 = transform.TransformPoint(lightMeshVertices[meshTriangles[t]]);
                                Vector3 p1 = transform.TransformPoint(lightMeshVertices[meshTriangles[t + 1]]);
                                Vector3 p2 = transform.TransformPoint(lightMeshVertices[meshTriangles[t + 2]]);
                                float triangleArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                                areaLight.triangleAreas.Add(triangleArea);
                            }
                        }

                        areaLight.triangleDistributions = new Distribution1D(areaLight.triangleAreas.ToArray(), 0, areaLight.triangleAreas.Count, 0, areaLight.triangleAreas.Count);
                        meshDistributions.Add(mesh, areaLight);
                        //lightTriangleDistributions.AddRange(areaLight.triangleDistributions);
                    }

                    float lightArea = 0;
                    //for (int t = 0; t < mesh.triangles.Length; t += 3)
                    //{
                    //    Vector3 p0 = transform.TransformPoint(mesh.vertices[mesh.triangles[t]]);
                    //    Vector3 p1 = transform.TransformPoint(mesh.vertices[mesh.triangles[t + 1]]);
                    //    Vector3 p2 = transform.TransformPoint(mesh.vertices[mesh.triangles[t + 2]]);
                    //    float triangleArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                    //    lightArea += triangleArea;
                    //}

                    lightMeshVertices.Clear();
                    mesh.GetVertices(lightMeshVertices);

                    for (int sm = 0; sm < mesh.subMeshCount; ++sm)
                    {
                        List<int> meshTriangles = new List<int>();
                        mesh.GetTriangles(meshTriangles, sm);
                        for (int t = 0; t < meshTriangles.Count; t += 3)
                        {
                            Vector3 p0 = transform.TransformPoint(lightMeshVertices[meshTriangles[t]]);
                            Vector3 p1 = transform.TransformPoint(lightMeshVertices[meshTriangles[t + 1]]);
                            Vector3 p2 = transform.TransformPoint(lightMeshVertices[meshTriangles[t + 2]]);
                            float triangleArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                            lightArea += triangleArea;
                        }
                    }

                    AreaLightInstance areaLightInstance = new AreaLightInstance();
                    areaLightInstance.light = areaLight;
                    areaLightInstance.meshInstanceID = meshInstances.Count;
                    areaLightInstance.area = lightArea;
                    //Shape shape = meshRenderer.GetComponent<Shape>();
                    //if (shape != null)
                    //    areaLightInstance.radiance = shape.lightSpectrum.linear.ToVector3().Mul(shape.spectrumScale);
                    //areaLightInstance.radiance = lightComponent.color.linear.ToVector3() * lightComponent.intensity;
                    Material lightMaterial = meshRenderer.sharedMaterial;
                    if (lightMaterial != null && lightMaterial.shader.name == "RayTracing/AreaLight")
                    {
                        Color emssionColor = lightMaterial.GetColor("_Emission");
                        Vector3 lightIntensity = lightMaterial.GetVector("_Intensity");
                        areaLightInstance.radiance = emssionColor.ToVector3().Mul(lightIntensity);
                    }
                    areaLightInstance.pointRadius = 0;
                    areaLightInstance.intensity = lightComponent.intensity; // shapes[i].lightIntensity;
                    areaLightInstances.Add(areaLightInstance);

                    lightIndex = gpuLights.Count;
                    GPULight gpuLight = new GPULight();
                    gpuLight.type = (int)LightInstance.LightType.Area;
                    gpuLight.radiance = areaLightInstance.radiance;
                    gpuLight.intensity = areaLightInstance.intensity;
                    //gpuLight.trianglesNum = mesh.triangles.Length / 3;
                    gpuLight.pointRadius = 0;
                    //why add 1? because the first discript is the light object distributions.
                    gpuLight.distributionDiscriptIndex = gpuLights.Count + 1;
                    gpuLight.meshInstanceID = meshInstances.Count;
                    gpuLights.Add(gpuLight);
                    Profiler.EndSample();
                }

                List<int> meshHandleIndices = null;
                meshHandlesDict.TryGetValue(mesh, out meshHandleIndices);
                Profiler.BeginSample("SetupMaterials");
                for (int sm = 0; sm < mesh.subMeshCount; ++sm)
                {
                    meshHandleIndex = meshHandleIndices[sm];
                    int materialIndex = SetupMaterials(meshRenderer, sm);
                    MeshHandle meshHandle = meshHandles[meshHandleIndex];
                    MeshInstance meshInstance = new MeshInstance(transform.localToWorldMatrix, transform.worldToLocalMatrix, meshHandleIndex,
                        materialIndex, lightIndex, meshHandle.triangleOffset, meshHandle.triangleCount);
                    meshInstances.Add(meshInstance);
                }
                Profiler.EndSample();
            }


            //创建bvh
            float timeBegin = Time.realtimeSinceStartup;
            Profiler.BeginSample("Build BVH");
            instBVHNodeAddr = bvhAccel.Build(meshInstances, meshHandles, gpuVertices, triangles);
            Profiler.EndSample();
            float timeInterval = Time.realtimeSinceStartup - timeBegin;
            Debug.Log("building bvh cost time:" + timeInterval);



            //创建对应的computebuffer
            //meshHandleBuffer = new ComputeBuffer(meshHandles.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(MeshHandle)), ComputeBufferType.Structured);
            //meshHandleBuffer.SetData(meshHandles.ToArray());

            meshInstanceBuffer = new ComputeBuffer(meshInstances.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(MeshInstance)), ComputeBufferType.Structured);
            meshInstanceBuffer.SetData(meshInstances);
        }
        else
        {
            for (int i = 0; i < meshRenderers.Length; ++i)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                //worldMatrices[i] = shapes[i].transform.localToWorldMatrix;
                //BSDFMaterial bsdfMaterial = shapes[i].GetComponent<BSDFMaterial>();
                //if (shapes[i].shapeType == Shape.ShapeType.triangleMesh && bsdfMaterial != null)
                {
                    MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                    Mesh mesh = meshFilter.sharedMesh;

                    if (mesh.subMeshCount > 1)
                    {
                        int primitiveTriangleOffset = gpuVertices.Count;
                        int primitiveVertexOffset = triangles.Count;

                        for (int j = 0; j < mesh.vertices.Length; ++j)
                        {
                            //positions.Add(shapes[i].transform.localToWorldMatrix.MultiplyPoint(mesh.vertices[j]));
                            //uvs.Add(mesh.uv[j]);
                            GPUVertex vertex = new GPUVertex();
                            vertex.position = meshRenderer.transform.localToWorldMatrix.MultiplyPoint(mesh.vertices[j]);
                            vertex.uv = mesh.uv[j];
                            vertex.normal = mesh.normals[j];
                            gpuVertices.Add(vertex);
                        }
                        for (int j = 0; j < mesh.triangles.Length; ++j)
                        {
                            triangles.Add(mesh.triangles[j] + primitiveVertexOffset);
                        }

                        int faceNum = mesh.triangles.Length / 3;

                        for (int f = 0; f < faceNum; ++f)
                        {
                            int tri0 = triangles[f * 3 + primitiveTriangleOffset];
                            int tri1 = triangles[f * 3 + 1 + primitiveTriangleOffset];
                            int tri2 = triangles[f * 3 + 2 + primitiveTriangleOffset];
                            primitives.Add(new Primitive(tri0, tri1, tri2, gpuVertices[tri0].position, gpuVertices[tri1].position, gpuVertices[tri2].position, renderObjectsNum, -1));
                        }
                    }
                    else
                    {
                        int primitiveTriangleOffset = gpuVertices.Count;
                        int primitiveVertexOffset = triangles.Count;

                        for (int j = 0; j < mesh.vertices.Length; ++j)
                        {
                            //positions.Add(shapes[i].transform.localToWorldMatrix.MultiplyPoint(mesh.vertices[j]));
                            //uvs.Add(mesh.uv[j]);
                            GPUVertex vertex = new GPUVertex();
                            vertex.position = meshRenderer.transform.localToWorldMatrix.MultiplyPoint(mesh.vertices[j]);
                            vertex.uv = mesh.uv[j];
                            vertex.normal = mesh.normals[j];
                            gpuVertices.Add(vertex);
                        }
                        for (int j = 0; j < mesh.triangles.Length; ++j)
                        {
                            triangles.Add(mesh.triangles[j] + primitiveTriangleOffset);
                        }

                        int faceNum = mesh.triangles.Length / 3;

                        for (int f = 0; f < faceNum; ++f)
                        {
                            int tri0 = triangles[f * 3 + primitiveVertexOffset];
                            int tri1 = triangles[f * 3 + 1 + primitiveVertexOffset];
                            int tri2 = triangles[f * 3 + 2 + primitiveVertexOffset];
                            primitives.Add(new Primitive(tri0, tri1, tri2, gpuVertices[tri0].position, gpuVertices[tri1].position, gpuVertices[tri2].position, renderObjectsNum, -1));
                        }
                    }

                    renderObjectsNum++;
                }
            }

            //bvhAccel.Build(primitives, gpuVertices, triangles);
        }
        Profiler.EndSample();

        Profiler.BeginSample("Create Scene Compute Buffers");
        int BVHNodeSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUBVHNode));
        if (BVHBuffer == null)
        {
            BVHBuffer = new ComputeBuffer(bvhAccel.m_nodes.Count, BVHNodeSize, ComputeBufferType.Structured);
            BVHBuffer.SetData(bvhAccel.m_nodes);
        }

        if (woodTriBuffer == null)
        {
            woodTriBuffer = new ComputeBuffer(bvhAccel.m_woodTriangleVertices.Count, 16, ComputeBufferType.Structured);
        }
        woodTriBuffer.SetData(bvhAccel.m_woodTriangleVertices);

        if (woodTriIndexBuffer == null)
        {
            woodTriIndexBuffer = new ComputeBuffer(bvhAccel.m_woodTriangleIndices.Count, sizeof(int), ComputeBufferType.Structured);
        }
        woodTriIndexBuffer.SetData(bvhAccel.m_woodTriangleIndices);

        if (verticesBuffer == null)
        {
            int vertexSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUVertex));
            verticesBuffer = new ComputeBuffer(gpuVertices.Count, vertexSize, ComputeBufferType.Structured);
        }
        verticesBuffer.SetData(gpuVertices);

        if (triangleBuffer == null)
        {
            triangleBuffer = new ComputeBuffer(triangles.Count, 4, ComputeBufferType.Default);
        }
        triangleBuffer.SetData(triangles);

        if (materialBuffer == null)
        {
            materialBuffer = new ComputeBuffer(gpuMaterials.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUMaterial)), ComputeBufferType.Structured);
        }
        materialBuffer.SetData(gpuMaterials);
        Profiler.EndSample();

        //environment light process
        Material skyBoxMaterial = RenderSettings.skybox;
        envLight.area = /*worldBound.extents.magnitude * */4.0f * Mathf.PI;
        GPULight gpuEnvLight = new GPULight();
        gpuEnvLight.type = (int)envLight.lightType;
        gpuEnvLight.meshInstanceID = -1;
        gpuEnvLight.distributionDiscriptIndex = gpuLights.Count + 1;
        if (skyBoxMaterial != null)
        {
            if (skyBoxMaterial.shader.name == "Skybox/Cubemap")
            {
                envLight.textureRadiance = skyBoxMaterial.GetTexture("_Tex") as Cubemap;
                if (envLight.textureRadiance == null)
                {
                    envLight.radiance = RenderSettings.ambientSkyColor.LinearToVector3();
                    gpuEnvLight.radiance = envLight.radiance;
                }
                else
                {
                    uint mask = 1;
                    gpuEnvLight.textureMask = MathUtil.UInt32BitsToSingle(mask);
                }
            }
        }
        areaLightInstances.Add(envLight);
        gpuLights.Add(gpuEnvLight);
        if (gpuLights.Count > 0)
        {
            lightBuffer = new ComputeBuffer(gpuLights.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPULight)), ComputeBufferType.Structured);
            lightBuffer.SetData(gpuLights);
        }
        else
        {
            lightBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPULight)), ComputeBufferType.Structured);
        }
    }

    int SetupMaterials(MeshRenderer renderer, int subMeshIndex)
    {
        //Renderer renderer = shape.GetComponent<MeshRenderer>();
        //if (renderer.sharedMaterial.HasProperty("_BaseColor"))
        //{
        //    Color _Color = renderer.sharedMaterials[subMeshIndex].GetColor("_BaseColor");
        //}
        int id = -1;
        if (materialIds.TryGetValue(renderer.sharedMaterials[subMeshIndex], out id))
        {
            return id;
        }
        MaterialParam materialParam = MaterialParam.ConvertUnityMaterial(renderer.sharedMaterials[subMeshIndex]);
        if (materialParam == null)
            return -1;
        GPUMaterial gpuMtl = materialParam.ConvertToGPUMaterial();
        //gpuMtl.baseColor = bsdfMaterial.matte.kd.spectrum.LinearToVector4(); //_Color.linear;

        id = gpuMaterials.Count;
        materialIds.Add(renderer.sharedMaterials[subMeshIndex], id);
        gpuMaterials.Add(gpuMtl);
        return id;
    }

    public void Release()
    {
        void ReleaseComputeBuffer(ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }

        ReleaseComputeBuffer(woodTriBuffer);
        ReleaseComputeBuffer(woodTriIndexBuffer);
        ReleaseComputeBuffer(verticesBuffer);
        ReleaseComputeBuffer(triangleBuffer);
        ReleaseComputeBuffer(meshInstanceBuffer);
        ReleaseComputeBuffer(BVHBuffer);
        ReleaseComputeBuffer(intersectBuffer);
        ReleaseComputeBuffer(materialBuffer);
        ReleaseComputeBuffer(lightBuffer);
    }
}
