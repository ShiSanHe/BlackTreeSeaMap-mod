# LAYER_CONFIG —— 三层 LayerConfig 草案

> 黑流树海 · STS2 网格地图 Mod（Phase 1 调研归档）
> 权威需求见根目录 `PLAN.md` §2.8；本文给出一份可直接落地的 `LayerConfig` 字段草案 + 三层预设值 + 关键约束说明。
> 实现对应文件：`source/Map/LayerConfig.cs`（字段命名与此一致）。

---

## 1. LayerConfig C# 字段草案

```csharp
namespace TreeSeaMap.Map;

/// <summary>每一层的生成配置。整个游戏流程只有 3 层（对应 3 幕），每层独立配置。</summary>
public sealed class LayerConfig
{
    // ---- 网格形状 ----
    /// <summary>网格宽度（格点列数）。默认 9；渲染层按实际纵横比自适应缩放。</summary>
    public int Width { get; set; } = 9;

    /// <summary>网格高度（格点行数）。默认 9；可与 Width 独立调整，不做高瘦限制。</summary>
    public int Height { get; set; } = 9;

    // ---- 行动力 ----
    /// <summary>
    /// 行动力 AP = 原版对应行数（Act1=15 / Act2=14 / Act3=13）。
    /// 每步移动耗 1；当前不可恢复；每层独立，进下一层重置。
    /// </summary>
    public int ActionPoint { get; set; } = 15;

    // ---- 拓扑密度 ----
    /// <summary>节点稀疏度（0-1），占用格点比例。</summary>
    public double Occupancy { get; set; } = 0.65;

    /// <summary>额外边密度（0-1），枝岔/可走路更多；环是副产品。</summary>
    public double RoadDensity { get; set; } = 0.6;

    // ---- 起点 / 出口策略 ----
    /// <summary>出口（Boss 房）固定坐标；null 表示默认 (Width/2, 0)（顶部中心）。</summary>
    public (int X, int Y)? ExitPosition { get; set; }

    /// <summary>起点固定坐标；null 表示 auto：距 Boss 最远且最短路 ≤ AP。</summary>
    public (int X, int Y)? StartPosition { get; set; }

    // ---- 事件分配（按原版密度随机，无保底；Phase 4）----
    /// <summary>精英数量（原版固定 5；SwarmingElites 升阶 ×1.6）。</summary>
    public int NumElites { get; set; } = 5;

    /// <summary>商店数量（原版固定 3）。</summary>
    public int NumShops { get; set; } = 3;

    /// <summary>篝火数量（原版 Act1≈6 / Act2=6 / Act3=5-6）。</summary>
    public int NumRests { get; set; } = 6;

    /// <summary>问号数量（原版 10-14，Act 不同略有差异）。</summary>
    public int NumUnknowns { get; set; } = 12;

    /// <summary>宝箱数量（网状图自定义；原版为"倒数第 7 行"整行固定）。【待 Phase 4 定值】</summary>
    public int NumTreasures { get; set; } = 7;

    // ---- 追猎战 ----
    /// <summary>追猎时 Boss 属性加成（+10%）。</summary>
    public double BossHpBuffOnChase { get; set; } = 0.1;

    // ---- 后续里程碑（Phase 9）----
    // public MapMode Mode { get; set; } = MapMode.Procedural;   // procedural | fixed
}
```

> 说明：其余未声明为「特殊」的格点全部 = **战斗（Monster）**，与原版一致（`StandardActMap.AssignPointTypes` 最后把 `Unassigned` 全部置为 `Monster`，`StandardActMap.cs:295-300`）。

---

## 2. 三层配置表

| 字段 | Layer1 (Act1) | Layer2 (Act2) | Layer3 (Act3) | 依据 |
|---|---|---|---|---|
| 对应幕 | `Overgrowth` | `Hive` | `Glory` | 默认幕 `ActModel.GetDefaultList()` |
| `Width` × `Height` | 9 × 9 | 9 × 9 | 9 × 9 | 默认 9×9；渲染按实际纵横比自适应，不做高瘦限制 |
| `ActionPoint` | **15** | **14** | **13** | `BaseNumberOfRooms`（`Overgrowth.cs:47` / `Hive.cs:47` / `Glory.cs:43`） |
| `Occupancy` | 0.65 | 0.65 | 0.65 | 节点稀疏度（约 53 格点/层 @9×9） |
| `RoadDensity` | 0.6 | 0.6 | 0.6 | 额外边密度 |
| 起点策略 | auto | auto | auto | 距 Boss 最远且最短路 ≤ AP（`StartPosition=null`） |
| 出口策略 | (Width/2, 0) | (Width/2, 0) | (Width/2, 0) | Boss 房默认顶部中心（`ExitPosition=null`） |
| `NumElites` | 5 | 5 | 5 | `MapPointTypeCounts.NumOfElites`（SwarmingElites ×1.6 → 8） |
| `NumShops` | 3 | 3 | 3 | `NumOfShops` 固定 3 |
| `NumRests` | ≈6（6~7） | ≈6（6~7） | ≈6（5~6） | Act1 `NextGaussianInt(7,1,6,7)` / Act2 `NextGaussianInt(6,1,6,7)` / Act3 `NextInt(5,7)` |
| `NumUnknowns` | ≈12（10~14） | ≈11（9~13） | ≈11（9~13） | Act1 `StandardRandomUnknownCount` / Act2、Act3 `Standard - 1` |
| `NumTreasures` | 【待定】~7 | 【待定】~7 | 【待定】~7 | 原版「倒数第 7 行」整行（约 7 个）；网状图自定义 |
| `BossHpBuffOnChase` | 0.1 | 0.1 | 0.1 | 追猎 +10% |

> 注：Act2/Act3 的 `unknownCount = StandardRandomUnknownCount - 1`，故问号略少于 Act1（`Hive.cs:120`、`Glory.cs:109`）。

---

## 3. 附：为什么 AP = 原版行数、起点→Boss 最短路 ≤ AP

### 3.1 AP = 原版行数（Act1=15 / Act2=14 / Act3=13）

- 原版地图是 **7 列 × (行数+1) 行** 的「长条」：每行 = 一个「楼层」，玩家从第 0 行（Ancient 起点）逐行上移到 Boss，**每进一个房间 `coord.row + 1`**，走到 Boss 恰好消耗「普通房间数」步。
- 原版 `BaseNumberOfRooms`：Act1(Overgrowth)=**15**、Act2(Hive)=**14**、Act3(Glory)=**13**（另 `Underdocks`=15，为 Act1 替代幕）。
- 因此「走完一整局」在原版等价于**移动 15/14/13 步**。网格图保留同样的总步数预算，手感与原版一致：**AP 取原版行数**，让「一层能做的移动总次数」与原版持平。
- 网格图里 `row` 不再单调递增（横向/回溯），所以「楼层」这个进度概念失效，改为「已消耗 AP」作为进度指标（奖励门槛 `已消耗AP > 4`、RNG 种子 `HashCode.Combine(Rng.Seed, 已消耗AP)`），见 PLAN §2.5。

### 3.2 起点→Boss 最短路 ≤ AP 约束

- 目的：**保证至少存在一条走得完的路线**——如果起点到 Boss 的最短路超过 AP，玩家必然在到 Boss 前耗尽 AP，触发「软性追猎」成为唯一结局，而非可选。
- 生成层（Phase 3）在「稀疏占用 → 连通修复 → Wilson 生成树 → road 额外边」之后，**选起点 = 距 Boss 最远、但最短路 ≤ AP 的节点**；校验阶段必须满足「全连通 / 无孤立 / 起点→Boss 最短路 ≤ AP」。
- 因为图是树 + 额外边（road density），任意两点间最短路 ≤ 树上的唯一路径长；额外边只会缩短最短路，不会破坏「≤ AP」。
- `MovementSystem.CanMove/Cost` 每步耗 1，曼哈顿距离是路线长度的下界，但**实际最短路 = 图上 BFS 长度**（受 occupancy/road 影响），故约束用「图上最短路」而非「曼哈顿距离」。

### 3.3 与 9×9 的关系

- 9×9 @ occupancy 0.65 ≈ 53 格点。起点/出口由「最短路 ≤ AP」约束在生成时确定，而不是固定在对角；因此 9×9 只是默认画布尺寸，实际可玩跨度由 AP 约束住（最长可走路线 ≈ AP + 追猎缓冲）。
- 若要更「紧凑」或更「铺开」，调 `Width/Height` 即可；渲染层按实际纵横比等比缩放+居中（patch `NMapScreen.SetMap` 布局公式），无需高瘦限制。
