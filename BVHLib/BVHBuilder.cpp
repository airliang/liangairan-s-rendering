#include "BVHBuilder.h"

namespace BVHLib
{
	BVHBuildNode* BVHBuilder::Build(Bounds3f* bounds, int boundsNum, GPUVertex* vertices, int verticesNum, int _maxPrimsInNode)
	{
		NodeSpec root; //new NodeSpec();
		m_refStack.clear();

		for (int i = 0; i < boundsNum; ++i)
		{
			//primitiveInfos.Add(new BVHPrimitiveInfo(i, prims[i].worldBound));
			Reference reference = Reference
			{
				i,  //reference's triIdx is the index in _primitives
				bounds[i]
			};
			m_refStack.emplace_back(reference);
		}
		for (int i = 0; i < boundsNum; ++i)
		{
			//root.bounds = GPUBounds.Union(root.bounds, primitiveInfos[i].worldBound);
			//root.centroidBounds = GPUBounds.Union(root.centroidBounds, primitiveInfos[i].worldBound.centroid);
			root.bounds.Union(m_refStack[i].bounds);
			root.centroidBounds.Union(m_refStack[i].bounds.Centroid());
		}
		root.numRef = boundsNum;

		// Remove degenerates.
		//把无效的boundingbox去掉，例如线和带负数的
		int firstRef = m_refStack.size() - root.numRef;
		for (int i = m_refStack.size() - 1; i >= firstRef; i--)
		{
			if (i >= m_refStack.size() || i < 0)
			{
				//Debug.LogError("Remove degenerates error!");
			}
			Vector3f size = m_refStack[i].bounds.Diagonal();
			//removes the negetive size and the line bounding
			if (m_refStack[i].bounds.MinSize() < 0.0f || (size.x + size.y + size.z) == m_refStack[i].bounds.MaxSize())
			{
				m_refStack[i] = m_refStack[m_refStack.size() - 1];
				m_refStack.erase(m_refStack.end() - 1);
			}
		}
		root.numRef = m_refStack.size() - firstRef;


		//m_rightBounds = new GPUBounds[Mathf.Max(root.numRef, NumSpatialBins) - 1];
		m_minOverlap = root.bounds.SurfaceArea() * m_splitAlpha;
		innerNodes = 0;
		leafNodes = 0;
		totalNodes = 0;

		BVHBuildNode* rootNode = RecursiveBuild(root, 0, 0, 1.0f);

		//Debug.Log("InnerNodes num = " + innerNodes);
		//Debug.Log("LeafNodes num = " + leafNodes);
		//Debug.Log("TotalNodes num = " + totalNodes);

		return rootNode;
	}
}