using MegaCrit.Sts2.Core.Entities.Relics;
using MoreEnchantments.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MoreEnchantments.Scripts.Relics;

/// <summary>
/// 遗物「锉刀」：拾起时，从牌组选择至多 1 张攻击牌，附魔「锯齿」1 层。
/// （类名加 Relic 后缀以避免与 System.IO.File 撞名。）
/// 控制台测试：relic add MORE_ENCHANTMENTS_RELIC_FILE_RELIC
/// </summary>
[RegisterRelic(typeof(ShopEnchantRelicPool))]
public class FileRelic : ShopEnchantRelicBase<SerratedEnchantment>
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override int MaxCards => 1;

    protected override int EnchantAmount => 1;
}
