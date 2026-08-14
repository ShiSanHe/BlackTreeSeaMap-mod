namespace TreeSeaMap.Map;

/// <summary>
/// 网格图上的移动规则。每步沿图边四方向移动，耗 1 行动力。
/// </summary>
public static class MovementSystem
{
    /// <summary>每步移动消耗的行动力。</summary>
    public const int CostPerStep = 1;

    /// <summary>能否从 from 一步走到 to（必须是相邻图边）。</summary>
    public static bool CanMove(Node from, Node to)
        => from.Neighbors.Contains(to);
}
