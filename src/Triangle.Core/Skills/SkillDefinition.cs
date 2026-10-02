namespace Triangle.Core.Skills;

public enum SkillEffect
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

public sealed record SkillDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    public int HpCost { get; init; }
    public int MpCost { get; init; }

    public SkillEffect Effect { get; init; } = SkillEffect.Damage;
    public DamageType DamageType { get; init; } = DamageType.Physical;
    public int Power { get; init; }

    public TargetSide Side { get; init; } = TargetSide.Enemy;
    public TargetRule Rule { get; init; } = TargetRule.Random;
    public RowRestriction Rows { get; init; } = RowRestriction.Any;
    public TargetScope Scope { get; init; } = TargetScope.Single;

    /// <summary>참이면 후위를 노릴 때 전위의 엄호를 받지 않는다.</summary>
    public bool IgnoresCover { get; init; }
}
