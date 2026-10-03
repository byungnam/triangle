namespace Triangle.Core.Skills;

/// <summary>스킬 레벨마다 주는 보너스의 종류. 값은 모두 정수 백분율이다.</summary>
public enum BonusKind
{
    /// <summary>위력 증가 (피해, 회복 모두). Tag가 있으면 그 태그의 행동에만.</summary>
    PowerPercent,

    /// <summary>회복량 증가.</summary>
    HealPercent,

    /// <summary>MP 소모 감소.</summary>
    MpCostReductionPercent,

    /// <summary>행동 뒤 대기 감소 ("연사"). Tag가 있으면 그 태그의 행동 뒤에만.</summary>
    DelayReductionPercent,

    MaxHpPercent,
    MaxMpPercent,

    /// <summary>받는 피해 감소.</summary>
    DamageTakenReductionPercent,

    /// <summary>방어(물리·마법) 증가. 주로 방어구가 준다.</summary>
    DefensePercent,
}

/// <param name="Level">1–5.</param>
public sealed record SkillRequirement(string SkillId, int Level);

/// <param name="Tag">null이면 모든 행동에 적용한다.</param>
public sealed record SkillBonus(BonusKind Kind, int PercentPerLevel, string? Tag = null);

/// <summary>
/// 숙련 트리의 패시브 스킬. 레벨 1–5. 그 트리의 숙련 포인트로 배운다 (Albion 데스티니 보드처럼
/// 장비를 써서 숙련을 올리고, 숙련 레벨이 주는 포인트로 찍는다).
/// 행동을 직접 주지 않고, 보너스를 주거나 행동의 요구 조건이 된다.
/// </summary>
public sealed record SkillDefinition
{
    public const int MaxLevel = 5;

    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>편집기용 메모.</summary>
    public string? Description { get; init; }

    /// <summary>이 스킬이 속한 숙련 트리 (masteries.json의 ID). 그 숙련의 포인트로만 배운다.</summary>
    public required string Mastery { get; init; }

    /// <summary>레벨 하나를 올리는 데 드는 포인트.</summary>
    public int Rank { get; init; } = 1;

    public IReadOnlyList<SkillRequirement> Prerequisites { get; init; } = [];
    public IReadOnlyList<SkillBonus> Bonuses { get; init; } = [];
}
