using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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

[System.Serializable]
[CreateAssetMenu(fileName = "WaterResources", menuName = "WaterResource", order = 0)]
public class WaterResources : ScriptableObject
{
    public Texture2D SurfaceMap;
    public Cubemap SkyCube;
    public float BumpScale;
    public float Transparent;
    public Color SeaBaseColor;
    public Color SeaDiffuseColor;

    public GerstnerWave[] Waves; // = new GerstnerWave[3];
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

    public bool showDebugInfo = true;
    private Mesh oceanMesh = null;

    private bool needRenderOcean = true;


    private ComputeBuffer vertexBuffer = null;
    private RenderTexture positionBuffer = null;
    private CommandBuffer renderProjectedGridCommand = null;
    private CommandBuffer renderOceanCommand = null;
    private Material projectedGridMaterial;
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
        ProjectGridResolution.width = Mathf.Max(Screen.width / 8, 128);
        ProjectGridResolution.height = Mathf.Max(Screen.height / 8, 128);
        
        /*
        oceanMesh = new Mesh();

        

        Vector3[] positions = new Vector3[ProjectGridResolution.width * ProjectGridResolution.height];
        Vector2[] uvs = new Vector2[ProjectGridResolution.width  * ProjectGridResolution.height];
        for (int i = 0; i < ProjectGridResolution.height; ++i)
        {
            for (int j = 0; j < ProjectGridResolution.width; ++j)
            {
                int index = i * ProjectGridResolution.width + j;
                uvs[index].x = (float)j / ProjectGridResolution.width;
                uvs[index].y = 1.0f - (float)i / ProjectGridResolution.height;
            }
        }
        Vector3[] normals = new Vector3[ProjectGridResolution.width * ProjectGridResolution.height];
        oceanMesh.vertices = positions;
        oceanMesh.uv = uvs;
        oceanMesh.normals = normals;

        int[] mTriangles = new int[(ProjectGridResolution.width - 1) * (ProjectGridResolution.height - 1) * 6];
        int nIndex = 0;
        //方向是从左到右，从上到下
        for (int i = 0; i < ProjectGridResolution.height - 1; ++i)
        {
            for (int j = 0; j < ProjectGridResolution.width - 1; ++j)
            {
                mTriangles[nIndex++] = i * ProjectGridResolution.width + j;
                mTriangles[nIndex++] = i * ProjectGridResolution.width + j + 1;
                mTriangles[nIndex++] = (i + 1) * ProjectGridResolution.width + j;
                mTriangles[nIndex++] = i * ProjectGridResolution.width + j + 1;
                mTriangles[nIndex++] = (i + 1) * ProjectGridResolution.width + j + 1;
                mTriangles[nIndex++] = (i + 1) * ProjectGridResolution.width + j;
            }
        }
        oceanMesh.triangles = mTriangles;


        
        Shader.EnableKeyword("INFINITE_OCEAN");
        */
        //TestProjectedGrid();
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

    private void CreateComputeShader()
    {
        /*
        if (projectGridShader != null)
        {
            if (vertexBuffer == null)
            {
                vertexBuffer = new ComputeBuffer(ProjectGridResolution.width * ProjectGridResolution.height, 8 * sizeof(float), ComputeBufferType.Default);

            }
            
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
            

            kMain = projectGridShader.FindKernel("CSMain");
            projectGridShader.SetBuffer(kMain, "vertexBuffer", vertexBuffer);
        }*/
    }


    private float IntersectPlane(Vector3 normal, Vector3 p, Vector3 rayOrig, Vector3 rayDir)
    {
        float rdn = Vector3.Dot(-rayDir, normal);
        if (rdn == 0 || rdn < float.Epsilon)
            return 0;

        return Mathf.Max(Vector3.Dot(rayOrig - p, normal) / rdn, 0);
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
        /*
        //判断camera是否看到water
        Camera mainCamera = Camera.main;
        mainCamera = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : Camera.main;

        float yInNear = mainCamera.nearClipPlane * Mathf.Tan(mainCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);


        float ratio = mainCamera.aspect;
        float xInNear = yInNear * ratio;
        NativeArray<Vector4> frustumCorner = new NativeArray<Vector4>(8, Allocator.Temp);
        frustumCorner[0] = new Vector4(-xInNear, yInNear, mainCamera.nearClipPlane, 1);
        frustumCorner[1] = new Vector4(xInNear, yInNear, mainCamera.nearClipPlane, 1);
        frustumCorner[2] = new Vector4(-xInNear, -yInNear, mainCamera.nearClipPlane, 1);
        frustumCorner[3] = new Vector4(xInNear, -yInNear, mainCamera.nearClipPlane, 1);

        float yInFar = mainCamera.farClipPlane * Mathf.Tan(mainCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float xInFar = yInFar * ratio;

        frustumCorner[4] = new Vector4(-xInFar, yInFar, mainCamera.farClipPlane, 1);
        frustumCorner[5] = new Vector4(xInFar, yInFar, mainCamera.farClipPlane, 1);
        frustumCorner[6] = new Vector4(-xInFar, -yInFar, mainCamera.farClipPlane, 1);
        frustumCorner[7] = new Vector4(xInFar, -yInFar, mainCamera.farClipPlane, 1);

        float mainCameraFarPlaneWidth = xInFar * 2.0f;

        for (int i = 0; i < 8; ++i)
        {
            frustumCorner[i] = mainCamera.transform.localToWorldMatrix.MultiplyPoint(frustumCorner[i]);
        }
        int hitCount = 0;
        for (int i = 4; i < 8; ++i)
        {
            if (frustumCorner[i].y < 0)
                hitCount++;
        }
        needRenderOcean = true;
        if (hitCount == 0)
        {
            needRenderOcean = false;
            return needRenderOcean;
        }

        
        else if (hitCount < 4)
        {
            //重新计算一个camera
            Vector3 orig = mainCamera.transform.position;
            Quaternion rotate = Quaternion.AngleAxis(mainCamera.fieldOfView * 0.5f, mainCamera.transform.right);
            Vector3 downVec = rotate * mainCamera.transform.forward;
            float t = IntersectPlane(Vector3.up, Vector3.zero, orig, downVec.normalized);
            Vector3 pDown = Vector3.zero;
            if (t > 0)
            {
                pDown = orig + downVec * t;
            }
            else
            {
                Debug.LogError("something wrong in intersect with bottom plane!");
            }

            orig = (frustumCorner[4] + frustumCorner[5]) * 0.5f;
            Vector3 vec = mainCamera.transform.position + mainCamera.transform.forward * mainCamera.farClipPlane - orig;
            vec.Normalize();
            Vector3 pFarIntersect = Vector3.zero;
            t = IntersectPlane(Vector3.up, Vector3.zero, orig, vec);

            if (t > 0)
            {
                pFarIntersect = orig + vec * t;
            }
            else
            {
                Debug.LogError("something wrong in intersect with far plane!");
            }

            Vector3 center = (pDown + pFarIntersect) * 0.5f;

            //
            float dis = Vector3.Distance(pDown, center) + 10.0f;

            Vector3 cameraInPlane = mainCamera.transform.forward;
            cameraInPlane.y = 0;
            cameraInPlane.Normalize();

            Vector3 tmp = center - cameraInPlane * dis;
            projectedCameraPos = tmp + Vector3.up * Mathf.Sqrt(3.0f) * dis;
            Vector3 cameraForward = (center - projectedCameraPos).normalized;

            //相机空间到世界空间的矩阵
            viewToWorld = Matrix4x4.LookAt(projectedCameraPos, center, Vector3.up);


            float far = Vector3.Distance(center, projectedCameraPos) * 2;
            Matrix4x4 perspective = Matrix4x4.Perspective(60.0f, mainCamera.aspect, mainCamera.nearClipPlane, far);
            //perspective = GL.GetGPUProjectionMatrix(perspective, false);
            if (SystemInfo.usesReversedZBuffer)
            {
                perspective.m22 = -perspective.m22;
                perspective.m32 = -perspective.m32;
                //screenToView.m22 = -screenToView.m22;
                //screenToView.m23 = -screenToView.m23;
                //viewToWorld.m11 = -viewToWorld.m11;
                //viewToWorld.m23 = -viewToWorld.m23;
            }
            screenToView = perspective.inverse;

            if (showDebugInfo)
            {
                float farPlaneHeight = far / Mathf.Sqrt(3) * 2;
                float farPlaneWidth = farPlaneHeight * mainCamera.aspect;
                //farplane 的up向量
                Vector3 farUp = viewToWorld.MultiplyVector(Vector3.up).normalized * farPlaneHeight * 0.5f;
                //farplane 的right向量
                Vector3 farRight = viewToWorld.MultiplyVector(Vector3.right).normalized * farPlaneWidth * 0.5f;
                Vector3 farCenter = projectedCameraPos + cameraForward * far;
                Vector3 farCornerTL = farCenter + farUp - farRight;
                Vector3 farCornerTR = farCenter + farUp + farRight;
                Vector3 farCornerBL = farCenter - farUp - farRight;
                Vector3 farCornerBR = farCenter - farUp + farRight;
                DrawDebugFrustrum(projectedCameraPos, farCornerTL, farCornerTR, farCornerBL, farCornerBR);
            }
            
        }
        else
        {
            //使用当 前的camera生成projected grid
            //viewToWorld = mainCamera.cameraToWorldMatrix;
            projectedCameraPos = mainCamera.transform.position - mainCamera.transform.forward * 50.0f;
            viewToWorld = Matrix4x4.LookAt(projectedCameraPos, mainCamera.transform.position + mainCamera.transform.forward, Vector3.up);

            Matrix4x4 perspective = Matrix4x4.Perspective(mainCamera.fieldOfView, mainCamera.aspect, mainCamera.nearClipPlane, mainCamera.farClipPlane + 50.0f);
            
            //perspective = GL.GetGPUProjectionMatrix(perspective, false);

            if (SystemInfo.usesReversedZBuffer)
            {
                //screenToView.m22 = -screenToView.m22;
                perspective.m22 = -perspective.m22;
                perspective.m32 = -perspective.m32;
            }
            screenToView = perspective.inverse;

            if (showDebugInfo)
                DrawDebugFrustrum(projectedCameraPos, frustumCorner[4], frustumCorner[5], frustumCorner[6], frustumCorner[7]);
        }
        */

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


        //if (projectGridShader != null)
        //{
        //    Matrix4x4 viewMatrix = Camera.main.cameraToWorldMatrix;
        //    Matrix4x4 projectMatrix = Matrix4x4.identity;

        //    projectMatrix = GL.GetGPUProjectionMatrix(projectMatrix, false);
        //}
        CreateGrid();
        

        /*
        if (needRenderOcean)
        {
            RenderProjectedGrid();
            //TestProjectedGrid();
            RenderOcean();
        }
        else
        {
            Camera mainCamera = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : Camera.main;
            if (renderProjectedGridCommand != null)
                mainCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, renderProjectedGridCommand);

            if (renderOceanCommand != null)
                mainCamera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, renderOceanCommand);
        }   
        */
    }

    private void RenderProjectedGrid()
    {
        if (renderProjectedGridCommand == null)
        {
            renderProjectedGridCommand = new CommandBuffer();
            renderProjectedGridCommand.name = "Render Projected Grid";
            Camera mainCamera = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : Camera.main;
            mainCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, renderProjectedGridCommand);
        }
        

        renderProjectedGridCommand.Clear();

        if (positionBuffer == null)
        {
            positionBuffer = new RenderTexture(ProjectGridResolution.width, ProjectGridResolution.height, 1, RenderTextureFormat.ARGBHalf);
            //renderProjectedGridCommand.SetRenderTarget(positionBuffer);
        }

        renderProjectedGridCommand.SetRenderTarget(positionBuffer, 0);
        renderProjectedGridCommand.ClearRenderTarget(true, true, Color.green);

        if (projectedGridMaterial == null)
        {
            projectedGridMaterial = new Material(Shader.Find("liangairan/ocean/projectedgrid"));
        }

        renderProjectedGridCommand.SetGlobalMatrix("screenToView", screenToView);
        renderProjectedGridCommand.SetGlobalMatrix("viewToWorld", viewToWorld);
        renderProjectedGridCommand.SetGlobalVector("cameraPosProj", projectedCameraPos);

        renderProjectedGridCommand.Blit(null, positionBuffer, projectedGridMaterial);

        
        
        //renderProjectedGridCommand.DrawMesh(oceanMesh, Matrix4x4.identity, oceanMaterial);
    }

    private void RenderOcean()
    {
        if (renderOceanCommand == null)
        {
            renderOceanCommand = new CommandBuffer();
            renderOceanCommand.name = "render ocean";
            Camera mainCamera = Camera.main;
            mainCamera = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : Camera.main;
            mainCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, renderOceanCommand);
            Debug.Log("AddCommandBuffer render ocean");
        }

        

        renderOceanCommand.Clear();
        if (positionBuffer != null)
            renderOceanCommand.SetGlobalTexture("_ProjectedGridMap", positionBuffer);
        renderOceanCommand.DrawMesh(oceanMesh, Matrix4x4.identity, oceanMaterial);
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

        if (renderProjectedGridCommand != null && Camera.main != null)
        {
            Camera mainCamera = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : Camera.main;
            mainCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, renderProjectedGridCommand);
            renderProjectedGridCommand.Clear();
            renderProjectedGridCommand = null;
        }

        if (renderOceanCommand != null && Camera.main != null)
        {
            Camera mainCamera = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : Camera.main;
            mainCamera.RemoveCommandBuffer(CameraEvent.AfterSkybox, renderOceanCommand);
            renderOceanCommand.Clear();
            renderOceanCommand = null;
        }

        
    }

    /*
    private void TestProjectedGrid()
    {
        CaculateProjectedGridFrustum();
        Vector3[] positions = new Vector3[ProjectGridResolution.width * ProjectGridResolution.height];
        for (int i = 0; i < ProjectGridResolution.height; ++i)
        {
            for (int j = 0; j < ProjectGridResolution.width; ++j)
            {
                int index = i * ProjectGridResolution.width + j;
                //positions[index].x = ((float)j / ProjectGridResolution.width - 0.5f) * 2 * ProjectGridResolution.width;
                //positions[index].z = ((float)i / ProjectGridResolution.height - 0.5f) * 2 * ProjectGridResolution.height;
                //positions[index].y = 0;
                positions[index].x = ((float)j / (ProjectGridResolution.width - 1) - 0.5f) * 2;
                positions[index].y = -((float)i / (ProjectGridResolution.height - 1) - 0.5f) * 2;
                positions[index].z = 1;
                Vector4 screenPos = Vector4.one;
                screenPos.x = ((float)j / (ProjectGridResolution.width - 1) - 0.5f) * 2;
                screenPos.y = -((float)i / (ProjectGridResolution.width - 1) - 0.5f) * 2;
                screenPos.z = -1.0f;
                screenPos.w = 1.0f;

                Vector3 viewPos = screenToView.MultiplyPoint(positions[index]);
                Vector4 viewPos2 = screenToView * screenPos;
                if (viewPos2.w != 0)
                    viewPos2 /= viewPos2.w;
                Vector3 camDir = screenToView.MultiplyPoint(positions[index]).normalized;
                Vector3 worldDir = viewToWorld.MultiplyVector(camDir).normalized;
                float t = -projectedCameraPos.y / worldDir.y;
                Vector3 planePos = projectedCameraPos + t * worldDir;

                positions[index] = planePos;
            }
        }
        oceanMesh.vertices = positions;
    }
    */

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

    /*
    private void LateUpdate()
    {
        if (needRenderOcean)
        {
            if (projectedGridMaterial == null)
            {
                projectedGridMaterial = new Material(Shader.Find("liangairan/ocean/projectedgrid"));
            }

            if (positionBuffer == null)
            {
                positionBuffer = new RenderTexture(ProjectGridResolution.width, ProjectGridResolution.height, 1, RenderTextureFormat.ARGBHalf);
            }

            projectedGridMaterial.SetMatrix("screenToView", screenToView);
            projectedGridMaterial.SetMatrix("viewToWorld", viewToWorld);
            projectedGridMaterial.SetVector("cameraPosProj", projectedCameraPos);
            Graphics.SetRenderTarget(positionBuffer);
            GL.Clear(true, true, Color.black);
            Graphics.Blit(null, positionBuffer, projectedGridMaterial);
            oceanMesh.bounds = new Bounds(Vector3.zero, new Vector3(9999, 100, 9999));
            Shader.SetGlobalTexture("_ProjectedGridMap", positionBuffer);
            

            if (shadedMode == OceanShadeMode.Shaded)
            {
                Graphics.DrawMesh(oceanMesh, Matrix4x4.identity, oceanMaterial, 0);
                
            }
            else if (shadedMode == OceanShadeMode.Wireframe)
            {
                if (wireFrameMaterial == null)
                {
                    wireFrameMaterial = new Material(Shader.Find("liangairan/ocean/wireframe"));
                }
                Graphics.DrawMesh(oceanMesh, Matrix4x4.identity, wireFrameMaterial, 0);
            }
            else if (shadedMode == OceanShadeMode.ShadedWireframe)
            {
                Graphics.DrawMesh(oceanMesh, Matrix4x4.identity, oceanMaterial, 0);
                if (wireFrameMaterial == null)
                {
                    wireFrameMaterial = new Material(Shader.Find("liangairan/ocean/wireframe"));
                }
                Graphics.DrawMesh(oceanMesh, Matrix4x4.identity, wireFrameMaterial, 0);
            }
        }
    }
    */

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

                oceanMaterial.SetVector("_Wave1", waterResource.Waves[0].waveData);
                oceanMaterial.SetVector("_Wave2", waterResource.Waves[1].waveData);
                oceanMaterial.SetVector("_Wave3", waterResource.Waves[2].waveData);
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
            meshFilter.mesh = oceanMesh;

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

    float GetWaterMaxHeight()
    {
        float maxHeight = 0;
        if (waterResource != null)
        {
            for (int i = 0; i < waterResource.Waves.Length; i++)
            {
                maxHeight = Mathf.Max(maxHeight, waterResource.Waves[i].waveData.y);
            }
        }

        return maxHeight;
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

        float fit = 20.0f;

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
