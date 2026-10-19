namespace Triangle.Core.Items;

/// <summary>아이템이 들어가는 부위 (Albion식). <see cref="Material"/>은 끼지 않는 제작 재료다.</summary>
public enum EquipmentSlot
{
    MainHand,
    OffHand,
    Head,
    Body,
    Feet,

    /// <summary>제작 재료. 장착할 수 없고 창고에 쌓인다.</summary>
    Material,

    /// <summary>
    /// 장신구 (로그라이트 재편, 2026-10-19). 한 사람이 둘까지 낀다. 계열과 행동이 없고, 보너스와 전투를 시작할 때 걸리는 효과로
    /// 규칙을 바꾼다. 원정 안의 상점에서만 얻으므로 지금 게임의 상점·전리품·편성 부위(<see cref="EquipmentSlots.All"/>)에는 없다.
    /// </summary>
    Trinket,
}

public static class EquipmentSlots
{
    /// <summary>장착하는 부위 5개 (표시 순서).</summary>
    public static IReadOnlyList<EquipmentSlot> All { get; } =
        [EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Feet];

    /// <summary>방어구 부위 (머리, 몸통, 신발). 방어구 숙련 경험치를 이 수로 나눈다.</summary>
    public static IReadOnlyList<EquipmentSlot> Armor { get; } = [EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Feet];

    public static bool IsWeapon(this EquipmentSlot slot) => slot is EquipmentSlot.MainHand or EquipmentSlot.OffHand;

    public static bool IsArmor(this EquipmentSlot slot) => slot is EquipmentSlot.Head or EquipmentSlot.Body or EquipmentSlot.Feet;

    public static bool IsEquipment(this EquipmentSlot slot) => slot != EquipmentSlot.Material;

    /// <summary>지금 게임(마을·원정)에서 끼는 부위인가. 장신구는 로그라이트 재편의 원정에서만 쓴다.</summary>
    public static bool IsCompanySlot(this EquipmentSlot slot) => All.Contains(slot);
}
