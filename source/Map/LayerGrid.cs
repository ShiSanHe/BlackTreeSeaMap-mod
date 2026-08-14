using System.Collections.Generic;

namespace TreeSeaMap.Map;

/// <summary>
/// 一层的网格拓扑结果（纯逻辑，不含任何游戏对象）。
/// 是生成器与桥接层之间的唯一数据契约。
/// </summary>
public sealed class LayerGrid
{
    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>本层行动力（Act1=15 / Act2=14 / Act3=13）。</summary>
    public int ActionPoint { get; init; }

    /// <summary>占用格点（稀疏占用后的格点集）。</summary>
    public Dictionary<(int X, int Y), Node> Nodes { get; } = new();

    /// <summary>生成树边（含 road density 额外边），无序对。</summary>
    public List<(Node A, Node B)> Edges { get; } = new();

    public Node? Start { get; set; }

    public Node? Exit { get; set; }

    /// <summary>起点 → 出口的最短路步数。</summary>
    public int StartToExitDistance { get; set; }

    /// <summary>整图是否连通（起点可达全部节点）。</summary>
    public bool IsConnected { get; set; }

    /// <summary>是否满足 AP 约束（最短路 ≤ 行动力）。</summary>
    public bool SatisfiesActionPoint => StartToExitDistance <= ActionPoint;

    public Node? Get(int x, int y)
        => Nodes.TryGetValue((x, y), out var n) ? n : null;

    public IEnumerable<Node> AllNodes => Nodes.Values;
}
