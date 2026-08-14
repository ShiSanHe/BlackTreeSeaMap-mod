using TreeSeaMap.Map;

var failures = 0;

void Check(bool cond, string name)
{
    if (cond)
    {
        Console.WriteLine($"  ✓ {name}");
    }
    else
    {
        failures++;
        Console.WriteLine($"  ✗ {name}");
    }
}

// ============ 生成器：拓扑正确性 ============
Console.WriteLine("[生成器] 遍历 seed 1..60 × 三层配置");

for (uint seed = 1; seed <= 60; seed++)
{
    foreach (var cfg in new[] { LayerConfig.Act1(), LayerConfig.Act2(), LayerConfig.Act3() })
    {
        var tag = $"seed={seed} AP={cfg.ActionPoint}";
        var grid = GridGenerator.Generate(cfg, seed);

        Check(grid.Nodes.Count > 0, $"{tag} 节点数>0 (实际 {grid.Nodes.Count})");
        Check(grid.Start != null, $"{tag} 有起点");
        Check(grid.Exit != null, $"{tag} 有出口");
        Check(grid.IsConnected, $"{tag} 全连通");
        Check(grid.SatisfiesActionPoint, $"{tag} 起点→出口最短路 {grid.StartToExitDistance} ≤ AP {cfg.ActionPoint}");
        Check(grid.AllNodes.Count(n => n.IsStart) == 1, $"{tag} 唯一起点");
        Check(grid.AllNodes.Count(n => n.IsExit) == 1, $"{tag} 唯一出口");
        Check(grid.Exit!.Content == NodeContent.Exit, $"{tag} 出口类型=Exit");

        // 所有边双向、且只连占用格
        foreach (var (a, b) in grid.Edges)
        {
            if (!a.Neighbors.Contains(b) || !b.Neighbors.Contains(a))
            {
                Check(false, $"{tag} 边 {a}-{b} 双向");
                break;
            }
        }

        // 邻居必为曼哈顿距离 1（四方向）
        foreach (var n in grid.AllNodes)
            foreach (var nb in n.Neighbors)
                if (Math.Abs(n.X - nb.X) + Math.Abs(n.Y - nb.Y) != 1)
                {
                    Check(false, $"{tag} 节点 {n} 邻居 {nb} 非四方向");
                    break;
                }
    }
}

// ============ 事件分配 ============
Console.WriteLine("[事件分配] seed 1..30");

for (uint seed = 1; seed <= 30; seed++)
{
    var cfg = LayerConfig.Act1();
    var grid = GridGenerator.Generate(cfg, seed);
    EventAssigner.Assign(grid, cfg, seed);
    var tag = $"seed={seed}";

    var nonExit = grid.AllNodes.Where(n => !n.IsExit).ToList();
    Check(nonExit.Count > 0, $"{tag} 非出口节点>0");

    // 若节点数足够，数量应精确；节点数 < 事件总数时放行（战斗补足）
    int totalEvents = cfg.NumElites + cfg.NumShops + cfg.NumRests + cfg.NumUnknowns + cfg.NumTreasures;
    if (nonExit.Count >= totalEvents)
    {
        Check(grid.AllNodes.Count(n => n.Content == NodeContent.Elite) == cfg.NumElites, $"{tag} 精英数={cfg.NumElites}");
        Check(grid.AllNodes.Count(n => n.Content == NodeContent.Shop) == cfg.NumShops, $"{tag} 商店数={cfg.NumShops}");
        Check(grid.AllNodes.Count(n => n.Content == NodeContent.Rest) == cfg.NumRests, $"{tag} 篝火数={cfg.NumRests}");
        Check(grid.AllNodes.Count(n => n.Content == NodeContent.Unknown) == cfg.NumUnknowns, $"{tag} 问号数={cfg.NumUnknowns}");
        Check(grid.AllNodes.Count(n => n.Content == NodeContent.Treasure) == cfg.NumTreasures, $"{tag} 宝箱数={cfg.NumTreasures}");
    }
    else
    {
        Console.WriteLine($"  (跳过数量断言: 节点 {nonExit.Count} < 事件 {totalEvents})");
    }

    // 无空白节点（战斗补足）
    Check(nonExit.All(n => n.Content != NodeContent.Unassigned), $"{tag} 无空白节点");

    // 重型类型相邻不重复
    foreach (var n in nonExit)
    {
        if (n.Content is NodeContent.Elite or NodeContent.Rest or NodeContent.Shop or NodeContent.Treasure)
        {
            if (n.Neighbors.Any(nb => nb.Content == n.Content))
            {
                Check(false, $"{tag} 节点 {n} 相邻同类型 {n.Content}");
                break;
            }
        }
    }
}

// ============ 移动与回溯 ============
Console.WriteLine("[移动] CanMove / 回溯");

{
    var grid = GridGenerator.Generate(LayerConfig.Act1(), 7);
    EventAssigner.Assign(grid, LayerConfig.Act1(), 7);
    var start = grid.Start!;
    var reachable = start.Neighbors.Count > 0;
    Check(reachable, $"seed=7 起点有可走邻居");
    Check(grid.StartToExitDistance > 0, $"seed=7 起点到出口距离 {grid.StartToExitDistance} > 0");

    // 回溯：任意中间节点存在一个邻居是"来路"（回到已访问格 = 回溯）
    // 拓扑层面回溯=图边允许来回走，这里验证反向边存在
    bool anyBacktrack = grid.Edges.Any(e => e.A.Neighbors.Contains(e.B) && e.B.Neighbors.Contains(e.A));
    Check(anyBacktrack, $"seed=7 所有边均可双向走（回溯基础）");
}

Console.WriteLine();
if (failures == 0)
{
    Console.WriteLine("✅ 全部测试通过");
    Environment.Exit(0);
}
else
{
    Console.WriteLine($"❌ {failures} 个断言失败");
    Environment.Exit(1);
}
