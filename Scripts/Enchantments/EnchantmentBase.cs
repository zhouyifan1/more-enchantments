using STS2RitsuLib.Scaffolding.Content;

namespace MoreEnchantments.Scripts.Enchantments;

/// <summary>
/// 本 Mod 附魔的抽象基类：统一图标路径约定 res://MoreEnchantments/images/enchantments/{类名}.png。
/// 注意：不要在此基类上标注 [RegisterEnchantment]，每个具体附魔类自行标注。
/// </summary>
public abstract class MoreEnchantmentsEnchantmentBase : ModEnchantmentTemplate
{
    // 1:1 图标，原版 64x64；未绘制前可临时用 "res://icon.svg" 占位
    public override EnchantmentAssetProfile AssetProfile => new(
        IconPath: $"res://MoreEnchantments/images/enchantments/{GetType().Name}.png"
    );

    // 可堆叠约定（功能块 E）：卡面显示层数角标（ShowAmount）的附魔默认可堆叠，
    // 同卡重复附着同型时叠加层数而不占新槽位；特例（无层数可堆叠/有层数不可堆叠）在具体附魔类显式覆写
    public override bool IsStackable => ShowAmount;
}
