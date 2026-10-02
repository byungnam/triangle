using Triangle.Core.Skills;

namespace Triangle.Core.Actions;

public enum ActionEffect
{
    Damage,
    Heal,
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

/// <summary>전술에서 쓰는 행동 (기본 공격, 화살, 치료 등). 요구 스킬 레벨을 채워야 쓸 수 있다.</summary>
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

    /// <summary>스킬 보너스가 어떤 행동에 적용되는지 정하는 분류 (예: "bow", "magic", "holy").</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>이 행동을 쓰려면 필요한 스킬 레벨.</summary>
    public IReadOnlyList<SkillRequirement> Requirements { get; init; } = [];
}
