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
- [v0.3 try-catch + ProjectSettings 兜底宽度] 用户反馈仍未解决 → NRE 假设可能不成立
- [v0.4 核验] 检查 godot.log 与 mods 目录 → **用户部署的是 16:29 旧版（29KB），最新 16:40（31KB）未部署**，旧版无 Layout 日志，不能据此判断 Postfix 未执行。mod 加载正常（:620）。
- [v0.4 新证据] 用户部署最新版后拿到两条首次开 run 的 Layout 日志，**insideTree=True ready=True（彻底推翻未入树 NRE 假设）**，两条日志属**同一次 SetMap**（中间仅 3 条存档日志）：
  - 第一次 Postfix（重摆前）：`start=(880, 969.375) boss=(760, -1440)` —— 异常，非网格坐标
  - 延迟重摆（重摆前）：`start=(670, 572.5) boss=(0, -600)` —— 正确网格坐标（col8,row1 / col4,row8）
- [v0.5 根因定位（锚点）] 锚点日志确认：**所有节点（含普通点）锚点都是中心 (0.5,0.5)**，非 start/boss 特例。start/boss 的异常坐标 = SetMap 原版偏移 **+ (960,540) = 0.5×(1920,1080)**。Godot 布局时渲染位置 = 锚点×父容器尺寸 + 偏移，`.Position` getter 返回设置值不代表渲染位置 → "日志正常但屏幕错位"。锚点重置后 start/boss/普通点位置全部正确（实测 anchors=(0,0,0,0) 后 start=-167.5/572.5、boss=0/-600、norm=-670/740 均为网格值）。**锚点问题已修好。**
- [v0.5 根因定位（横向偏左 = 不居中真凶）] 锚点重置后地图坐标正确，但**地图整体偏左**：9×9 地图 x 范围 = [-670, 670]，**中心在 x=0，而非屏幕中心 x=960**（第一列 norm col0 在 x=-670，屏幕外）。旧 offsetX=-(列数-1)×cell×0.5 让地图中心对齐坐标原点（Godot 屏幕坐标原点在左上角），整图左移 960-0=960px。SL 后"变正常居中"推测：SL 时 SetMap 未重摆（或走不同路径），节点停在 SetMap 原版+锚点偏移状态（中心≈894），反而凑近屏幕中心 → 视觉"居中"。**修复：offsetX=(屏幕宽 - 地图宽)/2。**
- [v0.5 修复] 重摆时先把每个节点锚点重置为 (0,0,0,0) 再设 Position（锚点问题）+ offsetX 改 (屏幕宽-地图宽)/2 居中（偏左问题）。日志保留锚点/父尺寸/setMap# 计数。

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
