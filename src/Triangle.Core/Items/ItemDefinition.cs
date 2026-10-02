namespace Triangle.Core.Items;

/// <summary>
/// 장비 아이템. 계열(<see cref="Mastery"/>)에 속하고, 슬롯(무기·방어구)은 계열이 정한다.
/// 전투와 숙련은 계열로 판단한다. 아이템별 수치 차이는 아직 없다.
/// </summary>
public sealed record ItemDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>편집기용 메모.</summary>
    public string? Description { get; init; }

    /// <summary>장비 계열(숙련) ID.</summary>
    public required string Mastery { get; init; }

    /// <summary>가격 (골드). 아직 상점이 없어 표시용이다.</summary>
    public int Price { get; init; }
}
