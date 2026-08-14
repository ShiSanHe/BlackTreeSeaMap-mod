using System.Collections.Generic;
using System.Linq;

namespace TreeSeaMap.Map;

/// <summary>
/// 事件分配器：把 6 种内容类型按 LayerConfig 数量随机撒到所有非出口节点。
/// 网状地图无保底关键节点、无行约束（原版"末行篝火/倒数第7行宝箱/第1行战斗"不适用）。
/// 相邻不重复约束仅对"重型"类型（精英/篝火/商店/宝箱）生效，且为软约束（队列耗尽则放行）。
/// 剩余未分配节点全部补为战斗（原版未分配默认 Monster）。
/// </summary>
public static class EventAssigner
{
    /// <summary>相邻不能重复的重型类型（对应原版 _childMapPointRestrictions 的精神）。</summary>
    private static readonly HashSet<NodeContent> Restricted = new()
    {
        NodeContent.Elite,
        NodeContent.Rest,
        NodeContent.Shop,
        NodeContent.Treasure,
    };

    public static void Assign(LayerGrid grid, LayerConfig cfg, uint seed)
    {
        var rng = new Mulberry32(seed);
        var nodes = grid.AllNodes.Where(n => !n.IsExit).ToList();
        Shuffle(nodes, rng);

        var queue = new Queue<NodeContent>();
        for (int i = 0; i < cfg.NumElites; i++) queue.Enqueue(NodeContent.Elite);
        for (int i = 0; i < cfg.NumShops; i++) queue.Enqueue(NodeContent.Shop);
        for (int i = 0; i < cfg.NumRests; i++) queue.Enqueue(NodeContent.Rest);
        for (int i = 0; i < cfg.NumUnknowns; i++) queue.Enqueue(NodeContent.Unknown);
        for (int i = 0; i < cfg.NumTreasures; i++) queue.Enqueue(NodeContent.Treasure);

        foreach (var node in nodes)
        {
            if (queue.Count == 0)
                break;
            node.Content = NextValid(queue, node);
        }

        foreach (var node in nodes)
            if (node.Content == NodeContent.Unassigned)
                node.Content = NodeContent.Battle;
    }

    private static NodeContent NextValid(Queue<NodeContent> queue, Node node)
    {
        for (int i = 0; i < queue.Count; i++)
        {
            var t = queue.Dequeue();
            if (IsValid(t, node))
                return t;
            queue.Enqueue(t);
        }
        // 所有剩余类型都撞相邻约束 → 兜底战斗（下一轮再尝试）
        return NodeContent.Battle;
    }

    private static bool IsValid(NodeContent t, Node node)
    {
        if (!Restricted.Contains(t))
            return true;
        return node.Neighbors.All(n => n.Content != t);
    }

    private static void Shuffle<T>(IList<T> list, Mulberry32 rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
