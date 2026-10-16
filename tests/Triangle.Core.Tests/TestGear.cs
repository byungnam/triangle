using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Progress;

namespace Triangle.Core.Tests;

/// <summary>테스트용 부위별 장비. 번호는 테스트 전체에서 겹치지 않고, 아이템 레벨은 1이다.</summary>
internal static class TestGear
{
    private static int _uid;

    public static Dictionary<EquipmentSlot, ItemInstance> Of(string? mainHand = null, string? body = null, string? offHand = null, string? head = null, string? feet = null)
    {
        var gear = new Dictionary<EquipmentSlot, ItemInstance>();
        foreach (var (slot, item) in new[]
                 {
                     (EquipmentSlot.MainHand, mainHand), (EquipmentSlot.OffHand, offHand), (EquipmentSlot.Head, head),
                     (EquipmentSlot.Body, body), (EquipmentSlot.Feet, feet),
                 })
        {
            if (item is not null)
            {
                gear[slot] = Piece(item);
            }
        }

        return gear;
    }

    /// <summary>아이템 ID 사전을 새 장비로 (모집 템플릿 장비 등).</summary>
    public static Dictionary<EquipmentSlot, ItemInstance> Pieces(IReadOnlyDictionary<EquipmentSlot, string> items) =>
        items.ToDictionary(p => p.Key, p => Piece(p.Value));

    public static ItemInstance Piece(string itemId) => new(Interlocked.Increment(ref _uid), itemId, 1);
}

/// <summary>
/// 테스트 편의: 아이템 ID로 장비 한 점을 고른다. 그 멤버가 이미 낀 장비가 먼저고, 없으면 창고에서 가장 좋은 한 점이다.
/// </summary>
internal static class TestStash
{
    public static bool Equip(this Company company, string memberId, string itemId, GameData data) =>
        company.Equip(memberId, UidFor(company, memberId, itemId), data);

    public static string? WhyCannotEquip(this Company company, string memberId, string itemId, GameData data) =>
        company.WhyCannotEquip(memberId, UidFor(company, memberId, itemId), data);

    /// <summary>재료면 하나, 장비면 아이템 레벨이 가장 낮은 한 점을 판다.</summary>
    public static bool Sell(this Company company, string itemId, GameData data) =>
        data.Items.TryGetValue(itemId, out var item) && item.IsEquipment
            ? company.WorstGear(itemId) is { } gear && company.Sell(gear.Uid, data)
            : company.SellMaterial(itemId, data);

    private static int UidFor(Company company, string memberId, string itemId) =>
        company.Member(memberId).Gear.Values.FirstOrDefault(g => g.ItemId == itemId)?.Uid
        ?? company.BestGear(itemId)?.Uid
        ?? 0;
}
