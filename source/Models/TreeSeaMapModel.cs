using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using TreeSeaMap.Bridge;
using TreeSeaMap.Map;

namespace TreeSeaMap.Models;

/// <summary>
/// 黑流树海地图模型：覆写官方 ModifyGeneratedMap，把自建的网格地图（TreeSeaActMap）注入跑局。
///
/// 用法（Entry）：
///   1. ModelDb.Inject(typeof(TreeSeaMapModel)) —— 官方为 mod 提供的模型注册入口
///   2. ModHelper.SubscribeForRunStateHooks(id, _ => [canonical 实例])
///   之后 RunState 每次 IterateHookListeners（含地图生成）都会调用本模型的 ModifyGeneratedMap。
///
/// 本方法为纯函数式（不修改 this），因此 canonical 实例即可安全调用；
/// 用 ModifierModel 作基类只是为了补上 AbstractModel 唯一的抽象成员 ShouldReceiveCombatHooks。
/// </summary>
public sealed class TreeSeaMapModel : ModifierModel
{
    public override ActMap ModifyGeneratedMap(IRunState runState, ActMap map, int actIndex)
    {
        var cfg = actIndex switch
        {
            0 => LayerConfig.Act1(),
            1 => LayerConfig.Act2(),
            _ => LayerConfig.Act3(),
        };

        // 沿用游戏派生种子：与官方地图生成相同公式（Rng.Seed + act_<n>_map），保证确定性
        uint seed = new Rng(runState.Rng.Seed, $"act_{actIndex + 1}_map").Seed;
        var grid = GridGenerator.Generate(cfg, seed);
        EventAssigner.Assign(grid, cfg, seed);
        return new TreeSeaActMap(grid);
    }
}
