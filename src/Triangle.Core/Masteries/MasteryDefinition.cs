namespace Triangle.Core.Masteries;

public enum EquipmentSlot
{
    Weapon,
    Armor,
}

/// <summary>
/// 장비 계열이자 숙련 트리 (예: 활, 판금). 유닛은 무기 계열 하나와 방어구 계열 하나를 장착하고,
/// 장착한 계열로 싸우면 그 숙련이 오른다. 숙련 레벨 1당 그 트리의 포인트 1점을 준다.
/// </summary>
public sealed record MasteryDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>편집기용 메모.</summary>
    public string? Description { get; init; }

    public required EquipmentSlot Slot { get; init; }
}
