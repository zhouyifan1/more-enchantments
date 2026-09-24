using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MoreEnchantments.Scripts.Data;
using MoreEnchantments.Scripts.Patches;
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
        EnchantLimitService.RegisterData();
        var limitPatcher = RitsuLibFramework.CreatePatcher(ModId, "enchantment-limit");
        limitPatcher.RegisterPatch<EnchantLimitCanEnchantPatch>();
        limitPatcher.RegisterPatch<EnchantLimitEnchantCommandPatch>();
        limitPatcher.RegisterPatch<EnchantLimitHookBroadcastPatch>();
        limitPatcher.RegisterPatch<EnchantNumericFoldPatch>();
        limitPatcher.RegisterPatch<EnchantOnPlayDispatchPatch>();
        limitPatcher.RegisterPatch<EnchantLimitHoverTipsPatch>();
        limitPatcher.RegisterPatch<EnchantLimitExtraCardTextPatch>();
        limitPatcher.RegisterPatch<EnchantLimitGlowPatch>();
        limitPatcher.RegisterPatch<EnchantLimitRecalculatePatch>();
        limitPatcher.RegisterPatch<EnchantLimitDowngradePatch>();
        limitPatcher.RegisterPatch<EnchantLimitClearEnchantmentPatch>();
        limitPatcher.RegisterPatch<EnchantLimitClonePatch>();
        limitPatcher.RegisterPatch<EnchantLimitDeserializePatch>();
        limitPatcher.RegisterPatch<EnchantLimitCardUiPatch>();
        limitPatcher.RegisterPatch<EnchantLimitCardPreviewPatch>();
        limitPatcher.RegisterPatch<EnchantLimitPreviewPatch>();
        limitPatcher.RegisterPatch<StackableEnchantmentPatch>();
        limitPatcher.RegisterPatch<FresnelLensCompatPatch>();
        limitPatcher.RegisterPatch<MysteriousPotionPatch>();
        // 非关键补丁，失败仅降级（上限回到原版 1，其余功能不受影响）
        if (!limitPatcher.PatchAll())
            Logger.Error("Enchantment-limit patches failed; enchantment limit stays at vanilla.");

        // 先古遗物池注入补丁（机器残片/老旧的怀表/弥达斯之触）——非关键，失败降级为 RitsuLib 追加式注册
        var ancientPatcher = RitsuLibFramework.CreatePatcher(ModId, "ancient-relic-pool");
        ancientPatcher.RegisterPatch<DarvRelicSetInjectionPatch>();
        ancientPatcher.RegisterPatch<NeowCurseOptionPatch>();
        ancientPatcher.RegisterPatch<OrobasPoolOneOptionPatch>();
        ancientPatcher.RegisterPatch<NonupeipePoolOptionPatch>();
        ancientPatcher.RegisterPatch<TanxPoolOptionPatch>();
        if (!ancientPatcher.PatchAll())
        {
            Logger.Error("Ancient relic pool patches failed; falling back to append-based ancient options.");
            AncientOptionRules.RegisterFallback();
        }

        // 商店扩展补丁——独立 patcher，失败只关闭商店功能（功能块 C）
        var shopPatcher = RitsuLibFramework.CreatePatcher(ModId, "shop");
        shopPatcher.RegisterPatch<ShopInventoryPatch>();
        shopPatcher.RegisterPatch<ShopRelicRestockPatch>();
        shopPatcher.RegisterPatch<ShopUiSlotsPatch>();
        shopPatcher.RegisterPatch<ShopUiNavigationPatch>();
        // 非关键补丁，失败仅降级为「无专属栏位」
        if (!shopPatcher.PatchAll())
            Logger.Error("Shop patches failed; enchant relic shop slots are disabled.");

        // 遗物配套补丁——非关键，失败仅降级对应遗物功能
        var relicPatcher = RitsuLibFramework.CreatePatcher(ModId, "relics");
        relicPatcher.RegisterPatch<SilverMetalLoadFixPatch>();
        if (!relicPatcher.PatchAll())
            Logger.Error("Relic patches failed; SilverMetal load fix is disabled.");

        Logger.Info("MoreEnchantments initialized!");
    }

    private static void DisableMod()
    {
        // 必要补丁无法应用时，在这里关闭本 Mod
        Logger.Error("Required patcher failed; MoreEnchantments is disabled.");
    }
}
