namespace Triangle.Core.Skills;

/// <summary>기본 스탯 종류. 스킬의 훈련 속도를 정하는 1차·2차 스탯에 쓴다.</summary>
public enum Stat
{
    Str,
    Dex,
    Vital,
    Intel,
    Speed,
}

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
}

/// <param name="Level">1–5.</param>
public sealed record SkillRequirement(string SkillId, int Level);

/// <param name="Tag">null이면 모든 행동에 적용한다.</param>
public sealed record SkillBonus(BonusKind Kind, int PercentPerLevel, string? Tag = null);

/// <summary>
/// 훈련하는 패시브 스킬 (EVE Online 방식). 레벨 1–5, 랭크가 높을수록 훈련에 SP가 더 든다.
/// 행동을 직접 주지 않고, 보너스를 주거나 행동의 요구 조건이 된다.
/// </summary>
public sealed record SkillDefinition
{
    public const int MaxLevel = 5;

    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>편집기용 메모.</summary>
    public string? Description { get; init; }

    /// <summary>스킬 묶음 (화면에서 묶어 보여주는 용도, 예: "궁술").</summary>
    public string Group { get; init; } = "";

    /// <summary>훈련 난이도 배수. 레벨별 필요 SP에 곱한다.</summary>
    public int Rank { get; init; } = 1;

    /// <summary>훈련 속도에 영향을 주는 스탯 (EVE의 속성처럼).</summary>
    public Stat Primary { get; init; } = Stat.Intel;
    public Stat Secondary { get; init; } = Stat.Vital;

    public IReadOnlyList<SkillRequirement> Prerequisites { get; init; } = [];
    public IReadOnlyList<SkillBonus> Bonuses { get; init; } = [];
}
