# BUG 记录 — 黑流树海 mod

> 游戏实测发现的 bug 记录。状态：🔴 待修 / 🟡 修复中 / 🟢 已修复
> 最近更新：2026-08-14（实测于 v0.107.1，macOS）

---

## Bug 1：首次进游戏地图错位（不居中），SL 后正常 🟢 已修复

**现象**：全新开一局（游戏启动后第一次开 run），地图显示错位/不居中；退出重进（SL）后地图恢复居中。

**复现**：首次开新 run 必现；SL 后消失。**无任何报错日志。**

**排查记录**：
- [v0.1 固定 1050 预算] 首次能正常显示网格地图（9×9 超屏那次）→ 证明首次 SetMap 时布局 Postfix 能执行
- [v0.2 自适应 GetViewportRect] 首次错位、SL 正常 → 怀疑节点未入树时 `GetViewportRect()` 抛 NRE 中断重摆
- [v0.3 try-catch + ProjectSettings 兜底宽度] 用户反馈仍未解决 → NRE 假设可能不成立
- [v0.4 核验] 检查 godot.log 与 mods 目录 → **用户部署的是 16:29 旧版（29KB），最新 16:40（31KB）未部署**，旧版无 Layout 日志，不能据此判断 Postfix 未执行。mod 加载正常（:620）。
- [v0.4 新证据] 用户部署最新版后拿到两条首次开 run 的 Layout 日志，**insideTree=True ready=True（彻底推翻未入树 NRE 假设）**，两条日志属**同一次 SetMap**（中间仅 3 条存档日志）：
  - 第一次 Postfix（重摆前）：`start=(880, 969.375) boss=(760, -1440)` —— 异常，非网格坐标
  - 延迟重摆（重摆前）：`start=(670, 572.5) boss=(0, -600)` —— 正确网格坐标（col8,row1 / col4,row8）
- [v0.5 根因定位（锚点）] 锚点日志确认：**所有节点（含普通点）锚点都是中心 (0.5,0.5)**，非 start/boss 特例。start/boss 的异常坐标 = SetMap 原版偏移 **+ (960,540) = 0.5×(1920,1080)**。Godot 布局时渲染位置 = 锚点×父容器尺寸 + 偏移，`.Position` getter 返回设置值不代表渲染位置 → "日志正常但屏幕错位"。锚点重置后 start/boss/普通点位置全部正确（实测 anchors=(0,0,0,0) 后 start=-167.5/572.5、boss=0/-600、norm=-670/740 均为网格值）。**锚点问题已修好。**
- [v0.5 根因定位（横向偏左 = 不居中真凶）] 锚点重置后地图坐标正确，但**地图整体偏左**：9×9 地图 x 范围 = [-670, 670]，**中心在 x=0，而非屏幕中心 x=960**（第一列 norm col0 在 x=-670，屏幕外）。旧 offsetX=-(列数-1)×cell×0.5 让地图中心对齐坐标原点（Godot 屏幕坐标原点在左上角），整图左移 960-0=960px。SL 后"变正常居中"推测：SL 时 SetMap 未重摆（或走不同路径），节点停在 SetMap 原版+锚点偏移状态（中心≈894），反而凑近屏幕中心 → 视觉"居中"。**修复：offsetX=(屏幕宽 - 地图宽)/2。**
- [v0.5 修复] 重摆时先把每个节点锚点重置为 (0,0,0,0) 再设 Position（锚点问题）+ offsetX 改 (屏幕宽-地图宽)/2 居中（偏左问题）。日志保留锚点/父尺寸/setMap# 计数。
- [v0.6 实测] 首次开 run 地图**已横向居中**（offsetX=290：norm col0=290、boss col4=960，地图中心=960=屏幕中心 ✓），锚点重置生效（anchors 全 0）。**但用户反馈：首次"有点居中"，SL 后布局"很标准"，两者仍不一致，用户想要 SL 后那种布局。**
- [v0.6 新根因：两条路径布局不一致] **首次新 run 走 `CreateMap → ModifyGeneratedMap`（我们的 TreeSeaActMap → Postfix 重摆居中方块）**；**SL 读档走 `SavedActMap → ModifyGeneratedMapLate`**。我们的 model **只覆写了 `ModifyGeneratedMap`，未覆写 `ModifyGeneratedMapLate`**（Hook.cs:1671 调用各 listener 的 Late 版本，基础实现返回原 map）→ SL 后 map 是 `SavedActMap`（9×9 数据但**类型非 TreeSeaActMap**）→ 布局 Postfix `if (map is not TreeSeaActMap) return` 早退 → **SL 后是原版布局公式**（_distX=1050/9=116.67、_distY=2325/8=290.625、offset(-500,740)+jitter、锚点偏移）。SL 后无 setMap#2 日志（Postfix 早退）印证。
- [v0.6 待定] 用户想要 SL 后的"标准"布局（待截图确认形态：高瘦竖条原版布局？），然后统一两条路径的布局。**待截图。**
- [v0.7 最终根因 = 我们的重摆本身就是错] **用户截图确认：SL 后（SavedActMap→原版布局）9×9"非常标准"、正是用户想要的。** 锚点偏移不是 bug 而是原版让地图居中的机制（渲染位置=锚点×父尺寸+偏移≈屏幕中心），原版布局对 9×9 天然居中、不超屏、纵向延伸。我们一路的"重摆成方块 + 锚点重置 + offsetX 居中"全是画蛇添足——它把首次从"原版标准布局"改成了"方块布局"，而 SL 后从没被重摆碰过所以一直是对的。**最早 Bug 1（首次错位、SL 正常）的完整解释：首次被重摆破坏、SL 保持原版，两条路径不一致。**
- [v0.7 修复] **移除重摆**：MapScreenLayoutPatch 的 Postfix 不再做任何布局改动（只留诊断日志）。首次与 SL 统一走 SetMap 原版布局。部署 17:20/28KB。

**相关代码**：`source/Patches/MapScreenLayoutPatch.cs`（`Relayout` / `ReadScreenWidth`）、`source/Models/TreeSeaMapModel.cs`（只覆写 ModifyGeneratedMap）

**技术背景**：
- NMapScreen 只能纵向滚动（`ProcessMouseEvent`/`UpdateScrollPosition` 只动 Y，clamp `[-600,1800]`），地图横宽必须整宽落屏
- 地图最高点 y = 740 - 高度，不得低于滚动下限 -600 → 高度 ≤ 740-(-600)=1340，否则顶行(Boss)滚不回来
- `GetViewportRect()` = `GetViewport().GetVisibleRect()`，节点未入树时 `GetViewport()` 返回 null 会抛 NRE
- 首次 vs SL 走的地图路径：新 run 走 `CreateMap → ModifyGeneratedMap`；读档走 `SavedActMap → ModifyGeneratedMapLate`。**两条路径 SetMap 都会被调用（RunManager.cs:755），但布局 Postfix 只对 TreeSeaActMap 生效，SL 后 map 是 SavedActMap → 早退**

---

## Bug 2：初始点有多条连接时只能走其中一条 🟡 待修

**现象**：某些地图的初始点（起点）连出多个节点时，只能选其中一个节点移动，其余不可达/不可点。**偶发**：测试 10 次仅第 1 次触发，之后未复现。

**用户指示**：先记录不修，优先修 Bug 1。

**猜测方向**（未验证）：
- `MapTravel.GetTravelablePointsFrom` / `RecalculateTravelability` 的可达点判定时序
- `EnterMapCoord`/`AddVisitedMapCoord` 的首次状态
- 投票同步（`MapSelectionSynchronizer`）首次初始化

**相关代码**：`source/Patches/TravelabilityPatch.cs`、`source/Bridge/TreeSeaActMap.cs`（四方向树边双向 `AddChildPoint`）、`MapTravel.GetTravelablePointsFrom`

---

## 已修复（历史记录）

- 🟢 游戏无法启动：`ModelDb.Inject` 与 `ModelDb.Init` 自动注册冲突 → DuplicateModelException → 移除 Inject（`source/Entry.cs`）
- 🟢 9×9 地图横向超屏、左侧无法点击：布局改屏幕自适应 + 居中（`MapScreenLayoutPatch.cs`）
