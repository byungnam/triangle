namespace Triangle.Core.Masteries;

/// <summary>계열의 종류. 무기 계열은 주무기·보조, 방어구 계열(재질)은 머리·몸통·신발 아이템이 속한다.</summary>
public enum MasteryKind
{
    Weapon,
    Armor,
}

/// <summary>
/// 장비 계열이자 숙련 트리 (예: 활, 판금). 장착한 아이템의 계열로 싸우면 그 숙련이 오른다.
/// 숙련 레벨 1당 그 트리의 포인트 1점을 준다.
/// </summary>
public sealed record MasteryDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>편집기용 메모.</summary>
    public string? Description { get; init; }

    public required MasteryKind Kind { get; init; }
}
