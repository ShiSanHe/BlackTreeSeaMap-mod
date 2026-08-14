namespace TreeSeaMap.Map;

/// <summary>
/// 每一层的生成配置。整个游戏流程只有 3 层（对应 3 幕），每层独立配置。
/// 初始配置草案见 docs/LAYER_CONFIG.md。
/// </summary>
public sealed class LayerConfig
{
    /// <summary>网格宽度（格点列数）。</summary>
    public int Width { get; set; } = 9;

    /// <summary>网格高度（格点行数）。</summary>
    public int Height { get; set; } = 9;

    /// <summary>
    /// 行动力 AP = 原版对应行数（Act1=15 / Act2=14 / Act3=13）。
    /// 当前不可恢复；每层独立，进下一层重置。
    /// </summary>
    public int ActionPoint { get; set; } = 15;

    /// <summary>节点稀疏度（0-1），占用格点比例。</summary>
    public double Occupancy { get; set; } = 0.65;

    /// <summary>额外边密度（0-1），枝岔更多、可走路更多；环是副产品。</summary>
    public double RoadDensity { get; set; } = 0.6;

    /// <summary>出口固定坐标；null 表示默认顶行中心 (Width/2, 0)。</summary>
    public (int X, int Y)? ExitPosition { get; set; }

    /// <summary>起点固定坐标；null 表示 auto（距出口最远且最短路 ≤ AP）。</summary>
    public (int X, int Y)? StartPosition { get; set; }

    // ---- Phase 4 事件分配（按原版密度随机，无保底）----

    /// <summary>精英战数量（原版固定 5）。</summary>
    public int NumElites { get; set; } = 5;

    /// <summary>商店数量（原版固定 3）。</summary>
    public int NumShops { get; set; } = 3;

    /// <summary>篝火数量（原版 Act1 约 6 / Act2 6 / Act3 5-6）。</summary>
    public int NumRests { get; set; } = 6;

    /// <summary>问号数量（原版 10-14，Act 不同略有差异）。</summary>
    public int NumUnknowns { get; set; } = 12;

    /// <summary>宝箱数量（网状图自定义；原版"倒数第 7 行"整行≈7，9×9 网格≈3 保持密度）。</summary>
    public int NumTreasures { get; set; } = 3;

    // ---- 追猎战 ----

    /// <summary>追猎时 Boss 属性加成（+10%）。</summary>
    public double BossHpBuffOnChase { get; set; } = 0.1;

    // ---- 三层预设 ----

    public static LayerConfig Act1() => new()
    {
        Width = 9, Height = 9, ActionPoint = 15, Occupancy = 0.65, RoadDensity = 0.6,
    };

    public static LayerConfig Act2() => new()
    {
        Width = 9, Height = 9, ActionPoint = 14, Occupancy = 0.65, RoadDensity = 0.6,
    };

    public static LayerConfig Act3() => new()
    {
        Width = 9, Height = 9, ActionPoint = 13, Occupancy = 0.65, RoadDensity = 0.6,
    };
}
