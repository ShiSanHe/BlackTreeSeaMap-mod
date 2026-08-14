using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace TreeSeaMap.Patches;

/// <summary>
/// NMapScreen 私有成员访问封装。NMapScreen 由游戏生成（.tscn 实例化），mod 无法改类，
/// 只能经 Harmony patch 访问其 private 字段/方法 —— 全部集中在此，避免 patch 内散落反射代码。
/// </summary>
internal static class NMapScreenReflect
{
    public static ActMap GetMap(NMapScreen s) => (ActMap)Get(s, "_map");

    public static RunState GetRunState(NMapScreen s) => (RunState)Get(s, "_runState");

    public static Dictionary<MapCoord, NMapPoint> GetPointDict(NMapScreen s)
        => (Dictionary<MapCoord, NMapPoint>)Get(s, "_mapPointDictionary");

    public static Dictionary<(MapCoord, MapCoord), IReadOnlyList<TextureRect>> GetPaths(NMapScreen s)
        => (Dictionary<(MapCoord, MapCoord), IReadOnlyList<TextureRect>>)Get(s, "_paths");

    public static Control GetPathsContainer(NMapScreen s) => (Control)Get(s, "_pathsContainer");

    public static Control GetMapContainer(NMapScreen s) => (Control)Get(s, "_mapContainer");

    public static NMapPoint GetStartingNode(NMapScreen s) => (NMapPoint)Get(s, "_startingPointNode");

    public static NMapPoint GetBossNode(NMapScreen s) => (NMapPoint)Get(s, "_bossPointNode");

    public static void SetDist(NMapScreen s, float distX, float distY)
    {
        Field(s, "_distX").SetValue(s, distX);
        Field(s, "_distY").SetValue(s, distY);
    }

    /// <summary>清空所有连线（坐标 bake 进 TextureRect，移动节点后必须重建）。</summary>
    public static void ClearPaths(NMapScreen s)
    {
        GetPaths(s).Clear();
        var container = GetPathsContainer(s);
        foreach (Node child in container.GetChildren().ToList())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>按当前图数据重画全部连线（普通点 + 起点 + Boss + 二阶段 Boss）。</summary>
    public static void DrawPathsForMap(NMapScreen s, ActMap map)
    {
        var dict = GetPointDict(s);
        foreach (var mp in map.GetAllMapPoints())
            InvokeDrawPaths(s, dict[mp.coord], mp);
        InvokeDrawPaths(s, dict[map.StartingMapPoint.coord], map.StartingMapPoint);
        InvokeDrawPaths(s, dict[map.BossMapPoint.coord], map.BossMapPoint);
        if (map.SecondBossMapPoint != null)
            InvokeDrawPaths(s, dict[map.SecondBossMapPoint.coord], map.SecondBossMapPoint);
    }

    private static void InvokeDrawPaths(NMapScreen s, NMapPoint node, MapPoint mp)
        => Method(s, "DrawPaths").Invoke(s, new object[] { node, mp });

    private static FieldInfo Field(NMapScreen s, string name)
        => typeof(NMapScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static object Get(NMapScreen s, string name) => Field(s, name).GetValue(s)!;

    private static MethodInfo Method(NMapScreen s, string name)
        => typeof(NMapScreen).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
