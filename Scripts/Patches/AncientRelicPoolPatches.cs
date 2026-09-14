using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Events;
using MoreEnchantments.Scripts.Data;
using MoreEnchantments.Scripts.Enchantments;
using MoreEnchantments.Scripts.Relics;
using STS2RitsuLib.Patching;
using STS2RitsuLib.Patching.Models;

namespace MoreEnchantments.Scripts.Patches;

/// <summary>
/// 达弗遗物池注入的懒触发补丁：首次 GenerateInitialOptions 前确保「老旧的怀表」已加入静态 _validRelicSets。
/// （只能懒注入：读取 _validRelicSets 会触发 Darv 静态构造并访问 ModelDb，mod 初始化时 ModelDb 未必就绪，
/// 且静态构造一旦抛异常该类型整进程不可用——故不在 Entry.Init 中提前尝试。）
/// 注入失败时降级为 RitsuLib 追加式注册（AncientOptionRules.RegisterDarvFallback）。
/// </summary>
public class DarvRelicSetInjectionPatch : IPatchMethod
{
    public static string PatchId => "ancient_darv_relic_set_injection";

    public static string Description => "达弗遗物池：向 _validRelicSets 注入老旧的怀表组（首次生成选项前懒触发）";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(Darv), "GenerateInitialOptions")];

    public static void Prefix()
    {
        if (AncientOptionRules.DarvInjected)
        {
            return;
        }
        try
        {
            AncientOptionRules.InjectDarvRelicSet();
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[DarvRelicSetInjectionPatch] 池注入失败，降级为追加式注册: {ex}");
            try
            {
                AncientOptionRules.RegisterDarvFallback();
            }
            catch (Exception ex2)
            {
                Entry.Logger.Error($"[DarvRelicSetInjectionPatch] 兜底注册也失败，老旧的怀表本次不可用: {ex2}");
            }
        }
    }
}

/// <summary>
/// 捏奥诅咒池（池3）注入：postfix 私有 CurseOptions getter，向候选数组追加「弥达斯之触」。
/// 之后原生流程自动生效：Rng.NextItem 均权选取（权重 1/11）、IsAllowedAtNeow 过滤、
/// AllPossibleOptions 覆盖（图鉴归属与控制台 ancient 命令可见）。
/// </summary>
public class NeowCurseOptionPatch : IPatchMethod
{
    public static string PatchId => "ancient_neow_curse_option";

    public static string Description => "捏奥诅咒池：CurseOptions getter 追加弥达斯之触选项";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(Neow), "CurseOptions", MethodType.Getter)];

    public static void Postfix(Neow __instance, ref IEnumerable<EventOption> __result)
    {
        try
        {
            __result = [.. __result, AncientOptionRules.CreateRelicOption<TouchOfMidas>(__instance)];
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[NeowCurseOptionPatch] 追加弥达斯之触选项失败: {ex}");
        }
    }
}

/// <summary>
/// 欧洛巴斯池1注入：postfix 私有 OptionPool1 getter，向候选数组追加「机器残片」。
/// 出场条件（牌组中可附魔且耗能≤2 的牌 ≥2）在此处检查；Owner 为 null 时属于图鉴/控制台展示路径，
/// 无条件追加以保证图鉴归属可见。
/// </summary>
public class OrobasPoolOneOptionPatch : IPatchMethod
{
    public static string PatchId => "ancient_orobas_pool1_option";

    public static string Description => "欧洛巴斯池1：OptionPool1 getter 追加机器残片选项（条件：可附魔且耗能≤2 的牌≥2）";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(Orobas), "OptionPool1", MethodType.Getter)];

    public static void Postfix(Orobas __instance, ref IEnumerable<EventOption> __result)
    {
        try
        {
            Player? owner = __instance.Owner;
            if (owner != null && AncientOptionRules.CountEnchantable<HyperLinkEnchantment>(owner, MachineFragment.IsAffordable) < 2)
            {
                return;
            }
            __result = [.. __result, AncientOptionRules.CreateRelicOption<MachineFragment>(__instance)];
        }
        catch (Exception ex)
        {
            Entry.Logger.Error($"[OrobasPoolOneOptionPatch] 追加机器残片选项失败: {ex}");
        }
    }
}
