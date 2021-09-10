using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Reference
{
    public int triIdx;
    public GPUBounds bounds;
    public Reference()
    {
        triIdx = -1;
        bounds = GPUBounds.DefaultBounds();
    }
};

//BVH树的节点描述
class NodeSpec
{
    public int numRef;     //该节点包含的所有子节点的三角形数量
    public GPUBounds bounds;  //节点的AABB盒子

    public NodeSpec()
    {
        numRef = 0;
        bounds = GPUBounds.DefaultBounds();
    }
};

struct ObjectSplit
{
    public float sah;
    public int   sortDim;
    public int   numLeft;
    public GPUBounds leftBounds;
    public GPUBounds rightBounds;

    public static ObjectSplit Default()
    {
        ObjectSplit objectSplit = new ObjectSplit();
        objectSplit.sah = float.MaxValue;
        objectSplit.sortDim = 0;
        objectSplit.leftBounds = GPUBounds.DefaultBounds();
        objectSplit.rightBounds = GPUBounds.DefaultBounds();
        return objectSplit;
    }
};

struct SpatialSplit
{
    public float sah;
    public int   dim;
    public float pos;

    public static SpatialSplit Default()
    {
        SpatialSplit spatialSplit = new SpatialSplit();
        spatialSplit.sah = float.MaxValue;
        spatialSplit.dim = 0;
        spatialSplit.pos = 0;
        return spatialSplit;
    }
};
class SpatialBin
{
    public GPUBounds bounds;
    public int enter;
    public int exit;

    public SpatialBin()
    {
        bounds = GPUBounds.DefaultBounds();
        enter = 0;
        exit = 0;
    }
};

/*
public class ReferenceCompair : IComparer<Reference>
{
    int sortDim;
    public ReferenceCompair(int dim)
    {
        sortDim = dim;
    }
    
    public int Compare(Reference ra, Reference rb)
    {
        if (ra == null)
        {
            if (rb == null)
            {
                // If x is null and y is null, they're
                // equal.
                return 0;
            }
            else
            {
                // If x is null and y is not null, y
                // is greater.
                return -1;
            }
        }
        else
        {
            if (rb == null)
            {
                return 1;
            }
            float ca = ra.bounds.min[sortDim] + ra.bounds.max[sortDim];
            float cb = rb.bounds.min[sortDim] + rb.bounds.max[sortDim];
            if (ca < cb || (ca == cb && ra.triIdx < rb.triIdx))
            {
                return -1;
            }
            else if (ca == cb && ra.triIdx == rb.triIdx)
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }
    }
}
*/

public class SplitBVHBuilder : BVHBuilder
{
    //private readonly int maxLevel = 64;
    float m_minOverlap = 0;   //划分空间的最小面积，意思是大于该面积，空间才可继续划分
    float m_splitAlpha = 1.0e-5f;   //what's this mean?
    int m_numDuplicates = 0;   //重复在多个节点上的三角形数量
    List<BVHPrimitiveInfo> primitiveInfos = new List<BVHPrimitiveInfo>();
    List<int> triangles = new List<int>();
    List<Reference> m_refStack = new List<Reference>();
    GPUBounds[] m_rightBounds = null;
    int m_sortDim;
    readonly int MaxDepth = 64;
    int MaxSpatialDepth = 48;
    readonly static int NumSpatialBins = 128;

    SpatialBin[,] m_bins = new SpatialBin[3, NumSpatialBins];

    //这个是整个场景的顶点和索引的引用List，不能释放掉
    List<int> _triangles;
    List<GPUVertex> _vertices;
    List<Primitive> _primitives;
    List<Primitive> _orderedPrimitives;
    Vector3 ClampVector3(float x, float y, float z, float min, float max)
    {
        return new Vector3(Mathf.Clamp(x, min, max), Mathf.Clamp(y, min, max), Mathf.Clamp(z, min, max));
    }

    Vector3 ClampVector3(Vector3 v, float min, float max)
    {
        return new Vector3(Mathf.Clamp(v.x, min, max), Mathf.Clamp(v.y, min, max), Mathf.Clamp(v.z, min, max));
    }

    Vector3Int ClampVector3Int(int x, int y, int z, int min, int max)
    {
        return new Vector3Int(Mathf.Clamp(x, min, max), Mathf.Clamp(y, min, max), Mathf.Clamp(z, min, max));
    }

    Vector3Int ClampVector3Int(Vector3Int v, int min, int max)
    {
        return new Vector3Int(Mathf.Clamp(v.x, min, max), Mathf.Clamp(v.y, min, max), Mathf.Clamp(v.z, min, max));
    }

    Vector3Int ClampVector3Int(Vector3Int v, Vector3Int min, Vector3Int max)
    {
        return new Vector3Int(Mathf.Clamp(v.x, min.x, max.x), Mathf.Clamp(v.y, min.y, max.y), Mathf.Clamp(v.z, min.z, max.z));
    }

    bool ReferenceCompare(List<Reference> references, int a, int b)
    {
        Reference ra = references[a];
        Reference rb = references[b];
        float ca = ra.bounds.min[m_sortDim] + ra.bounds.max[m_sortDim];
        float cb = rb.bounds.min[m_sortDim] + rb.bounds.max[m_sortDim];
        return (ca < cb || (ca == cb && ra.triIdx < rb.triIdx));
    }

    void SortSwap(List<Reference> references, int a, int b)
    {
        Reference tmp = references[a];
        references[a] = references[b];
        references[b] = tmp;
    }
    public override BVHBuildNode Build(List<Primitive> prims, List<Primitive> orderedPrims, List<GPUVertex> vertices, List<int> triangles, int _maxPrimsInNode = 4)
    {
        _triangles = triangles;
        _vertices = vertices;
        _primitives = prims;
        _orderedPrimitives = orderedPrims;
        maxPrimsInNode = _maxPrimsInNode;
        NodeSpec root = new NodeSpec();

        for (int i = 0; i < prims.Count; ++i)
        {
            primitiveInfos.Add(new BVHPrimitiveInfo(i, GPUBounds.ConvertUnityBounds(prims[i].worldBound)));
            Reference reference = new Reference();
            reference.triIdx = i;  //reference's triIdx is the index in _primitives
            reference.bounds = GPUBounds.ConvertUnityBounds(prims[i].worldBound);
            m_refStack.Add(reference);
        }
        for (int i = 0; i < prims.Count; ++i)
        {
            root.bounds = GPUBounds.Union(root.bounds, primitiveInfos[i].worldBound);
        }
        root.numRef = prims.Count;

        m_rightBounds = new GPUBounds[Mathf.Max(root.numRef, NumSpatialBins) - 1];
        m_minOverlap = root.bounds.SurfaceArea() * m_splitAlpha;

        return RecursiveBuild(root, 0, 0, 1.0f);
    }

    BVHBuildNode RecursiveBuild(NodeSpec spec, int level, float progressStart, float progressEnd)
    {
        totalNodes++;
        // Remove degenerates.
        //把无效的boundingbox去掉，例如线和带负数的
         {
            int firstRef = m_refStack.Count - spec.numRef;
            for (int i = m_refStack.Count - 1; i >= firstRef; i--)
            {
                if (i >= m_refStack.Count || i < 0)
                {
                    Debug.LogError("Remove degenerates error!");
                }
                Vector3 size = m_refStack[i].bounds.Diagonal;
                //removes the negetive size and the line bounding
                if (m_refStack[i].bounds.MinSize() < 0.0f || (size.x + size.y + size.z) == m_refStack[i].bounds.MaxSize())
                {
                    m_refStack[i] = m_refStack[m_refStack.Count - 1];
                    m_refStack.RemoveAt(m_refStack.Count - 1);
                }
            }
            spec.numRef = m_refStack.Count - firstRef;
        }

        if (spec.numRef <= maxPrimsInNode || level >= MaxDepth)
            return CreateLeaf(spec);

        //find the split candidate
        //判断split space和split object的依据是？
        float area = spec.bounds.SurfaceArea();
        float leafSAH = area * GetTriangleCost(spec.numRef);
        //这里是因为2个子节点？
        float nodeSAH = area * GetNodeCost(2);
        ObjectSplit objectSplit = FindObjectSplit(spec, nodeSAH);

        SpatialSplit spatial = SpatialSplit.Default();
        if (level < MaxSpatialDepth)
        {
            GPUBounds overlap = objectSplit.leftBounds;
            overlap.Intersect(objectSplit.rightBounds);
            //由于object划分会产生overlap的区域，当overlap的区域＞minOverlap的时候，需要划分spatial split
            if (overlap.SurfaceArea() >= m_minOverlap)
                spatial = FindSpatialSplit(spec, nodeSAH);
        }

        BVHBuildNode node = new BVHBuildNode();
        float minSAH = Mathf.Min(Mathf.Min(leafSAH, objectSplit.sah), spatial.sah);
        if (minSAH == leafSAH && spec.numRef <= maxPrimsInNode)
        {
            //for (int i = 0; i < spec.numRef; i++)
            //{
            //    //tris.add(m_refStack.removeLast().triIdx);
            //    Reference last = m_refStack[m_refStack.Count - 1];
            //    m_refStack.RemoveAt(m_refStack.Count - 1);
            //    _orderedPrimitives.Add(_primitives[last.triIdx]);
            //}
            //node.InitLeaf(_orderedPrimitives.Count - spec.numRef, spec.numRef, spec.bounds);
            return CreateLeaf(spec);
        }

        NodeSpec left = new NodeSpec();
        NodeSpec right = new NodeSpec();
        if (minSAH == spatial.sah)
            PerformSpatialSplit(left, right, spec, spatial);
        if (left.numRef == 0 || right.numRef == 0)
            PerformObjectSplit(left, right, spec, objectSplit);

        m_numDuplicates += left.numRef + right.numRef - spec.numRef;
        float progressMid = Mathf.Lerp(progressStart, progressEnd, (float)right.numRef / (float)(left.numRef + right.numRef));
        BVHBuildNode rightNode = RecursiveBuild(right, level + 1, progressStart, progressMid);
        BVHBuildNode leftNode = RecursiveBuild(left, level + 1, progressMid, progressEnd);
        BVHBuildNode innerNode =  BVHBuildNode.CreateInnerNode(spec.bounds, leftNode, rightNode);
        return innerNode;
    }

    ObjectSplit FindObjectSplit(NodeSpec spec, float nodeSAH)
    {
        ObjectSplit split = ObjectSplit.Default();
        //const Reference* refPtr = m_refStack.getPtr(m_refStack.getSize() - spec.numRef);
        int beginIndex = m_refStack.Count - spec.numRef;
        int count = spec.numRef;
        Reference refPtr = m_refStack[m_refStack.Count - spec.numRef];
        float bestTieBreak = float.MaxValue;

        // Sort along each dimension.

        for (m_sortDim = 0; m_sortDim < 3; m_sortDim++)
        {
            //sort(this, m_refStack.getSize() - spec.numRef, m_refStack.getSize(), sortCompare, sortSwap);

            //m_refStack.Sort(m_refStack.Count - spec.numRef, count, new ReferenceCompair(m_sortDim));
            BVHSort.Sort<Reference>(m_refStack.Count - spec.numRef, count, m_refStack, ReferenceCompare, SortSwap);
            // Sweep right to left and determine bounds.

            GPUBounds rightBounds = GPUBounds.DefaultBounds();
            for (int i = spec.numRef - 1; i > 0; i--)
            {
                rightBounds = GPUBounds.Union(m_refStack[beginIndex + i].bounds, rightBounds);
                m_rightBounds[i - 1] = rightBounds;
            }

            // Sweep left to right and select lowest SAH.

            GPUBounds leftBounds = GPUBounds.DefaultBounds();
            for (int i = 1; i < spec.numRef; i++)
            {
                leftBounds = GPUBounds.Union(m_refStack[beginIndex + i - 1].bounds, leftBounds);
                float sah = nodeSAH + leftBounds.SurfaceArea() * GetTriangleCost(i) + m_rightBounds[beginIndex + i - 1].SurfaceArea() * GetTriangleCost(spec.numRef - i);
                float tieBreak = ((float)i * (float)i) + ((float)(spec.numRef - i) * (float)(spec.numRef - i));
                if (sah < split.sah || (sah == split.sah && tieBreak < bestTieBreak))
                {
                    split.sah = sah;
                    split.sortDim = m_sortDim;
                    split.numLeft = i;
                    split.leftBounds = leftBounds;
                    split.rightBounds = m_rightBounds[i - 1];
                    bestTieBreak = tieBreak;
                }
            }
        }
        return split;
    }
    SpatialSplit FindSpatialSplit(NodeSpec spec, float nodeSAH)
    {
        // Initialize bins.
        Vector3 origin = spec.bounds.min;
        Vector3 binSize = (spec.bounds.max - origin) * (1.0f / (float)NumSpatialBins);
        Vector3 invBinSize = new Vector3(1.0f / binSize.x, 1.0f / binSize.y, 1.0f / binSize.z);

        for (int dim = 0; dim< 3; dim++)
        {
            for (int i = 0; i < NumSpatialBins; i++)
            {
                if (m_bins[dim, i] == null)
                {
                    m_bins[dim, i] = new SpatialBin();
                }
                SpatialBin bin = m_bins[dim, i];
                //bin.bounds = GPUBounds.DefaultBounds();
                //bin.enter = 0;
                //bin.exit = 0;
            }
        }

        // Chop references into bins.

        for (int refIdx = m_refStack.Count - spec.numRef; refIdx < m_refStack.Count; refIdx++)
        {
            Reference reference = m_refStack[refIdx];
            Vector3 minMinusOrig = reference.bounds.min - origin;
            Vector3 maxMinusOrig = reference.bounds.max - origin;
            
            Vector3Int firstBin = ClampVector3Int(Vector3Int.FloorToInt(new Vector3(minMinusOrig.x * invBinSize.x, minMinusOrig.y * invBinSize.y, minMinusOrig.z * invBinSize.z)),
                0, NumSpatialBins - 1);
            Vector3Int lastBin = ClampVector3Int(Vector3Int.FloorToInt(new Vector3(maxMinusOrig.x * invBinSize.x, maxMinusOrig.y * invBinSize.y, maxMinusOrig.z * invBinSize.z)), 
                firstBin, new Vector3Int(NumSpatialBins - 1, NumSpatialBins - 1, NumSpatialBins - 1));

            for (int dim = 0; dim< 3; dim++)
            {
                Reference currRef = reference;
                for (int i = firstBin[dim]; i<lastBin[dim]; i++)
                {
                    Reference leftRef = new Reference();
                    Reference rightRef = new Reference();
                    float splitPos = origin[dim] + binSize[dim] * (float)(i + 1);
                    SplitReference(leftRef, rightRef, currRef, dim, splitPos);
                    m_bins[dim, i].bounds = GPUBounds.Union(m_bins[dim, i].bounds, leftRef.bounds);
                    currRef = rightRef;
                }
                m_bins[dim, lastBin[dim]].bounds = GPUBounds.Union(m_bins[dim, lastBin[dim]].bounds, currRef.bounds);
                m_bins[dim, firstBin[dim]].enter++;
                m_bins[dim, lastBin[dim]].exit++;
            }
        }

        // Select best split plane.

        SpatialSplit split = SpatialSplit.Default();
        for (int dim = 0; dim< 3; dim++)
        {
            // Sweep right to left and determine bounds.

            GPUBounds rightBounds = GPUBounds.DefaultBounds();
            for (int i = NumSpatialBins - 1; i > 0; i--)
            {
                rightBounds = GPUBounds.Union(rightBounds, m_bins[dim, i].bounds);
                m_rightBounds[i - 1] = rightBounds;
            }

            // Sweep left to right and select lowest SAH.

            GPUBounds leftBounds = GPUBounds.DefaultBounds();
            int leftNum = 0;
            int rightNum = spec.numRef;

            for (int i = 1; i < NumSpatialBins; i++)
            {
                leftBounds = GPUBounds.Union(leftBounds, m_bins[dim, i - 1].bounds);
                leftNum += m_bins[dim, i - 1].enter;
                rightNum -= m_bins[dim, i - 1].exit;

                float sah = nodeSAH + leftBounds.SurfaceArea() * GetTriangleCost(leftNum) + m_rightBounds[i - 1].SurfaceArea() * GetTriangleCost(rightNum);
                if (sah < split.sah)
                {
                    split.sah = sah;
                    split.dim = dim;
                    split.pos = origin[dim] + binSize[dim] * (float) i;
                }
            }
        }
        return split;
    }

    //return the SAH ray triangle intersect cost
    float GetTriangleCost(int triangles)
    {
        //1.0表示一次求交的消耗
        return triangles * 1.0f;
    }

    //return the SAH ray node intersect cost
    float GetNodeCost(int nodes)
    {
        //1.0表示一次求交的消耗
        return nodes * 1.0f;
    }

    BVHBuildNode CreateLeaf(NodeSpec spec)
    {
        //List<int> tris = m_bvh.getTriIndices();
        for (int i = 0; i < spec.numRef; i++)
        {
            Reference last = m_refStack[m_refStack.Count - 1];
            m_refStack.RemoveAt(m_refStack.Count - 1);
            _orderedPrimitives.Add(_primitives[last.triIdx]);
        }
        BVHBuildNode leafNode = new BVHBuildNode();
        leafNode.InitLeaf(_orderedPrimitives.Count - spec.numRef, spec.numRef, spec.bounds);
        return leafNode;
        //return new BVHBuildNode(spec.bounds, tris.getSize() - spec.numRef, tris.getSize());
    }

    void SplitReference(Reference left, Reference right, Reference reference, int dim, float pos)
    {
        // Initialize references.

        left.triIdx = right.triIdx = reference.triIdx;
        left.bounds = right.bounds = GPUBounds.DefaultBounds();

        // Loop over vertices/edges.

        //const Vec3i* tris = (const Vec3i*)m_bvh.getScene()->getTriVtxIndexBuffer().getPtr();
        //const Vec3f* verts = (const Vec3f*)m_bvh.getScene()->getVtxPosBuffer().getPtr();
        //const Vec3i& inds = tris[ref.triIdx];
        //const Vec3f* v1 = &verts[inds.z];
        Primitive triangle = _primitives[reference.triIdx];
        //if (triangle.triangleOffset + 2 >= _triangles.Count)
        //{
        //    Debug.LogError("Triangle Out of range");
        //}
        //int triIndex = _triangles[triangle.triangleOffset + 2];
        Vector3 v1 = _vertices[triangle.triIndices.z].position;

        for (int i = 0; i < 3; i++)
        {
            Vector3 v0 = v1;
            //v1 = _positions[_triangles[triangle.triangleOffset + i]];
            v1 = _vertices[triangle.triIndices[i]].position;
            float v0p = v0[dim];
            float v1p = v1[dim];

            // Insert vertex to the boxes it belongs to.

            if (v0p <= pos)
                left.bounds = GPUBounds.Union(left.bounds, v0);
            if (v0p >= pos)
                right.bounds = GPUBounds.Union(right.bounds, v0);

            // Edge intersects the plane => insert intersection to both boxes.

            if ((v0p < pos && v1p > pos) || (v0p > pos && v1p < pos))
            {
                Vector3 t = Vector3.Lerp(v0, v1, Mathf.Clamp((pos - v0p) * (1 / (v1p - v0p)), 0.0f, 1.0f));
                left.bounds = GPUBounds.Union(left.bounds, t);
                right.bounds = GPUBounds.Union(right.bounds, t);
            }
        }

        // Intersect with original bounds.

        left.bounds.max[dim] = pos;
        right.bounds.min[dim] = pos;
        left.bounds.Intersect(reference.bounds);
        right.bounds.Intersect(reference.bounds);
    }

    void PerformObjectSplit(NodeSpec left, NodeSpec right, NodeSpec spec, ObjectSplit split)
    {
        m_sortDim = split.sortDim;
        int count = spec.numRef;
        //sort(this, m_refStack.getSize() - spec.numRef, m_refStack.getSize(), sortCompare, sortSwap);
        //m_refStack.Sort(m_refStack.Count - spec.numRef, count, new ReferenceCompair(m_sortDim));
        BVHSort.Sort(m_refStack.Count - spec.numRef, count, m_refStack, ReferenceCompare, SortSwap);

        left.numRef = split.numLeft;
        left.bounds = split.leftBounds;
        right.numRef = spec.numRef - split.numLeft;
        right.bounds = split.rightBounds;
    }

    void PerformSpatialSplit(NodeSpec left, NodeSpec right, NodeSpec spec, SpatialSplit split)
    {
        // Categorize references and compute bounds.
        //
        // Left-hand side:      [leftStart, leftEnd[
        // Uncategorized/split: [leftEnd, rightStart[
        // Right-hand side:     [rightStart, refs.getSize()[

        //Array<Reference>& refs = m_refStack;
        int leftStart = m_refStack.Count - spec.numRef;
        int leftEnd = leftStart;
        int rightStart = m_refStack.Count;
        left.bounds = right.bounds = GPUBounds.DefaultBounds();

        for (int i = leftEnd; i < rightStart; i++)
        {
            // Entirely on the left-hand side?

            if (m_refStack[i].bounds.max[split.dim] <= split.pos)
            {
                left.bounds = GPUBounds.Union(left.bounds, m_refStack[i].bounds);
                //swap(refs[i], refs[leftEnd++]);
                Reference tmp = m_refStack[i];
                m_refStack[i] = m_refStack[leftEnd];
                m_refStack[leftEnd] = tmp;
                leftEnd++;
            }

            // Entirely on the right-hand side?

            else if (m_refStack[i].bounds.min[split.dim] >= split.pos)
            {
                right.bounds = GPUBounds.Union(right.bounds, m_refStack[i].bounds);
                //swap(refs[i--], refs[--rightStart]);
                --rightStart;
                Reference tmp = m_refStack[i];
                m_refStack[i] = m_refStack[rightStart];
                m_refStack[rightStart] = tmp;
                i--;
            }
        }

        // Duplicate or unsplit references intersecting both sides.

        while (leftEnd < rightStart)
        {
            // Split reference.

            Reference lref = new Reference();
            Reference rref = new Reference();
            SplitReference(lref, rref, m_refStack[leftEnd], split.dim, split.pos);

            // Compute SAH for duplicate/unsplit candidates.

            GPUBounds lub = left.bounds;  // Unsplit to left:     new left-hand bounds.
            GPUBounds rub = right.bounds; // Unsplit to right:    new right-hand bounds.
            GPUBounds ldb = left.bounds;  // Duplicate:           new left-hand bounds.
            GPUBounds rdb = right.bounds; // Duplicate:           new right-hand bounds.
            lub = GPUBounds.Union(lub, m_refStack[leftEnd].bounds);
            rub = GPUBounds.Union(rub, m_refStack[leftEnd].bounds);
            ldb = GPUBounds.Union(ldb, lref.bounds);
            rdb = GPUBounds.Union(rdb, rref.bounds);

            float lac = GetTriangleCost(leftEnd - leftStart);
            float rac = GetTriangleCost(m_refStack.Count - rightStart);
            float lbc = GetTriangleCost(leftEnd - leftStart + 1);
            float rbc = GetTriangleCost(m_refStack.Count - rightStart + 1);

            float unsplitLeftSAH = lub.SurfaceArea() * lbc + right.bounds.SurfaceArea() * rac;
            float unsplitRightSAH = left.bounds.SurfaceArea() * lac + rub.SurfaceArea() * rbc;
            float duplicateSAH = ldb.SurfaceArea() * lbc + rdb.SurfaceArea() * rbc;
            float minSAH = Mathf.Min(unsplitLeftSAH, unsplitRightSAH, duplicateSAH);

            // Unsplit to left?

            if (minSAH == unsplitLeftSAH)
            {
                left.bounds = lub;
                leftEnd++;
            }

            // Unsplit to right?

            else if (minSAH == unsplitRightSAH)
            {
                right.bounds = rub;
                //swap(refs[leftEnd], refs[--rightStart]);
                --rightStart;
                Reference tmp = m_refStack[leftEnd];
                m_refStack[leftEnd] = m_refStack[rightStart];
                m_refStack[rightStart] = tmp;
            }

            // Duplicate?

            else
            {
                left.bounds = ldb;
                right.bounds = rdb;
                m_refStack[leftEnd++] = lref;
                m_refStack.Add(rref);
            }
        }

        left.numRef = leftEnd - leftStart;
        right.numRef = m_refStack.Count - rightStart;
    }
}
