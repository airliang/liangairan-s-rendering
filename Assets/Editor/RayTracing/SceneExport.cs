using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;
using System;
using System.Linq;

class TriangleMesh
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<Vector3> normals = new List<Vector3>();
    public List<Vector3> tangents = new List<Vector3>();
    public List<Vector2> uvs = new List<Vector2>();
    public List<int> triangles = new List<int>();
}

class SceneWriter
{
    public static string OutputPath = "Assets/RayTracing";

    private int FileOffset = 0;

    private List<Mesh> triangleMeshes = new List<Mesh>();
    private List<Medium> mediums = new List<Medium>();
    private Mesh rectangleMesh = null;

    public void WriteTransform(FileStream fs, Transform transform, bool ignoreScale)
    {
        byte[] bytes = BitConverter.GetBytes(transform.position.x);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(transform.position.y);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(transform.position.z);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(transform.rotation.x);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(transform.rotation.y);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(transform.rotation.z);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(transform.rotation.w);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(ignoreScale);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        if (!ignoreScale)
        {
            bytes = BitConverter.GetBytes(transform.localScale.x);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(transform.localScale.y);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(transform.localScale.z);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }
    }

    public void WriteMediumInterface(FileStream fs, Transform transform)
    {
        MediumInterface mi = transform.GetComponent<MediumInterface>();
        bool hasMedium = mi != null;
        byte[] bytes = BitConverter.GetBytes(hasMedium);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        if (hasMedium)
        {
            int insideIndex = FindMediumIndex(mi.inside);

            bytes = BitConverter.GetBytes(insideIndex);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            int outsideIndex = FindMediumIndex(mi.outside);

            bytes = BitConverter.GetBytes(outsideIndex);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }
    }

    public void WriteLight(FileStream fs, RTLight light)
    {
        WriteTransform(fs, light.transform, true);

        byte[] bytes = BitConverter.GetBytes((int)light.lightType);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(light.color.r);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(light.color.g);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(light.color.b);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(light.intensity);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        //int mediumIndex = FindObjectMedium(light.transform);

        //bytes = BitConverter.GetBytes(mediumIndex);
        //fs.Write(bytes, 0, bytes.Length);
        //FileOffset += bytes.Length;

        WriteMediumInterface(fs, light.transform);

        switch (light.lightType)
        {
            case RTLight.LightType.delta_distant:
                {
                    bytes = BitConverter.GetBytes(-light.transform.forward.x);
                    fs.Write(bytes, 0, bytes.Length);
                    FileOffset += bytes.Length;

                    bytes = BitConverter.GetBytes(-light.transform.forward.y);
                    fs.Write(bytes, 0, bytes.Length);
                    FileOffset += bytes.Length;

                    bytes = BitConverter.GetBytes(-light.transform.forward.z);
                    fs.Write(bytes, 0, bytes.Length);
                    FileOffset += bytes.Length;
                }
                break;
            case RTLight.LightType.delta_point:
                {
                    bytes = BitConverter.GetBytes(light.pointRadius);
                    fs.Write(bytes, 0, bytes.Length);
                    FileOffset += bytes.Length;
                }
                break;
        }
    }

    public void WriteShape(FileStream fs, Shape shape)
    {
        byte[] bytes = BitConverter.GetBytes((int)shape.shapeType);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bool ignoreScale = !(shape.shapeType == Shape.ShapeType.triangleMesh || shape.shapeType == Shape.ShapeType.rectangle);
        WriteTransform(fs, shape.transform, ignoreScale);

        WriteMediumInterface(fs, shape.transform);

        bytes = BitConverter.GetBytes(shape.isAreaLight);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        if (shape.isAreaLight)
        {
            bytes = BitConverter.GetBytes(shape.lightSpectrum.r * shape.spectrumScale.x);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(shape.lightSpectrum.g * shape.spectrumScale.y);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(shape.lightSpectrum.b * shape.spectrumScale.z);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(shape.lightIntensity);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }

        if (shape.shapeType == Shape.ShapeType.sphere)
        {
            //write the radius
            bytes = BitConverter.GetBytes(shape.transform.localScale.x * 0.5f);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }
        else if (shape.shapeType == Shape.ShapeType.triangleMesh)
        {
            MeshFilter meshRenderer = shape.GetComponent<MeshFilter>();
            Mesh mesh = meshRenderer.sharedMesh;
            int meshIndex = -1;
            if (triangleMeshes.Contains(mesh))
            {
                meshIndex = triangleMeshes.IndexOf(mesh);
            }
            else
            {
                meshIndex = triangleMeshes.Count;
                triangleMeshes.Add(mesh);
            }

            //写入对应mesh的index
            bytes = BitConverter.GetBytes(meshIndex);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }
        else if (shape.shapeType == Shape.ShapeType.rectangle)
        {
            if (rectangleMesh == null)
            {
                rectangleMesh = new Mesh();

                Vector3[] vertices = new Vector3[4];
                vertices[0] = new Vector3(-5.0f, 0.0f, 5.0f);
                vertices[1] = new Vector3(5.0f, 0.0f, 5.0f);
                vertices[2] = new Vector3(-5.0f, 0.0f, -5.0f);
                vertices[3] = new Vector3(5.0f, 0.0f, -5.0f);
                rectangleMesh.vertices = vertices;
                int[] triangles = new int[] { 0, 1, 2, 1, 3, 2 };
                rectangleMesh.triangles = triangles;

                Vector3[] normals = new Vector3[4];
                for (int i = 0; i < 4; ++i)
                {
                    normals[i] = Vector3.up;
                }
                rectangleMesh.normals = normals;

                Vector2[] uvs = new Vector2[] { new Vector2(0.0f, 0.0f), 
                    new Vector2(1.0f, 0.0f), new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f) };
                rectangleMesh.uv = uvs;

                triangleMeshes.Add(rectangleMesh);
            }

            int meshIndex = triangleMeshes.IndexOf(rectangleMesh);

            bytes = BitConverter.GetBytes(meshIndex);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            if (shape.isAreaLight)
            {
                Debug.Log("Shape is areaLight, meshIndex = " + meshIndex + " triangles = " + rectangleMesh.triangles.Length / 3);
            }
        }

        BSDFMaterial bsdfMaterial = shape.gameObject.GetComponent<BSDFMaterial>();
        bool hasMaterial = bsdfMaterial != null;
        bytes = BitConverter.GetBytes(hasMaterial);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;
        if (hasMaterial)
        {
            WriteMaterial(fs, bsdfMaterial);
        }
        
    }

    private void WriteTexture(FileStream fs, BSDFSpectrumTexture texture)
    {
        byte[] bytes = BitConverter.GetBytes((int)texture.type);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        if (texture.type == BSDFTextureType.Constant)
        {

            bytes = BitConverter.GetBytes(texture.spectrum.r);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(texture.spectrum.g);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(texture.spectrum.b);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }
        else if (texture.type == BSDFTextureType.Image)
        {
            //暂时不支持
            bytes = System.Text.Encoding.Default.GetBytes(texture.imageFile);
            byte[] imageFileBytes = new byte[256];
            for (int i = 0; i < bytes.Length; ++i)
            {
                imageFileBytes[i] = bytes[i];
            }
            fs.Write(imageFileBytes, 0, 256);
            FileOffset += 256;

            bytes = BitConverter.GetBytes(texture.gamma);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes((int)texture.wrap);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes((int)texture.mappingType);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            if (texture.mappingType == BSDFTextureUVMapping.UVMapping2D)
            {
                bytes = BitConverter.GetBytes((int)texture.uvMapping2D.su);
                fs.Write(bytes, 0, bytes.Length);
                FileOffset += bytes.Length;

                bytes = BitConverter.GetBytes((int)texture.uvMapping2D.sv);
                fs.Write(bytes, 0, bytes.Length);
                FileOffset += bytes.Length;

                bytes = BitConverter.GetBytes((int)texture.uvMapping2D.du);
                fs.Write(bytes, 0, bytes.Length);
                FileOffset += bytes.Length;

                bytes = BitConverter.GetBytes((int)texture.uvMapping2D.dv);
                fs.Write(bytes, 0, bytes.Length);
                FileOffset += bytes.Length;
            }
        }
    }

    private void WriteTexture(FileStream fs, BSDFFloatTexture texture)
    {
        byte[] bytes = BitConverter.GetBytes((int)texture.type);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        if (texture.type == BSDFTextureType.Constant)
        {
            bytes = BitConverter.GetBytes(texture.constantValue);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }
        else if (texture.type == BSDFTextureType.Image)
        {
            //暂时不支持
        }
    }

    public void WriteMaterial(FileStream fs, BSDFMaterial material)
    {
        byte[] bytes = BitConverter.GetBytes((int)material.materialType);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        if (material.materialType == BSDFMaterial.BSDFType.Matte)
        {
            WriteTexture(fs, material.matte.kd);
            WriteTexture(fs, material.matte.sigma);
        }
        else if (material.materialType == BSDFMaterial.BSDFType.Plastic)
        {
            WriteTexture(fs, material.plastic.kd);
            WriteTexture(fs, material.plastic.ks);
            WriteTexture(fs, material.plastic.roughnessTexture);
        }
        else if (material.materialType == BSDFMaterial.BSDFType.Mirror)
        {
            WriteTexture(fs, material.mirror.kr);
        }
        else if (material.materialType == BSDFMaterial.BSDFType.Glass)
        {
            WriteTexture(fs, material.glass.kr);
            WriteTexture(fs, material.glass.ks);
            WriteTexture(fs, material.glass.uRougness);
            WriteTexture(fs, material.glass.vRougness);
            WriteTexture(fs, material.glass.index);
        }
    }

    public void WriteCamera(FileStream fs, Camera camera)
    {
        WriteTransform(fs, camera.transform, true);
        byte[] bytes = BitConverter.GetBytes(camera.fieldOfView * Mathf.Deg2Rad);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes(camera.orthographic);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        //int mediumIndex = FindObjectMedium(camera.transform);

        //bytes = BitConverter.GetBytes(mediumIndex);
        //fs.Write(bytes, 0, bytes.Length);
        //FileOffset += bytes.Length;

    }

    private void WriteMesh(FileStream fs, Mesh mesh)
    {
        //write the vertices count
        byte[] bytes = BitConverter.GetBytes(mesh.vertexCount);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        for (int i = 0; i < mesh.vertexCount; ++i)
        {
            Vector3 position = mesh.vertices[i];
            bytes = BitConverter.GetBytes(position.x);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(position.y);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(position.z);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }

        if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal))
        {
            mesh.RecalculateNormals();
        }
        for (int i = 0; i < mesh.vertexCount; ++i)
        {
            Vector3 normal = mesh.normals[i];
            bytes = BitConverter.GetBytes(normal.x);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(normal.y);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(normal.z);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }

        if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent))
        {
            mesh.RecalculateTangents();
        }

        for (int i = 0; i < mesh.vertexCount; ++i)
        {
            Vector3 tangent = mesh.tangents[i];
            bytes = BitConverter.GetBytes(tangent.x);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(tangent.y);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(tangent.z);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }

        bool hasUV = false;
        if (mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))
        {
            hasUV = true;
            bytes = BitConverter.GetBytes(hasUV);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
            for (int i = 0; i < mesh.vertexCount; ++i)
            {
                Vector2 uv = mesh.uv[i];
                bytes = BitConverter.GetBytes(uv.x);
                fs.Write(bytes, 0, bytes.Length);
                FileOffset += bytes.Length;

                bytes = BitConverter.GetBytes(uv.y);
                fs.Write(bytes, 0, bytes.Length);
                FileOffset += bytes.Length;
            }
        }
        else
        {
            bytes = BitConverter.GetBytes(hasUV);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }

        int triangles = mesh.triangles.Length;
        bytes = BitConverter.GetBytes(triangles);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        for (int i = 0; i < triangles; ++i)
        {
            bytes = BitConverter.GetBytes(mesh.triangles[i]);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }
    }

    public void WriteTriangleMeshes(string fileName)
    {
        FileOffset = 0;
        FileStream fs = new FileStream(OutputPath + fileName, FileMode.OpenOrCreate, FileAccess.Write);
        //先写入triangleMesh的个数
        byte[] bytes = BitConverter.GetBytes(triangleMeshes.Count);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        for (int i = 0; i < triangleMeshes.Count; ++i)
        {
            WriteMesh(fs, triangleMeshes[i]);
        }

        fs.Close();

        Debug.Log(OutputPath + fileName + " write completed! totalBytes = " + FileOffset);
    }

    public void WriteMedium(FileStream fs, Medium medium)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(medium.name);
        byte[] nameBytes = new byte[128];
        for (int i = 0; i < bytes.Length; ++i)
        {
            nameBytes[i] = bytes[i];
        }
        fs.Write(nameBytes, 0, 128);
        FileOffset += bytes.Length;

        bytes = BitConverter.GetBytes((int)medium.type);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;

        if (medium.type == Medium.MediumType.Homogeneous)
        {
            bytes = BitConverter.GetBytes(medium.sigma_a.r);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(medium.sigma_a.g);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(medium.sigma_a.b);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(medium.sigma_s.r);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(medium.sigma_s.g);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(medium.sigma_s.b);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;

            bytes = BitConverter.GetBytes(medium.g);
            fs.Write(bytes, 0, bytes.Length);
            FileOffset += bytes.Length;
        }
        else
        {

        }
    }

    public void WriteScene(string filename)
    {
        FileOffset = 0;
        FileStream fs = new FileStream(OutputPath + filename, FileMode.OpenOrCreate, FileAccess.Write);

        Medium[] mediumsTmp = UnityEngine.Object.FindObjectsOfType<Medium>();
        byte[] bytes = BitConverter.GetBytes(mediumsTmp.Length);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;
        for (int i = 0; i < mediumsTmp.Length; ++i)
        {
            WriteMedium(fs, mediumsTmp[i]);
        }
        mediums = mediumsTmp.ToList();

        Camera camera = GameObject.FindObjectOfType<Camera>();
        WriteCamera(fs, camera);

        RTLight[] lights = GameObject.FindObjectsOfType<RTLight>();
        bytes = BitConverter.GetBytes(lights.Length);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;
        for (int i = 0; i < lights.Length; ++i)
        {
            WriteLight(fs, lights[i]);
        }
        Shape[] shapes = GameObject.FindObjectsOfType<Shape>();
        bytes = BitConverter.GetBytes(shapes.Length);
        fs.Write(bytes, 0, bytes.Length);
        FileOffset += bytes.Length;
        for (int i = 0; i < shapes.Length; ++i)
        {
            WriteShape(fs, shapes[i]);
        }
        fs.Close();
        Debug.Log(OutputPath + filename + " write completed! totalBytes = " + FileOffset);
    }

    public void Clear()
    {
        triangleMeshes.Clear();
        if (rectangleMesh != null)
        {
            rectangleMesh.Clear();
        }
        rectangleMesh = null;

        mediums.Clear();
    }

    public int FindMediumIndex(Medium medium)
    {
        if (medium == null)
            return -1;

        for (int i = 0; i < mediums.Count; ++i)
        {
            if (medium == mediums[i])
                return i;
        }
        return -1;
    }
}

public static class SceneExport
{
    
    [MenuItem("GameObject/Export Scene for RayTracing", false, 8)]
    private static void ExportRayTracingScene()
    {

        //GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        //for (int i = 0; i < rootObjects.Length; i++)
        //{
        //    GameObject rootObject = rootObjects[i];

        //}
        Ray ray = new Ray(new Vector3(-2.75519872f, 2.73157096f, 13.1390285f), new Vector3(0.383678913f, 0.121772133f, -0.915402591f));
        ray = new Ray(new Vector3(-2.75519872f, 2.73157096f, 13.1390285f), new Vector3(0, 0, -1.0f));
        RaycastHit info;
        bool hit = Physics.Raycast(ray, out info);
        if (hit)
        {
            Debug.LogWarning("ray hit succeed, info name = " + info.collider.gameObject.name);
        }
        else
        {
            Debug.LogWarning("ray hit nothing!");
        }

        SceneWriter sw = new SceneWriter();
        sw.WriteScene("/scene.rt");

        sw.WriteTriangleMeshes("/scene.m");
        sw.Clear();
    }
    
    
}
