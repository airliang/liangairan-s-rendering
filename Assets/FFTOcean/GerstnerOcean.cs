using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class GerstnerWave
{
    //波的方向
    public Vector2 direction;
    //圆形波的中心位置(0, 1)
    public Vector2 circle;
    //波长
    public float crest2crest;
    public float amplitude;
    public float speed;
    public float steepness;
}

public struct VertexData
{
    Vector3 position;
    Vector2 uv;
    Vector3 normal;
}

[ExecuteInEditMode]
public class GerstnerOcean : MonoBehaviour
{
    public int resolution = 512;
    public float gridLength = 1.0f;

    public GerstnerWave wave1;
    public GerstnerWave wave2;
    public GerstnerWave wave3;

    public ComputeShader projectGridShader;
    private int kMain = 0;
    private int ProjectGridResolution = 128;

    private Material oceanMaterial;
    private Mesh oceanMesh = null;
    private VertexData[] oceanVertexData = null;
    //private WaterMesh waterMesh = null;

    private ComputeBuffer vertexBuffer = null;
    private RenderTexture positionBuffer = null;
    private RenderTexture normalBuffer = null;

    bool materialDirty = false;
    // Start is called before the first frame update
    void Start()
    {
        oceanVertexData = new VertexData[ProjectGridResolution * ProjectGridResolution];
        //waterMesh = new WaterMesh();
        //Mesh oceanMesh = waterMesh.CreateWaterMesh(gridLength, resolution);
        oceanMesh = new Mesh();

        int[] mTriangles = new int[(ProjectGridResolution - 1) * (ProjectGridResolution - 1) * 6];
        int nIndex = 0;
        for (int i = 0; i < ProjectGridResolution - 1; ++i)
        {
            for (int j = 0; j < ProjectGridResolution - 1; ++j)
            {
                mTriangles[nIndex++] = i * ProjectGridResolution + j;
                mTriangles[nIndex++] = i * ProjectGridResolution + j + 1;
                mTriangles[nIndex++] = (i + 1) * ProjectGridResolution + j;
                mTriangles[nIndex++] = i * ProjectGridResolution + j + 1;
                mTriangles[nIndex++] = (i + 1) * ProjectGridResolution + j + 1;
                mTriangles[nIndex++] = (i + 1) * ProjectGridResolution + j;
            }
        }
        oceanMesh.triangles = mTriangles;

        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            gameObject.AddComponent<MeshRenderer>();
        }

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }
        meshFilter.mesh = oceanMesh;

        if (oceanMaterial == null)
        {
            oceanMaterial = new Material(Shader.Find("liangairan/ocean/ocean"));

            gameObject.GetComponent<Renderer>().sharedMaterial = oceanMaterial;
            
        }
    }

    private void CreateComputeShader()
    {
        if (projectGridShader != null)
        {
            if (vertexBuffer == null)
            {
                vertexBuffer = new ComputeBuffer(ProjectGridResolution * ProjectGridResolution, 8 * sizeof(float), ComputeBufferType.Default);

            }
            /*
            if (positionBuffer == null)
            {
                positionBuffer = new RenderTexture(ProjectGridResolution, ProjectGridResolution, 0, RenderTextureFormat.ARGBFloat);
                positionBuffer.enableRandomWrite = true;
                positionBuffer.wrapMode = TextureWrapMode.Repeat;
                positionBuffer.filterMode = FilterMode.Point;
                positionBuffer.useMipMap = false;
                positionBuffer.Create();
            }

            if (normalBuffer == null)
            {
                normalBuffer = new RenderTexture(ProjectGridResolution, ProjectGridResolution, 0, RenderTextureFormat.ARGBFloat);
                normalBuffer.enableRandomWrite = true;
                normalBuffer.wrapMode = TextureWrapMode.Repeat;
                normalBuffer.filterMode = FilterMode.Point;
                normalBuffer.useMipMap = false;
                normalBuffer.Create();
            }
            */

            kMain = projectGridShader.FindKernel("CSMain");
            projectGridShader.SetBuffer(kMain, "vertexBuffer", vertexBuffer);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //判断camera是否看到water
        Camera mainCamera = Camera.main;
        float yInNear = mainCamera.nearClipPlane * Mathf.Tan(mainCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
 

        float ratio = mainCamera.aspect;
        float xInNear = yInNear * ratio;
        NativeArray<Vector4> frustumCorner = new NativeArray<Vector4>(8, Allocator.Temp);
        frustumCorner[0] = new Vector4(-xInNear, yInNear, mainCamera.nearClipPlane, 1);
        frustumCorner[1] = new Vector4(xInNear, yInNear, mainCamera.nearClipPlane, 1);
        frustumCorner[2] = new Vector4(-xInNear, -yInNear, mainCamera.nearClipPlane, 1);
        frustumCorner[3] = new Vector4(xInNear, yInNear, mainCamera.nearClipPlane, 1);

        float yInFar = mainCamera.farClipPlane * Mathf.Tan(mainCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float xInFar = yInFar * ratio;

        frustumCorner[4] = new Vector4(-xInFar, yInFar, mainCamera.farClipPlane, 1);
        frustumCorner[5] = new Vector4(xInFar, yInFar, mainCamera.farClipPlane, 1);
        frustumCorner[6] = new Vector4(-xInFar, -yInFar, mainCamera.farClipPlane, 1);
        frustumCorner[7] = new Vector4(xInFar, yInFar, mainCamera.farClipPlane, 1);

        for (int i = 0; i < 8; ++i)
        {
            frustumCorner[i] = mainCamera.cameraToWorldMatrix * frustumCorner[i];
        }

        int hitCount = 0;
        for (int i = 4; i < 8; ++i)
        {
            if (frustumCorner[i].y < 0)
                hitCount++;
        }

        if (projectGridShader != null)
        {

            Matrix4x4 viewMatrix = Camera.main.cameraToWorldMatrix;
            Matrix4x4 projectMatrix = Matrix4x4.identity;

            if (hitCount < 4)

            GL.GetGPUProjectionMatrix(projectMatrix, false);
            projectGridShader.Dispatch(kMain, 128 / 8, 128 / 8, 1);

            vertexBuffer.GetData(oceanVertexData);
            oceanMesh.SetVertexBufferData<VertexData>(oceanVertexData, 0, 0, ProjectGridResolution * ProjectGridResolution);
        }
    }

    private void OnDestroy()
    {
        if (oceanMesh != null)
        {
            oceanMesh.Clear();
            oceanMesh = null;
        }
        if (vertexBuffer != null)
        {
            //DestroyImmediate(vertexBuffer);
            vertexBuffer.Release();
            vertexBuffer = null;
        }
        if (positionBuffer != null)
        {
            DestroyImmediate(positionBuffer);
            positionBuffer = null;
        }

        if (normalBuffer != null)
        {
            DestroyImmediate(normalBuffer);
            normalBuffer = null;
        }
    }
}
