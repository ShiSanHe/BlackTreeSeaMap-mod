using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using TreeSeaMap.Models;

namespace TreeSeaMap;

/// <summary>
/// Mod 入口。
/// Phase 2：加载 Harmony + 打日志。
/// Phase 5：注册网格地图模型 —— 通过 ModHelper.SubscribeForRunStateHooks 让 RunState 每次迭代
/// hook（含地图生成）都带上我们的 canonical 模型实例，从而覆写 ModifyGeneratedMap 替换为网格地图。
/// 注意：绝不能手动 ModelDb.Inject —— ModelDb.Init() 启动时会遍历 AllAbstractModelSubtypes，
/// 其内含 ReflectionHelper.GetSubtypesInMods（自动发现 mod 里的 AbstractModel 子类）并创建 canonical
/// 实例；再 Inject 会 DuplicateModelException 导致游戏启动崩溃（guzhenren 同款陷阱）。
/// Phase 6：将在此接入 RitsuLib 的跑局数据存储（GetRunSavedDataStore）与追猎系统。
/// </summary>
[ModInitializer("Init")]
public static class Entry
{
    public const string ModId = "treeseamap";

    public static void Init()
    {
        var harmony = new Harmony("sts2.treeseamap");
        harmony.PatchAll();

        // 模型由 ModelDb.Init 自动注册；这里只取 canonical 实例（不能 new，构造会撞 DuplicateModelException）。
        ModHelper.SubscribeForRunStateHooks(ModId, _ => new AbstractModel[]
        {
            ModelDb.GetById<AbstractModel>(ModelDb.GetId(typeof(TreeSeaMapModel))),
        });

        Log.Info("[TreeSeaMap] 黑流树海已初始化 (Phase 5 网格地图)");
    }
}
