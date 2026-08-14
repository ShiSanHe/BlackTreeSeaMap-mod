using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using TreeSeaMap.Bridge;

namespace TreeSeaMap.Patches;

/// <summary>
/// 可达性重算：替换原版「末行 → Boss 无条件可达」的硬假设。
///
/// 原版 RecalculateTravelability（:878-881）把「最后访问点在末行 row==GetRowCount()-1」
/// 当作 Boss 直接 Travelable。网格地图没有"末行"概念，Boss 应在玩家走到其相邻格时才可达。
///
/// 网格版：完全走通用分支 —— 最后访问点的可达集 = MapTravel.GetTravelablePointsFrom = Children。
/// 由于 TreeSeaActMap 把 Boss 与其相邻格做了双向边，Boss 会出现在相邻格的 Children 里 → 自动 Travelable。
/// 只对 TreeSeaActMap 生效；Prefix 返回 false 整体替换，原版地图走原逻辑。
/// </summary>
[HarmonyPatch(typeof(NMapScreen), "RecalculateTravelability")]
public static class TravelabilityPatch
{
    [HarmonyPrefix]
    public static bool Prefix(NMapScreen __instance)
    {
        var map = NMapScreenReflect.GetMap(__instance);
        if (map is not TreeSeaActMap)
            return true;

        var runState = NMapScreenReflect.GetRunState(__instance);
        var dict = NMapScreenReflect.GetPointDict(__instance);
        var startingNode = NMapScreenReflect.GetStartingNode(__instance);
        var visited = runState.VisitedMapCoords;

        if (visited.Any())
        {
            foreach (var node in dict.Values)
                node.State = MapPointState.Untravelable;
            foreach (var coord in visited)
                if (dict.TryGetValue(coord, out var node))
                    node.State = MapPointState.Traveled;

            var last = visited[^1];
            if (dict.TryGetValue(last, out var current))
            {
                foreach (var mp in MapTravel.GetTravelablePointsFrom(runState, current.Point))
                    if (dict.TryGetValue(mp.coord, out var node))
                        node.State = MapPointState.Travelable;
            }
            else
            {
                Log.Error($"Last visited coord {last} not found in map, falling back to starting point");
                startingNode.State = MapPointState.Travelable;
            }
        }
        else
        {
            startingNode.State = MapPointState.Travelable;
        }

        return false;
    }
}
