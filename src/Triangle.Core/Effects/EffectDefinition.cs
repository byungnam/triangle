namespace Triangle.Core.Effects;

public enum EffectKind
{
    Buff,
    Debuff,
}

/// <summary>효과가 주는 보정의 종류. 값은 정수 %, 음수도 된다.</summary>
public enum EffectModifierKind
{
    /// <summary>주는 피해 증감.</summary>
    PowerPercent,

    /// <summary>방어(물리·마법) 증감.</summary>
    DefensePercent,

    /// <summary>행동 뒤 대기 감소. 음수면 둔화.</summary>
    DelayReductionPercent,

    /// <summary>받는 피해 감소. 음수면 취약.</summary>
    DamageTakenReductionPercent,
}

public sealed record EffectModifier(EffectModifierKind Kind, int Percent);

/// <summary>
/// 버프·디버프·지속 피해·지속 회복. 지속시간은 효과를 받은 유닛의 행동 횟수로 센다.
/// 그 유닛의 턴이 시작될 때 매 턴 HP 변화가 들어가고, 턴이 끝날 때 남은 횟수가 줄어든다.
/// </summary>
public sealed record EffectDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>편집기용 메모.</summary>
    public string? Description { get; init; }

    public EffectKind Kind { get; init; } = EffectKind.Buff;

    public IReadOnlyList<EffectModifier> Modifiers { get; init; } = [];

    /// <summary>턴 시작마다 최대 HP의 이 %만큼 HP가 변한다. 음수면 지속 피해, 양수면 지속 회복.</summary>
    public int TickHpPercent { get; init; }
}

/// <summary>행동이 대상에게 거는 효과와 지속시간(대상의 행동 횟수).</summary>
public sealed record EffectApplication(string EffectId, int Duration);
