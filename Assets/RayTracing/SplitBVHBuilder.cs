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
    public int startIdx;   //primitive在总的primitve中的位置
    public int numRef;     //该节点包含的所有子节点的三角形数量
    public GPUBounds bounds;  //节点的AABB盒子
    public GPUBounds centroidBounds;
    public NodeSpec()
    {
        startIdx = 0;
        numRef = 0;
        bounds = GPUBounds.DefaultBounds();
        centroidBounds = GPUBounds.DefaultBounds();
    }
};

/*
struct ObjectSplit
{
    public float sah;
    public int   sortDim;
    public int   numLeft;
    public GPUBounds leftBounds;
    public GPUBounds rightBounds;
    public float overlap;

    public static ObjectSplit Default()
    {
        ObjectSplit objectSplit = new ObjectSplit();
        objectSplit.sah = float.MaxValue;
        objectSplit.sortDim = 0;
        objectSplit.leftBounds = GPUBounds.DefaultBounds();
        objectSplit.rightBounds = GPUBounds.DefaultBounds();
        objectSplit.overlap = 0;
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
*/


struct SahSplit
{
    public int   dim;     //按哪个轴
    public float pos;   //划分的位置
    public float sah;     //消耗的sah
    public float overlap; //overlap的比例，spatial是0

    public static SahSplit Default()
    {
        SahSplit split = new SahSplit();
        split.sah = float.MaxValue;
        split.dim = 0;
        split.pos = 0;
        return split;
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
    float m_traversalCost = 0.125f;
    int m_numDuplicates = 0;   //重复在多个节点上的三角形数量
    List<BVHPrimitiveInfo> primitiveInfos = new List<BVHPrimitiveInfo>();
    List<int> triangles = new List<int>();
    List<Reference> m_refStack = new List<Reference>();
    //GPUBounds[] m_rightBounds = null;
    int m_sortDim;
    readonly int MaxDepth = 64;
    int MaxSpatialDepth = 48;
    readonly static int NumSpatialBins = 64;

    private int innerNodes = 0;
    private int leafNodes = 0;

    public enum SplitType
    {
        kObject,
        kSpatial
    };

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

    Vector3 ClampVector3(Vector3 v, Vector3 min, Vector3 max)
    {
        return new Vector3(Mathf.Clamp(v.x, min.x, max.x), Mathf.Clamp(v.y, min.y, max.y), Mathf.Clamp(v.z, min.z, max.z));
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

    bool compl(float a, float b)
    {
        return a < b;
    }

    bool compge(float a, float b)
    {
        return a >= b;
    }

    delegate bool Cmp(float a, float b);
    //delegate bool cmp2(float a, float b);

    public override BVHBuildNode Build(List<Primitive> prims, List<Primitive> orderedPrims, List<GPUVertex> vertices, List<int> triangles, int _maxPrimsInNode = 1)
    {
        _triangles = triangles;
        _vertices = vertices;
        _primitives = prims;
        _orderedPrimitives = orderedPrims;
        maxPrimsInNode = _maxPrimsInNode;
        NodeSpec root = new NodeSpec();
        m_refStack.Clear();

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
            root.centroidBounds = GPUBounds.Union(root.centroidBounds, primitiveInfos[i].worldBound.centroid);
        }
        root.numRef = prims.Count;

        // Remove degenerates.
        //把无效的boundingbox去掉，例如线和带负数的
        int firstRef = m_refStack.Count - root.numRef;
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
        root.numRef = m_refStack.Count - firstRef;
        

        //m_rightBounds = new GPUBounds[Mathf.Max(root.numRef, NumSpatialBins) - 1];
        m_minOverlap = root.bounds.SurfaceArea() * m_splitAlpha;
        innerNodes = 0;
        leafNodes = 0;
        totalNodes = 0;

        BVHBuildNode rootNode = RecursiveBuild(root, 0, 0, 1.0f);
        Debug.Log("InnerNodes num = " + innerNodes);
        Debug.Log("LeafNodes num = " + leafNodes);
        Debug.Log("TotalNodes num = " + totalNodes);

        return rootNode;
    }

    BVHBuildNode RecursiveBuild(NodeSpec spec, int level, float progressStart, float progressEnd)
    {
        totalNodes++;
        
        if (spec.numRef <= maxPrimsInNode || level >= MaxDepth)
            return CreateLeaf(spec);

        //find the split candidate
        //判断split space和split object的依据是？
        float area = spec.bounds.SurfaceArea();
        float leafSAH = GetTriangleCost(spec.numRef);
        //这里是因为2个子节点？
        float nodeSAH = area * GetNodeCost(2);

        // Choose the maximum extent
        int axis = spec.centroidBounds.MaximunExtent();
        float border = spec.centroidBounds.centroid[axis];

        SplitType split_type = SplitType.kObject;

        SahSplit objectSplit = FindObjectSplit(spec, nodeSAH);

        SahSplit spatialSplit = SahSplit.Default();
        if (level < MaxSpatialDepth && objectSplit.overlap >= m_minOverlap)
        {
            //由于object划分会产生overlap的区域，当overlap的区域＞minOverlap的时候，需要划分spatial split
            spatialSplit = FindSpatialSplit(spec, nodeSAH);
        }

        BVHBuildNode node = new BVHBuildNode();
        float minSAH = Mathf.Min(objectSplit.sah, spatialSplit.sah);
        //minSAH = Mathf.Min(minSAH, leafSAH);
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
            //return CreateLeaf(spec);
        }

        if (objectSplit.sah < spatialSplit.sah)
        {
            axis = objectSplit.dim;
        }
        else
        {
            split_type = SplitType.kSpatial;
            axis = spatialSplit.dim;
        }

        if (split_type == SplitType.kSpatial)
        {
            int elems = spec.startIdx + spec.numRef * 2;
            if (m_refStack.Count < elems)
            {
                //primrefs.resize(elems);
                List<Reference> extras = new List<Reference>();
                for (int i = 0; i < elems - m_refStack.Count; ++i)
                    extras.Add(new Reference());
                m_refStack.AddRange(extras);
            }

            // Split prim refs and add extra refs to request
            int extra_refs = 0;
            SplitPrimRefs(spatialSplit, spec, m_refStack, ref extra_refs);
            spec.numRef += extra_refs;
            border = spatialSplit.pos;
            axis = spatialSplit.dim;
        }
        else
        {
            border = !float.IsNaN(objectSplit.pos) ? objectSplit.pos : border;
            axis = !float.IsNaN(objectSplit.pos) ? objectSplit.dim : axis;
        }

        //分组，把原来ref队列进行分组
        // Start partitioning and updating extents for children at the same time
        GPUBounds leftbounds = GPUBounds.DefaultBounds();
        GPUBounds rightbounds = GPUBounds.DefaultBounds();
        GPUBounds leftcentroid_bounds = GPUBounds.DefaultBounds();
        GPUBounds rightcentroid_bounds = GPUBounds.DefaultBounds();
        int splitidx = spec.startIdx;

        bool near2far = ((spec.numRef + spec.startIdx) & 0x1) != 0;



        Cmp cmp1 = compl;//near2far ? compl : compge;
        if (!near2far)
            cmp1 = compge;
        Cmp cmp2 = compge;
        if (!near2far)
            cmp2 = compl;

        //bool(*cmpl)(float, float) = [](float a, float b)-> bool { return a < b; };
        //bool(*cmpge)(float, float) = [](float a, float b)-> bool { return a >= b; };
        //auto cmp1 = near2far ? cmpl : cmpge;
        //auto cmp2 = near2far ? cmpge : cmpl;

        if (spec.centroidBounds.Extend[axis] > 0.0f)
        {
            int first = spec.startIdx;
            int last = spec.startIdx + spec.numRef;

            while (true)
            {
                while ((first != last) && cmp1(m_refStack[first].bounds.centroid[axis], border))
                {
                    leftbounds = GPUBounds.Union(m_refStack[first].bounds, leftbounds);
                    leftcentroid_bounds = GPUBounds.Union(leftcentroid_bounds, m_refStack[first].bounds.centroid);
                    ++first;
                }

                if (first == last--) 
                    break;

                rightbounds = GPUBounds.Union(m_refStack[first].bounds, rightbounds);
                rightcentroid_bounds = GPUBounds.Union(rightcentroid_bounds, m_refStack[first].bounds.centroid);

                while ((first != last) && cmp2(m_refStack[last].bounds.centroid[axis], border))
                {
                    rightbounds = GPUBounds.Union(m_refStack[last].bounds, rightbounds);
                    rightcentroid_bounds = GPUBounds.Union(rightcentroid_bounds, m_refStack[last].bounds.centroid);
                    --last;
                }

                if (first == last) 
                    break;

                leftbounds = GPUBounds.Union(m_refStack[last].bounds, leftbounds);
                leftcentroid_bounds = GPUBounds.Union(leftcentroid_bounds, m_refStack[last].bounds.centroid);

                //std::swap(primrefs[first++], primrefs[last]);
                SortSwap(m_refStack, first, last);
                first++;
            }


            splitidx = first;
        }


        if (splitidx == spec.startIdx || splitidx == spec.startIdx + spec.numRef)
        {
            splitidx = spec.startIdx + (spec.numRef >> 1);

            for (int i = spec.startIdx; i < splitidx; ++i)
            {
                leftbounds = GPUBounds.Union(m_refStack[i].bounds, leftbounds);
                leftcentroid_bounds = GPUBounds.Union(leftcentroid_bounds, m_refStack[i].bounds.centroid);
            }

            for (int i = splitidx; i < spec.startIdx + spec.numRef; ++i)
            {
                rightbounds = GPUBounds.Union(m_refStack[i].bounds, rightbounds);
                rightcentroid_bounds = GPUBounds.Union(rightcentroid_bounds, m_refStack[i].bounds.centroid);
            }
        }
        //分组结束

        NodeSpec left = new NodeSpec();
        left.startIdx = spec.startIdx;
        left.numRef = splitidx - spec.startIdx;
        left.bounds = leftbounds;
        left.centroidBounds = leftcentroid_bounds;
        NodeSpec right = new NodeSpec();
        right.startIdx = splitidx;
        right.numRef = spec.numRef - (splitidx - spec.startIdx);
        right.bounds = rightbounds;
        right.centroidBounds = rightcentroid_bounds;

        //if (minSAH == spatialSplit.sah)
        //    PerformSpatialSplit(left, right, spec, spatialSplit);
        //if (left.numRef == 0 || right.numRef == 0)
        //    PerformObjectSplit(left, right, spec, objectSplit);

        m_numDuplicates += left.numRef + right.numRef - spec.numRef;
        float progressMid = Mathf.Lerp(progressStart, progressEnd, (float)right.numRef / (float)(left.numRef + right.numRef));
        BVHBuildNode rightNode = RecursiveBuild(right, level + 1, progressStart, progressMid);
        BVHBuildNode leftNode = RecursiveBuild(left, level + 1, progressMid, progressEnd);
        BVHBuildNode innerNode = CreateInnerNode(spec.bounds, leftNode, rightNode);
        return innerNode;
    }

    SahSplit FindObjectSplit(NodeSpec spec, float nodeSAH)
    {
        SahSplit split = SahSplit.Default();

        Vector3 origin = spec.bounds.min;
        Vector3 binSize = (spec.bounds.max - origin) * (1.0f / (float)NumSpatialBins);
        int splitidx = -1;

        int start = spec.startIdx;
        int end = start + spec.numRef;
        float sah = float.MaxValue;
        float thisNodeSurfaceArea = spec.bounds.SurfaceArea();

        for (int axis = 0; axis < 3; axis++)
        {
            GPUBounds[] rightBounds = new GPUBounds[NumSpatialBins - 1];
            float centroid_rng = spec.centroidBounds.Extend[axis];

            if (centroid_rng == 0.0f) 
                continue;
            int nBuckets = NumSpatialBins;
            BucketInfo[] buckets = new BucketInfo[nBuckets];
            for (int i = 0; i < nBuckets; ++i)
            {
                buckets[i] = new BucketInfo();
            }

            // Initialize _BucketInfo_ for SAH partition buckets
            for (int i = start; i < end; ++i)
            {
                //计算当前的Primitive属于哪个bucket
                int b = (int)(nBuckets *
                    spec.centroidBounds.Offset(m_refStack[i].bounds.centroid)[axis]);
                if (b == nBuckets)
                    b = nBuckets - 1;
                //CHECK_GE(b, 0);
                //CHECK_LT(b, nBuckets);
                buckets[b].count++;
                //计算bucket的bounds
                buckets[b].bounds =
                    GPUBounds.Union(buckets[b].bounds, m_refStack[i].bounds);
            }

            //用pbrt的方法没必要sort了
            //BVHSort.Sort<Reference>(start, end, m_refStack, ReferenceCompare, SortSwap);
            // Sweep right to left and determine bounds.

            GPUBounds rightBox = GPUBounds.DefaultBounds();
            for (int i = nBuckets - 1; i > 0; i--)
            {
                rightBox = GPUBounds.Union(buckets[i].bounds, rightBox);
                rightBounds[i - 1] = rightBox;
            }

            // Sweep left to right and select lowest SAH.

            GPUBounds leftBounds = GPUBounds.DefaultBounds();
            int leftcount = 0;
            int rightcount = spec.numRef;

            //分组，计算每组的cost
            //cost(A,B) = t_trav + pA∑t_isect(ai) + pB∑t_isect(ai)
            //t_trav = 0.125; t_isect = 1
            float[] cost = new float[nBuckets - 1];

            for (int i = 0; i < nBuckets - 1; ++i)
            {
                /*
                GPUBounds bA = GPUBounds.DefaultBounds();
                //bA.SetMinMax(min, max);
                GPUBounds bB = GPUBounds.DefaultBounds();
                //bB.SetMinMax(min, max);
                int count0 = 0, count1 = 0;
                for (int j = 0; j <= i; ++j)
                {
                    bA = GPUBounds.Union(bA, buckets[j].bounds);
                    count0 += buckets[j].count;
                }
                for (int j = i + 1; j < nBuckets; ++j)
                {
                    bB = GPUBounds.Union(bB, buckets[j].bounds);
                    count1 += buckets[j].count;
                }
                //t_trav = 0.125f
                cost[i] = 0.125f +
                    (count0 * bA.SurfaceArea() +
                        count1 * bB.SurfaceArea()) /
                    spec.bounds.SurfaceArea();
                */
                leftBounds = GPUBounds.Union(buckets[i].bounds, leftBounds);
                leftcount += buckets[i].count;
                rightcount -= buckets[i].count;
                float sahTemp = m_traversalCost +
                    (GetTriangleCost(leftcount) * leftBounds.SurfaceArea() + GetTriangleCost(rightcount) * rightBox.SurfaceArea()) /
                    thisNodeSurfaceArea;

                if (sahTemp < sah)
                {
                    sah = sahTemp;
                    split.sah = sah;
                    split.dim = axis;
                    splitidx = i;
                    //split.numLeft = i;
                    //split.leftBounds = leftBounds;
                    //split.rightBounds = m_rightBounds[i];
                    split.overlap = GPUBounds.Intersection(leftBounds, rightBounds[i]).SurfaceArea() / thisNodeSurfaceArea;
                }
            }
        }

        if (splitidx != -1)
        {
            split.pos = spec.centroidBounds.min[split.dim] + (splitidx + 1) * (spec.centroidBounds.Extend[split.dim] / NumSpatialBins);
        }

        //split.overlap = GPUBounds.Intersection(split.leftBounds, split.rightBounds).SurfaceArea() / spec.bounds.SurfaceArea();
        return split;


        /*
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
                float sah = m_traversalCost + leftBounds.SurfaceArea() * GetTriangleCost(i) + m_rightBounds[beginIndex + i - 1].SurfaceArea() * GetTriangleCost(spec.numRef - i);
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
        */
    }
    SahSplit FindSpatialSplit(NodeSpec spec, float nodeSAH)
    {
        // Initialize bins.
        Vector3 origin = spec.bounds.min;
        Vector3 binSize = (spec.bounds.max - origin) * (1.0f / (float)NumSpatialBins);
        Vector3 invBinSize = new Vector3(1.0f / binSize.x, 1.0f / binSize.y, 1.0f / binSize.z);

        SpatialBin[,] m_bins = new SpatialBin[3, NumSpatialBins];

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

        for (int refIdx = spec.startIdx; refIdx < spec.startIdx + spec.numRef; refIdx++)
        {
            Reference reference = m_refStack[refIdx];
            Vector3 minMinusOrig = reference.bounds.min - origin;
            Vector3 maxMinusOrig = reference.bounds.max - origin;

            //Vector3Int firstBin = ClampVector3Int(Vector3Int.FloorToInt(new Vector3(minMinusOrig.x * invBinSize.x, minMinusOrig.y * invBinSize.y, minMinusOrig.z * invBinSize.z)),
            //    0, new Vector3Int(NumSpatialBins - 1, NumSpatialBins - 1, NumSpatialBins - 1));
            //Vector3Int lastBin = ClampVector3Int(Vector3Int.FloorToInt(new Vector3(maxMinusOrig.x * invBinSize.x, maxMinusOrig.y * invBinSize.y, maxMinusOrig.z * invBinSize.z)), 
            //    firstBin, new Vector3Int(NumSpatialBins - 1, NumSpatialBins - 1, NumSpatialBins - 1));
            Vector3 firstBin = ClampVector3(new Vector3(minMinusOrig.x * invBinSize.x, minMinusOrig.y * invBinSize.y, minMinusOrig.z * invBinSize.z), 0, NumSpatialBins - 1);
            Vector3 lastBin = ClampVector3(new Vector3(maxMinusOrig.x * invBinSize.x, maxMinusOrig.y * invBinSize.y, maxMinusOrig.z * invBinSize.z), firstBin,
                 new Vector3(NumSpatialBins - 1, NumSpatialBins - 1, NumSpatialBins - 1));

            for (int dim = 0; dim< 3; dim++)
            {
                if (spec.bounds.Extend[dim] == 0.0f)
                    continue;
                Reference currRef = reference;
                for (int i = (int)firstBin[dim]; i < (int)lastBin[dim]; i++)
                {
                    Reference leftRef = new Reference();
                    Reference rightRef = new Reference();
                    float splitPos = origin[dim] + binSize[dim] * (float)(i + 1);
                    //SplitReference(leftRef, rightRef, currRef, dim, splitPos);
                    if (SplitPrimRef(currRef, dim, splitPos, leftRef, rightRef))
                    {
                        m_bins[dim, i].bounds = GPUBounds.Union(m_bins[dim, i].bounds, leftRef.bounds);
                        currRef = rightRef;
                    }
                }
                m_bins[dim, (int)lastBin[dim]].bounds = GPUBounds.Union(m_bins[dim, (int)lastBin[dim]].bounds, currRef.bounds);
                m_bins[dim, (int)firstBin[dim]].enter++;
                m_bins[dim, (int)lastBin[dim]].exit++;
            }
        }

        // Select best split plane.

        SahSplit split = SahSplit.Default();
        for (int dim = 0; dim < 3; dim++)
        {
            if (spec.bounds.Extend[dim] == 0.0f)
                continue;
            // Sweep right to left and determine bounds.
            GPUBounds[] rightBounds = new GPUBounds[NumSpatialBins - 1];
            GPUBounds rightBox = GPUBounds.DefaultBounds();
            for (int i = NumSpatialBins - 1; i > 0; i--)
            {
                rightBox = GPUBounds.Union(rightBox, m_bins[dim, i].bounds);
                rightBounds[i - 1] = rightBox;
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

                float sah = m_traversalCost + (leftBounds.SurfaceArea() * GetTriangleCost(leftNum) + rightBounds[i - 1].SurfaceArea() * GetTriangleCost(rightNum)) /
                    spec.bounds.SurfaceArea();
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

    BVHBuildNode CreateInnerNode(GPUBounds bounds, BVHBuildNode left, BVHBuildNode right)
    {
        innerNodes++;
        BVHBuildNode node = new BVHBuildNode();
        node.bounds = bounds;
        node.childrenLeft = left;
        node.childrenRight = right;
        node.nPrimitives = 0;
        return node;
    }

    BVHBuildNode CreateLeaf(NodeSpec spec)
    {
        leafNodes++;
        //List<int> tris = m_bvh.getTriIndices();
        for (int i = spec.startIdx; i < spec.startIdx + spec.numRef; i++)
        {
            //Reference last = m_refStack[m_refStack.Count - 1];
            //m_refStack.RemoveAt(m_refStack.Count - 1);
            Reference primRef = m_refStack[i];
            //if (!_orderedPrimitives.Contains(_primitives[primRef.triIdx]))
                _orderedPrimitives.Add(_primitives[primRef.triIdx]);
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

    /*
    void PerformObjectSplit(NodeSpec left, NodeSpec right, NodeSpec spec, SahSplit split)
    {
        m_sortDim = split.sortDim;
        int count = spec.numRef;
        int start = m_refStack.Count - spec.numRef;
        int end = start + count;
        //sort(this, m_refStack.getSize() - spec.numRef, m_refStack.getSize(), sortCompare, sortSwap);
        //m_refStack.Sort(m_refStack.Count - spec.numRef, count, new ReferenceCompair(m_sortDim));
        BVHSort.Sort(start, end, m_refStack, ReferenceCompare, SortSwap);

        left.numRef = split.numLeft;
        left.bounds = split.leftBounds;
        right.numRef = spec.numRef - split.numLeft;
        right.bounds = split.rightBounds;
    }
    */

    bool SplitPrimRef(Reference refPrim, int axis, float split, Reference leftref, Reference rightref)
    {
        // Start with left and right refs equal to original ref
        leftref.triIdx = rightref.triIdx = refPrim.triIdx;
        leftref.bounds = rightref.bounds = refPrim.bounds;

        // Only split if split value is within our bounds range
        if (split > refPrim.bounds.min[axis] && split < refPrim.bounds.max[axis])
        {
            // Trim left box on the right
            leftref.bounds.max[axis] = split;
            // Trim right box on the left
            rightref.bounds.min[axis] = split;
            return true;
        }

        return false;
    }

    void SplitPrimRefs(SahSplit split, NodeSpec req, List<Reference> refs, ref int extra_refs)
    {
        // We are going to append new primitives at the end of the array
        int appendprims = req.numRef;

        // Split refs if any of them require to be split
        for (int i = req.startIdx; i < req.startIdx + req.numRef; ++i)
        {
            //assert(static_cast<size_t>(req.startidx + appendprims) < refs.size());

            Reference leftref = new Reference();
            Reference rightref = new Reference();
            if (SplitPrimRef(refs[i], split.dim, split.pos, leftref, rightref))
            {
                // Copy left ref instead of original
                refs[i] = leftref;
                // Append right one at the end
                refs[req.startIdx + appendprims++] = rightref;
            }
        }

        // Return number of primitives after this operation
        extra_refs = appendprims - req.numRef;
    }

    void PerformSpatialSplit(NodeSpec left, NodeSpec right, NodeSpec spec, SahSplit split)
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
