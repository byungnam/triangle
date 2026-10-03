using Triangle.Core.Effects;
using Triangle.Core.Skills;

namespace Triangle.Core.Actions;

public enum ActionEffect
{
    Damage,
    Heal,

    /// <summary>MP 회복 (위력 × 지능 보정, 최대 MP까지).</summary>
    RestoreMp,

    /// <summary>피해·회복 없이 효과만 건다.</summary>
    None,
}

/// <summary>물리는 Str로 강해지고 Def로 경감된다. 마법은 Intel로 강해지고 MDef로 경감된다.</summary>
public enum DamageType
{
    Physical,
    Magical,
}

public enum TargetSide
{
    Enemy,
    Ally,
    Self,
}

public enum TargetRule
{
    Random,
    FrontFirst,
    BackFirst,
    LowestHpRatio,

    /// <summary>이 행동이 거는 첫 효과가 아직 없는 대상을 우선 (무작위). 모두 있으면 아무나.</summary>
    WithoutEffectFirst,
}

public enum RowRestriction
{
    Any,
    FrontOnly,
    BackOnly,
}

public enum TargetScope
{
    Single,
    All,
}

/// <summary>
/// 전술에서 쓰는 행동 (기본 공격, 화살, 치료 등). 쓸 수 있으려면 둘 다 채워야 한다:
/// - 장착한 아이템의 행동 칸에서 고른 행동이거나, 누구나 쓰는 공용 행동(<see cref="Universal"/>)이다.
/// - 요구 스킬 레벨(<see cref="Requirements"/>)을 채운다.
/// </summary>
public sealed record ActionDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>편집기용 메모. 게임 규칙에는 영향이 없다.</summary>
    public string? Description { get; init; }

    public int HpCost { get; init; }
    public int MpCost { get; init; }

    public ActionEffect Effect { get; init; } = ActionEffect.Damage;
    public DamageType DamageType { get; init; } = DamageType.Physical;
    public int Power { get; init; }

    public TargetSide Side { get; init; } = TargetSide.Enemy;
    public TargetRule Rule { get; init; } = TargetRule.Random;
    public RowRestriction Rows { get; init; } = RowRestriction.Any;
    public TargetScope Scope { get; init; } = TargetScope.Single;

    /// <summary>참이면 후위를 노릴 때 전위의 엄호를 받지 않는다.</summary>
    public bool IgnoresCover { get; init; }

    /// <summary>스킬 보너스가 어떤 행동에 적용되는지 정하는 분류. 장착한 무기 계열도 자동으로 더해진다.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>이 행동을 쓰려면 필요한 스킬 레벨.</summary>
    public IReadOnlyList<SkillRequirement> Requirements { get; init; } = [];

    /// <summary>대상에게 거는 효과 (피해·회복 뒤, 대상이 살아 있으면).</summary>
    public IReadOnlyList<EffectApplication> Applies { get; init; } = [];

    /// <summary>참이면 아이템 없이 누구나 쓴다 (예: 기본 공격). 거짓이면 아이템 행동 칸에서 골라야 한다.</summary>
    public bool Universal { get; init; }

    /// <summary>
    /// 고른 행동과 스킬 레벨로 이 행동을 쓸 수 있는가.
    /// <paramref name="granted"/>가 null이면 아이템 제한이 없다 (적 유닛).
    /// </summary>
    public bool IsUsableBy(IReadOnlySet<string>? granted, SkillSet skills) => IsGranted(granted) && skills.Meets(Requirements);

    /// <summary>공용 행동이거나 고른 행동인가 (스킬 요구는 따로 본다).</summary>
    public bool IsGranted(IReadOnlySet<string>? granted) => Universal || granted is null || granted.Contains(Id);

    /// <summary>
    /// 스킬 보너스를 고를 때 쓰는 태그: 행동의 태그 + 장착한 무기 계열.
    /// 그래서 "활 위력" 보너스는 활을 든 유닛의 기본 공격에도 붙는다.
    /// </summary>
    public IReadOnlyCollection<string> BonusTags(string? weapon) => weapon is null || Tags.Contains(weapon) ? Tags : [.. Tags, weapon];
}
