using System.Collections.Generic;
using MegaCrit.Sts2.Core.Map;
using TreeSeaMap.Map;

namespace TreeSeaMap.Bridge;

/// <summary>
/// 把 LayerGrid（网格拓扑 + 事件）翻译成游戏的 ActMap/MapPoint 图。
///
/// 坐标映射：col = X，row = (Height-1) - Y —— 让起点在下方、Boss 在上方，
/// 符合原版「row 越大越接近 Boss、渲染向上」的方向约定（NMapScreen 布局公式 y 随 row 减小）。
///
/// 关键约定：
/// - 起点格（Start）与出口格（Exit/Boss）的 Grid cell 留空，单独持有特殊 MapPoint。
///   ActMap.GetPoint 对 Boss/Starting 坐标特殊分派，若 Grid 里也放了普通点会被遮蔽永远取不到。
/// - 四方向树边用 AddChildPoint 双向建（a.Children+b.parents 与反向），天然支持四方向移动与回溯：
///   MapTravel.GetTravelablePointsFrom 返回 currentPoint.Children，即四方向邻居。
/// - Boss 与其相邻格双向连线：玩家走到 Boss 相邻格后，Boss 进入 Children → 可达可进房。
/// </summary>
public sealed class TreeSeaActMap : ActMap
{
    private readonly MapPoint?[,] _grid;
    private readonly MapPoint _boss;
    private readonly MapPoint _starting;

    public override MapPoint BossMapPoint => _boss;

    public override MapPoint StartingMapPoint => _starting;

    public TreeSeaActMap(LayerGrid layer)
    {
        int width = layer.Width, height = layer.Height;
        _grid = new MapPoint?[width, height];

        // 1. 普通节点 → MapPoint（跳过起点/出口格，它们走特殊点）
        var mapPoints = new Dictionary<Node, MapPoint>();
        foreach (var node in layer.AllNodes)
        {
            if (node.IsStart || node.IsExit)
                continue;
            var mp = new MapPoint(node.X, height - 1 - node.Y)
            {
                PointType = ToPointType(node.Content),
                CanBeModified = false,
            };
            mapPoints[node] = mp;
            _grid[mp.coord.col, mp.coord.row] = mp;
        }

        // 2. 起点 / 出口特殊点（不进 Grid）
        _starting = new MapPoint(layer.Start!.X, height - 1 - layer.Start.Y)
        {
            PointType = MapPointType.Ancient,
            CanBeModified = false,
        };
        _boss = new MapPoint(layer.Exit!.X, height - 1 - layer.Exit.Y)
        {
            PointType = MapPointType.Boss,
            CanBeModified = false,
        };

        // 3. 普通节点之间的双向边
        foreach (var node in mapPoints.Keys)
        {
            foreach (var nb in node.Neighbors)
            {
                if (!mapPoints.TryGetValue(nb, out var nbMp))
                    continue;
                LinkBidirectional(mapPoints[node], nbMp);
            }
        }

        // 4. 起点/出口与其相邻格的（双向）边 —— 玩家走到相邻格即可达特殊点
        LinkToNeighbors(layer, mapPoints, _starting, layer.Start!);
        LinkToNeighbors(layer, mapPoints, _boss, layer.Exit!);
    }

    /// <summary>双向边：a.Children ∪ b.parents 与 b.Children ∪ a.parents。</summary>
    private static void LinkBidirectional(MapPoint a, MapPoint b)
    {
        a.AddChildPoint(b);
        b.AddChildPoint(a);
    }

    private static void LinkToNeighbors(LayerGrid layer, Dictionary<Node, MapPoint> mapPoints, MapPoint special, Node specialNode)
    {
        foreach (var nb in specialNode.Neighbors)
        {
            if (!mapPoints.TryGetValue(nb, out var nbMp))
                continue;
            LinkBidirectional(special, nbMp);
        }
    }

    private static MapPointType ToPointType(NodeContent content) => content switch
    {
        NodeContent.Battle => MapPointType.Monster,
        NodeContent.Unknown => MapPointType.Unknown,
        NodeContent.Rest => MapPointType.RestSite,
        NodeContent.Shop => MapPointType.Shop,
        NodeContent.Treasure => MapPointType.Treasure,
        NodeContent.Elite => MapPointType.Elite,
        _ => MapPointType.Unassigned,
    };

    protected override MapPoint?[,] Grid => _grid;
}
