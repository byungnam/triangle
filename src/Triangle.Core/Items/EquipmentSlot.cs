namespace Triangle.Core.Items;

/// <summary>
/// 아이템이 들어가는 부위. 판에서 멤버는 무기(주무기), 방어구(몸통), 장신구 둘만 낀다(로그라이트 재편).
/// 보조·머리·신발은 적과 전술 측정의 기준 파티가 쓴다.
/// </summary>
public enum EquipmentSlot
{
    MainHand,
    OffHand,
    Head,
    Body,
    Feet,

    /// <summary>
    /// 장신구 (로그라이트 재편, 2026-10-19). 한 사람이 둘까지 낀다. 계열과 행동이 없고, 보너스와 전투를 시작할 때 걸리는 효과로
    /// 규칙을 바꾼다. 판의 상점과 이벤트에서 얻는다.
    /// </summary>
    Trinket,
}

public static class EquipmentSlots
{
    /// <summary>장신구가 아닌 장착 부위 5개 (표시 순서, 적과 전술 측정의 장비).</summary>
    public static IReadOnlyList<EquipmentSlot> All { get; } =
        [EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Feet];

    /// <summary>방어구 부위 (머리, 몸통, 신발).</summary>
    public static IReadOnlyList<EquipmentSlot> Armor { get; } = [EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Feet];

    public static bool IsWeapon(this EquipmentSlot slot) => slot is EquipmentSlot.MainHand or EquipmentSlot.OffHand;

    public static bool IsArmor(this EquipmentSlot slot) => slot is EquipmentSlot.Head or EquipmentSlot.Body or EquipmentSlot.Feet;

}
