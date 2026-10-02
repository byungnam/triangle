using Triangle.Core.Data;

namespace Triangle.Core.Items;

/// <summary>
/// 마을 상점 규칙: T1~<see cref="MaxTier"/> 장비를 정가에 무한히 판다.
/// 창고의 아이템은 무엇이든(재료 포함) 가격의 <see cref="SellPercent"/>%(버림)에 사 준다.
/// </summary>
public static class Shop
{
    public const int MaxTier = 2;

    public const int SellPercent = 40;

    /// <summary>상점이 파는 장비 (부위, 계열, 티어 순).</summary>
    public static IReadOnlyList<ItemDefinition> Stock(GameData data) =>
        data.EquipmentInTiers(1, MaxTier)
            .OrderBy(i => i.Slot)
            .ThenBy(i => i.Mastery)
            .ThenBy(i => i.Tier)
            .ToList();

    public static bool Sells(ItemDefinition item) => item.IsEquipment && item.Tier <= MaxTier;

    public static int SellPrice(ItemDefinition item) => item.Price * SellPercent / 100;
}
