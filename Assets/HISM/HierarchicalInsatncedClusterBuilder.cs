using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;


//clustertree是一个多维的树，所以一个node有多个child
public struct HISMClusterNode
{
    public int FirstInstance; //第一个instance的序号，这个时候instance已经排序了
    public int LastInstance;  //最后一个instance的
    public int FirstChild;
    public int LastChild;
    //public Bounds bounds;
    public Vector3 boundMin;
    public Vector3 boundMax;

    public static HISMClusterNode DefaultClusterNode()
    {
        HISMClusterNode defaultNode = new HISMClusterNode
        {
            FirstInstance = -1,
            LastInstance = -1,
            FirstChild = -1,
            LastChild = -1,
            boundMin = Vector3.positiveInfinity,
            boundMax = Vector3.negativeInfinity
        };
        return defaultNode;
    }
}

public class HISMClusterTree
{
    //这个nodes的排序要搞清楚
    public List<HISMClusterNode> Nodes = new List<HISMClusterNode>();
    public List<int> SortedInstances = new List<int>();
    public int[] InstanceReorderTable;
    public int BottomLevelStart = 0;   //the first cluster instance node index
    //public NativeArray<>
}

public class HierarchicalInstancedClusterBuilder
{
    int orignalNum;
    Bounds instBounds;  //instance的local space下的bounding box
    int branchingFactor = 1;    //类似一个划分一个node的instance最大数量
    int MaxInstancesPerLeaf = 1;
    int InternalNodeBranchingFactor = 16;
    List<int> SortIndex = new List<int>();
    List<Vector3> SortPoints = new List<Vector3>();
    List<Matrix4x4> Transforms = new List<Matrix4x4>();

    HISMClusterTree clusterTree = new HISMClusterTree();

    public HISMClusterTree GetClusterTree()
    {
        return clusterTree;
    }

    struct RunPair
    {
        public int Start;
        public int Num;

        public RunPair(int InStart, int InNum)
        {
            Start = InStart;
            Num = InNum;
        }
    };

    List<RunPair> Clusters = new List<RunPair>();
    struct SortPair
    {
        public float d;
        public int Index;
	}


    List<SortPair> SortPairs = new List<SortPair>();

    public HierarchicalInstancedClusterBuilder(List<Matrix4x4> transforms, Bounds meshBox, int nodeBranchingFactor)
    {
        Transforms = transforms;
        orignalNum = Transforms.Count;
        instBounds = meshBox;
        InternalNodeBranchingFactor = nodeBranchingFactor;
    }

    public void BuildTree(ref NativeArray<Matrix4x4> worldMatrix)
    {
        Init();

        if (orignalNum == 0)
        {
            return;
        }

        branchingFactor = MaxInstancesPerLeaf;
        Split(orignalNum);

        //clusterTree.Nodes = new HISMClusterNode[Clusters.Count];
        for (int i = 0; i < Clusters.Count; ++i)
        {
            clusterTree.Nodes.Add(new HISMClusterNode());
        }

        clusterTree.SortedInstances.AddRange(SortIndex);
        int NumRoots = Clusters.Count;

        for (int Index = 0; Index < Clusters.Count; Index++)
        {
            HISMClusterNode Node = clusterTree.Nodes[Index];
            Node.FirstInstance = Clusters[Index].Start;
            Node.LastInstance = Clusters[Index].Start + Clusters[Index].Num - 1;
            Bounds NodeBox = BoundingUtils.DefaultBounds();

            for (int InstanceIndex = Node.FirstInstance; InstanceIndex <= Node.LastInstance; InstanceIndex++)
            {
                Matrix4x4 thisInstTrans = Transforms[clusterTree.SortedInstances[InstanceIndex]];
                Bounds ThisInstBox = BoundingUtils.TransformBounds(ref thisInstTrans, ref instBounds); //InstBox.TransformBy(ThisInstTrans);
                //NodeBox += ThisInstBox;
                NodeBox.Encapsulate(ThisInstBox);  //BoundingUtils.Union(NodeBox, ThisInstBox);

                //if (GenerateInstanceScalingRange)
                //{
                //    FVector CurrentScale = ThisInstTrans.GetScaleVector();

                //    Node.MinInstanceScale = Node.MinInstanceScale.ComponentMin(CurrentScale);
                //    Node.MaxInstanceScale = Node.MaxInstanceScale.ComponentMax(CurrentScale);
                //}
            }
            Node.boundMin = NodeBox.min;
            Node.boundMax = NodeBox.max;
            clusterTree.Nodes[Index] = Node;
        }

        //下面开始自底向上构建tree
        //int[] InverseSortIndex;
        //int[] InverseInstanceIndex;
        //int[] LevelStarts;   //每一层的起始位置?
        //List<HISMClusterNode> OldNodes = new List<HISMClusterNode>();
            
        while (NumRoots > 1)
        {
            SortIndex.Clear();
            SortPoints.Clear();

            for (int Index = 0; Index < NumRoots; ++Index)
            {
                SortIndex.Add(Index);
                //HISMClusterNode node = clusterTree.Nodes[Index];
                SortPoints.Add((clusterTree.Nodes[Index].boundMax + clusterTree.Nodes[Index].boundMin) * 0.5f);
            }

            branchingFactor = InternalNodeBranchingFactor;
            //这里再次对cluster进行排序
            //sortIndex也会刷新
            Split(NumRoots);

            //InverseSortIndex = new int[NumRoots];
            //for (int i = 0; i < NumRoots; ++i)
            //{
            //    InverseSortIndex[SortIndex[i]] = i;
            //}

            //由于每split一次，SortIndex就会刷新一次，所以每次split后要重新定义RemapSortIndex

            if (NumRoots == orignalNum)
            {
                int[] RemapSortIndex = new int[orignalNum];
                int OutIndex = 0;
                for (int Index = 0; Index < NumRoots; Index++)
                {
                    int FirstInstance = clusterTree.Nodes[SortIndex[Index]].FirstInstance;
                    int LastInstance = clusterTree.Nodes[SortIndex[Index]].LastInstance;
                    for (int InstanceIndex = FirstInstance; InstanceIndex <= LastInstance; InstanceIndex++)
                    {
                        RemapSortIndex[OutIndex++] = InstanceIndex;
                    }
                }

                List<int> OldInstanceIndex = new List<int>();
                OldInstanceIndex.AddRange(clusterTree.SortedInstances);
                for (int Index = 0; Index < orignalNum; Index++)
                {
                    clusterTree.SortedInstances[Index] = OldInstanceIndex[RemapSortIndex[Index]];
                }
            }

            int NewNum = clusterTree.Nodes.Count + Clusters.Count;

            //OldNodes.Clear();
            //OldNodes.AddRange(clusterTree.Nodes);
            //clusterTree.Nodes.Clear();

            for (int Index = 0; Index < Clusters.Count; ++Index)
            {
                clusterTree.Nodes.Insert(Index, new HISMClusterNode());
            }
            //clusterTree.Nodes.AddRange(OldNodes);

            //for (int Index = 0; Index < OldNodes.Count; Index++)
            //{
            //    clusterTree.Nodes[InverseChildIndex[Index]] = OldNodes[Index];
            //}

            int OldIndex = Clusters.Count;
            clusterTree.BottomLevelStart += OldIndex;
            //int32 InstanceTracker = 0;
            for (int Index = 0; Index < Clusters.Count; Index++)
            {
                HISMClusterNode Node = clusterTree.Nodes[Index];
                Node.FirstChild = OldIndex;
                OldIndex += Clusters[Index].Num;
                Node.LastChild = OldIndex - 1;
                Node.FirstInstance = clusterTree.Nodes[Node.FirstChild].FirstInstance;
                //checkSlow(Node.FirstInstance == InstanceTracker);
                Node.LastInstance = clusterTree.Nodes[Node.LastChild].LastInstance;
                //InstanceTracker = Node.LastInstance + 1;
                //checkSlow(InstanceTracker <= Num);
                Bounds NodeBox = BoundingUtils.DefaultBounds();
                for (int ChildIndex = Node.FirstChild; ChildIndex <= Node.LastChild; ChildIndex++)
                {
                    HISMClusterNode ChildNode = clusterTree.Nodes[ChildIndex];
                    NodeBox.Encapsulate(ChildNode.boundMin);
                    NodeBox.Encapsulate(ChildNode.boundMax);

                    //if (GenerateInstanceScalingRange)
                    //{
                    //    Node.MinInstanceScale = Node.MinInstanceScale.ComponentMin(ChildNode.MinInstanceScale);
                    //    Node.MaxInstanceScale = Node.MaxInstanceScale.ComponentMax(ChildNode.MaxInstanceScale);
                    //}
                }
                Node.boundMin = NodeBox.min;
                Node.boundMax = NodeBox.max;

                clusterTree.Nodes[Index] = Node;
            }
            NumRoots = Clusters.Count;
        }

        clusterTree.InstanceReorderTable = new int[orignalNum];
        for (int Index = 0; Index < orignalNum; Index++)
        {
            clusterTree.InstanceReorderTable[clusterTree.SortedInstances[Index]] = Index;
        }

        //List<Matrix4x4> worldMatrix = new List<Matrix4x4>();
        //worldMatrix.AddRange(Transforms);
        for (int i = 0; i < worldMatrix.Length; ++i)
        {
            worldMatrix[i] = Transforms[clusterTree.SortedInstances[i]];
        }
    }

    void Init()
    {
        SortIndex.Clear();
        SortPoints.Clear();
        //初始化

        for (int Index = 0; Index < orignalNum; Index++)
        {
            SortPoints.Add(Transforms[Index].GetColumn(3));

            SortIndex.Add(Index);
        }
    }

    void Split(int num)
    {
        Clusters.Clear();
        //对SortIndex排序

        Split(0, num - 1);

        Clusters.Sort((a, b) => a.Start.CompareTo(b.Start));

            
    }

    void Split(int Start, int End)
    {
        int NumRange = 1 + End - Start;
        //FBox ClusterBounds(ForceInit);
        Bounds ClusterBounds = BoundingUtils.DefaultBounds();
        for (int Index = Start; Index <= End; Index++)
        {
            ClusterBounds.Encapsulate(SortPoints[SortIndex[Index]]); // = BoundingUtils.Union(ClusterBounds, SortPoints[SortIndex[Index]]);
        }

        if (NumRange <= branchingFactor)
        {
            Clusters.Add(new RunPair(Start, NumRange));
            return;
        }
        //checkSlow(NumRange >= 2);
        SortPairs.Clear();
        int BestAxis = -1;
        float BestAxisValue = -1.0f;
        //寻找对应的最长轴做划分
        for (int Axis = 0; Axis < 3; Axis++)
        {
            float ThisAxisValue = ClusterBounds.max[Axis] - ClusterBounds.min[Axis];
            if (Axis == 0 || ThisAxisValue > BestAxisValue)
            {
                BestAxis = Axis;
                BestAxisValue = ThisAxisValue;
            }
        }

        //提取SortPair用于排序
        for (int Index = Start; Index <= End; Index++)
        {
            SortPair Pair;

            Pair.Index = SortIndex[Index];
            Pair.d = SortPoints[Pair.Index][BestAxis];
            SortPairs.Add(Pair);
        }
        //按升序排列
        SortPairs.Sort((a, b) => a.d.CompareTo(b.d));
        for (int Index = Start; Index <= End; Index++)
        {
            SortIndex[Index] = SortPairs[Index - Start].Index;
        }

        int Half = NumRange / 2;

        int EndLeft = Start + Half - 1;
        int StartRight = 1 + End - Half;

        if ((NumRange & 1) > 0)
        {
            if (SortPairs[Half].d - SortPairs[Half - 1].d < SortPairs[Half + 1].d - SortPairs[Half].d)
            {
                EndLeft++;
            }
            else
            {
                StartRight--;
            }
        }
        //checkSlow(EndLeft + 1 == StartRight);
        //checkSlow(EndLeft >= Start);
        //checkSlow(End >= StartRight);

        Split(Start, EndLeft);
        Split(StartRight, End);
    }
}

