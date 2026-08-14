# BUG 记录 — 黑流树海 mod

> 游戏实测发现的 bug 记录。状态：🔴 待修 / 🟡 修复中 / 🟢 已修复
> 最近更新：2026-08-14（实测于 v0.107.1，macOS）

---

## Bug 1：首次进游戏地图错位（不居中），SL 后正常 🟡 修复中

**现象**：全新开一局（游戏启动后第一次开 run），地图显示错位/不居中；退出重进（SL）后地图恢复居中。

**复现**：首次开新 run 必现；SL 后消失。**无任何报错日志。**

**排查记录**：
- [v0.1 固定 1050 预算] 首次能正常显示网格地图（9×9 超屏那次）→ 证明首次 SetMap 时布局 Postfix 能执行
- [v0.2 自适应 GetViewportRect] 首次错位、SL 正常 → 怀疑节点未入树时 `GetViewportRect()` 抛 NRE 中断重摆
- [v0.3 try-catch + ProjectSettings 兜底宽度] **仍未解决** → NRE 假设不成立或另有根因
- [v0.4 下一步] 加诊断日志对比首次 vs SL 的 viewport/布局值；加延迟重摆兜底覆盖首次时序

**相关代码**：`source/Patches/MapScreenLayoutPatch.cs`（`Relayout` / `ReadScreenWidth`）

**技术背景**：
- NMapScreen 只能纵向滚动（`ProcessMouseEvent`/`UpdateScrollPosition` 只动 Y，clamp `[-600,1800]`），地图横宽必须整宽落屏
- 地图最高点 y = 740 - 高度，不得低于滚动下限 -600 → 高度 ≤ 740-(-600)=1340，否则顶行(Boss)滚不回来
- `GetViewportRect()` = `GetViewport().GetVisibleRect()`，节点未入树时 `GetViewport()` 返回 null 会抛 NRE
- 首次 vs SL 走的地图路径：新 run 走 `CreateMap → ModifyGeneratedMap`；读档走 `SavedActMap → ModifyGeneratedMapLate`

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
