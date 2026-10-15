namespace Triangle.Core.Tactics;

/// <summary>
/// 전술 조건. HP/MP 조건은 두 가지다:
/// - 백분율 조건(…AtLeast/…AtMost): 값은 최대치 대비 %(0–100).
/// - 수치 조건(…AmountAtLeast/…AmountAtMost): 값은 실제 HP·MP. 범위 제한이 없다.
/// 턴 조건의 값은 해당 유닛 자신의 행동 횟수(1부터)다.
/// 적 수 조건의 값은 인원수다. 적은 수와 상태로만 본다: 특정 적을 가리키는 조건은 두지 않는다(대상은 행동이 정한다).
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

    /// <summary>살아있는 적(소환물 포함)의 수.</summary>
    EnemyAliveAtLeast,
    EnemyAliveAtMost,

    /// <summary>쓰러진 적(소환물 포함)의 수.</summary>
    EnemyDeadAtLeast,
    EnemyDeadAtMost,

    /// <summary>전열·후열에 살아있는 적의 수 (지금 줄 기준).</summary>
    EnemyFrontAtLeast,
    EnemyFrontAtMost,
    EnemyBackAtLeast,
    EnemyBackAtMost,

    /// <summary>살아있는 적(소환물 포함) 중 한 명이라도 해당하면 참 (2026-10-15). 누구를 노릴지는 여전히 행동이 정한다.</summary>
    AnyEnemyHpAtLeast,
    AnyEnemyHpAtMost,
    AnyEnemyHpAmountAtLeast,
    AnyEnemyHpAmountAtMost,

    /// <summary>쓰러진 아군(소환물 포함, 지속 시간이 끝나 사라진 소환물은 빼고)의 수.</summary>
    AllyDownAtLeast,
    AllyDownAtMost,

    /// <summary>
    /// 그 전술의 행동이 거는 첫 효과가 자신에게 / 살아있는 아군 누군가에게 / 살아있는 적 누군가에게 없으면 참.
    /// 값은 쓰지 않는다. 효과를 걸지 않는 행동이면 늘 거짓이다.
    /// </summary>
    SelfLacksActionEffect,
    AnyAllyLacksActionEffect,
    AnyEnemyLacksActionEffect,

    /// <summary>살아있는 아군(자신·소환물 포함) 중 디버프가 걸린 유닛이 있으면 참. 값은 쓰지 않는다.</summary>
    AnyAllyHasDebuff,
}

public static class ConditionKinds
{
    /// <summary>값이 최대치 대비 %인 조건 (0–100).</summary>
    public static bool IsPercent(this Condition condition) =>
        condition is >= Condition.SelfHpAtLeast and <= Condition.AllyAverageMpAtMost
            or Condition.AnyEnemyHpAtLeast or Condition.AnyEnemyHpAtMost;

    /// <summary>값이 실제 HP·MP 수치인 조건 (범위 제한 없음).</summary>
    public static bool IsAmount(this Condition condition) =>
        condition is >= Condition.SelfHpAmountAtLeast and <= Condition.AllyAverageMpAmountAtMost
            or Condition.AnyEnemyHpAmountAtLeast or Condition.AnyEnemyHpAmountAtMost;

    /// <summary>값이 적 인원수인 조건 (0 이상).</summary>
    public static bool IsEnemyCount(this Condition condition) =>
        condition is >= Condition.EnemyAliveAtLeast and <= Condition.EnemyBackAtMost;

    /// <summary>값이 아군 인원수인 조건 (0 이상).</summary>
    public static bool IsAllyCount(this Condition condition) =>
        condition is Condition.AllyDownAtLeast or Condition.AllyDownAtMost;

    /// <summary>그 전술의 행동이 거는 효과를 보는 조건.</summary>
    public static bool UsesActionEffect(this Condition condition) =>
        condition is Condition.SelfLacksActionEffect or Condition.AnyAllyLacksActionEffect or Condition.AnyEnemyLacksActionEffect;

    /// <summary>값을 쓰는 조건인가 (항상, 효과·디버프 조건은 값이 없다).</summary>
    public static bool HasValue(this Condition condition) =>
        condition is not Condition.Always and not Condition.AnyAllyHasDebuff && !condition.UsesActionEffect();
}
