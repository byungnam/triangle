namespace Triangle.Core.Tactics;

/// <summary>
/// 전술 조건. HP/MP 조건의 값은 백분율(0–100)이고,
/// 턴 조건의 값은 해당 유닛 자신의 행동 횟수(1부터)다.
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
}
