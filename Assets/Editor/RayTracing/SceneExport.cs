using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;
using System;

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

    public void WriteLight(FileStream fs, Light light)
    {
        WriteTransform(fs, light.transform, true);

        byte[] bytes = BitConverter.GetBytes((int)light.type);
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

        switch (light.type)
        {
            case LightType.Directional:
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
            case LightType.Point:
                {

                }
                break;
            case LightType.Spot:
                break;
            case LightType.Area:
                break;
            case LightType.Disc:
                {
                    //写入半径
                    bytes = BitConverter.GetBytes(light.range);
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

        WriteTransform(fs, shape.transform, false);

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

        BSDFMaterial bsdfMaterial = shape.gameObject.GetComponent<BSDFMaterial>();
        WriteMaterial(fs, bsdfMaterial);
    }

    public void WriteDiskShape(FileStream fs, Shape shape)
    {

    }

    public void WriteRetangleShape(FileStream fs, Shape shape)
    {

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

    public void WriteScene(string filename)
    {
        FileOffset = 0;
        FileStream fs = new FileStream(OutputPath + filename, FileMode.OpenOrCreate, FileAccess.Write);

        Camera camera = GameObject.FindObjectOfType<Camera>();
        WriteCamera(fs, camera);

        Light[] lights = GameObject.FindObjectsOfType<Light>();
        byte[] bytes = BitConverter.GetBytes(lights.Length);
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


        SceneWriter sw = new SceneWriter();
        sw.WriteScene("/scene.rt");

        sw.WriteTriangleMeshes("/scene.m");
    }
    
    
}
