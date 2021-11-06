using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct BVHNode
{
    public bool isLeaf;
    public int index;
    public int primitiveStart;   //primitive offset in primtive array
    public int primitiveNum;     //primitive's count
    public Bounds bounds;
}

struct BVHPrimitiveInfo
{
	public BVHPrimitiveInfo(int priIndex, GPUBounds bounds)	
	{
		primitiveIndex = priIndex;
		worldBound = bounds;
	}
	//primitive's index in primitive array
	public int primitiveIndex;
	
	public GPUBounds worldBound;
	//worldbound's central point
	//Vector3 centroid;
};
public class BVHBuildNode
{
	// BVHBuildNode Public Methods
	//public static BVHBuildNode CreateInnerNode(GPUBounds bounds, BVHBuildNode left, BVHBuildNode right)
 //   {
	//	BVHBuildNode node = new BVHBuildNode();
	//	node.bounds = bounds;
	//	node.childrenLeft = left;
	//	node.childrenRight = right;
	//	node.nPrimitives = 0;
	//	return node;
 //   }
	public void InitLeaf(int first, int n, GPUBounds b) 
	{
		firstPrimOffset = first;
		nPrimitives = n;
		bounds = b;
		//children[0] = children[1] = nullptr;
		//++leafNodes;
		//++totalLeafNodes;
		//totalPrimitives += n;
	}
	public void InitInterior(int axis, BVHBuildNode c0, BVHBuildNode c1)
	{
		childrenLeft = c0;
		childrenRight = c1;
		//bounds.SetMinMax(Vector3.Min(c0.bounds.min, c1.bounds.min), Vector3.Max(c0.bounds.max, c1.bounds.max));
		bounds = GPUBounds.Union(c0.bounds, c1.bounds);
		splitAxis = axis;
		nPrimitives = 0;
		//++interiorNodes;
	}
	public GPUBounds bounds;
	public BVHBuildNode childrenLeft;
	public BVHBuildNode childrenRight;
	public int splitAxis;
	public string name;
	
	//leaf node���õ�������
	public int firstPrimOffset;
	//the number of primtives in leaf. 0 is a interior node
	//if is toplevel build node is the bottom bvh offset
	public int nPrimitives;

	public bool IsLeaf()
    {
		return nPrimitives > 0;
    }
};



//use as an array can transfer to the compute shader
public struct LinearBVHNode
{
	public GPUBounds bounds;  //64bytes
	
	public int primitivesOffset;    // leaf
	public int secondChildOffset;   // interior

	public ushort nPrimitives;  // 0 -> interior node
	public ushort axis;          // interior node: xyz
	//public int vertexOffset;     //vertexbuffer offset
	//public int faceOffset;       //indexbuffer offset
    //ushort pad;        // ensure 32 byte total size
};

class StackEntry
{
	public BVHBuildNode node;
	public int idx;

	public StackEntry(BVHBuildNode n, int i)
	{
		node = n;
		idx = i;
	}

	public int EncodeIdx() { return node.IsLeaf() ? ~idx : idx; }
};

public class BVHAccel
{
	int maxPrimsInNode;
	
	BVHBuildNode root;
	//List<Primitive> primitives;
	//public LinearBVHNode[] linearNodes;
	//BVHBuilder builder = new SplitBVHBuilder();
	public static int maxLeafSize = 4;  //a node can contain leaves
										//scene informations
										//Texture2D bvhNodeTexture;
	//public GPUBVHNode[] m_nodes;
	//gpu�е�bvh nodes����
	public List<GPUBVHNode> m_nodes = new List<GPUBVHNode>();
	public List<Vector4> m_woodTriangleVertices = new List<Vector4>();
	public List<int> m_woodTriangleIndices = new List<int>();
	public List<GPUVertex> sceneVertices;
	//�����instance bvh�������Vertices�� local space vertex���������world space vertex
	//public List<GPUVertex> m_vertices = new List<GPUVertex>();

	//woop's triangle transform
	Vector4[] m_woop = new Vector4[3];


	public int instBVHNodeAddr = 0;

	public static unsafe int SingleToInt32Bits(float value)
	{
		return *(int*)(&value);
	}

	public static unsafe Vector4Int SingleToInt32Bits(Vector4 value)
    {
		Vector4Int result = new Vector4Int();
		result.x = SingleToInt32Bits(value.x);
		result.y = SingleToInt32Bits(value.y);
		result.z = SingleToInt32Bits(value.z);
		result.w = SingleToInt32Bits(value.w);
		return result;
	}
	public static unsafe float Int32BitsToSingle(int value)
	{
		return *(float*)(&value);
	}

	int min_min(int a, int b, int c)
	{
		return Mathf.Min(Mathf.Min(a, b), c);
	}
	int min_max(int a, int b, int c)
	{
		return Mathf.Max(Mathf.Min(a, b), c);

	}
	int max_min(int a, int b, int c)
	{
		return Mathf.Min(Mathf.Max(a, b), c);
	}

	int max_max(int a, int b, int c)
	{
		return Mathf.Max(Mathf.Max(a, b), c);
	}

	float fmin_fmin(float a, float b, float c) { return Int32BitsToSingle(min_min(SingleToInt32Bits(a), SingleToInt32Bits(b), SingleToInt32Bits(c))); }
	float fmin_fmax(float a, float b, float c) { return Int32BitsToSingle(min_max(SingleToInt32Bits(a), SingleToInt32Bits(b), SingleToInt32Bits(c))); }
	float fmax_fmin(float a, float b, float c) { return Int32BitsToSingle(max_min(SingleToInt32Bits(a), SingleToInt32Bits(b), SingleToInt32Bits(c))); }
	float fmax_fmax(float a, float b, float c) { return Int32BitsToSingle(max_max(SingleToInt32Bits(a), SingleToInt32Bits(b), SingleToInt32Bits(c))); }

	float spanBeginKepler(float a0, float a1, float b0, float b1, float c0, float c1, float d) 
	{ 
		return fmax_fmax(Mathf.Min(a0, a1), Mathf.Min(b0, b1), fmin_fmax(c0, c1, d)); 
	}
	float spanEndKepler(float a0, float a1, float b0, float b1, float c0, float c1, float d) 
	{ 
		return fmin_fmin(Mathf.Max(a0, a1), Mathf.Max(b0, b1), fmax_fmin(c0, c1, d)); 
	}

	public void Build(List<Primitive> primitives, List<GPUVertex> vertices, List<int> triangles)
    {
		sceneVertices = vertices;
		/*
		primitives = prims;
		maxPrimsInNode = maxPrims;
		List<BVHPrimitiveInfo> primitiveInfos = new List<BVHPrimitiveInfo>();
		for (int i = 0; i < primitives.Count; ++i)
			primitiveInfos.Add(new BVHPrimitiveInfo(i, ConvertUnityBounds(primitives[i].worldBound)));
		//List<Primitive> orderedPrims = new List<Primitive>();
		root = RecursiveBuild(ref primitiveInfos, 0, prims.Count, ref orderedPrims);
		primitives = orderedPrims;
		*/
		BVHBuilder builder = new SplitBVHBuilder();
		List<Primitive> orderedPrims = new List<Primitive>();
		root = builder.Build(primitives, orderedPrims, vertices, triangles);
		primitives = orderedPrims;
		int offset = 0;
		LinearBVHNode[] linearNodes = new LinearBVHNode[builder.TotalNodes];
		FlattenBVHTree(root, ref offset, linearNodes);
		int totalPrimitives = 0;
		for (int i = 0; i < linearNodes.Length; ++i)
        {
			totalPrimitives += linearNodes[i].nPrimitives;
		}
		CreateCompact(root, primitives, vertices);
		//bvhNodeTexture = new Texture2D(builder.TotalNodes, 1, TextureFormat.RGBAFloat, false);
	}

	public void Clear()
    {
		sceneVertices = null;
		root = null;
		//linearNodes = null;
	}
	int FlattenBVHTree(BVHBuildNode node, ref int offset, LinearBVHNode[] linearNodes)
	{
		//LinearBVHNode linearNode = linearNodes[offset];
		int curOffset = offset;
		linearNodes[curOffset].bounds.max = node.bounds.max;
		linearNodes[curOffset].bounds.min = node.bounds.min;
		int myOffset = offset++;
		if (node.nPrimitives > 0)
		{
			//��һ��Ҷ�ӽڵ�
			linearNodes[curOffset].nPrimitives = (ushort)node.nPrimitives;
			linearNodes[curOffset].primitivesOffset = node.firstPrimOffset;
		}
		else
		{
			linearNodes[curOffset].axis = (ushort)node.splitAxis;
			linearNodes[curOffset].nPrimitives = 0;
			//���ﷵ����offset
			FlattenBVHTree(node.childrenLeft, ref offset, linearNodes);
			linearNodes[curOffset].secondChildOffset = FlattenBVHTree(node.childrenRight, ref offset, linearNodes);
		}

		return myOffset;
	}

	public void DrawDebug()
    {
		/*
		if (linearNodes != null)
        {
			for (int i = 0; i < linearNodes.Length; ++i)
			{
				if (linearNodes[i].nPrimitives > 0)
                {
					GPUBounds bound = linearNodes[i].bounds;
					RenderDebug.DrawDebugBound(bound.min.x, bound.max.x, bound.min.y, bound.max.y, bound.min.z, bound.max.z);
				}
				
			}
		}
		*/

		//m_nodesȫ��inner node���ҵ���Ӧ�������λ�����
		for (int i = 0; i < m_nodes.Count; ++i)
		{
			if (SingleToInt32Bits(m_nodes[i].cids.x) >= 0)
            {
				RenderDebug.DrawDebugBound(m_nodes[i].b0xy.x, m_nodes[i].b0xy.y, m_nodes[i].b0xy.z, m_nodes[i].b0xy.w, m_nodes[i].b01z.x, m_nodes[i].b01z.y, Color.white);
			}
			else
            {
				//RenderDebug.DrawDebugBound(m_nodes[i].b0xy.x, m_nodes[i].b0xy.y, m_nodes[i].b0xy.z, m_nodes[i].b0xy.w, m_nodes[i].b01z.x, m_nodes[i].b01z.y, Color.white);

				int triAddr = SingleToInt32Bits(m_nodes[i].cids.x);
				int triNum = SingleToInt32Bits(m_nodes[i].cids.z);

                for (int tri = ~triAddr; tri < ~triAddr + triNum * 3; tri += 3)
                {
                    RenderDebug.DrawTriangle(sceneVertices[m_woodTriangleIndices[tri]].position,
						sceneVertices[m_woodTriangleIndices[tri + 1]].position,
						sceneVertices[m_woodTriangleIndices[tri + 2]].position, Color.red);
                }
                //RenderDebug.DrawDebugBound(m_nodes[i].b0xy.x, m_nodes[i].b0xy.y, m_nodes[i].b0xy.z, m_nodes[i].b0xy.w, m_nodes[i].b01z.x, m_nodes[i].b01z.y, Color.white);
            }

			if (SingleToInt32Bits(m_nodes[i].cids.y) >= 0)
			{
				RenderDebug.DrawDebugBound(m_nodes[i].b1xy.x, m_nodes[i].b1xy.y, m_nodes[i].b1xy.z, m_nodes[i].b1xy.w, m_nodes[i].b01z.z, m_nodes[i].b01z.w, Color.white);
			}
			else
            {
				//if (!drawBound)
				RenderDebug.DrawDebugBound(m_nodes[i].b1xy.x, m_nodes[i].b1xy.y, m_nodes[i].b1xy.z, m_nodes[i].b1xy.w, m_nodes[i].b01z.z, m_nodes[i].b01z.w, Color.gray);
				int triAddr = SingleToInt32Bits(m_nodes[i].cids.y);
				int triNum = SingleToInt32Bits(m_nodes[i].cids.w);

				//for (int tri = ~triAddr; tri < ~triAddr + triNum * 3; tri += 3)
				//{
				//	RenderDebug.DrawTriangle(m_vertices[tri].position, m_vertices[tri + 1].position, m_vertices[tri + 2].position, Color.yellow);
				//}
				//RenderDebug.DrawDebugBound(m_nodes[i].b1xy.x, m_nodes[i].b1xy.y, m_nodes[i].b1xy.z, m_nodes[i].b1xy.w, m_nodes[i].b01z.z, m_nodes[i].b01z.w, Color.red);
			}
		}
    }

	public static void TestPartition()
	{
		List<int> numbers = new List<int>();
		int[] a = { 5, 2, 9, 3, 7, 1, 6, 0, 4 };
		for (int i = 0; i < a.Length; ++i)
			numbers.Add(a[i]);

		int mid = std.partition<int>(ref numbers, 0, numbers.Count,
			(a) =>
			{
				return a < 5;
			});

		Debug.Log(numbers);
	}

	void CreateBasic(BVHBuildNode root, List<Vector3> positions, int totalNodes)
    {
		//m_nodes.resizeDiscard((root->getSubtreeSize(BVH_STAT_NODE_COUNT) * 64 + Align - 1) & -Align);
		GPUBVHNode[] nodes = new GPUBVHNode[totalNodes];

		int nextNodeIdx = 0;
		List<StackEntry> stack = new List<StackEntry>();
		stack.Add(new StackEntry(root, nextNodeIdx++));
		GPUBounds b0 = new GPUBounds();
		GPUBounds b1 = new GPUBounds();
		int c0;
		int c1;
		while (stack.Count > 0)
		{
			StackEntry e = stack[stack.Count - 1];
			stack.RemoveAt(stack.Count - 1);

			GPUBVHNode gpuBVH = new GPUBVHNode();


			// Leaf?
			if (e.node.IsLeaf())
			{
				BVHBuildNode leaf = e.node;
				//gpuBVH.b1 = leaf.bounds;
				//gpuBVH.b2 = leaf.bounds;
				//gpuBVH.idx1 = leaf.firstPrimOffset;
				//gpuBVH.idx2 = leaf.nPrimitives;
				b0 = leaf.bounds;
				b1 = leaf.bounds;
				c0 = leaf.firstPrimOffset;
				c1 = leaf.nPrimitives;
			}

			// Internal node?

			else
			{
				StackEntry e0 = new StackEntry(e.node.childrenLeft, nextNodeIdx++);
				stack.Add(e0);
				StackEntry e1 = new StackEntry(e.node.childrenRight, nextNodeIdx++);
				stack.Add(e1);
				//gpuBVH.b1 = e0.node.bounds;
				//gpuBVH.b2 = e1.node.bounds;
				//gpuBVH.idx1 = e0.EncodeIdx();
				//gpuBVH.idx2 = e1.EncodeIdx();
				b0 = e0.node.bounds;
				b1 = e1.node.bounds;
				c0 = e0.EncodeIdx();
				c1 = e1.EncodeIdx();
			}

			gpuBVH.b0xy = new Vector4(b0.min.x, b0.max.x, b0.min.y, b0.max.y);
			gpuBVH.b1xy = new Vector4(b1.min.x, b1.max.x, b1.min.y, b1.max.y);
			gpuBVH.b01z = new Vector4(b0.min.z, b0.max.z, b1.min.z, b1.max.z);
			gpuBVH.cids = new Vector4(c0, c1, 0, 0);

			nodes[e.idx] = gpuBVH;
		}
		m_nodes.AddRange(nodes);
	}

	//create the gpu bvh nodes
	//param meshNode�����Ƿ�һ��mesh�µ�bvh����
	//gpuVertices 
	//bottomLevel
	//bottomLevelOffset   bottomlevel��bvh��m_nodes�е�����
	void CreateCompact(BVHBuildNode root, List<Primitive> primitives, List<GPUVertex> gpuVertices, bool bottomLevel = true, List<int> botomLevelOffset = null)
	{
		//GPUBVHNode[] nodes = new GPUBVHNode[nodesNum];
		List<GPUBVHNode> nodes = new List<GPUBVHNode>(m_nodes);
		nodes.Add(new GPUBVHNode());
		//int nextNodeIdx = 0;
		List<StackEntry> stack = new List<StackEntry>();
		stack.Add(new StackEntry(root, m_nodes.Count));
		GPUBounds b0 = new GPUBounds();
		GPUBounds b1 = new GPUBounds();
		
		while (stack.Count > 0)
		{
			int c0 = 0;  //bottomlevel bvh: left child vertices offset; toplevel bvh: 
			int c1 = 0;  //bottomlevel bvh: right child vertices offset; toplevel bvh: 
			int c2 = 0;  //bottomlevel bvh: left child primtives num; toplevel bvh: left child bottomlevel bvh offset
			int c3 = 0;  //bottomlevel bvh: right child primitves num
			StackEntry e = stack[stack.Count - 1];
			stack.RemoveAt(stack.Count - 1);

			//left child
			if (!e.node.childrenLeft.IsLeaf())
            {
				c0 = nodes.Count;//++nextNodeIdx;
				stack.Add(new StackEntry(e.node.childrenLeft, c0));
				nodes.Add(new GPUBVHNode());
			}

			if (e.node.childrenLeft.IsLeaf())
			{
				if (bottomLevel)
                {
					c0 = ~m_woodTriangleVertices.Count;
					BVHBuildNode child = e.node.childrenLeft;
					//����������
					for (int i = child.firstPrimOffset; i < child.firstPrimOffset + child.nPrimitives; ++i)
					{
						//��������ÿ�����㰴˳��д��buffer���֤��ÿ�������ε�������������
						UnitTriangle(i, gpuVertices, primitives);
						Primitive primitive = primitives[i];

						for (int v = 0; v < 3; ++v)
						{
							m_woodTriangleVertices.Add(m_woop[v]);
							m_woodTriangleIndices.Add(primitive.triIndices[v]);
							Vector4 worldPos = gpuVertices[primitive.triIndices[v]].position;
							Vector4 uv = gpuVertices[primitive.triIndices[v]].uv;
							//if (v == 0)
							//	worldPos.w = Int32BitsToSingle(primitive.materialIndex);
							
							//m_vertices.Add(new GPUVertex(worldPos, uv));
						}
					}
					m_woodTriangleVertices.Add(new Vector4(Int32BitsToSingle(int.MaxValue), 0, 0 , 0));
					m_woodTriangleIndices.Add(int.MaxValue);
					//m_vertices.Add(new GPUVertex(new Vector4(Int32BitsToSingle(int.MaxValue), 0, 0, 0), Vector4.zero));
					c2 = child.nPrimitives;
				}
				else
                {
					BVHBuildNode child = e.node.childrenLeft;
					//����meshInst
					
					Primitive primitive = primitives[child.firstPrimOffset];
					
					c0 = botomLevelOffset[primitive.meshIndex];
					c2 = primitive.meshInstIndex;
				}

			}

			//right child
			if (!e.node.childrenRight.IsLeaf())
			{
				c1 = nodes.Count;//++nextNodeIdx;
				stack.Add(new StackEntry(e.node.childrenRight, c1));
				nodes.Add(new GPUBVHNode());
			}
			
			if (e.node.childrenRight.IsLeaf())
			{
				if (bottomLevel)
				{
					c1 = ~m_woodTriangleVertices.Count;
					BVHBuildNode child = e.node.childrenRight;
					//����������
					for (int i = child.firstPrimOffset; i < child.firstPrimOffset + child.nPrimitives; ++i)
					{
						//��������д��buffer��
						UnitTriangle(i, gpuVertices, primitives);
						Primitive primitive = primitives[i];
						for (int v = 0; v < 3; ++v)
						{
							m_woodTriangleVertices.Add(m_woop[v]);
							m_woodTriangleIndices.Add(primitive.triIndices[v]);
							//m_vertices.Add(new GPUVertex(gpuVertices[primitive.triIndices[v]].position, gpuVertices[primitive.triIndices[v]].uv));
						}

					}
					m_woodTriangleVertices.Add(new Vector4(Int32BitsToSingle(int.MaxValue), 0, 0, 0));
					m_woodTriangleIndices.Add(int.MaxValue);
					//m_vertices.Add(new GPUVertex(new Vector4(Int32BitsToSingle(int.MaxValue), 0, 0, 0), Vector4.zero));
					c3 = child.nPrimitives;
				}
				else
                {
					BVHBuildNode child = e.node.childrenRight;
					//����meshInst

					Primitive primitive = primitives[child.firstPrimOffset];

					c1 = botomLevelOffset[primitive.meshIndex];
					c3 = primitive.meshInstIndex;
				}
			}

			//��ӽ���
			GPUBVHNode gpuBVH = new GPUBVHNode();
			b0 = e.node.childrenLeft.bounds;
			b1 = e.node.childrenRight.bounds;

			gpuBVH.b0xy = new Vector4(b0.min.x, b0.max.x, b0.min.y, b0.max.y);
			gpuBVH.b1xy = new Vector4(b1.min.x, b1.max.x, b1.min.y, b1.max.y);
			gpuBVH.b01z = new Vector4(b0.min.z, b0.max.z, b1.min.z, b1.max.z);

			//if (!bottomLevel && c0 > 0)
			//	c0 += m_nodes.Count;

			//if (!bottomLevel && c1 > 0)
			//	c1 += m_nodes.Count;
			gpuBVH.cids = new Vector4(Int32BitsToSingle(c0), Int32BitsToSingle(c1), Int32BitsToSingle(c2), Int32BitsToSingle(c3));

			nodes[e.idx] = gpuBVH;
		}

		m_nodes = nodes;
		//if (bottomLevel)
		//	m_nodes = nodes;
		//else
		//	m_meshInstanceBVHNodes = nodes;
	}

	//x is left child index
	//y is right child index
	Vector2Int GetNodeChildIndex(Vector4 cids)
    {
		Vector2Int childIndex = new Vector2Int(-1, -1);
		childIndex.x = SingleToInt32Bits(cids.x);
		childIndex.y = SingleToInt32Bits(cids.y);
		return childIndex;
    }

	Vector2Int GetTopLevelLeaveMeshInstance(Vector4 cids)
    {
		Vector2Int childIndex = new Vector2Int(-1, -1);
		childIndex.x = SingleToInt32Bits(cids.z);
		childIndex.y = SingleToInt32Bits(cids.w);
		return childIndex;
	}
	void UnitTriangle(int triIndex, List<GPUVertex> vertices, List<Primitive> primitives)
    {
		Primitive primitive = primitives[triIndex];
		//primitive.triangleOffset 
		Vector3 v0 = vertices[primitive.triIndices.x].position;
		Vector3 v1 = vertices[primitive.triIndices.y].position;
		Vector3 v2 = vertices[primitive.triIndices.z].position;

		Matrix4x4 matrix = new Matrix4x4();
		Vector4 col0 = v0 - v2;
		col0.w = 0;
		matrix.SetColumn(0, col0);
		Vector4 col1 = v1 - v2;
		col1.w = 0;
		matrix.SetColumn(1, col1);
		Vector4 col2 = Vector3.Cross(v0 - v2, v1 - v2);
		col2.w = 0;
		matrix.SetColumn(2, col2);
		Vector4 col3 = v2;
		col3.w = 1;
		matrix.SetColumn(3, col3);
		matrix = Matrix4x4.Inverse(matrix);

		m_woop[0] = matrix.GetRow(0);
		m_woop[1] = matrix.GetRow(1);
		m_woop[2] = matrix.GetRow(2);

		//Vector3 normal = Vector3.Cross(m_woop[0], m_woop[1]);
		//Vector3 normal2 = Vector3.Cross(col0, col1);
		//normal.Normalize();
		//normal2.Normalize();
	}

	Vector3 MinOrMax(GPUBVHNode box, int n)
	{
		return n == 0 ? new Vector3(box.b0xy.x, box.b0xy.z, box.b01z.x) : new Vector3(box.b0xy.y, box.b0xy.y, box.b01z.y);
	}
	bool RayBoundIntersect(Vector3 rayOrig, Vector4 bxy, Vector2 bz, float idirx, float idiry, float idirz, float rayTMax, out float tMin)
    {
		int signX = (int)Mathf.Sign(idirx);
		int signY = (int)Mathf.Sign(idiry);
		int signZ = (int)Mathf.Sign(idirz);
		signX = signX < 0 ? 1 : 0;
		signY = signY < 0 ? 1 : 0;
		signZ = signZ < 0 ? 1 : 0;

		tMin = (bxy[signX] - rayOrig.x) * idirx;
		float tMax = (bxy[1 - signX] - rayOrig.x) * idirx;
		float tyMin = (bxy[signY + 2] - rayOrig.y) * idiry;
		float tyMax = (bxy[1- signY + 2] - rayOrig.y) * idiry;

		if (tMin > tyMax || tyMin > tMax)
			return false;

		tMin = Mathf.Max(tMin, tyMin);

		tMax = Mathf.Min(tMax, tyMax);

		float tzMin = (bz[signZ] - rayOrig.z) * idirz;
		float tzMax = (bz[1 - signZ] - rayOrig.z) * idirz;

		if ((tMin > tzMax) || (tzMin > tMax))
			return false;
		tMin = Mathf.Max(tMin, tzMin);
		tMax = Mathf.Min(tMax, tzMax);

		if (rayTMax < tMin)
			return false;

		return true;
	}

	bool RayBoundIntersect(Vector3 rayOrig, Vector3 rayDirection, float tMin, float tMax, Vector3 invDir, Vector3 bMin, Vector3 bMax, out float hitT)
    {
        Vector3 f = (bMax - rayOrig);
        f.Scale(invDir);
        Vector3 n = (bMin - rayOrig);
        n.Scale(invDir);

        Vector3 tmax = Vector3.Max(f, n);
        Vector3 tmin = Vector3.Min(f, n);

        float t1 = Mathf.Min(Mathf.Min(tmax.x, Mathf.Min(tmax.y, tmax.z)), tMax);
        float t0 = Mathf.Max(Mathf.Max(tmin.x, Mathf.Max(tmin.y, tmin.z)), tMin);
        bool intersect = t0 < t1;
        if (intersect)
            hitT = t0;
        else
            hitT = 0;
        return intersect;
    }

	public Vector3 GetInverseDirection(Vector3 rayDir)
    {
		float ooeps = Mathf.Pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number

		float idirx = 1.0f / (Mathf.Abs(rayDir.x) > ooeps ? rayDir.x : Mathf.Sign(rayDir.x) * ooeps); // inverse ray direction
		float idiry = 1.0f / (Mathf.Abs(rayDir.y) > ooeps ? rayDir.y : Mathf.Sign(rayDir.y) * ooeps); // inverse ray direction
		float idirz = 1.0f / (Mathf.Abs(rayDir.z) > ooeps ? rayDir.z : Mathf.Sign(rayDir.z) * ooeps); // inverse ray direction
		Vector3 invDir = new Vector3(idirx, idiry, idirz);
		return invDir;
	}

	public bool IntersectMeshBVH(Vector4 rayOrig, Vector4 rayDir, int bvhOffset, out float hitT, out int hitIndex)
    {
		const int INVALID_INDEX = 0x76543210;

		//GPURay TempRay = new GPURay();
		int[] traversalStack = new int[64];
		traversalStack[0] = INVALID_INDEX;
		int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).

		//instBVHOffset >= m_nodes.Count˵��û��inst
		int nodeAddr = bvhOffset;

		float tmin = rayDir.w;
		hitT = rayOrig.w;

		Vector3 invDir = GetInverseDirection(rayDir);
		int stackIndex = 0;
		hitIndex = -1;

		while (nodeAddr != INVALID_INDEX)
		{
			while ((uint)nodeAddr < (uint)INVALID_INDEX)
			{
				GPUBVHNode curNode = m_nodes[nodeAddr];
				Vector4Int cnodes = SingleToInt32Bits(curNode.cids);

				//left child ray-bound intersection test
				float tMin = 0;
				bool traverseChild0 = RayBoundIntersect(rayOrig, curNode.b0xy, new Vector2(curNode.b01z.x, curNode.b01z.y), invDir.x, invDir.y, invDir.z, hitT, out tMin);

				//right child ray-bound intersection test
				float tMin1 = 0;
				bool traverseChild1 = RayBoundIntersect(rayOrig, curNode.b1xy, new Vector2(curNode.b01z.z, curNode.b01z.w), invDir.x, invDir.y, invDir.z, hitT, out tMin1);


				bool swp = (tMin1 < tMin);

				tmin = Mathf.Min(tMin1, tMin);

				if (!traverseChild0 && !traverseChild1)
				{
					nodeAddr = traversalStack[stackIndex];
					stackIndex--;
				}
				// Otherwise => fetch child pointers.
				else
				{
					nodeAddr = (traverseChild0) ? cnodes.x : cnodes.y;
					//primitivesNum = (traverseChild0) ? cnodes.z : cnodes.w;
					//primitivesNum2 = (traverseChild0) ? cnodes.w : cnodes.z;
					//if (!swp)
					//	swp = primitivesNum2 > 0 && primitivesNum == 0;
					// Both children were intersected => push the farther one.
					if (traverseChild0 && traverseChild1)
					{
						if (swp)
						{
							//swap(nodeAddr, cnodes.y);
							int tmp = nodeAddr;
							nodeAddr = cnodes.y;
							cnodes.y = tmp;
							//tmp = primitivesNum;
							//primitivesNum = primitivesNum2;
							//primitivesNum2 = tmp;
						}
						//stackPtr += 4;
						//stackIndex++;
						//*(int*)stackPtr = cnodes.y;
						traversalStack[++stackIndex] = cnodes.y;
					}
				}

				// First leaf => postpone and continue traversal.
				if (nodeAddr < 0 && leafAddr >= 0)     // Postpone max 1
				{
					//leafAddr2= leafAddr;          // postpone 2
					leafAddr = nodeAddr;            //leafAddr���˵�ǰҪ����Ľڵ�
					nodeAddr = traversalStack[stackIndex];  //��ջ��nodeAddr���ʱ������һ��Ҫ���ʵ�node
					stackIndex--;
				}

				if (!(leafAddr >= 0))   //leaf nodeС��0����Ҫ����Ҷ�ӽڵ㣬�˳�ѭ��
					break;
			}

			//����Ҷ��
			while (leafAddr < 0)
			{
				for (int triAddr = ~leafAddr; /*triAddr < ~leafAddr + primitivesNum * 3*/; triAddr += 3)
				{
					Vector4 m0 = m_woodTriangleVertices[triAddr];     //matrix row 0 

					if (SingleToInt32Bits(m0.x) == 0x7fffffff)
						break;

					Vector4 m1 = m_woodTriangleVertices[triAddr + 1]; //matrix row 1 
					Vector4 m2 = m_woodTriangleVertices[triAddr + 2]; //matrix row 2

					//Oz is a point, must plus w
					float Oz = m2.w + Vector3.Dot(rayOrig, m2);//origx * m2.x + origy * m2.y + origz * m2.z;
															   //Dz is a vector
					float invDz = 1.0f / Vector3.Dot(rayDir, m2);//(dirx * m2.x + diry * m2.y + dirz * m2.z);
					float t = -Oz * invDz;

					Vector3 normal = Vector3.Cross(m0, m1).normalized;
					if (Vector3.Dot(normal, rayDir) >= 0)
					{
						//RenderDebug.DrawNormal(m_worldVertices[triAddr], m_worldVertices[triAddr + 1], m_worldVertices[triAddr + 2], 0.3f, 0.35f);
						continue;
					}

					//if t is in bounding and less than the ray.tMax
					if (/*t >= tmin && */t < hitT)
					{
						// Compute and check barycentric u.
						float Ox = m0.w + Vector3.Dot(rayOrig, m0);//origx * m0.x + origy * m0.y + origz * m0.z;
						float Dx = Vector3.Dot(rayDir, m0); //dirx * m0.x + diry * m0.y + dirz * m0.z;
						float u = Ox + t * Dx;

						if (u >= 0.0f)
						{
							// Compute and check barycentric v.
							float Oy = m1.w + Vector3.Dot(rayOrig, m1);//origx * m1.x + origy * m1.y + origz * m1.z;
							float Dy = Vector3.Dot(rayDir, m1);//dirx * m1.x + diry * m1.y + dirz * m1.z;
							float v = Oy + t * Dy;

							if (v >= 0.0f && u + v <= 1.0f)
							{
								// Record intersection.
								// Closest intersection not required => terminate.
								hitT = t;
								hitIndex = triAddr;
								//return true;
							}
						}
					}

				} // triangle

				// Another leaf was postponed => process it as well.
				leafAddr = nodeAddr;
				if (nodeAddr < 0)
				{
					nodeAddr = traversalStack[stackIndex--];
					//primitivesNum = primitivesNum2;
				}
			} // leaf
		}

		//tRay = hitT;
		//return false;
		if (hitIndex != -1)
		{
			//RenderDebug.DrawTriangle(m_worldVertices[hitIndex], m_worldVertices[hitIndex + 1], m_worldVertices[hitIndex + 2], Color.green);

		}
		return hitIndex != -1;
	}


	public bool IntersectInstTest(GPURay ray, List<MeshInstance> meshInstances, List<MeshHandle> meshHandles, int instBVHOffset)
    {
		const int INVALID_INDEX = 0x76543210;

		int[] traversalStack = new int[64];
		traversalStack[0] = INVALID_INDEX;

		//instBVHOffset >= m_nodes.Count˵��û��inst
		int nodeAddr = instBVHOffset >= m_nodes.Count ? 0 : instBVHOffset;

		float tmin = ray.direction.w;
		float hitT = ray.orig.w;  //tmax
           // Ray origin.
		//float ooeps = Mathf.Pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number

		//Vector3 invDir = new Vector3(idirx, idiry, idirz);
		Vector3 invDir = GetInverseDirection(ray.direction);
		Vector3 invWorldDir = invDir;

		int stackIndex = 0;   //��ǰtraversalStack��������
		MeshInstance meshInstance = new MeshInstance();
		//MeshInstance meshInstanceDebug = new MeshInstance();
		//signX = signX < 0 ? 1 : 0;
		//signY = signY < 0 ? 1 : 0;
		//signZ = signZ < 0 ? 1 : 0;

		//int meshInstanceIndex = 0;
		int hitIndex = -1;
		int hitBVHNode = -1;
		int hitMeshIndex = -1;

		//ֻ��һ��mesh��ʱ��
		if (nodeAddr == 0)
        {
			meshInstance = meshInstances[0];
			GPURay rayTemp = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);
			//invDir = GetInverseDirection(ray.direction);
			float bvhHit = hitT;
			int meshHitTriangleIndex = -1;
			if (IntersectMeshBVH(rayTemp.orig, rayTemp.direction, 0, out bvhHit, out meshHitTriangleIndex))
			{
				hitMeshIndex = 0;
				if (bvhHit < hitT)
				{
					hitBVHNode = 0;
					hitT = bvhHit;
					hitIndex = meshHitTriangleIndex;
				}
			}
		}
		else
        {
			while (nodeAddr != INVALID_INDEX)
			{

				GPUBVHNode curNode = m_nodes[nodeAddr];
				Vector4Int cnodes = SingleToInt32Bits(curNode.cids);

				float t0 = 0;
				//left child ray-bound intersection test
				//bool traverseChild0 = RayBoundIntersect(new Vector3(ray.orig.x, ray.orig.y, ray.orig.z), new Vector3(ray.direction.x, ray.direction.y, ray.direction.z),
				//	ray.direction.w, ray.orig.w, invDir,
				//	new Vector3(curNode.b0xy.x, curNode.b0xy.z, curNode.b01z.x), 
				//	new Vector3(curNode.b0xy.z, curNode.b0xy.w, curNode.b01z.y), out t0);
				bool traverseChild0 = RayBoundIntersect(ray.orig, curNode.b0xy, new Vector2(curNode.b01z.x, curNode.b01z.y), invDir.x, invDir.y, invDir.z, hitT, out t0);

				float t1 = 0;
				//right child ray-bound intersection test
				//bool traverseChild1 = RayBoundIntersect(new Vector3(ray.orig.x, ray.orig.y, ray.orig.z), new Vector3(ray.direction.x, ray.direction.y, ray.direction.z),
				//	ray.direction.w, ray.orig.w, invDir,
				//	new Vector3(curNode.b1xy.x, curNode.b1xy.z, curNode.b01z.z),
				//	new Vector3(curNode.b1xy.z, curNode.b1xy.w, curNode.b01z.w), out t1);
				bool traverseChild1 = RayBoundIntersect(ray.orig, curNode.b1xy, new Vector2(curNode.b01z.z, curNode.b01z.w), invDir.x, invDir.y, invDir.z, hitT, out t1);

				Vector2Int next = GetNodeChildIndex(curNode.cids); //new Vector2Int(INVALID_INDEX, INVALID_INDEX);
				Vector2Int nextMeshInstanceIds = nodeAddr >= instBVHOffset ? GetTopLevelLeaveMeshInstance(curNode.cids) : new Vector2Int(-1, -1);
				if (!traverseChild0)
				{
					next.x = INVALID_INDEX;
					nextMeshInstanceIds.x = -1;
				}
				if (!traverseChild1)
				{
					next.y = INVALID_INDEX;
					nextMeshInstanceIds.y = -1;
				}

				bool swp = false;
				//3 cases after boundrayintersect
				if (traverseChild0 && traverseChild1)
				{
					//����������
					swp = (t1 < t0);
					if (swp)
					{
						next = new Vector2Int(next.y, next.x);
						nextMeshInstanceIds = new Vector2Int(nextMeshInstanceIds.y, nextMeshInstanceIds.x);
					}

					//next.y��ջ
					bool curNodeIsX = false;
					if (next.x >= instBVHNodeAddr)
					{
						nodeAddr = next.x;
						curNodeIsX = true;
					}
					else
					{
						if (next.y >= instBVHNodeAddr)
							nodeAddr = next.y;
						else
							nodeAddr = traversalStack[stackIndex--];
					}
					//������ջ�Ŀ�����bottomlevel bvh leaf
					if (next.y >= instBVHNodeAddr && curNodeIsX)
						traversalStack[++stackIndex] = next.y;
					//if (nodeAddr >= instBVHOffset)
					//            {
					//	meshInstanceIndex = nextMeshInstanceIds.x;
					//	meshInstanceStack[stackIndex] = nextMeshInstanceIds.y;
					//}

					//if (0 <= next.y && YieldInstruction < )
				}
				else if (!traverseChild0 && !traverseChild1)
				{
					//������������
					//meshInstanceIndex = nodeAddr >= instBVHOffset ? meshInstanceStack[stackIndex + 1] : meshInstanceIndex;
					nodeAddr = traversalStack[stackIndex--];

				}
				else
				{
					//ֻ������һ������
					if (nodeAddr >= instBVHOffset)
					{
						//meshInstanceIndex = traverseChild0 ? nextMeshInstanceIds.x : nextMeshInstanceIds.y;
						int nextNode = traverseChild0 ? next.x : next.y;
						if (nextNode >= instBVHNodeAddr)
						{
							nodeAddr = nextNode;
						}
						else
							nodeAddr = traversalStack[stackIndex--];
					}
				}


				for (int i = 0; i < 2; ++i)
				{
					//���next��bottom level bvh node
					if (0 <= next[i] && next[i] < instBVHNodeAddr)
					{
						meshInstance = meshInstances[nextMeshInstanceIds[i]];
						GPURay rayTemp = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);
						//invDir = GetInverseDirection(ray.direction);
						float bvhHit = hitT;
						int meshHitTriangleIndex = -1;
						if (IntersectMeshBVH(rayTemp.orig, rayTemp.direction, next[i], out bvhHit, out meshHitTriangleIndex))
						{
							if (bvhHit < hitT)
							{
								hitT = bvhHit;
								hitIndex = meshHitTriangleIndex;
								hitBVHNode = next[i];
								hitMeshIndex = nextMeshInstanceIds[i];
							}
						}
						else if (nextMeshInstanceIds[i] == 1)
						{
							//Debug.Log("error happen!");
							int a = 0;
						}
					}
				}
			}
		}
		
		

		//for (int i = 0; i < meshInstances.Count; ++i)
		if (hitBVHNode >= 0)
        {
            MeshInstance meshInstanceTmp = meshInstances[hitMeshIndex];
            MeshHandle meshHandle = meshHandles[meshInstanceTmp.meshHandleIndex];
            Vector3 worldBoundMin = Vector3.zero;
            Vector3 worldBoundMax = Vector3.zero;
            GPUBounds.TransformBounds(ref meshInstanceTmp.localToWorld, meshHandle.localBounds.min, meshHandle.localBounds.max, out worldBoundMin, out worldBoundMax);
            RenderDebug.DrawDebugBound(worldBoundMin, worldBoundMax, Color.white);

            if (hitIndex != -1)
			{
				int triAddrDebug = hitIndex;
				RenderDebug.DrawTriangle(meshInstanceTmp.localToWorld.MultiplyPoint(sceneVertices[m_woodTriangleIndices[triAddrDebug]].position),
					meshInstanceTmp.localToWorld.MultiplyPoint(sceneVertices[m_woodTriangleIndices[triAddrDebug + 1]].position),
					meshInstanceTmp.localToWorld.MultiplyPoint(sceneVertices[m_woodTriangleIndices[triAddrDebug + 2]].position), Color.green);
			}
		}
		

		return hitIndex != -1;
	}
	public bool IntersectBVHTriangleTest(GPURay ray, int bvhOffset, out float tRay)
    {
		const int EntrypointSentinel = 0x76543210;
		Vector4 rayOrig = ray.orig;
		Vector4 rayDir = ray.direction;
		int[] traversalStack = new int[64];
		traversalStack[0] = EntrypointSentinel;
		int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).
		int nodeAddr = bvhOffset;
		//int primitivesNum = 0;   //��ǰ�ڵ��primitives����
		//int primitivesNum2 = 0;
		int triIdx = 0;
		float tmin = rayDir.w;
		float hitT = rayOrig.w;  //tmax
		float origx = rayOrig.x;
		float origy = rayOrig.y;
		float origz = rayOrig.z;            // Ray origin.
		float ooeps = Mathf.Pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number
		float idirx = 1.0f / (Mathf.Abs(rayDir.x) > ooeps ? rayDir.x : Mathf.Sign(rayDir.x) * ooeps); // inverse ray direction
		float idiry = 1.0f / (Mathf.Abs(rayDir.y) > ooeps ? rayDir.y : Mathf.Sign(rayDir.y) * ooeps); // inverse ray direction
		float idirz = 1.0f / (Mathf.Abs(rayDir.z) > ooeps ? rayDir.z : Mathf.Sign(rayDir.z) * ooeps); // inverse ray direction
		int signX = (int)Mathf.Sign(idirx);
		int signY = (int)Mathf.Sign(idiry);
		int signZ = (int)Mathf.Sign(idirz);
		signX = signX < 0 ? 1 : 0;
		signY = signY < 0 ? 1 : 0;
		signZ = signZ < 0 ? 1 : 0;

		float dirx = rayDir.x;
		float diry = rayDir.y;
		float dirz = rayDir.z;
		float oodx = rayOrig.x * idirx;  // ray origin / ray direction
		float oody = rayOrig.y * idiry;  // ray origin / ray direction
		float oodz = rayOrig.z * idirz;  // ray origin / ray direction
		int stackIndex = 0;   //��ǰtraversalStack��������
		int hitIndex = -1;

		//���nodeAddr����������
		while (nodeAddr != EntrypointSentinel)
		{
			while ((uint)nodeAddr < (uint)EntrypointSentinel)
			{
				GPUBVHNode curNode = m_nodes[nodeAddr];
				Vector4Int cnodes = SingleToInt32Bits(curNode.cids);

				//left child ray-bound intersection test
				float tMin = 0;
				bool traverseChild0 = RayBoundIntersect(rayOrig, curNode.b0xy, new Vector2(curNode.b01z.x, curNode.b01z.y), idirx, idiry, idirz, hitT, out tMin);

				//right child ray-bound intersection test
				float tMin1 = 0;
				bool traverseChild1 = RayBoundIntersect(rayOrig, curNode.b1xy, new Vector2(curNode.b01z.z, curNode.b01z.w), idirx, idiry, idirz, hitT, out tMin1);


				bool swp = (tMin1 < tMin);

				tmin = Mathf.Min(tMin1, tMin);

				if (!traverseChild0 && !traverseChild1)
				{
					nodeAddr = traversalStack[stackIndex];
					stackIndex--;
				}
				// Otherwise => fetch child pointers.
				else
				{
					nodeAddr = (traverseChild0) ? cnodes.x : cnodes.y;
					//primitivesNum = (traverseChild0) ? cnodes.z : cnodes.w;
					//primitivesNum2 = (traverseChild0) ? cnodes.w : cnodes.z;
					//if (!swp)
					//	swp = primitivesNum2 > 0 && primitivesNum == 0;
					// Both children were intersected => push the farther one.
					if (traverseChild0 && traverseChild1)
					{
						if (swp)
						{
							//swap(nodeAddr, cnodes.y);
							int tmp = nodeAddr;
							nodeAddr = cnodes.y;
							cnodes.y = tmp;
							//tmp = primitivesNum;
							//primitivesNum = primitivesNum2;
							//primitivesNum2 = tmp;
						}
						//stackPtr += 4;
						//stackIndex++;
						//*(int*)stackPtr = cnodes.y;
						traversalStack[++stackIndex] = cnodes.y;
					}
				}

				// First leaf => postpone and continue traversal.
				if (nodeAddr < 0 && leafAddr >= 0)     // Postpone max 1
				{
					//leafAddr2= leafAddr;          // postpone 2
					leafAddr = nodeAddr;            //leafAddr���˵�ǰҪ����Ľڵ�
					nodeAddr = traversalStack[stackIndex];  //��ջ��nodeAddr���ʱ������һ��Ҫ���ʵ�node
					stackIndex--;
				}

				if (!(leafAddr >= 0))   //leaf nodeС��0����Ҫ����Ҷ�ӽڵ㣬�˳�ѭ��
					break;
			}

			//����Ҷ��
			while (leafAddr < 0)
			{
				for (int triAddr = ~leafAddr; /*triAddr < ~leafAddr + primitivesNum * 3*/; triAddr += 3)
				{
					Vector4 m0 = m_woodTriangleVertices[triAddr];     //matrix row 0 

					if (SingleToInt32Bits(m0.x) == 0x7fffffff)
						break;

					Vector4 m1 = m_woodTriangleVertices[triAddr + 1]; //matrix row 1 
					Vector4 m2 = m_woodTriangleVertices[triAddr + 2]; //matrix row 2

					//Oz is a point, must plus w
					float Oz = m2.w + origx * m2.x + origy * m2.y + origz * m2.z;
					//Dz is a vector
					float invDz = 1.0f / (dirx * m2.x + diry * m2.y + dirz * m2.z);
					float t = -Oz * invDz;

					Vector3 normal = Vector3.Cross(m0, m1).normalized;
					if (Vector3.Dot(normal, rayDir) >= 0)
                    {
						//RenderDebug.DrawNormal(m_worldVertices[triAddr], m_worldVertices[triAddr + 1], m_worldVertices[triAddr + 2], 0.3f, 0.35f);
						continue;
                    }

					//if t is in bounding and less than the ray.tMax
					if (t > tmin && t < hitT)
					{
						// Compute and check barycentric u.
						float Ox = m0.w + origx * m0.x + origy * m0.y + origz * m0.z;
						float Dx = dirx * m0.x + diry * m0.y + dirz * m0.z;
						float u = Ox + t * Dx;

						if (u >= 0.0f)
						{
							// Compute and check barycentric v.
							float Oy = m1.w + origx * m1.x + origy * m1.y + origz * m1.z;
							float Dy = dirx * m1.x + diry * m1.y + dirz * m1.z;
							float v = Oy + t * Dy;

							if (v >= 0.0f && u + v <= 1.0f)
							{
								// Record intersection.
								// Closest intersection not required => terminate.
								hitT = t;
								hitIndex = triAddr;
								//return true;
							}
						}
					}

				} // triangle

				// Another leaf was postponed => process it as well.
				leafAddr = nodeAddr;
				if (nodeAddr < 0)
				{
					nodeAddr = traversalStack[stackIndex--];
					//primitivesNum = primitivesNum2;
				}
			} // leaf
		}

		tRay = hitT;
		//return false;
		if (hitIndex != -1)
        {
			RenderDebug.DrawTriangle(sceneVertices[m_woodTriangleIndices[hitIndex]].position, 
				sceneVertices[m_woodTriangleIndices[hitIndex + 1]].position, 
				sceneVertices[m_woodTriangleIndices[hitIndex + 2]].position, Color.green);

		}
		return hitIndex != -1;
	}

	//build 2 types bvh
	//meshinstance bvh and mesh primitive bvh
	//return toplevel bvh��bvhbuffer�е�λ��
	public int Build(List<Transform> meshTransforms, List<MeshInstance> meshInstances, List<MeshHandle> meshHandles, List<GPUVertex> vertices, List<int> triangles)
    {
		instBVHNodeAddr = 0;
		List<int> instBVHOffset = new List<int>();

		//���ȴ���ÿ��meshHandle��localspace��bvh
		sceneVertices = vertices;

		for (int i = 0; i < meshHandles.Count; ++i)
		{
			MeshHandle meshH = meshHandles[i];
			meshH.bvhOffset = instBVHNodeAddr;
			meshHandles[i] = meshH;
			instBVHOffset.Add(instBVHNodeAddr);
			int nodesNum = BuildMeshBVH(meshHandles[i], vertices, triangles);
			instBVHNodeAddr = nodesNum;
		}
		// build the instance bounding box bvh
		BuildInstBVH(meshTransforms, meshInstances, vertices, triangles, instBVHOffset);

		MeshInstance meshInstance = meshInstances[0];
		MeshHandle meshHandle = meshHandles[meshInstance.meshHandleIndex];
		meshInstance.bvhOffset = meshHandle.bvhOffset;
		Vector3 worldBoundMin = Vector3.zero;
		Vector3 worldBoundMax = Vector3.zero;
		GPUBounds.TransformBounds(ref meshInstance.localToWorld, meshHandle.localBounds.min, meshHandle.localBounds.max, out worldBoundMin, out worldBoundMax);
		RenderDebug.DrawDebugBound(worldBoundMin, worldBoundMax, Color.white);

		return instBVHNodeAddr;
	}

	public void BuildInstBVH(List<Transform> meshTransforms, List<MeshInstance> meshInstances, List<GPUVertex> vertices, List<int> triangles, List<int> instBVHOffset)
    {
		List<Primitive> primitives = new List<Primitive>();
		for (int i = 0; i < meshTransforms.Count; ++i)
		{
			MeshInstance meshInst = meshInstances[i];
			int meshHandleIndex = meshInst.meshHandleIndex;
			Renderer renderer = meshTransforms[i].GetComponent<Renderer>();
			Primitive meshInstPrim = new Primitive(renderer.bounds, meshHandleIndex, i);
			primitives.Add(meshInstPrim);
		}
		List<Primitive> orderedPrims = new List<Primitive>();
		//instance����Ҫ��split��
		BVHBuilder instBuilder = new BVHBuilder();
		//��֤һ��leafһ��inst������maxPrimsInNode������1
		if (primitives.Count > 1)
        {
			BVHBuildNode instRoot = instBuilder.Build(primitives, orderedPrims, null, null, 1);
			primitives = orderedPrims;

			CreateCompact(instRoot, primitives, vertices, false, instBVHOffset);
		}
		
	}

	//����m_nodes������
	public int BuildMeshBVH(MeshHandle meshHandle, List<GPUVertex> vertices, List<int> triangles)
    {
		
		//����MeshHandle��primitives
		List<Primitive> primitives = new List<Primitive>();
		int faceNum = meshHandle.triangleCount / 3;
		for (int f = 0; f < faceNum; ++f)
		{
			int tri0 = triangles[f * 3 + meshHandle.triangleOffset];
			int tri1 = triangles[f * 3 + 1 + meshHandle.triangleOffset];
			int tri2 = triangles[f * 3 + 2 + meshHandle.triangleOffset];
			primitives.Add(new Primitive(tri0, tri1, tri2, vertices[tri0].position, vertices[tri1].position, vertices[tri2].position, 0, 0));
		}
		List<Primitive> orderedPrims = new List<Primitive>();
		BVHBuilder builder = new SplitBVHBuilder();
		BVHBuildNode meshRoot = builder.Build(primitives, orderedPrims, vertices, triangles, primitives.Count < 3 ? 1 : maxLeafSize);
		//CreateCompact���ɵ�m_nodes����ֻ��inner node�����ݣ��������ﷵ��m_nodes.Count
		CreateCompact(meshRoot, primitives, vertices, true);
		return m_nodes.Count;
	}
}
