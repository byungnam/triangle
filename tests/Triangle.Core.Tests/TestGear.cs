using Triangle.Core.Items;

namespace Triangle.Core.Tests;

/// <summary>테스트용 부위별 장비 사전.</summary>
internal static class TestGear
{
    public static Dictionary<EquipmentSlot, string> Of(string? mainHand = null, string? body = null, string? offHand = null, string? head = null, string? feet = null)
    {
        var gear = new Dictionary<EquipmentSlot, string>();
        foreach (var (slot, item) in new[]
                 {
                     (EquipmentSlot.MainHand, mainHand), (EquipmentSlot.OffHand, offHand), (EquipmentSlot.Head, head),
                     (EquipmentSlot.Body, body), (EquipmentSlot.Feet, feet),
                 })
        {
            if (item is not null)
            {
                gear[slot] = item;
            }
        }

        return gear;
    }
}
