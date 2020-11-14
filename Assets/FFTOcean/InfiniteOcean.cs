using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/*
[System.Serializable]
public struct GerstnerWave
{
    //波的方向
    //x  wavelength
    //y  amplitude
    //zw direction
    public Vector4 waveData;
    //圆形波的中心位置(0, 1)
    public Vector2 circle;
    //波长
    //public float wavelength;
    //public float amplitude;
    //public float speed;
    public float steepness;
}
*/
[System.Serializable]
[CreateAssetMenu(fileName = "WaterResources", menuName = "water/WaterResource", order = 0)]
public class WaterResources : ScriptableObject
{
    public Texture2D SurfaceMap;
    public Cubemap SkyCube;
    public float BumpScale;
    public float Transparent;
    public Color SeaBaseColor;
    public Color SeaDiffuseColor;

    //public GerstnerWave[] Waves; // = new GerstnerWave[3];
}


public enum OceanShadeMode
{
    Shaded,
    Wireframe,
    ShadedWireframe,
}

public enum MESH_RESOLUTION 
{ 
    LOW,    
    MEDIUM, 
    HIGH,
};

public enum WaveType
{
    Gerstner,
    FFT,
    None,
}

[ExecuteInEditMode]
public class InfiniteOcean : MonoBehaviour
{
    //public int resolution = 512;
    //public float gridLength = 1.0f;

    //public GerstnerWave wave1;
    //public GerstnerWave wave2;
    //public GerstnerWave wave3;

    //public ComputeShader projectGridShader;
    //private int kMain = 0;
    private Resolution ProjectGridResolution;

    public Material oceanMaterial;
    public Material wireFrameMaterial;
    public WaterResources waterResource;
    public MESH_RESOLUTION resolution = MESH_RESOLUTION.HIGH;
    public OceanShadeMode shadedMode = OceanShadeMode.Shaded;
    public WaveType waveType = WaveType.Gerstner;
    public WaterWave waterWave;
    public bool showDebugInfo = true;
    private Mesh oceanMesh = null;

    private bool needRenderOcean = true;


    //private ComputeBuffer vertexBuffer = null;
    //private RenderTexture positionBuffer = null;
    //private CommandBuffer renderProjectedGridCommand = null;
    //private CommandBuffer renderOceanCommand = null;
    //private Material projectedGridMaterial;
    private Matrix4x4 screenToView;
    private Matrix4x4 viewToWorld;
    private Vector3 projectedCameraPos;  //camera pos in world space
    private Matrix4x4 interpolation;

    //frustum顺序：
    //0 near top left
    //1 near top right
    //2 near bottom left
    //3 near bottom right
    //4 far  top left
    //5 far  top right
    //6 far  bottom left
    //7 far  bottom right
    private List<Vector3> frustumCorners = new List<Vector3>();  //world space camera frustum corners 
    private List<Vector4> unitCorners = new List<Vector4>();
    private List<Vector4> quad = new List<Vector4>();

    //原来的frustum的12条边和ocean的波动平面的交点列表
    private List<Vector3> frustumInOceanBoundPoints = new List<Vector3>();
    readonly static int[,] m_frustumEdges =
        {
            {0,1}, {1,3}, {2,3}, {2,0},
            {4,5}, {5,7}, {6,7}, {6,4},
            {0,4}, {1,5}, {2,6}, {3,7}
        };

    //用于渲染projected grid的新的frustum的8个点
    private List<Vector3> projectedFrustumCorners = new List<Vector3>();

    public float waterLevel = 0;
    //bool materialDirty = false;
    // Start is called before the first frame update
    void Start()
    {
        //ProjectGridResolution = Screen.currentResolution;
        //每8个像素一个网格
        //ProjectGridResolution.width = Mathf.Max(Screen.width / 8, 128);
        //ProjectGridResolution.height = Mathf.Max(Screen.height / 8, 128);
    }

    private int PixelsPerGrid()
    {
        switch (resolution)
        {
            case MESH_RESOLUTION.HIGH:
                return 8;
            case MESH_RESOLUTION.MEDIUM:
                return 16;
            case MESH_RESOLUTION.LOW:
                return 32;
        }
        return 32;
    }


    bool SegmentPlaneIntersection(Vector3 a, Vector3 b, Vector3 n, float d, out Vector3 intersect)
    {
        Vector3 ab = b - a;

        float t = (d - Vector3.Dot(n, a)) / Vector3.Dot(n, ab);

        if (t > -0.0 && t <= 1.0)
        {
            intersect = a + ab * t;

            return true;
        }
        intersect = Vector3.zero;
        return false;
    }

    bool CaculateProjectedGridFrustum(Camera cam)
    {
        //---以下是新方法---
        

        if (unitCorners.Count == 0)
        {
            //near
            unitCorners.Add(new Vector4(-1, 1, -1, 1));
            unitCorners.Add(new Vector4(1, 1, -1, 1));
            unitCorners.Add(new Vector4(-1, -1, -1, 1));
            unitCorners.Add(new Vector4(1, -1, -1, 1));

            //far
            unitCorners.Add(new Vector4(-1, 1, 1, 1));
            unitCorners.Add(new Vector4(1, 1, 1, 1));
            unitCorners.Add(new Vector4(-1, -1, 1, 1));
            unitCorners.Add(new Vector4(1, -1, 1, 1));
        }

        if (frustumCorners.Count == 0)
        {
            for (int i = 0; i < 8; ++i)
            {
                frustumCorners.Add(Vector3.zero);
            }
        }

        Matrix4x4 perspective = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        
        screenToView = perspective.inverse;
        //提高campos的位置，为了远处高度不被裁剪掉。
        //projectedCameraPos = cam.transform.position + Vector3.up * 20.0f;
        //viewToWorld = Matrix4x4.LookAt(projectedCameraPos, cam.transform.forward, Vector3.up);
        viewToWorld = AimProjector(cam);

        //Matrix4x4 screenToWorld = viewToWorld * screenToView;
        //Matrix4x4 worldToScreen = perspective * viewToWorld.inverse;

        //下面计算出原来frustum的世界空间的坐标
        Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
        Matrix4x4 ivp = vp.inverse;
        for (int i = 0; i < 8; ++i)
        {
            Vector4 tmp = ivp * unitCorners[i];
            tmp /= tmp.w;
            frustumCorners[i] = tmp;
        }

        //近截面投影到海平面上
        vp = perspective * viewToWorld.inverse;
        Matrix4x4 projectorR = CreateRangeMatrix(vp);
        Matrix4x4 projectorIVP = vp.inverse * projectorR;

        //从新计算frustum的8个位置并求出4条边和海平面的交点
        if (quad.Count == 0)
        {
            //左下角
            quad.Add(new Vector4(0, 0, 0, 1));
            //右下角
            quad.Add(new Vector4(1, 0, 0, 1));
            //左上角
            quad.Add(new Vector4(0, 1, 0, 1));
            //右上角
            quad.Add(new Vector4(1, 1, 0, 1));
        }
        //------------------
        //连接near far四个角的直线和海平面的交点
        Vector4 bottomLeft = ProjectedFrustumInsertPlane(projectorIVP, quad[0]);
        interpolation.SetRow(0, bottomLeft);

        Vector4 bottomRight = ProjectedFrustumInsertPlane(projectorIVP, quad[1]);
        interpolation.SetRow(1, bottomRight);

        Vector4 topLeft = ProjectedFrustumInsertPlane(projectorIVP, quad[2]);
        interpolation.SetRow(2, topLeft);

        Vector4 topRight = ProjectedFrustumInsertPlane(projectorIVP, quad[3]);
        interpolation.SetRow(3, topRight);

        return needRenderOcean;
    }

    private void OnWillRenderObject()
    {
        Camera cam = Camera.current;
        if (cam == null)
        {
            return;
        }
        needRenderOcean = CaculateProjectedGridFrustum(cam);

        if (needRenderOcean)
        {
            Shader.SetGlobalMatrix("frustumInterpolation", interpolation);
            Shader.SetGlobalFloat("_camFarPlane", cam.farClipPlane);
        }
    }

    // Update is called once per frame
    void Update()
    {
        CreateGrid();
        
        switch (waveType)
        {
            case WaveType.Gerstner:
                if (waterWave != null && waterWave is FFTWave)
                {
                    waterWave.enabled = false;
                    Destroy(waterWave);
                }
                waterWave = GetComponent<GerstnerWave>();
                if (waterWave == null)
                {
                    waterWave = gameObject.AddComponent<GerstnerWave>();
                }
                if (!waterWave.enabled)
                {
                    waterWave.enabled = true;
                }
                Shader.EnableKeyword("GERSTNER_WAVE");
                Shader.DisableKeyword("FFT_WAVE");
                waterWave.ApplyMaterial(oceanMaterial);
                break;
            case WaveType.FFT:
                Shader.DisableKeyword("GERSTNER_WAVE");
                Shader.EnableKeyword("FFT_WAVE");
                if (waterWave != null && waterWave is GerstnerWave)
                {
                    waterWave.enabled = false;
                    Destroy(waterWave);
                }
                waterWave = GetComponent<FFTWave>();
                if (waterWave == null)
                {
                    waterWave = gameObject.AddComponent<FFTWave>();
                }
                if (!waterWave.enabled)
                {
                    waterWave.enabled = true;
                }
                waterWave.ApplyMaterial(oceanMaterial);
                break;
            case WaveType.None:
                if (waterWave != null && waterWave.enabled)
                {
                    waterWave.enabled = false;
                }
                Shader.DisableKeyword("GERSTNER_WAVE");
                Shader.DisableKeyword("FFT_WAVE");
                break;
        }
        
    }


    private void OnDestroy()
    {
        if (oceanMaterial != null)
        {
            DestroyImmediate(oceanMaterial);
            oceanMaterial = null;
        }
        if (wireFrameMaterial != null)
        {
            DestroyImmediate(wireFrameMaterial);
            wireFrameMaterial = null;
        }
        if (oceanMesh != null)
        {
            oceanMesh.Clear();
            oceanMesh = null;
        }
    }

    private void DrawDebugFrustrum(Vector3 cameraPos, Vector3 farTopLeft, Vector3 farTopRight,
        Vector3 farBottomLeft, Vector3 farBottomRight)
    {
        Debug.DrawLine(cameraPos, farTopLeft);
        Debug.DrawLine(cameraPos, farTopRight);
        Debug.DrawLine(cameraPos, farBottomLeft);
        Debug.DrawLine(cameraPos, farBottomRight);

        Debug.DrawLine(farTopLeft, farTopRight);
        Debug.DrawLine(farTopRight, farBottomRight);
        Debug.DrawLine(farBottomRight, farBottomLeft);
        Debug.DrawLine(farTopLeft, farBottomLeft);
    }

    
    void CreateGrid()
    {
        int pixelsPerGrid = PixelsPerGrid();
        int numVertX = Mathf.Min(Screen.width / pixelsPerGrid, 128);
        int numVertY = Mathf.Min(Screen.height / pixelsPerGrid, 128);
        bool needUpdateMesh = false;
        if (ProjectGridResolution.width != numVertX || ProjectGridResolution.height != numVertY)
        {
            ProjectGridResolution.width = numVertX;
            ProjectGridResolution.height = numVertY;
            needUpdateMesh = true;
        }

        if (oceanMaterial == null)
        {
            oceanMaterial = new Material(Shader.Find("liangairan/ocean/ocean"));
            //oceanMaterial.EnableKeyword("INFINITE_OCEAN");

            if (waterResource == null)
            {
                waterResource = Resources.Load<WaterResources>("WaterResources");
            }

            if (waterResource != null)
            {
                oceanMaterial.SetColor("_SeaBaseColor", waterResource.SeaBaseColor);
                oceanMaterial.SetColor("_SeaWaterColor", waterResource.SeaDiffuseColor);
                oceanMaterial.SetFloat("_Transparent", waterResource.Transparent);

                if (waterResource.SurfaceMap != null)
                {
                    oceanMaterial.SetTexture("_SurfaceMap", waterResource.SurfaceMap);
                }

                if (waterResource.SkyCube != null)
                {
                    oceanMaterial.SetTexture("_skyCube", waterResource.SkyCube);
                }

                //oceanMaterial.SetVector("_Wave1", waterResource.Waves[0].waveData);
                //oceanMaterial.SetVector("_Wave2", waterResource.Waves[1].waveData);
                //oceanMaterial.SetVector("_Wave3", waterResource.Waves[2].waveData);
                oceanMaterial.SetFloat("_BumpScale", waterResource.BumpScale);
            }
        }

        if (needUpdateMesh)
        {
            if (oceanMesh != null)
                oceanMesh.Clear();

            oceanMesh = CreateProjectedMesh(numVertX, numVertY);

            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = gameObject.AddComponent<MeshRenderer>();
            }
            renderer.sharedMaterial = oceanMaterial;

            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            meshFilter.sharedMesh = oceanMesh;

            oceanMesh.bounds = new Bounds(Camera.current != null ? Camera.current.transform.position : Vector3.zero, new Vector3(9999, 100, 9999));
        }

        
    }

    Mesh CreateProjectedMesh(int numVertsX, int numVertsY)
    {
        Vector3[] vertices = new Vector3[numVertsX * numVertsY];
        Vector2[] texcoords = new Vector2[numVertsX * numVertsY];
        int[] indices = new int[numVertsX * numVertsY * 6];

        //Percentage of verts that will be in the border.
        //Only a small number is needed.
        float border = 0.1f;

        for (int x = 0; x < numVertsX; x++)
        {
            for (int y = 0; y < numVertsY; y++)
            {

                Vector2 uv = new Vector3((float)x / (float)(numVertsX - 1), (float)y / (float)(numVertsY - 1));

                //从左下角到右上角创建vertex
                if (true)
                {
                    //Add border. Values outside of 0-1 are verts that will be in the border.
                    //把uv从0-1映射到[-border, 1 + border]
                    uv.x = uv.x * (1.0f + border * 2.0f) - border;
                    uv.y = uv.y * (1.0f + border * 2.0f) - border;

                    //The screen uv is used for the interpolation to calculate the
                    //world position from the interpolation matrix so must be in a 0-1 range.
                    Vector2 screenUV = uv;
                    screenUV.x = Mathf.Clamp01(screenUV.x);
                    screenUV.y = Mathf.Clamp01(screenUV.y);

                    //For the edge verts calculate the direction in screen space 
                    //and normalize. Only the directions length is needed but store the
                    //x and y direction because edge colors are output sometimes for debugging.
                    //越往屏幕边缘，edgeDirection越大
                    Vector2 edgeDirection = uv;

                    if (edgeDirection.x < 0.0f)
                        edgeDirection.x = Mathf.Abs(edgeDirection.x) / border;
                    else if (edgeDirection.x > 1.0f)
                        edgeDirection.x = Mathf.Max(0.0f, edgeDirection.x - 1.0f) / border;
                    else
                        edgeDirection.x = 0.0f;

                    if (edgeDirection.y < 0.0f)
                        edgeDirection.y = Mathf.Abs(edgeDirection.y) / border;
                    else if (edgeDirection.y > 1.0f)
                        edgeDirection.y = Mathf.Max(0.0f, edgeDirection.y - 1.0f) / border;
                    else
                        edgeDirection.y = 0.0f;

                    edgeDirection.x = Mathf.Pow(edgeDirection.x, 2);
                    edgeDirection.y = Mathf.Pow(edgeDirection.y, 2);

                    texcoords[x + y * numVertsX] = edgeDirection;
                    vertices[x + y * numVertsX] = new Vector3(screenUV.x, screenUV.y, 0.0f);
                }
                else
                {

                    texcoords[x + y * numVertsX] = new Vector2(0, 0);
                    vertices[x + y * numVertsX] = new Vector3(uv.x, uv.y, 0.0f);
                }
            }
        }

        int num = 0;
        for (int x = 0; x < numVertsX - 1; x++)
        {
            for (int y = 0; y < numVertsY - 1; y++)
            {
                indices[num++] = x + y * numVertsX;
                indices[num++] = x + (y + 1) * numVertsX;
                indices[num++] = (x + 1) + y * numVertsX;

                indices[num++] = x + (y + 1) * numVertsX;
                indices[num++] = (x + 1) + (y + 1) * numVertsX;
                indices[num++] = (x + 1) + y * numVertsX;
            }
        }

        Mesh mesh = new Mesh();

        mesh.vertices = vertices;
        mesh.uv = texcoords;
        mesh.triangles = indices;
        mesh.name = "Projected Grid Mesh";
        mesh.hideFlags = HideFlags.HideAndDontSave;

        return mesh;
    }


    private void LateUpdate()
    {
        if (shadedMode == OceanShadeMode.ShadedWireframe)
        {
            if (wireFrameMaterial == null)
            {
                wireFrameMaterial = new Material(Shader.Find("liangairan/ocean/wireframe"));
            }
            if (wireFrameMaterial)
                Graphics.DrawMesh(oceanMesh, Matrix4x4.identity, wireFrameMaterial, 0);
        }
    }

    //最关键的函数，创建一个近截面缩放平移矩阵，该矩阵只包含可见的海平面
    Matrix4x4 CreateRangeMatrix(Matrix4x4 vp)
    {
        Matrix4x4 rMatrix = Matrix4x4.identity;

        //range是海水上下波动的
        float range = 0; // GetWaterMaxHeight();

        if (frustumInOceanBoundPoints.Count == 0)
        {
            for (int i = 0; i < 32; ++i)
                frustumInOceanBoundPoints.Add(Vector3.zero);
        }

        //frusturm的12条边与海平面的交点数量
        //+frustrum8个点在海平面范围里的数量
        int intersectCount = 0;
        for (int i = 0; i < 8; ++i)
        {
            if (frustumCorners[i].y <= range && frustumCorners[i].y >= -range)
            {
                frustumInOceanBoundPoints[intersectCount] = frustumCorners[i];
                intersectCount++;
            }
        }

        //12条边和海平面相交
        for (int i = 0; i < 12; ++i)
        {
            int idx0 = m_frustumEdges[i, 0];
            Vector3 p0 = frustumCorners[idx0];

            int idx1 = m_frustumEdges[i, 1];
            Vector3 p1 = frustumCorners[idx1];

            Vector3 intersect;
            if (SegmentPlaneIntersection(p0, p1, Vector3.up, waterLevel + range, out intersect))
            {
                frustumInOceanBoundPoints[intersectCount] = intersect;
                intersectCount++;
            }

            if (SegmentPlaneIntersection(p0, p1, Vector3.up, waterLevel - range, out intersect))
            {
                frustumInOceanBoundPoints[intersectCount] = intersect;
                intersectCount++;
            }
        }

        float xmin = float.PositiveInfinity;
        float ymin = float.PositiveInfinity;
        float xmax = float.NegativeInfinity;
        float ymax = float.NegativeInfinity;

        //Now convert each world space position into
        //projector screen space. The min/max x/y values
        //are then used for the range conversion matrix.
        Vector3 point = Vector3.zero;
        for (int i = 0; i < intersectCount; i++)
        {
            point[0] = frustumInOceanBoundPoints[i][0];
            point[1] = waterLevel;
            point[2] = frustumInOceanBoundPoints[i][2];


            point = vp.MultiplyPoint(point);

            if (point[0] < xmin) xmin = point[0];
            if (point[1] < ymin) ymin = point[1];
            if (point[0] > xmax) xmax = point[0];
            if (point[1] > ymax) ymax = point[1];

        }
        rMatrix[0] = xmax - xmin; 
        rMatrix[12] = xmin;
        rMatrix[5] = ymax - ymin; 
        rMatrix[13] = ymin;
        return rMatrix;
    }

    Vector4 ProjectedFrustumInsertPlane(Matrix4x4 projectedTransform, Vector4 corner)
    {
        corner.z = -1;
        Vector4 nearCornerWorld = projectedTransform * corner;

        corner.z = 1;
        Vector4 farCornerWorld = projectedTransform * corner;

        Vector4 nearToFar = farCornerWorld - nearCornerWorld;

        Vector4 result = Vector4.zero;
        float t = (nearToFar[3] * waterLevel - nearCornerWorld[1]) / (nearToFar[1] - nearToFar[3] * waterLevel);

        result = nearCornerWorld + nearToFar * t;
        return result;
    }

    Matrix4x4 AimProjector(Camera cam)
    {
        projectedCameraPos = cam.transform.position;

        float fit = 20.0f;  //避免nearplane和sea bound有交集

        float range = 0;// Math.Max(0.0, m_ocean.FindMaxDisplacement(true)) + fit;


        //If the camera is below the sea level then flip the projection.
        //Make sure projection position is above or below the wave range.
        if (projectedCameraPos[1] < waterLevel)
        {
            //IsFlipped = true;
            projectedCameraPos[1] = Mathf.Min(projectedCameraPos[1], waterLevel - range);
        }
        else
        {
            //IsFlipped = false;
            projectedCameraPos[1] = Mathf.Max(projectedCameraPos[1], waterLevel + range);
        }

        Vector3 lookAt = projectedCameraPos + cam.transform.forward * 50;
        lookAt.y = waterLevel;

        return Matrix4x4.LookAt(projectedCameraPos, lookAt, Vector3.up);
    }
}
