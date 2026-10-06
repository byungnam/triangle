using Triangle.Core.Effects;
using Triangle.Core.Skills;
using Triangle.Core.Units;

namespace Triangle.Core.Actions;

public enum ActionEffect
{
    Damage,
    Heal,

    /// <summary>MP 회복 (위력 × 지능 보정, 최대 MP까지).</summary>
    RestoreMp,

    /// <summary>피해·회복 없이 효과만 건다.</summary>
    None,

    /// <summary>보호막: 위력 × 지능 보정(회복 보너스 적용)만큼 피해를 먼저 흡수한다. 더 큰 보호막만 덮어쓴다.</summary>
    Shield,

    /// <summary>소환: <see cref="ActionDefinition.Summon"/>의 유닛을 시전자 편에 부른다. 대상은 자신이다.</summary>
    Summon,
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

    /// <summary>MP 비율이 가장 낮은 대상. 동률이면 앞쪽.</summary>
    LowestMpRatio,

    /// <summary>디버프가 걸린 대상을 우선 (무작위). 없으면 HP 비율이 가장 낮은 대상.</summary>
    WithDebuffFirst,
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

    /// <summary>
    /// 이 행동 뒤 대기 수치. 비우면 기본값(<c>CombatRules.TimeConstant</c>, 1000)이다.
    /// 실제 대기 = 이 수치 / 속도 (버림) × (100 − 대기 감소%) / 100.
    /// </summary>
    public int? Delay { get; init; }

    /// <summary>스킬 보너스가 어떤 행동에 적용되는지 정하는 분류. 장착한 무기 계열도 자동으로 더해진다.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>이 행동을 쓰려면 필요한 스킬 레벨.</summary>
    public IReadOnlyList<SkillRequirement> Requirements { get; init; } = [];

    /// <summary>대상에게 거는 효과 (피해·회복 뒤, 대상이 살아 있으면).</summary>
    public IReadOnlyList<EffectApplication> Applies { get; init; } = [];

    /// <summary>참이면 아이템 없이 누구나 쓴다 (예: 기본 공격). 거짓이면 아이템 행동 칸에서 골라야 한다.</summary>
    public bool Universal { get; init; }

    /// <summary>
    /// 이 계열 주무기를 들면 행동 칸 없이 쓸 수 있다 (예: 화염 무기의 정령 소환). 요구 스킬은 따로 본다.
    /// </summary>
    public string? Weapon { get; init; }

    /// <summary>참이면 소환된 유닛만 쓰는 행동이다. 파티의 전술 목록에는 나오지 않는다.</summary>
    public bool SummonOnly { get; init; }

    /// <summary>한 대상을 이만큼 연달아 친다 (1 = 한 번). 대상이 쓰러지면 멈춘다.</summary>
    public int Hits { get; init; } = 1;

    /// <summary>단일 대상 뒤에 같은 편의 다른 후보를 이만큼 더 무작위로 친다 (연쇄). 엄호는 받지 않는다.</summary>
    public int ExtraTargets { get; init; }

    /// <summary>처형: 대상이 잃은 HP 비율만큼 피해가 늘어난다. 100이면 HP 0에 가까울수록 최대 2배.</summary>
    public int MissingHpBonusPercent { get; init; }

    /// <summary>대상에게 이 효과가 걸려 있으면 피해가 늘어난다.</summary>
    public EffectBonus? BonusAgainst { get; init; }

    /// <summary>넘어뜨림: 대상의 다음 차례를 이 대기 수치(속도로 나눔)만큼 늦춘다. 영창도 끊는다.</summary>
    public int PushBack { get; init; }

    /// <summary>대상의 MP를 이만큼 깎는다.</summary>
    public int MpDamage { get; init; }

    /// <summary>대상을 이 줄로 옮긴다 (갈고리 당기기 → 전위, 밀쳐내기 → 후위).</summary>
    public Row? MoveTo { get; init; }

    /// <summary>참이면 대상의 디버프를 모두 지운다.</summary>
    public bool RemovesDebuffs { get; init; }

    /// <summary>영창 횟수. 2 이상이면 이 행동을 연속으로 이만큼 써야 효과가 난다. 다른 행동, 기절, 넘어뜨림은 처음부터 다시 하게 한다.</summary>
    public int Chant { get; init; } = 1;

    /// <summary>참이면 전투당 한 번만 효과를 낸다.</summary>
    public bool OncePerBattle { get; init; }

    /// <summary>소환하는 유닛 (<see cref="ActionEffect.Summon"/>).</summary>
    public SummonDefinition? Summon { get; init; }

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

/// <summary>대상에게 <paramref name="EffectId"/>가 걸려 있으면 피해 +<paramref name="Percent"/>%.</summary>
public sealed record EffectBonus(string EffectId, int Percent);

/// <summary>
/// 소환 유닛. 시전자의 무기 위력 배율과 소환 강화 스킬 레벨만큼 능력치가 오른다.
/// 시전자당 하나만 있고, 다시 부르면 바뀐다. 시전자가 쓰러지면 사라진다. 승패는 소환 유닛을 세지 않는다.
/// </summary>
/// <param name="Duration">소환 유닛 자신의 행동 횟수. null이면 전투가 끝날 때까지.</param>
/// <param name="ActionId">소환 유닛이 매 차례 쓰는 행동 (<see cref="ActionDefinition.SummonOnly"/>).</param>
/// <param name="StatSkill">레벨마다 능력치 +<paramref name="StatPercentPerLevel"/>%를 주는 스킬.</param>
public sealed record SummonDefinition(
    string Name,
    Row Row,
    Stats Stats,
    string ActionId,
    int? Duration = null,
    string? StatSkill = null,
    int StatPercentPerLevel = 0);
