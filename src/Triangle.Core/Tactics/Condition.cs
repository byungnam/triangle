namespace Triangle.Core.Tactics;

/// <summary>
/// 전술 조건. HP/MP 조건은 두 가지다:
/// - 백분율 조건(…AtLeast/…AtMost): 값은 최대치 대비 %(0–100).
/// - 수치 조건(…AmountAtLeast/…AmountAtMost): 값은 실제 HP·MP. 범위 제한이 없다.
/// 턴 조건의 값은 해당 유닛 자신의 행동 횟수(1부터)다.
/// JSON에는 이름으로 저장되므로 새 조건은 끝에 덧붙인다.
/// </summary>
public enum Condition
{
    Always,

    SelfHpAtLeast,
    SelfHpAtMost,
    SelfMpAtLeast,
    SelfMpAtMost,

    /// <summary>살아있는 아군(자신 포함) 중 한 명이라도 해당하면 참.</summary>
    AnyAllyHpAtLeast,
    AnyAllyHpAtMost,
    AnyAllyMpAtLeast,
    AnyAllyMpAtMost,

    /// <summary>살아있는 아군(자신 포함)의 비율 평균.</summary>
    AllyAverageHpAtLeast,
    AllyAverageHpAtMost,
    AllyAverageMpAtLeast,
    AllyAverageMpAtMost,

    /// <summary>이 전술의 조건이 참으로 판정된 횟수가 값보다 적으면 참. 턴을 잃은 경우도 센다.</summary>
    MaxUses,
    FromTurn,
    UntilTurn,
    OnTurn,
    EveryNthTurn,

    SelfHpAmountAtLeast,
    SelfHpAmountAtMost,
    SelfMpAmountAtLeast,
    SelfMpAmountAtMost,

    /// <summary>살아있는 아군(자신 포함) 중 한 명이라도 해당하면 참.</summary>
    AnyAllyHpAmountAtLeast,
    AnyAllyHpAmountAtMost,
    AnyAllyMpAmountAtLeast,
    AnyAllyMpAmountAtMost,

    /// <summary>살아있는 아군(자신 포함)의 평균. 정수로 비교한다(합계 ≷ 값 × 인원).</summary>
    AllyAverageHpAmountAtLeast,
    AllyAverageHpAmountAtMost,
    AllyAverageMpAmountAtLeast,
    AllyAverageMpAmountAtMost,
}

public static class ConditionKinds
{
    /// <summary>값이 최대치 대비 %인 조건 (0–100).</summary>
    public static bool IsPercent(this Condition condition) =>
        condition is >= Condition.SelfHpAtLeast and <= Condition.AllyAverageMpAtMost;

    /// <summary>값이 실제 HP·MP 수치인 조건 (범위 제한 없음).</summary>
    public static bool IsAmount(this Condition condition) =>
        condition is >= Condition.SelfHpAmountAtLeast and <= Condition.AllyAverageMpAmountAtMost;
}
