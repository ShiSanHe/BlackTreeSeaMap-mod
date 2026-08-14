# PATCH_TARGETS —— 精确 patch 目标表

> 黑流树海 · STS2 网格地图 Mod（Phase 1 调研归档）
> 权威需求与方案见根目录 `PLAN.md`；本文是本 Mod 全部 Harmony patch / 桥接复用目标的精确清单。

## 数据来源说明

- **反编译源码**：`../lib/sts2-decompiled/MegaCrit/sts2/`（即 `MegaCrit.Sts2.Core` 命名空间根）。
- **游戏版本**：v0.107.1（`mod_manifest.json` 的 `min_game_version` 亦为 `0.107.1`）。
- **行号**：以下行号均已按反编译 `.cs` 逐行核对（**权威**）。`PLAN.md` 中引用的少数行号（如布局公式 `:730-738`）与本文略有出入，**以本文为准**（`PLAN.md` 为早先估读）。
- **命名空间**：所有游戏类型默认 `MegaCrit.Sts2.Core.*`，表中省略前缀。
- 签名不确定处已标注 **【待核实】**。

---

## 1. 生成替换（Map Generation Replacement）

核心思路：**不 patch 生成器**，而是注册一个 `AbstractModel` 覆写 `ModifyGeneratedMap`，返回自建的 `TreeSeaActMap : ActMap`（见 §7 数据结构桥接）。

| 文件（相对 `Core/`） | 方法 / 符号 | 完整 C# 签名（原样） | 行号 | mod 用途 | patch 方案 |
|---|---|---|---|---|---|
| `Hooks/Hook.cs` | `Hook.ModifyGeneratedMap` | `public static ActMap ModifyGeneratedMap(IRunState runState, ActMap map, int actIndex)` | 1658 | **官方地图替换点**（遍历 `IterateHookListeners`，每个 model 可换地图） | **直接复用**（不 patch）。见 `AbstractModel.ModifyGeneratedMap` |
| `Hooks/Hook.cs` | `Hook.ModifyGeneratedMapLate` | `public static ActMap ModifyGeneratedMapLate(IRunState runState, ActMap map, int actIndex)` | 1671 | 拓扑后标注（**禁止换地图实例**，多人重连存档地图也走此路） | 可选复用（quest 标记等元数据） |
| `Hooks/Hook.cs` | `Hook.AfterMapGenerated` | `public static async Task AfterMapGenerated(IRunState runState, ActMap map, int actIndex)` | 637 | 生成后回调 | 可选复用 |
| `Models/AbstractModel.cs` | `AbstractModel.ModifyGeneratedMap` | `public virtual ActMap ModifyGeneratedMap(IRunState runState, ActMap map, int actIndex)` | 1613 | **覆写目标**：`TreeSeaMapModel` 继承 `AbstractModel` 后覆写，返回 `TreeSeaActMap` | **直接桥接复用**（继承覆写，无需 patch） |
| `Models/AbstractModel.cs` | `AbstractModel.ModifyGeneratedMapLate` | `public virtual ActMap ModifyGeneratedMapLate(IRunState runState, ActMap map, int actIndex)` | 1627 | 覆写目标（晚期） | 桥接复用 |
| `Models/AbstractModel.cs` | `AbstractModel.AfterMapGenerated` | `public virtual Task AfterMapGenerated(ActMap map, int actIndex)` | 799 | 生成后钩子 | 可选复用 |
| `Modding/ModHelper.cs` | `ModHelper.SubscribeForRunStateHooks` | `public static void SubscribeForRunStateHooks(string id, RunHookSubscriptionDelegate del)` | 100 | 注册 mod 的 `AbstractModel`（返回 `IEnumerable<AbstractModel>`） | **直接复用** |
| `Modding/RunHookSubscriptionDelegate.cs` | `RunHookSubscriptionDelegate` | `public delegate IEnumerable<AbstractModel> RunHookSubscriptionDelegate(RunState runState);` | 7 | 委托签名 | 直接复用 |
| `Runs/RunManager.cs` | `RunManager.GenerateMap` | `public async Task GenerateMap()` | 724 | 生成流程入口（`State.Act.CreateMap` → `Hook.ModifyGeneratedMap` → `State.Map = map` → `NMapScreen.SetMap`） | 不 patch，仅理解流程 |
| `Models/ActModel.cs` | `ActModel.CreateMap` | `public ActMap CreateMap(RunState runState, bool replaceTreasureWithElites)` | 522 | 默认建图（内部 `StandardActMap.CreateFor`） | 不 patch（被 `ModifyGeneratedMap` 替换） |
| `Models/ActModel.cs` | `ActModel.GetMapPointTypes` | `public abstract MapPointTypeCounts GetMapPointTypes(Rng mapRng)` | 517 | 各幕事件密度来源 | 只读参考（见 LAYER_CONFIG.md） |
| `Map/StandardActMap.cs` | `StandardActMap.CreateFor` | `public static StandardActMap CreateFor(RunState runState, bool replaceTreasureWithElites)` | 111 | 原版建图（将被替换） | 只读参考 |

---

## 2. 移动门禁（Movement Gating）

原版 `MapTravel.GetTravelablePointsFrom` 是「自由旅行 → 下一行全部；否则 → `Children`」。网格图四向移动 + 回溯要改这里。

| 文件 | 方法 / 符号 | 完整 C# 签名（原样） | 行号 | mod 用途 | patch 方案 |
|---|---|---|---|---|---|
| `Map/MapTravel.cs` | `MapTravel.GetTravelablePointsFrom` | `public static IEnumerable<MapPoint> GetTravelablePointsFrom(IRunState runState, MapPoint currentPoint)` | 14 | **四向可达点** | **Harmony Postfix**：用 `TreeSeaActMap` 提供的「四方向邻居（有双向边）」替换返回值；回溯时含已访问格 |
| `Map/MapTravel.cs` | 自由旅行分支 | `if (Hook.ShouldAllowFreeTravel(runState)) { return runState.Map.GetPointsInRow(currentPoint.coord.row + 1); }` | 16-19 | 理解原版门禁逻辑 | 只读参考（网格图不走该分支） |

---

## 3. 进房与 AP（Room Entry & Action Points）

| 文件 | 方法 / 符号 | 完整 C# 签名（原样） | 行号 | mod 用途 | patch 方案 |
|---|---|---|---|---|---|
| `Runs/RunManager.cs` | `RunManager.EnterMapCoord` | `public Task EnterMapCoord(MapCoord coord)` | 758 | **移动/进房入口** | **Harmony Prefix/Postfix**：移动扣 1 AP；`AddVisitedMapCoord` 返回 false（回溯）时走「只移 marker、不重触发」分支 |
| `Runs/RunManager.cs` | `RunManager.EnterMapCoordInternal` | `private Task EnterMapCoordInternal(MapCoord coord, AbstractRoom? preFinishedRoom, bool saveGame)` | 788 | 进房内层（`coord.row + 1` 传给 `EnterMapPointInternal`） | 参考；网格图 `row+1` 不再代表进度 |
| `Runs/RunManager.cs` | `RunManager.EnterMapPointInternal` | `public async Task EnterMapPointInternal(int actFloor, MapPointType pointType, AbstractRoom? preFinishedRoom, bool saveGame)` | 801 | 进房核心；`State.ActFloor = actFloor`（813 行） | 参考；AP 扣减/追猎状态在此上下游接线 |
| `Runs/RunState.cs` | `RunState.AddVisitedMapCoord` | `public bool AddVisitedMapCoord(MapCoord coord)` | 460 | 防重进（已含则返回 `false`） | **patch**：回溯时「已访问格只移 marker」需在此/`EnterMapCoord` 处理 |
| `Runs/RunState.cs` | `RunState.ActFloor` | `public int ActFloor { get; set; }` | 152 | 原版楼层进度（`row+1`） | **用「已消耗 AP」替代**（奖励门槛 / RNG 种子），Phase 5 落实 |
| `Runs/RunState.cs` | `RunState.CurrentMapPoint` | `public MapPoint? CurrentMapPoint` | 127 | 当前所在点 | 参考 |
| `Runs/RunManager.cs` | `RunManager.RollRoomTypeFor` | `private RoomType RollRoomTypeFor(MapPointType pointType, IEnumerable<RoomType> blacklist)` | 902 | 节点类型 → 房间类型映射 | 只读参考（6 类映射由 `PointType` 天然驱动） |
| `Runs/RunManager.cs` | `RunManager.CreateRoom` | `private AbstractRoom CreateRoom(RoomType roomType, MapPointType mapPointType = MapPointType.Unassigned, AbstractModel? model = null)` | 867 | 创建房间 | 只读参考（Boss→`CombatRoom`、Treasure→`TreasureRoom` 等） |

---

## 4. 结算与追猎（Rewards & Chase Battle）

追猎检测时机 = **房间结算后从奖励屏返回地图屏**（`ProceedFromTerminalRewardsScreen`）；Boss +10% = patch 怪物生成。

| 文件 | 方法 / 符号 | 完整 C# 签名（原样） | 行号 | mod 用途 | patch 方案 |
|---|---|---|---|---|---|
| `Runs/RunManager.cs` | `RunManager.ProceedFromTerminalRewardsScreen` | `public async Task ProceedFromTerminalRewardsScreen()` | 1329 | **追猎检测点**（奖励屏→地图屏） | **Harmony Postfix**：结算后 `AP==0` 且不在 Boss 房 → 强制进 Boss 房（+10%）；`AP==1` 选非 Boss 房 → 进房交互完再追猎 |
| `Combat/CombatState.cs` | `CombatState.CreateCreature` | `public Creature CreateCreature(MonsterModel monster, CombatSide side, string? slot)` | 232 | **Boss +10% 属性点** | **Harmony Postfix**：判定 Boss 遭遇后 `MaxHp/CurrentHp ×1.1`。【待核实】Boss 判定方式（`Encounter?.RoomType == RoomType.Boss` 或 `monster` 来自 Boss 遭遇） |
| `Entities/Creatures/Creature.cs` | `Creature` 构造（怪物） | `public Creature(MonsterModel monster, CombatSide side, string? slotName)` | 346 | HP 初始化（`_maxHp = maxInitialHp; _currentHp = maxInitialHp;` 358-359） | 只读参考（+10% 在 `CreateCreature` 后或此 ctor 后） |
| `Entities/Creatures/Creature.cs` | `Creature.ScaleMonsterHpForMultiplayer` | `public void ScaleMonsterHpForMultiplayer(EncounterModel? encounter, int playerCount, int actIndex)` | 385 | 多人 HP 缩放（在 `CreateCreature` 内调用） | 参考 |
| `Models/ActModel.cs` | `ActModel.PullNextEncounter` | `public EncounterModel PullNextEncounter(RoomType roomType)` | 445 | Boss 遭遇来源（`RoomType.Boss` → `_rooms.NextBossEncounter`） | 参考 |

---

## 5. 换幕（Act Transition）

Boss 胜利 → 奖励 → 投票换幕 → 下一幕 `GenerateMap()`。本 Mod 沿「Boss 战胜利 → 直接下一层（下一幕）」复用原版流程，**不 patch**。

| 文件 | 方法 / 符号 | 完整 C# 签名（原样） | 行号 | mod 用途 | patch 方案 |
|---|---|---|---|---|---|
| `Runs/RunManager.cs` | `RunManager.EnterNextAct` | `public async Task EnterNextAct()` | 1209 | Boss 胜利后换幕入口 | 复用 |
| `Runs/RunManager.cs` | `RunManager.EnterAct` | `public async Task EnterAct(int currentActIndex, bool doTransition = true)` | 1251 | 换幕 | 复用 |
| `Runs/RunManager.cs` | `RunManager.SetActInternal` | `public async Task SetActInternal(int actIndex)` | 1289 | 设置当前幕 | 复用 |
| `GameActions/VoteToMoveToNextActAction.cs` | `VoteToMoveToNextActAction` | `public VoteToMoveToNextActAction(Player player)` | 25 | Boss 胜利投票换幕 | 复用 |
| `Multiplayer/Game/ActChangeSynchronizer.cs` | `ActChangeSynchronizer` | `public ActChangeSynchronizer(RunState runState)` | 22 | 换幕同步 | 复用 |
| `GameActions/MoveToMapCoordAction.cs` | `MoveToMapCoordAction` | `public MoveToMapCoordAction(Player player, MapCoord destination)` | 29 | 移动裁决（投票后 host 执行） | 复用 |

---

## 6. 渲染布局（Rendering & Layout）

原版 `NMapScreen` 按「长条」（7 列 × 行数）硬编码，全部集中在一文件。**先 patch 布局公式，视觉不足再自建屏**。

| 文件 | 方法 / 符号 | 完整 C# 签名 / 常量（原样） | 行号 | mod 用途 | patch 方案 |
|---|---|---|---|---|---|
| `Nodes/Screens/Map/NMapScreen.cs` | 布局常量 | `_scrollLimitTop = 1800f` / `_scrollLimitBottom = -600f` / `_totalHeight = 2325f` / `_totalWidth = 1050f` | 546-556 | 长条布局硬编码 | **patch**：替换为按实际纵横比等比缩放 |
| 同上 | `NMapScreen.SetMap` | `public void SetMap(ActMap map, uint seed, bool clearDrawings)` | 715 | 布局公式所在 | **Harmony Postfix / Transpiler**：重算布局 |
| 同上 | 布局公式 | `_distY = 2325f / (float)(rowCount - 1) * num;` / `_distX = 1050f / (float)columnCount;` | 730-731 | 网格纵横比失真根源 | **patch**（`num` = 双 Boss 0.9 缩放，729 行） |
| 同上 | 普通点坐标 | `new Vector2(allMapPoint.coord.col, allMapPoint.coord.row) * vector2 + vector`（`vector2 = (_distX, -_distY)`、`vector = (-500f, 740f)`） | 733-738 | 等比缩放 + 居中 | **patch** |
| 同上 | Boss 固定坐标 | `_bossPointNode.Position = new Vector2(-200f, -1980f * num);` | 747 | Boss 位置（固定） | **patch**（按网格坐标定位） |
| 同上 | Ancient/起点固定坐标 | `_startingPointNode.Position = new Vector2(-80f, (float)map.StartingMapPoint.coord.row * (0f - _distY) + 720f);` | 762 | 起点位置（固定） | **patch** |
| 同上 | `NMapScreen.DrawPaths` | `private void DrawPaths(NMapPoint mapPointNode, MapPoint mapPoint)` | 830 | 只画 `mapPoint.Children`（自下而上单向） | **patch**：网格双向边（正反两方向连线各画一次） |
| 同上 | `NMapScreen.RecalculateTravelability` | `private void RecalculateTravelability()` | 854 | 可达点状态 | **patch**：末行→Boss 假设不适用于网格 |
| 同上 | 末行→Boss 假设 | `if (mapCoord.row == _map.GetRowCount() - 1) { _bossPointNode.State = MapPointState.Travelable; return; }` | 878-881 | 网格无「末行」概念 | **patch**：Boss 可达改为「与 Boss 有边」 |
| 同上 | `NMapScreen.TravelToMapCoord` | `public async Task TravelToMapCoord(MapCoord coord)` | 996 | 移动动画与裁决 | 复用 |
| 同上 | `NMapScreen.OnMapPointSelectedLocally` | `public void OnMapPointSelectedLocally(NMapPoint point)` | 915 | 点击 → 投票 | 复用（不 patch） |

可复用组件（自建屏备选）：`NNormalMapPoint` / `NBossMapPoint` / `NAncientMapPoint`（继承 `NMapPoint`）、`NMapMarker`、`NMapBg`、`NMapDrawings`、`NMapLegendItem`。接入点：`NMapScreen.Instance => NRun.Instance?.GlobalUi.MapScreen`（594 行）。

---

## 7. 数据结构桥接（Data Structure Bridge）

`TreeSeaActMap : ActMap` 把 `LayerGrid` 转成 `MapPoint` 图（四方向树边双向）。以下均为**直接继承 / 直接复用**，无 patch。

| 文件 | 方法 / 符号 | 完整 C# 签名（原样） | 行号 | mod 用途 | patch 方案 |
|---|---|---|---|---|---|
| `Map/ActMap.cs` | `ActMap` 抽象基类 | `public abstract MapPoint BossMapPoint { get; }` / `public abstract MapPoint StartingMapPoint { get; }` / `public virtual MapPoint? SecondBossMapPoint => null;` / `protected abstract MapPoint?[,] Grid { get; }` | 12 / 14 / 20 / 22 | `TreeSeaActMap` 继承实现 | 直接继承 |
| 同上 | 查询方法 | `public int GetColumnCount()` / `public int GetRowCount()` / `public IEnumerable<MapPoint> GetAllMapPoints()` / `public MapPoint? GetPoint(int col, int row)` / `public IEnumerable<MapPoint> GetPointsInRow(int row)` / `public bool HasPoint(MapCoord coord)` | 24 / 29 / 34 / 70 / 49 / 106 | 渲染与移动查询 | 直接复用 |
| `Map/MapPoint.cs` | `MapPoint` | `public MapPoint(int col, int row)` / `public MapCoord coord` / `public MapPointType PointType { get; set; }` / `public bool CanBeModified { get; set; } = true;` / `public readonly HashSet<MapPoint> parents` / `public HashSet<MapPoint> Children { get; }` / `public void AddChildPoint(MapPoint child)` / `public void RemoveChildPoint(MapPoint child)` | 44 / 14 / 22 / 20 / 12 / 26 / 52 / 58 | 建边（双向 = 两次 `AddChildPoint`） | 直接复用 |
| `Map/MapCoord.cs` | `MapCoord` | `public struct MapCoord(int col, int row) : IEquatable<MapCoord>, IComparable<MapCoord>, IPacketSerializable` | 11 | 坐标（`col`/`row` 各为 `int`） | 直接复用 |
| `Map/MapPointType.cs` | `MapPointType` 枚举 | `Unassigned, Unknown, Shop, Treasure, RestSite, Monster, Elite, Boss, Ancient` | 7-18 | 节点类型映射（见 PLAN §2.3） | 直接复用 |
| `Map/MapPointState.cs` | `MapPointState` 枚举 | `None, Travelable, Traveled, Untravelable` | 3-9 | 节点状态（与类型分离） | 直接复用 |
| `Map/MapPointTypeCounts.cs` | `MapPointTypeCounts` | `public int NumOfElites { get; init; }` / `public int NumOfShops { get; } = 3;` / `public int NumOfUnknowns { get; }` / `public int NumOfRests { get; }` / `public static int StandardRandomUnknownCount(Rng rng)` | 14 / 16 / 18 / 20 / 30 | 事件密度（精英=round(5×SwarmingElites?1.6:1)；问号=`NextGaussianInt(12,1,10,14)`） | 参考（数据值） |

---

## 8. RitsuLib 与工程

| 文件 | 方法 / 符号 | 完整 C# 签名（原样） | 行号 | mod 用途 | patch 方案 |
|---|---|---|---|---|---|
| `../lib/STS2-RitsuLib/src/RitsuLibFramework.cs` | `RitsuLibFramework.GetRunSavedDataStore` | `public static RunSavedDataStore GetRunSavedDataStore(string modId)` | 682 | **AP 持久化**（每层独立、层间重置） | **直接复用** |
| 同上 | `RitsuLibFramework.CreateContentPack` | `public static ModContentPackBuilder CreateContentPack(string modId)` | 1222 | 内容注册（可选） | 直接复用 |
| 同上 | `RitsuLibFramework.CreatePatcher` | `public static ModPatcher CreatePatcher(string ownerModId, string patcherName, string? patcherLabel = null, LogType logType = LogType.Generic)` | 1695 | 补丁器（可选，替代直接 `new Harmony(...)`） | 直接复用 |

**工程（已存在，Phase 2 骨架）**：

- `TreeSeaMap.csproj`：`Microsoft.NET.Sdk`、`net9.0`、`EnableDynamicLoading`、`<Reference>` 指向 `sts2.dll` / `0Harmony.dll` / `GodotSharp.dll` / `SmartFormat.dll` / `STS2-RitsuLib.dll`。
- `mod_manifest.json`：`id=treeseamap`、`has_dll=true`、`has_pck=false`、`dependencies:[{id:"STS2-RitsuLib"}]`、`min_game_version=0.107.1`。
- `source/Entry.cs`：`[ModInitializer("Init")]` + `new Harmony("sts2.treeseamap").PatchAll()`。

---

## 9. 风险与未知

- **反编译只含代码**（3425 个 `.cs`），**无 `.tscn/.tres/.png`**：游戏资源在安装目录 `Slay the Spire 2.pck`（可能加密）。编译 / patch / 运行时**不受影响**；自建屏研究需解包 pck 或用「代码推断 + 运行时场景树 dump」。
- **版本漂移**：所有行号/签名基于 **v0.107.1**；游戏更新后需重跑 `ilspycmd` 核对，尤其 `NMapScreen.SetMap` 布局公式（本 Mod 最脆弱的 patch 点）。
- **Boss +10% 判定【待核实】**：`CombatState.CreateCreature` 内如何可靠识别「Boss 遭遇」尚未验证——候选：`Encounter?.RoomType == RoomType.Boss`（需确认 `Encounter` 属性在生成时机已就绪）或 `monster` 类型判断。
- **宝箱数量待定**：原版是「倒数第 7 行整行」（`AssignPointTypes` 中 `GetRowCount()-7`），网状图无行概念，需自定义 `NumTreasures`（当前 `LayerConfig.cs` 占位为 1，**待 Phase 4 定值**）。
- **ActFloor 替代**：`NRewardsScreen.cs:580 if (_runState.ActFloor > 4)`（奖励门槛）与 `MapSplitVoteAnimation.cs:68 new Rng((uint)HashCode.Combine(_runState.Rng.Seed, _runState.ActFloor))`（RNG 种子）两处依赖「楼层」，网格图需改用「已消耗 AP」——实现落点 Phase 5。
- **回溯实现细节**：`RunState.AddVisitedMapCoord` 返回 `false` 的语义（已访问格）需 patch，使「已访问格只移 marker、不重触发」。
- **双 Boss（Ascension 10+）**：`SecondBossMapPoint` 在网格图下的位置与渲染（`NMapScreen` 双 Boss 缩放 0.75 / 坐标 -2280）尚未设计。
- **多人**：`GetNumberOfRooms(isMultiplayer)` 减 1；网格图 AP 是否随多人调整待定。投票流程沿用原版（`MapSelectionSynchronizer`），本 Mod 不另做。
