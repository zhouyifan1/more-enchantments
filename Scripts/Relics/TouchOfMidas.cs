using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「弥达斯之触」（先古，捏奥诅咒位选项）：拾起时失去 10 点最大生命值，
/// 从牌组选择至多 2 张攻击牌附魔「贪婪」10 层。
/// 失去最大生命值参考遗物 LeafyPoultice（CreatureCmd.LoseMaxHp）。
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_TOUCH_OF_MIDAS
/// </summary>
[RegisterRelic(typeof(EventRelicPool))]
public class TouchOfMidas : EnchantOnPickupRelicBase<GreedyEnchantment>
{
    // 失去的最大生命值（DynamicVar "MaxHp"，本地化用 {MaxHp} 占位）
    private const string _maxHpKey = "MaxHp";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override int MaxCards => 2;

    protected override int EnchantAmount => 10;

    protected override IEnumerable<DynamicVar> CanonicalVars => base.CanonicalVars.Concat([new MaxHpVar(_maxHpKey, 10m)]);

    // 拾起时：先失去最大生命值（参考 LeafyPoultice），再选牌附魔
    public override async Task AfterObtained()
    {
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner.Creature, DynamicVars[_maxHpKey].BaseValue, isFromCard: false);
        await base.AfterObtained();
    }
}
