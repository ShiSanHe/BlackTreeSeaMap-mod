# 黑流树海 · STS2 网格地图 Mod — 项目计划（PLAN）

> 本文件是项目的**权威计划文档**（需求规格 + 技术方案 + 分阶段实施），供任何新对话直接接手。
> 早期需求草稿 `map.md` 已删除并融入本文；本文的「需求规格」是唯一权威版本。
> 技术事实均来自反编译源码 `../lib/sts2-decompiled/`（游戏版本 v0.107.1）。

---

## 1. 项目概述

为《杀戮尖塔2》(STS2) 做大型地图 mod：把原版**线性自下而上**的地图改为**多层网格自由探索**，参考《明日方舟·沉沦者的黑流树海》肉鸽。

- **Mod 名**：TreeSeaMap / 黑流树海
- **API 库**：RitsuLib（`../lib/STS2-RitsuLib/STS2-RitsuLib.dll`），**不用** BaseLib
- **Mod 结构参考**：`../guzhenren`（不照抄）
- **扩展方式**：官方 `ModifyGeneratedMap` 替换地图 + Harmony patch 移动/渲染/战斗
- **验收里程碑**：**Phase 6**（进游戏能玩：网格地图、6 种节点、四向移动+回溯、AP 消耗、追猎战）

---

## 2. 需求规格（当前权威）

### 2.1 多层结构，层内无方向进度
- **整个游戏流程只有 3 层，对应原版 3 幕**：`Layer1(Act1) → Layer2(Act2) → Layer3(Act3)`；**打完第 3 层 Boss 即通关结算**（复用原版结算流程）
- **层与层之间是明确进度关系**（Boss 战胜利 → 下一层 = 下一幕）；层内 X/Y 方向**不代表进度**（左右不是前进/后退，上下不是更深/更浅）
- 每层是一张独立网格地图；每层可配置：大小、行动力、事件密度、出口规则等（`LayerConfig`，禁止硬编码所有层）

### 2.2 网格与节点（Grid 是树）
- 地图建模为二维规则网格坐标空间（`Grid[x][y]`）；游戏内部地图本就是 `MapPoint[col,row]` 网格，坐标模型天然兼容
- 图论上是**树/连通图**：任意两格点可达、无孤立点
- **环不特意生成**（用户确认"环不重要"）；但可通过**额外边（road density）**增加枝岔与可走路，形成的环是副产品

### 2.3 节点类型（初始 6 种，无空白）
- **所有格点都有事件，无空白节点**（修正 map.md §24"大量格点可无事件"）
- 初始类型：
  | 类型 | 说明 | 游戏映射 |
  |---|---|---|
  | 战斗 BATTLE | 普通战斗 | `Monster` |
  | 问号 UNKNOWN | 随机事件 | `Unknown` |
  | 篝火 REST | 休息 | `RestSite` |
  | 商店 SHOP | 购买 | `Shop` |
  | 宝箱 TREASURE | 奖励 | `Treasure` |
  | 精英 ELITE | 强战斗、奖励更好 | `Elite` |
- 出口 = **Boss 房**，是一个特殊节点（`EXIT` → `Boss`）
- **节点类型与状态分离**：`Content`（类型）与 `State`（ACTIVE/COMPLETED 等）是两个字段

### 2.4 事件分配规则（按原版密度随机，无保底）
- **先生成节点拓扑（空地图），再对每个节点分配事件**——两阶段分离（用户明确要求）
- **无保底关键节点、无行约束**：网状地图不沿用原版"末行=篝火/倒数第7行=宝箱/第1行=战斗"的行固定
- 按**原版密度**随机分配（数据来自 `MapPointTypeCounts.cs` + 各章 `GetMapPointTypes`）：
  - 精英 = 5（固定；SwarmingElites 升阶 ×1.6）
  - 商店 = 3（固定）
  - 篝火 ≈ 6（Act1 高斯 6-7 / Act2 6 / Act3 5-6）
  - 问号 = 10~14（`NextGaussianInt(12,1,10,14)`）
  - 宝箱 = 每层数量**待定**（原版是"倒数第7行"整行固定，网状图需自定义数量）
  - **其余全部 = 战斗**（原版未分配点默认 Monster）

### 2.5 行动力（AP）与追猎战
- **移动 = 沿图边四方向逐格移动，每步耗 1 行动力**（曼哈顿距离 = 路线长度的基本度量）
- **AP = 原版行数**：Act1=15 / Act2=14 / Act3=13（`BaseNumberOfRooms`）
- **AP 当前不可恢复**（无回满/增加机制）；后续 phase 可能加新机制；**每层 AP 独立，进下一层重置**
- **生成约束：起点→Boss 最短路 ≤ AP**（保证至少存在一条走得完的路线）
- **回溯**：可走回已访问格，只移 marker、**不重触发事件**
- **追猎战**（Boss 是出口节点，AP 耗尽=软性推向出口；检测时机 = **房间结算时**，即 `RunManager.ProceedFromTerminalRewardsScreen` 从奖励屏返回地图屏时）：
  1. 结算后 **AP=0** 且未在 Boss 房 → **直接强制进入 Boss 房**开始战斗，Boss 属性 +10%
  2. 结算后 **AP=1** 且选了非 Boss 房 → **先进入所选房间交互**（进房时 AP 扣到 0）→ 交互完结算 → 再触发追猎进 Boss 房
  3. **AP 耗尽前就已打到 Boss 房** → Boss 战胜利 → **直接进入下一层**（Boss 是出口节点，本层结束）
- **多人**：投票适用，**所有人一起行动**（同地图、同步结算），无其他特例
- **进度指标 = 已消耗 AP（替代原版 ActFloor/楼层数）**：原版用"楼层数"判断进度（奖励门槛 `ActFloor > 4`、RNG 种子 `HashCode.Combine(Rng.Seed, ActFloor)`）；网格图无楼层概念，改用**已消耗 AP**——奖励门槛 = **"移动消耗超过 4 点行动力"**（`已消耗AP > 4` 取代 `ActFloor > 4`），RNG 种子 = `HashCode.Combine(Rng.Seed, 已消耗AP)`

### 2.6 起点与出口
- 每层存在起点 Start 与出口 Exit(Boss)；层内"进度"由**起点到出口的可达关系**决定，不由方向决定
- 起点默认 = **距 Boss 最远但最短路 ≤ AP 的节点**（可配置/随机）

### 2.7 渲染与显示（玩家怎么看地图）
- 原版地图屏 `NMapScreen` 的长条布局硬编码集中在一个文件，**先 patch 后升级**：
  1. **patch 布局公式**：让网格地图按实际纵横比**等比缩放+居中**显示（替换原版 `_distX=1050/列数`、`_distY=2325/(行数-1)`）
  2. 修正 Boss/Ancient 固定坐标、DrawPaths 双向连线、滚动自适应、末行→Boss 可达假设
  3. 复用原版全部交互（点击/投票/marker/状态色/选中特效）
  4. 若 patch 后视觉不足 → 升级**自建地图屏**（复用原版 `NMapPoint` 组件，选点接回投票管线）
- 滚动**不是重点**（用户确认）；关键是要显示正常、玩家能看懂地图
- **本阶段无视野/迷雾，所有节点透明可见**

### 2.8 每层 LayerConfig
```csharp
LayerConfig {
  width, height,            // 矩形可调（渲染层自适应，不做高瘦限制）
  actionPoint,              // AP = 原版对应行数（Act1=15/Act2=14/Act3=13）
  startingPosition, exitPosition,   // 或 auto（距 Boss 最远但最短路 ≤ AP）
  occupancy,                // 节点稀疏度(0-1)
  roadDensity,              // 额外边密度(0-1)，枝岔/可走路更多；环是副产品
  eventCounts,              // 事件数量（精英5/商店3/篝火~6/问号~12/宝箱待定，其余战斗）
  bossHpBuffOnChase,        // 追猎 +10%
  mode,                     // Phase 9：procedural | fixed
}
```

### 2.9 暂不实现 / 后续里程碑
- **传送点**（跳到已探索/其他区域）、**羽瞰点**（揭示周围）——Phase 8
- **曲折密道**（额外 Teleport Edge）——后续
- **加工品 = 遗物（relic）**，**不是地图节点**——修改移动规则/奖励系统（map.md §18），后续
- **视野/迷雾/未知状态**——暂不做，全可见
- **空白节点**——暂不做，全格点有事件

---

## 3. 技术背景（反编译源码事实）

### 3.1 地图生成流程
```
RunManager.GenerateMap()                    Runs/RunManager.cs:724
  → State.Act.CreateMap(State,false)        Models/ActModel.cs:522
  → StandardActMap.CreateFor(runState,...)  Map/StandardActMap.cs:111
  → Hook.ModifyGeneratedMap(runState,map,actIndex)   Hooks/Hook.cs:1658  ★官方替换点
      遍历 runState.IterateHookListeners(null)，对每个 AbstractModel 调 ModifyGeneratedMap()
      （注释：To replace the map, use ModifyGeneratedMap）
  → State.Map = map; NMapScreen.SetMap(map,seed,true)
```

### 3.2 数据结构
- `MapPoint`：`coord`、`PointType`、`CanBeModified`、`parents:HashSet`、`Children:HashSet`、`AddChildPoint/RemoveChildPoint`
- `MapCoord{col,row}`；`MapPointType`：Unassigned/Unknown/Shop/Treasure/RestSite/Monster/Elite/Boss/Ancient
- `MapPointState`：None/Travelable/Traveled/Untravelable
- `ActMap` 抽象基类：`Grid`、`BossMapPoint`、`StartingMapPoint`、`GetPoint/GetPointsInRow/GetAllMapPoints/GetColumnCount/GetRowCount`
- **原版地图尺寸**：7 列固定 × `(BaseNumberOfRooms+1)` 行；Act1(Overgrowth)=16、Act2(Hive)=15、Act3(Glory)=14（普通房间 15/14/13）；多人减 1

### 3.3 移动与交互管线
```
MapTravel.GetTravelablePointsFrom(runState, currentPoint)   Map/MapTravel.cs:14  ★移动门禁
  → 默认返回 currentPoint.Children（自由旅行模型则返回下一行全部）
NMapScreen.RecalculateTravelability()   Nodes/Screens/Map/NMapScreen.cs:854
  → 全标 Untravelable → 已访问标 Traveled → 最后访问点的可达点标 Travelable（末行时 Boss 可达）
点击 → OnMapPointSelectedLocally → VoteForMapCoordAction → MapSelectionSynchronizer
  →（host）MoveToMapCoordAction → NMapScreen.TravelToMapCoord → RunManager.EnterMapCoord
  → AddVisitedMapCoord（已访问则 false）→ EnterMapPointInternal(coord.row+1, pointType,…)
  → RollRoomTypeFor → CreateRoom → EnterRoom
```
- **Boss 胜利 → 奖励 → VoteToMoveToNextActAction → ActChangeSynchronizer → 下一幕 → GenerateMap()**
- **ActFloor（当前楼层）——解释**：
  - 原版地图是 7 列 ×(行数+1) 行的网格，**每行 = 一个"楼层"**；玩家从第 0 行（Ancient 起点）出发，每进一个房间 `coord.row`+1
  - `ActFloor = coord.row + 1`（RunManager.cs:813 赋值），即"当前在第几行"；换幕时 `ActFloor++`（ActChangeSynchronizer.cs:68）
  - **用途 1 · 奖励门槛**：`NRewardsScreen.cs:580 if (_runState.ActFloor > 4)`——楼层超 4 才发某类奖励（进度判断）
  - **用途 2 · RNG 种子**：`HashCode.Combine(Rng.Seed, ActFloor)`（MapSplitVoteAnimation.cs:68 / EventSplitVoteAnimation.cs:49）——投票/事件随机以"游戏种子+当前楼层"为种子
  - **网格图下的替代 = 已消耗 AP（用户拍板）**：原版 `row` 越大=离 Boss 越近，`ActFloor=row+1` 天然是进度指标；网格图自由探索，横向/回溯时 `row` 不单调递增、不代表进度。改用**已消耗 AP** 当进度指标：
    - **奖励门槛**：原版"超过 4 层"→ **"移动消耗超过 4 点行动力"**（`已消耗AP > 4` 取代 `ActFloor > 4`）
    - **RNG 种子**：改用 `HashCode.Combine(Rng.Seed, 已消耗AP)`（种子仍共用游戏 RNG）
  - → 技术实现由实施者（我）探索，Phase 5 落实
- 单机也走"投票→裁决→移动"全流程

### 3.4 渲染层硬编码（NMapScreen.cs，集中在一文件）
- 常量 `_totalHeight=2325/_totalWidth=1050`、滚动钳制 `_scrollLimitTop=1800/_scrollLimitBottom=-600`（:546-552）
- 布局公式 `_distY=2325/(rowCount-1)`（:730）、`_distX=1050/columnCount`（:731）、`Position=(col,row)*(_distX,-_distY)+offset(-500,740)`（:733-738）
- Boss 固定 `(-200,-1980)`（:747）、Ancient 固定 `(-80,row*-_distY+720)`（:762）
- `RecalculateTravelability` 末行→Boss 假设（:878-881）
- `DrawPaths` 只画 `mapPoint.Children`（自下而上，:830-843）——网格双向边需扩展
- 可复用组件：`NNormalMapPoint/NBossMapPoint/NAncientMapPoint`（继承 NMapPoint，从 .tscn 实例化）、`NMapMarker`、`NMapNodeSelectVfx/NMapCircleVfx`、`NMultiplayerVoteContainer`、`NMapBg`、`NMapDrawings`
- 接入点：`NMapScreen.Instance = NRun.Instance?.GlobalUi.MapScreen`（:594，run.tscn `%MapScreen`）；**无官方 UI 替换 hook**；mod 可 `PreloadManager.Cache.GetScene("...").Instantiate<T>()` 运行时建节点

### 3.5 扩展点
| 目标 | 方式 |
|---|---|
| 替换整张地图 | 注册 `AbstractModel` 覆写 `ModifyGeneratedMap`；`ModHelper.SubscribeForRunStateHooks(id, (RunState)→IEnumerable<AbstractModel>)` |
| 改变可达点 | patch `MapTravel.GetTravelablePointsFrom` |
| 移动/扣 AP | patch `RunManager.EnterMapCoord` / `NMapScreen.TravelToMapCoord` |
| 回溯/重进格 | patch `NMapScreen.RecalculateTravelability` / `MapTravel` |
| 渲染布局 | patch `NMapScreen.SetMap` 布局公式 / Boss/Ancient 坐标 / DrawPaths |
| 追猎检测 | patch 房间结算点（`RunManager.ProceedFromTerminalRewardsScreen` 返回地图屏），AP=0 → 强制进 Boss 房 |
| 追猎 +10% | patch Boss 遭遇/属性构造 |

### 3.6 Mod 基础设施
- 入口：`[ModInitializer("Init")]` 静态类 + `Init()`；`new Harmony("sts2.treeseamap").PatchAll()`
- 工程：`Microsoft.NET.Sdk`；net9.0、`EnableDynamicLoading`、`<PackageReference GodotSharp 4.4.0 / Lib.Harmony 2.4.2>`、`<Reference>` 指向 sts2.dll/0Harmony.dll/SmartFormat.dll + `STS2-RitsuLib.dll`
- 清单 `mod_manifest.json`：`id=treeseamap`、`has_dll=true`、`has_pck=false`、`dependencies:[{id:"STS2-RitsuLib"}]`
- RitsuLib：`RitsuLibFramework.CreateContentPack/CreatePatcher/GetRunSavedDataStore`
- 部署：`mods/TreeSeaMap/`；**RitsuLib 运行时需 `mods/STS2-RitsuLib/`**（当前未装）
- **反编译只含代码**（3425 个 .cs，无 .tscn/.tres/.png/project.godot）；游戏资源在安装目录 `Slay the Spire 2.pck`。mod 编译/运行时不受影响；研究/自建屏参考可解包 pck（GodotPCKExplorer/godotpcktool，可能加密）

---

## 4. 架构设计（四层）

```
生成层  ProceduralGridGenerator / 纯逻辑、可单测，不碰游戏代码
   │    稀疏占用 → 连通修复(曼哈顿桥) → Wilson随机生成树 → road密度额外边
   │    → 起点(距Boss最远且最短路≤AP) → 校验(全连通/无孤立/最短路≤AP) → 事件分配
   ▼
桥接层  TreeSeaActMap : ActMap —— 把 LayerGrid 转成 MapPoint 图（四方向树边双向）
   │    StartingMapPoint=Start、BossMapPoint=Exit；PointType 映射见 §2.3
   ▼
接入层  TreeSeaMapModel(ModifyGeneratedMap) + Harmony patches
   │    MapTravel / RecalculateTravelability / EnterMapCoord / ActFloor
   │    NMapScreen 布局公式 / Boss/Ancient 坐标 / DrawPaths
   ▼
系统层  ActionPointSystem(AP) · ChaseBattleSystem(追猎+10%) · LayerProgressSystem(换层)
```

- **数据契约**：`LayerGrid` 是生成层与桥接层之间的唯一接口，算法（程序/固定地图）可随时替换
- 桥接只要边建对，原版 `SetMap` 渲染、点击、进房、换幕自动工作；渲染比例由接入层 patch 兜底

---

## 5. 分阶段实施

| Phase | 内容 | 状态 |
|---|---|---|
| 0 | 反编译 sts2.dll → `../lib/sts2-decompiled/`（3425 .cs） | ✅ 已完成 |
| 1 | 调研归档：`docs/PATCH_TARGETS.md`（精确 patch 签名）、`docs/LAYER_CONFIG.md`（各层配置草案） | ⏳ |
| 2 | Mod 骨架：csproj / manifest / Entry / deploy.sh；游戏 Mods 列表出现、加载无报错 | ⏳ |
| 3 | 生成器·网格拓扑（纯逻辑+单测，不含事件）：Node/MapGraph/LayerConfig/GridGenerator + 可视化 `map.html`（已实现） | ⏳ |
| 4 | 事件分配：`EventAssigner.cs` 6 种类型按原版密度随机、无保底；单测 | ⏳ |
| 5 | 桥接+渲染接入（决策门）：TreeSeaActMap / TreeSeaMapModel / MapTravel 等 patch / NMapScreen 布局 patch；**里程碑：进游戏看到纵横比正常的网格地图** | ⏳ |
| 6 | 行动力+追猎战：ActionPointSystem(AP 存 RitsuLib RunSavedData) / ChaseBattleSystem(Boss+10%) / AP HUD | ⏳ **验收里程碑** |
| 7 | 层级推进+打磨：LayerProgressSystem / 调参 / README | 🔜 |
| 8 | 新节点类型：传送点(TELEPORT)、羽瞰点(SCOUT)；加工品=relic（改奖励系统） | 🔜 |
| 9 | 生成算法变更 / 固定地图：`LayerConfig.mode`、手绘 JSON 模板、`map_editor.html` | 🔜 |

### Phase 详细说明
- **Phase 2**：`TreeSeaMap.csproj`、`mod_manifest.json`、`source/Entry.cs`；`tools/deploy.sh`（build → 复制 DLL+manifest 到 `mods/TreeSeaMap/`，并复制 RitsuLib 到 `mods/STS2-RitsuLib/`）
- **Phase 3**：稀疏占用 → 连通修复 → Wilson 生成树 → road density → Start/Exit(AP 约束) → Validation；`MovementSystem.CanMove/Cost`；`map.html`（根目录，可调宽高/occupancy/road/AP/种子）为同算法 JS 版
- **Phase 4**：`EventAssigner` 按 `LayerConfig.eventCounts` 撒 6 种类型；与拓扑解耦（先地图后事件）
- **Phase 5**：`TreeSeaActMap`（ActMap 子类）+ `TreeSeaMapModel`（ModifyGeneratedMap + `SubscribeForRunStateHooks`）；patch MapTravel/RecalculateTravelability/EnterMapCoord/ActFloor；`MapScreenPatches.cs`（SetMapLayoutPatch 等比缩放居中、BossAncientPosPatch、DrawPathsPatch、滚动自适应）；视觉不足升级自建屏
- **Phase 6**：AP 存 RitsuLib RunSavedData、`EnterMapCoord` 扣 1、**每层独立、层间重置**、HUD（GodotSharp 运行时 Label，免 pck）；ChaseBattleSystem（**房间结算时检测**：AP=0→强制进 Boss+10%；AP=1 先进普通房交互完→再追猎；提前打 Boss→直接下一层）

---

## 6. 文件结构

**游戏侧（只读参考）**：`../lib/sts2-decompiled/MegaCrit/sts2/Core/`
- `Map/`：StandardActMap / ActMap / MapPoint / MapCoord / MapPointType / MapPointState / MapTravel / MapPostProcessing / MapPointTypeCounts
- `Nodes/Screens/Map/NMapScreen.cs`（布局/交互/滚动硬编码全在此）
- `Runs/RunManager.cs`（GenerateMap/EnterMapCoord）、`Models/ActModel.cs`（CreateMap/GetNumberOfRooms）、`Models/Acts/*.cs`（各章 GetMapPointTypes）
- `GameActions/MoveToMapCoordAction.cs`、`Hooks/Hook.cs`、`Modding/ModHelper.cs`

**Mod 侧（当前目录）**：
```
PLAN.md                ← 本文件（权威计划，融合了已删除的 map.md 草稿）
map.html               ← 拓扑生成器可视化（已实现：宽高/occupancy/road/AP/种子）
TreeSeaMap.csproj   mod_manifest.json
source/Entry.cs
source/Map/       Cell.cs LayerGrid.cs LayerConfig.cs GridGenerator.cs EventAssigner.cs MovementSystem.cs
source/Bridge/    TreeSeaActMap.cs
source/Models/    TreeSeaMapModel.cs
source/Systems/   ActionPointSystem.cs ChaseBattleSystem.cs LayerProgressSystem.cs
source/Patches/   MapTravelPatches.cs MapScreenPatches.cs RunManagerPatches.cs BossStatPatches.cs
source/UI/        ApHud.cs（GodotSharp 运行时 Label，免 pck）
tools/deploy.sh
tools/map_editor.html   （Phase 9，固定地图可视化编辑）
docs/PATCH_TARGETS.md  docs/LAYER_CONFIG.md
```

---

## 7. 验证

1. **单测（Phase 3/4）**：生成器连通/无环/最短路≤AP/距离；事件分配类型数量分布/战斗补全/无空白
2. **构建**：`dotnet build TreeSeaMap.csproj` 零错误
3. **部署**：`tools/deploy.sh` → `mods/TreeSeaMap/` + `mods/STS2-RitsuLib/`
4. **游戏实测**（Steam 启动）：
   - Mods 列表出现 TreeSeaMap；进一局看到**纵横比正常**的网格地图（6 种节点类型用颜色/图标区分）
   - 四方向自由移动、回溯、绕路；每步扣 AP（HUD 可见）
   - 战斗/问号/篝火/商店/宝箱/精英格进入对应房间（宝箱复用原版宝箱房、精英接原版精英战）；已走过格不重触发
   - AP 耗尽且未到 Boss → 追猎（结算后触发）：进 Boss 房、Boss +10%、战斗
   - AP=1 选择非 Boss 房 → 先进房交互完 → 结算后追猎进 Boss 房
   - AP 耗尽前打到 Boss → Boss 胜利 → 直接下一层；打完第 3 层 Boss → 通关结算
5. **日志**：mod `Log.Info` + 文件日志；Godot 输出看报错
6. 每阶段小步实测再叠加

---

## 8. 风险与依赖

- **依赖**：RitsuLib 运行时 DLL 必须在游戏 mods 目录（deploy 处理）；游戏版本漂移需重跑 ilspycmd 核对 patch 签名（当前 v0.107.1）
- **反编译缺资源**：只有 .cs，无 .tscn/.tres/.png。编译/patch/运行时**不受影响**（游戏自带 pck）；研究原版 UI/自建屏可选解包 `Slay the Spire 2.pck`（可能加密），失败则用"代码推断+运行时场景树 dump"
- **回溯/重进格**：`AddVisitedMapCoord` 防重进，需 patch"走过格只移 marker"，是主要实现细节
- **ActFloor**：横向/回溯移动时 `coord.row+1` 失真，已拍板用**已消耗 AP** 替代（奖励门槛=消耗AP>4、RNG 种子=`HashCode.Combine(种子, 已消耗AP)`），Phase 5 落实
- **Boss +10%**：需定位 Boss 遭遇/属性构造点（CombatState/PullNextEncounter）再 patch
- **渲染比例**：原版地图屏按长条硬编码（1050px 横分列、2325px 纵分行数），方形网格会横向挤/纵向拉长，必须 patch 布局公式
- **追猎战语义**：AP 耗尽=软性推向出口（Boss 是出口节点）。三条路径已由用户定义（AP=0 结算后强进 Boss / AP=1 先进普通房再追猎 / 提前打 Boss 直接下一层），按此实现，实测确认手感与 +10% 数值

---

## 9. 当前进度（截至本文件编写）

- ✅ 反编译完成（Phase 0）
- ✅ 计划已批准，验收里程碑 = Phase 6
- ✅ `map.html` 拓扑生成器可视化（含 road density 与 AP 约束）
- ✅ 反编译调研：生成/移动/进房/换幕/渲染硬编码/扩展点已读通
- ⏳ 待开始：Phase 1（docs 归档）→ Phase 6（行动力+追猎战）
