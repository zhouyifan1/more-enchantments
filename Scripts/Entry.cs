using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;

namespace MoreEnchantments.Scripts;

// 必须要加的属性，用于注册Mod。字符串和初始化函数命名一致。
[ModInitializer(nameof(Init))]
public class Entry
{
    public const string ModId = "MoreEnchantments";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    // 初始化函数
    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 使得tscn可以加载自定义脚本（商店UI等场景脚本需要）
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);

        // 必须：启用 [RegisterXxx] 注解自动注册
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // RitsuLib 封装补丁系统：按功能块划分独立 patcher
        // 关键补丁（附魔/遗物/事件主体逻辑）——失败则禁用 Mod
        var corePatcher = RitsuLibFramework.CreatePatcher(ModId, "core");
        // corePatcher.RegisterPatches<CorePatches>();
        RitsuLibFramework.ApplyRequiredPatcher(corePatcher, DisableMod);

        // 附魔栏位上限补丁——独立 patcher，失败则恢复原版上限（功能块 E）
        // var limitPatcher = RitsuLibFramework.CreatePatcher(ModId, "enchantment-limit");

        // 商店扩展补丁——独立 patcher，失败只关闭商店功能（功能块 C）
        // var shopPatcher = RitsuLibFramework.CreatePatcher(ModId, "shop");

        Logger.Info("MoreEnchantments initialized!");
    }

    private static void DisableMod()
    {
        // 必要补丁无法应用时，在这里关闭本 Mod
        Logger.Error("Required patcher failed; MoreEnchantments is disabled.");
    }
}
