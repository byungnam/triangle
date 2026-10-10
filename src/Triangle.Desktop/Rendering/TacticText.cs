using Triangle.Core.Tactics;

namespace Triangle.Desktop.Rendering;

/// <summary>전술 조건과 값의 화면 표시, 기본값, 입력 검증.</summary>
internal static class TacticText
{
    /// <summary>드롭다운 순서: 같은 대상끼리 %와 수치를 나란히 둔다.</summary>
    public static readonly IReadOnlyList<Condition> Conditions =
    [
        Condition.Always,
        Condition.SelfHpAtLeast, Condition.SelfHpAtMost, Condition.SelfHpAmountAtLeast, Condition.SelfHpAmountAtMost,
        Condition.SelfMpAtLeast, Condition.SelfMpAtMost, Condition.SelfMpAmountAtLeast, Condition.SelfMpAmountAtMost,
        Condition.AnyAllyHpAtLeast, Condition.AnyAllyHpAtMost, Condition.AnyAllyHpAmountAtLeast, Condition.AnyAllyHpAmountAtMost,
        Condition.AnyAllyMpAtLeast, Condition.AnyAllyMpAtMost, Condition.AnyAllyMpAmountAtLeast, Condition.AnyAllyMpAmountAtMost,
        Condition.AllyAverageHpAtLeast, Condition.AllyAverageHpAtMost, Condition.AllyAverageHpAmountAtLeast, Condition.AllyAverageHpAmountAtMost,
        Condition.AllyAverageMpAtLeast, Condition.AllyAverageMpAtMost, Condition.AllyAverageMpAmountAtLeast, Condition.AllyAverageMpAmountAtMost,
        Condition.EnemyAliveAtLeast, Condition.EnemyAliveAtMost, Condition.EnemyDeadAtLeast, Condition.EnemyDeadAtMost,
        Condition.EnemyFrontAtLeast, Condition.EnemyFrontAtMost, Condition.EnemyBackAtLeast, Condition.EnemyBackAtMost,
        Condition.MaxUses, Condition.FromTurn, Condition.UntilTurn, Condition.OnTurn, Condition.EveryNthTurn,
    ];

    public static string ConditionLabel(Condition condition) => condition switch
    {
        Condition.Always => "항상",
        Condition.MaxUses => "최대 사용 횟수",
        Condition.FromTurn => "지정 턴부터",
        Condition.UntilTurn => "지정 턴까지",
        Condition.OnTurn => "지정 턴에만",
        Condition.EveryNthTurn => "턴 주기",
        _ when condition.IsEnemyCount() => $"{EnemySubject(condition)} {(IsAtLeast(condition) ? "이상" : "이하")}",
        _ => $"{Subject(condition)} {(IsHp(condition) ? "HP" : "MP")} {(IsAtLeast(condition) ? "이상" : "이하")} ({(condition.IsAmount() ? "수치" : "%")})",
    };

    public static bool HasValue(Condition condition) => condition != Condition.Always;

    /// <summary>입력칸 옆에 붙는 단위.</summary>
    public static string Unit(Condition condition) => condition switch
    {
        _ when condition.IsPercent() => "%",
        _ when condition.IsAmount() => IsHp(condition) ? "HP" : "MP",
        _ when condition.IsEnemyCount() => "명",
        Condition.MaxUses => "회",
        Condition.EveryNthTurn => "턴마다",
        Condition.Always => "",
        _ => "번째 턴",
    };

    /// <summary>
    /// 입력한 값이 이 조건에 맞지 않으면 이유, 맞으면 null.
    /// 백분율은 0–100, 턴 주기는 1 이상, 횟수·턴·인원수는 0 이상. 수치 조건은 검증하지 않는다.
    /// </summary>
    public static string? ValidationError(Condition condition, int value) => condition switch
    {
        _ when condition.IsPercent() => value is < 0 or > 100 ? "0~100만" : null,
        _ when condition.IsAmount() => null,
        Condition.EveryNthTurn => value < 1 ? "1 이상만" : null,
        Condition.Always => null,
        _ => value < 0 ? "0 이상만" : null,
    };

    public static int DefaultValue(Condition condition) => condition switch
    {
        Condition.Always => 0,
        _ when condition.IsPercent() => 50,
        _ when condition.IsAmount() => IsHp(condition) ? 300 : 50,
        _ when condition.IsEnemyCount() => 2,
        Condition.EveryNthTurn => 2,
        _ => 1,
    };

    /// <summary>조건을 바꿀 때 값의 종류(%, HP 수치, MP 수치, 횟수, 턴)가 같으면 값을 유지한다.</summary>
    public static int ValueAfterConditionChange(Condition from, Condition to, int value) =>
        Kind(from) == Kind(to) ? value : DefaultValue(to);

    private static string Kind(Condition condition) => condition switch
    {
        Condition.Always => "none",
        _ when condition.IsPercent() => "percent",
        _ when condition.IsAmount() => IsHp(condition) ? "hp" : "mp",
        _ when condition.IsEnemyCount() => "count",
        Condition.MaxUses => "uses",
        Condition.EveryNthTurn => "period",
        _ => "turn",
    };

    private static string Subject(Condition condition) => condition.ToString() switch
    {
        var n when n.StartsWith("Self") => "자신",
        var n when n.StartsWith("AnyAlly") => "아군 누군가",
        _ => "아군 평균",
    };

    private static string EnemySubject(Condition condition) => condition.ToString() switch
    {
        var n when n.StartsWith("EnemyAlive") => "살아있는 적",
        var n when n.StartsWith("EnemyDead") => "쓰러진 적",
        var n when n.StartsWith("EnemyFront") => "적 전열",
        _ => "적 후열",
    };

    private static bool IsHp(Condition condition) => condition.ToString().Contains("Hp");

    private static bool IsAtLeast(Condition condition) => condition.ToString().EndsWith("AtLeast");
}
