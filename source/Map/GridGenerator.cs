using System;
using System.Collections.Generic;
using System.Linq;

namespace TreeSeaMap.Map;

/// <summary>
/// 网格拓扑生成器。
/// 算法与 map.html 的 genMap() 逐行对齐（mulberry32 PRNG）：
///   1. 稀疏占用（occupancy 比例随机占格）
///   2. 曼哈顿连通修复（把各分量桥接到主分量）
///   3. Wilson 随机生成树（保证连通、无孤立点）
///   4. road density 额外边（枝岔更多；环是副产品）
///   5. BFS 距出口距离 → 起点 = 距出口最远且最短路 ≤ AP 的节点
///   6. 连通校验
/// 纯逻辑、无游戏依赖，可单测。
/// </summary>
public static class GridGenerator
{
    private static readonly (int Dx, int Dy)[] Adj4 =
        { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>按配置与种子生成一层拓扑。</summary>
    public static LayerGrid Generate(LayerConfig cfg, uint seed)
    {
        var rng = new Mulberry32(seed);
        int w = cfg.Width, h = cfg.Height;

        var grid = new LayerGrid { Width = w, Height = h, ActionPoint = cfg.ActionPoint };

        // 1. 稀疏占用
        var occ = new HashSet<(int, int)>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (rng.Next() < cfg.Occupancy)
                    occ.Add((x, y));

        // 出口：默认顶行中心，可配置；强制占用
        var exit = cfg.ExitPosition ?? (w / 2, 0);
        exit = (Math.Clamp(exit.X, 0, w - 1), Math.Clamp(exit.Y, 0, h - 1));
        occ.Add(exit);
        var exitNode = new Node(exit.X, exit.Y) { IsExit = true, Content = NodeContent.Exit };
        grid.Exit = exitNode;
        grid.Nodes[exit] = exitNode;

        // 2. 连通修复：曼哈顿桥接各分量到主分量
        RepairConnectivity(occ, grid);

        // 3. Wilson 随机生成树
        var edgeSet = new HashSet<(int, int, int, int)>();
        var edges = new List<((int, int), (int, int))>();
        WilsonTree(occ, grid, rng, edges, edgeSet);

        // 4. road density：在生成树基础上补额外连接
        if (cfg.RoadDensity > 0)
        {
            foreach (var k in occ)
                foreach (var (dx, dy) in Adj4)
                {
                    var nk = (k.Item1 + dx, k.Item2 + dy);
                    if (!occ.Contains(nk))
                        continue;
                    if (EdgeKey(k, nk, out var key) && edgeSet.Contains(key))
                        continue;
                    if (rng.Next() < cfg.RoadDensity)
                    {
                        edgeSet.Add(key);
                        edges.Add((k, nk));
                    }
                }
        }

        // 建邻接
        foreach (var (a, b) in edges)
        {
            if (grid.Get(a.Item1, a.Item2) is not { } na || grid.Get(b.Item1, b.Item2) is not { } nb)
                continue;
            na.Neighbors.Add(nb);
            nb.Neighbors.Add(na);
            grid.Edges.Add((na, nb));
        }

        // 5. 距出口距离 + 起点
        ComputeStart(occ, grid);

        // 6. 连通校验
        grid.IsConnected = IsAllReachable(grid);

        return grid;
    }

    // ---- 连通修复 ----

    private static void RepairConnectivity(HashSet<(int, int)> occ, LayerGrid grid)
    {
        var unvisited = new HashSet<(int, int)>(occ);
        var comps = new List<HashSet<(int, int)>>();
        while (unvisited.Count > 0)
        {
            var seed = unvisited.First();
            var comp = BfsComponent(occ, seed);
            foreach (var k in comp)
                unvisited.Remove(k);
            comps.Add(comp);
        }

        var main = comps[0];
        for (int i = 1; i < comps.Count; i++)
        {
            var comp = comps[i];
            var best = (Dist: int.MaxValue, A: (0, 0), B: (0, 0));
            foreach (var a in main)
                foreach (var b in comp)
                {
                    int d = Math.Abs(a.Item1 - b.Item1) + Math.Abs(a.Item2 - b.Item2);
                    if (d < best.Dist)
                        best = (d, a, b);
                }

            var (x, y) = best.A;
            var (tx, ty) = best.B;
            while (x != tx || y != ty)
            {
                if (x != tx)
                    x += tx > x ? 1 : -1;
                else
                    y += ty > y ? 1 : -1;
                var k = (x, y);
                if (!occ.Contains(k))
                {
                    occ.Add(k);
                    grid.Nodes[k] = new Node(k.Item1, k.Item2);
                    main.Add(k);
                }
            }

            foreach (var k in comp)
                main.Add(k);
        }

        // 确保所有占用格都有对应 Node
        foreach (var k in occ)
            if (!grid.Nodes.ContainsKey(k))
                grid.Nodes[k] = new Node(k.Item1, k.Item2);
    }

    private static HashSet<(int, int)> BfsComponent(HashSet<(int, int)> occ, (int, int) seed)
    {
        var comp = new HashSet<(int, int)> { seed };
        var q = new Queue<(int, int)>();
        q.Enqueue(seed);
        while (q.Count > 0)
        {
            var n = q.Dequeue();
            foreach (var nb in Nb4(occ, n))
                if (comp.Add(nb))
                    q.Enqueue(nb);
        }
        return comp;
    }

    // ---- Wilson 随机生成树 ----

    private static void WilsonTree(
        HashSet<(int, int)> occ,
        LayerGrid grid,
        Mulberry32 rng,
        List<((int, int), (int, int))> edges,
        HashSet<(int, int, int, int)> edgeSet)
    {
        var inTree = new HashSet<(int, int)> { (grid.Exit!.X, grid.Exit.Y) };
        var occArr = occ.ToList();
        int guard = 0, maxGuard = occArr.Count * 50;

        while (inTree.Count < occArr.Count && guard < maxGuard)
        {
            guard++;
            var cands = occArr.Where(k => !inTree.Contains(k)).ToList();
            var s = cands[rng.NextInt(cands.Count)];
            var path = new List<(int, int)> { s };
            var seen = new Dictionary<(int, int), int> { [s] = 0 };

            while (!inTree.Contains(s))
            {
                var nbs = Nb4(occ, s);
                var nb = nbs[rng.NextInt(nbs.Count)];
                if (seen.TryGetValue(nb, out int idx))
                {
                    // 撞到自己走过的路径（形成环）→ 截断到该位置（回退环）
                    for (int j = idx + 1; j < path.Count; j++)
                        seen.Remove(path[j]);
                    path.RemoveRange(idx + 1, path.Count - (idx + 1));
                }
                else
                {
                    seen[nb] = path.Count;
                    path.Add(nb);
                }
                s = path[path.Count - 1];
            }

            for (int j = 0; j < path.Count - 1; j++)
                AddEdge(occ, grid, edges, edgeSet, inTree, path[j], path[j + 1]);
        }
    }

    private static void AddEdge(
        HashSet<(int, int)> occ,
        LayerGrid grid,
        List<((int, int), (int, int))> edges,
        HashSet<(int, int, int, int)> edgeSet,
        HashSet<(int, int)> inTree,
        (int, int) a,
        (int, int) b)
    {
        if (!EdgeKey(a, b, out var key))
            return;
        if (!edgeSet.Add(key))
            return;
        edges.Add((a, b));
        inTree.Add(a);
        inTree.Add(b);
    }

    /// <summary>生成无序边 key，a、b 必须相邻（曼哈顿距离 1）。</summary>
    private static bool EdgeKey((int, int) a, (int, int) b, out (int, int, int, int) key)
    {
        var (ax, ay) = a;
        var (bx, by) = b;
        if (Math.Abs(ax - bx) + Math.Abs(ay - by) != 1)
        {
            key = default;
            return false;
        }
        // 字典序去重：小的在前
        key = (ax, ay).CompareTo((bx, by)) < 0 ? (ax, ay, bx, by) : (bx, by, ax, ay);
        return true;
    }

    // ---- 距离与起点 ----

    private static void ComputeStart(HashSet<(int, int)> occ, LayerGrid grid)
    {
        var exit = (grid.Exit!.X, grid.Exit.Y);
        var dist = new Dictionary<(int, int), int> { [exit] = 0 };
        var q = new Queue<(int, int)>();
        q.Enqueue(exit);
        while (q.Count > 0)
        {
            var n = q.Dequeue();
            foreach (var nb in Nb4(occ, n))
                if (!dist.ContainsKey(nb))
                {
                    dist[nb] = dist[n] + 1;
                    q.Enqueue(nb);
                }
        }

        // 起点：距出口最远但最短路 ≤ AP；若无满足者，取全局最远
        Node? best = null;
        foreach (var k in occ)
        {
            if (k == exit || !dist.ContainsKey(k))
                continue;
            var d = dist[k];
            if (d > grid.ActionPoint)
                continue;
            if (best == null || d > dist[(best.X, best.Y)])
                best = grid.Nodes[k];
        }
        if (best == null)
        {
            foreach (var k in occ)
            {
                if (k == exit || !dist.ContainsKey(k))
                    continue;
                var n = grid.Nodes[k];
                if (best == null || dist[(best.X, best.Y)] < dist[k])
                    best = n;
            }
        }

        if (best != null)
        {
            best.IsStart = true;
            grid.Start = best;
            grid.StartToExitDistance = dist[(best.X, best.Y)];
        }
    }

    // ---- 工具 ----

    private static List<(int, int)> Nb4(HashSet<(int, int)> occ, (int, int) k)
    {
        var r = new List<(int, int)>(4);
        foreach (var (dx, dy) in Adj4)
        {
            var nk = (k.Item1 + dx, k.Item2 + dy);
            if (occ.Contains(nk))
                r.Add(nk);
        }
        return r;
    }

    private static bool IsAllReachable(LayerGrid grid)
    {
        if (grid.Start == null)
            return false;
        var seen = new HashSet<Node> { grid.Start };
        var q = new Queue<Node>();
        q.Enqueue(grid.Start);
        while (q.Count > 0)
        {
            var n = q.Dequeue();
            foreach (var nb in n.Neighbors)
                if (seen.Add(nb))
                    q.Enqueue(nb);
        }
        return seen.Count == grid.Nodes.Count;
    }
}
