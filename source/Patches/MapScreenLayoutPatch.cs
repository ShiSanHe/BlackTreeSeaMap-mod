using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using TreeSeaMap.Bridge;

namespace TreeSeaMap.Patches;

/// <summary>
/// 网格地图布局：SetMap 结束后把格子改成方形并居中。
///
/// 原版 NMapScreen 按「7 列长条」硬编码：_distX=1050/列数、_distY=2325/(行数-1)。
/// 矩形网格（如 9×9）直接用会显示成高瘦竖条或横向超屏。这里让横纵间距相等（方形格）——
/// cell 取「屏幕宽×0.8 / (列数-1)」与「1340 / (行数-1)」的较小者：
///   横向：NMapScreen 只能纵向滚动（滚动只 clamp Y，[-600,1800]），所以所有节点列必须整宽落在屏幕内；
///   纵向：地图最高点（Boss，y = 740 - 高度）不得低于滚动下限 -600，否则顶行滚不回来够不着，
///        即高度 ≤ 740 - (-600) = 1340。
/// 最后把所有点（含 Boss/Ancient）按 (col,row) 映射重摆、横向居中，清空连线重新绘制。
///
/// 只对 TreeSeaActMap 生效，原版地图走原逻辑。
///
/// 坑（首次开跑地图不居中）：GetViewportRect() = GetViewport().GetVisibleRect()，节点未入树时
/// GetViewport() 返回 null → 抛 NRE。首次开新 run 时 NMapScreen 可能尚未 add_child 进场景树，
/// 此时直接调 GetViewportRect() 会让整个 Postfix 中断，地图停在原版布局（偏左竖条）；SL 后已入树
/// 才正常。因此宽度读取做兜底（未入树时用 ProjectSettings 初始分辨率），并整体 try-catch。
/// </summary>
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
public static class MapScreenLayoutPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance)
    {
        try
        {
            Relayout(__instance);
        }
        catch (Exception e)
        {
            Log.Warn($"[TreeSeaMap] 地图布局重摆失败，本次沿用原版布局：{e}");
        }

        // 首次开跑地图屏可能尚未入树 / 视口未稳定（Bug 1），延迟到节点就绪后再重摆一次兜底，
        // 确保首次显示也是正确居中；SL 后节点已就绪，会额外跑一次但结果一致、无害。
        ScheduleRelayout(__instance);
    }

    /// <summary>
    /// 延迟到节点就绪后再重摆一次，覆盖「首次 SetMap 时地图屏未入树 / 视口未稳定」的时序。
    /// </summary>
    private static void ScheduleRelayout(NMapScreen s)
    {
        if (s.IsInsideTree())
        {
            Callable.From(() =>
            {
                try
                {
                    Relayout(s);
                }
                catch (Exception e)
                {
                    Log.Warn($"[TreeSeaMap] 延迟重摆失败：{e}");
                }
            }).CallDeferred();
            return;
        }

        void OnReady()
        {
            s.Ready -= OnReady;
            try
            {
                Relayout(s);
            }
            catch (Exception e)
            {
                Log.Warn($"[TreeSeaMap] Ready 后重摆失败：{e}");
            }
        }
        s.Ready += OnReady;
    }

    private static void Relayout(NMapScreen s)
    {
        var map = NMapScreenReflect.GetMap(s);
        if (map is not TreeSeaActMap)
            return;

        int colCount = map.GetColumnCount();
        int rowCount = map.GetRowCount();
        if (colCount < 2 || rowCount < 2)
            return;

        // 方形格间距：横向按屏幕宽(×0.8)缩入视口，纵向按滚动范围约束（顶 offset 740 - 滚动下限 -600 = 1340）。
        // 取较小者 = 整图放得下且尽量大。
        float availW = ReadScreenWidth(s) * 0.8f;
        float availH = 740f + 600f; // 地图最高点不能低于滚动下限，否则顶行滚不回来
        float cell = MathF.Min(availW / (colCount - 1), availH / (rowCount - 1));
        float offsetX = -(colCount - 1) * cell * 0.5f;
        NMapScreenReflect.SetDist(s, cell, cell);

        // 诊断日志（Bug 1 排查用，对比首次 vs SL）：map 类型 / 行列 / 树状态 / 屏幕宽 / cell / 容器与节点实际位置
        var startNode = NMapScreenReflect.GetStartingNode(s);
        var bossNode = NMapScreenReflect.GetBossNode(s);
        var container = NMapScreenReflect.GetMapContainer(s);
        Log.Info($"[TreeSeaMap][Layout] map={map.GetType().Name} {colCount}x{rowCount} " +
                 $"insideTree={s.IsInsideTree()} ready={s.IsNodeReady()} " +
                 $"screenW={ReadScreenWidth(s):F0} cell={cell:F1} offsetX={offsetX:F0} " +
                 $"screen pos={s.Position} size={s.Size} " +
                 $"container pos={container?.Position} scale={container?.Scale} " +
                 $"start={startNode?.Position} boss={bossNode?.Position}");

        // 重摆所有点（普通点 + Boss + Ancient 全在 _mapPointDictionary），横向按地图实际宽度居中
        Vector2 offset = new(-(colCount - 1) * cell * 0.5f, 740f);
        Vector2 step = new(cell, -cell);
        foreach (var kv in NMapScreenReflect.GetPointDict(s))
            kv.Value.Position = new Vector2(kv.Key.col, kv.Key.row) * step + offset;

        // 连线坐标已 bake 进 TextureRect，清空重建
        NMapScreenReflect.ClearPaths(s);
        NMapScreenReflect.DrawPathsForMap(s, map);

        // 重画后恢复已访问路径的 Traveled 色（SetMap 的染色被清空冲掉了）
        var runState = NMapScreenReflect.GetRunState(s);
        var paths = NMapScreenReflect.GetPaths(s);
        var visited = runState.VisitedMapCoords;
        for (int i = 0; i < visited.Count - 1; i++)
        {
            if (!paths.TryGetValue((visited[i], visited[i + 1]), out var rects))
                continue;
            foreach (var rect in rects)
            {
                rect.Modulate = runState.Act.MapTraveledColor;
                rect.Scale = Vector2.One * 1.2f;
            }
        }
    }

    /// <summary>
    /// 屏幕宽度预算。优先用 NMapScreen 实际视口（精确匹配当前窗口）；节点未入树时 GetViewportRect
    /// 会抛 NRE，故先判 IsInsideTree，未入树则退回 ProjectSettings 初始分辨率。地图始终对称居中，
    /// 预算只影响 cell（地图整体缩放），所以两种来源视觉一致。
    /// </summary>
    private static float ReadScreenWidth(NMapScreen s)
    {
        if (s.IsInsideTree())
        {
            float w = s.GetViewportRect().Size.X;
            if (w > 0f)
                return w;
        }
        int fallback = ProjectSettings.GetSetting("display/window/size/viewport_width", 1920).AsInt32();
        return fallback > 0 ? fallback : 1920f;
    }
}
