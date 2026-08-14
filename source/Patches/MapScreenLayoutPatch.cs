using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using TreeSeaMap.Bridge;

namespace TreeSeaMap.Patches;

/// <summary>
/// 地图布局：不再重摆（v0.7）。
///
/// 用户实测确认：SL 读档后的地图（SavedActMap → ModifyGeneratedMapLate → SetMap 原版布局）
/// "非常标准"，而首次新 run 我们重摆成"方块"反而破坏了布局，两条路径不一致。
///
/// 原版布局（_distX=1050/列数、_distY=2325/(行数-1)、offset(-500,740)+jitter）对 9×9 网格天然适配：
/// 节点锚点 (0.5,0.5) 的渲染偏移（渲染位置 = 锚点×父尺寸+偏移）恰好把地图中心拉到屏幕中心附近，
/// 横向不超屏、纵向延伸（STS 标准观感）。所以移除重摆，首次与 SL 统一走 SetMap 原版布局。
///
/// 本 patch 仅保留诊断日志，确认两条路径都走 SetMap 且 map 类型/尺寸正确。
/// </summary>
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
public static class MapScreenLayoutPatch
{
    private static int _setMapCount;

    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance)
    {
        var map = NMapScreenReflect.GetMap(__instance);
        if (map is not TreeSeaActMap)
            return;
        Log.Info($"[TreeSeaMap][Layout] setMap#{(++_setMapCount)} {map.GetType().Name} " +
                 $"{map.GetColumnCount()}x{map.GetRowCount()} 原版布局(不重摆)");
    }
}
