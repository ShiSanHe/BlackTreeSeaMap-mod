namespace TreeSeaMap.Map;

/// <summary>
/// 节点内容类型。本阶段（Phase 3）只生成拓扑，内容由 Phase 4 事件分配器填充。
/// 与游戏 MapPointType 的映射见 Phase 5 桥接层。
/// </summary>
public enum NodeContent
{
    /// <summary>尚未分配（Phase 4 之前或作为占位）。</summary>
    Unassigned,

    /// <summary>战斗（游戏 Monster）。</summary>
    Battle,

    /// <summary>问号（游戏 Unknown）。</summary>
    Unknown,

    /// <summary>篝火（游戏 RestSite）。</summary>
    Rest,

    /// <summary>商店（游戏 Shop）。</summary>
    Shop,

    /// <summary>宝箱（游戏 Treasure）。</summary>
    Treasure,

    /// <summary>精英战（游戏 Elite）。</summary>
    Elite,

    /// <summary>出口 = Boss 房（游戏 Boss）。用户拍板：出口就是一个 Boss 节点。</summary>
    Exit,
}
