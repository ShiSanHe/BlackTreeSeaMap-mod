using System.Collections.Generic;

namespace TreeSeaMap.Map;

/// <summary>网格图中的一个节点（格点）。</summary>
public sealed class Node
{
    public int X { get; }

    public int Y { get; }

    /// <summary>是否起点（Ancient）。</summary>
    public bool IsStart { get; internal set; }

    /// <summary>是否出口（Boss）。</summary>
    public bool IsExit { get; internal set; }

    /// <summary>内容类型，Phase 4 分配。</summary>
    public NodeContent Content { get; set; } = NodeContent.Unassigned;

    /// <summary>四方向相邻的可走邻居（沿生成树边/额外边）。</summary>
    public HashSet<Node> Neighbors { get; } = new();

    public int Degree => Neighbors.Count;

    public Node(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X},{Y})";
}
