using Triangle.Core.Tactics;

namespace Triangle.Desktop.Rendering;

/// <summary>전술 조건과 값의 화면 표시, 값 선택지.</summary>
internal static class TacticText
{
    public static readonly IReadOnlyList<Condition> Conditions = Enum.GetValues<Condition>();

    public static string ConditionLabel(Condition condition) => condition switch
    {
        Condition.Always => "항상",
        Condition.SelfHpAtLeast => "자신 HP 이상",
        Condition.SelfHpAtMost => "자신 HP 이하",
        Condition.SelfMpAtLeast => "자신 MP 이상",
        Condition.SelfMpAtMost => "자신 MP 이하",
        Condition.AnyAllyHpAtLeast => "아군 누군가 HP 이상",
        Condition.AnyAllyHpAtMost => "아군 누군가 HP 이하",
        Condition.AnyAllyMpAtLeast => "아군 누군가 MP 이상",
        Condition.AnyAllyMpAtMost => "아군 누군가 MP 이하",
        Condition.AllyAverageHpAtLeast => "아군 평균 HP 이상",
        Condition.AllyAverageHpAtMost => "아군 평균 HP 이하",
        Condition.AllyAverageMpAtLeast => "아군 평균 MP 이상",
        Condition.AllyAverageMpAtMost => "아군 평균 MP 이하",
        Condition.MaxUses => "최대 사용 횟수",
        Condition.FromTurn => "지정 턴부터",
        Condition.UntilTurn => "지정 턴까지",
        Condition.OnTurn => "지정 턴에만",
        Condition.EveryNthTurn => "턴 주기",
        _ => condition.ToString(),
    };

    public static bool HasValue(Condition condition) => condition != Condition.Always;

    public static bool IsPercent(Condition condition) =>
        condition is >= Condition.SelfHpAtLeast and <= Condition.AllyAverageMpAtMost;

    public static string ValueLabel(Condition condition, int value) => condition switch
    {
        _ when IsPercent(condition) => $"{value}%",
        Condition.MaxUses => $"{value}회",
        Condition.EveryNthTurn => $"{value}턴마다",
        Condition.Always => "",
        _ => $"{value}턴",
    };

    /// <summary>값 선택지. 현재 값이 선택지에 없으면(데이터에서 직접 넣은 값 등) 끼워 넣는다.</summary>
    public static IReadOnlyList<int> ValueOptions(Condition condition, int current)
    {
        var options = IsPercent(condition)
            ? Enumerable.Range(0, 11).Select(i => i * 10).ToList()
            : Enumerable.Range(1, 10).ToList();

        if (!options.Contains(current))
        {
            options.Add(current);
            options.Sort();
        }

        return options;
    }

    public static int DefaultValue(Condition condition) => condition switch
    {
        Condition.Always => 0,
        _ when IsPercent(condition) => 50,
        Condition.EveryNthTurn => 2,
        _ => 1,
    };

    /// <summary>조건을 바꿀 때 값의 종류(%, 횟수, 턴)가 같으면 값을 유지한다.</summary>
    public static int ValueAfterConditionChange(Condition from, Condition to, int value) =>
        Kind(from) == Kind(to) ? value : DefaultValue(to);

    private static int Kind(Condition condition) => condition switch
    {
        Condition.Always => 0,
        _ when IsPercent(condition) => 1,
        Condition.MaxUses => 2,
        Condition.EveryNthTurn => 3,
        _ => 4,
    };
}
