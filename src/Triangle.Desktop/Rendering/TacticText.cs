using Microsoft.Xna.Framework;
using Triangle.Core.Tactics;

namespace Triangle.Desktop.Rendering;

/// <summary>전술 조건과 값의 화면 표시, 기본값, 입력 검증.</summary>
internal static class TacticText
{
    public static bool HasValue(Condition condition) => condition.HasValue();

    /// <summary>입력칸 옆에 붙는 단위.</summary>
    public static string Unit(Condition condition) => condition switch
    {
        _ when condition.IsPercent() => "%",
        _ when condition.IsAmount() => IsHp(condition) ? "HP" : "MP",
        _ when condition.IsEnemyCount() || condition.IsAllyCount() => "명",
        _ when !condition.HasValue() => "",
        Condition.MaxUses => "회",
        Condition.EveryNthTurn => "턴마다",
        _ => "번째 턴",
    };

    /// <summary>
    /// 입력한 값이 이 조건에 맞지 않으면 이유, 맞으면 null.
    /// 백분율은 0–100, 턴 주기는 1 이상, 횟수·턴·인원수는 0 이상. 수치 조건은 검증하지 않는다.
    /// </summary>
    public static string? ValidationError(Condition condition, int value) => condition switch
    {
        _ when !condition.HasValue() => null,
        _ when condition.IsPercent() => value is < 0 or > 100 ? "0~100만" : null,
        _ when condition.IsAmount() => null,
        Condition.EveryNthTurn => value < 1 ? "1 이상만" : null,
        _ => value < 0 ? "0 이상만" : null,
    };

    public static int DefaultValue(Condition condition) => condition switch
    {
        _ when !condition.HasValue() => 0,
        Condition.AnyEnemyHpAtMost or Condition.AnyEnemyHpAtLeast => 30,
        _ when condition.IsPercent() => 50,
        _ when condition.IsAmount() => IsHp(condition) ? 300 : 50,
        _ when condition.IsEnemyCount() => 2,
        _ when condition.IsAllyCount() => 1,
        Condition.EveryNthTurn => 2,
        _ => 1,
    };

    /// <summary>조건을 바꿀 때 값의 종류(%, HP 수치, MP 수치, 횟수, 턴)가 같으면 값을 유지한다.</summary>
    public static int ValueAfterConditionChange(Condition from, Condition to, int value) =>
        Kind(from) == Kind(to) ? value : DefaultValue(to);

    private static string Kind(Condition condition) => condition switch
    {
        _ when !condition.HasValue() => "none",
        _ when condition.IsPercent() => "percent",
        _ when condition.IsAmount() => IsHp(condition) ? "hp" : "mp",
        _ when condition.IsEnemyCount() || condition.IsAllyCount() => "count",
        Condition.MaxUses => "uses",
        Condition.EveryNthTurn => "period",
        _ => "turn",
    };

    private static bool IsHp(Condition condition) => condition.ToString().Contains("Hp");

    // ── 조건을 세 칸(대상 · 이상/이하 · %/수치)으로 나눠 고르기 ─────────────

    /// <summary>조건 칸의 첫 드롭다운: 무엇을 보는가. 이름 접두어와 표시 이름, 색.</summary>
    public static readonly IReadOnlyList<(string Key, string Label, Color Color)> Subjects =
    [
        ("Always", "항상", Theme.Text),
        ("SelfHp", "자신 HP", Theme.Text),
        ("SelfMp", "자신 MP", Theme.Text),
        ("AnyAllyHp", "아군 누군가 HP", Theme.Heal),
        ("AnyAllyMp", "아군 누군가 MP", Theme.Heal),
        ("AllyAverageHp", "아군 평균 HP", Theme.Heal),
        ("AllyAverageMp", "아군 평균 MP", Theme.Heal),
        ("AllyDown", "쓰러진 아군 수", Theme.Heal),
        ("AnyAllyHasDebuff", "디버프 걸린 아군 있음", Theme.Heal),
        ("AnyAllyLacksActionEffect", "효과 없는 아군 있음", Theme.Heal),
        ("SelfLacksActionEffect", "자신에게 효과 없음", Theme.Text),
        ("AnyEnemyHp", "적 누군가 HP", Theme.Enemy),
        ("AnyEnemyLacksActionEffect", "효과 없는 적 있음", Theme.Enemy),
        ("EnemyAlive", "살아있는 적 수", Theme.Enemy),
        ("EnemyDead", "쓰러진 적 수", Theme.Enemy),
        ("EnemyFront", "적 전열 수", Theme.Enemy),
        ("EnemyBack", "적 후열 수", Theme.Enemy),
        ("MaxUses", "최대 사용 횟수", Theme.Cover),
        ("FromTurn", "지정 턴부터", Theme.Cover),
        ("UntilTurn", "지정 턴까지", Theme.Cover),
        ("OnTurn", "지정 턴에만", Theme.Cover),
        ("EveryNthTurn", "턴 주기", Theme.Cover),
    ];

    /// <summary>조건 → (대상 번호, 이상인가, %인가). 이상/이하나 %/수치가 없는 조건은 그 칸이 null.</summary>
    public static (int Subject, bool? AtLeast, bool? Percent) Split(Condition condition)
    {
        var name = condition.ToString();
        bool? atLeast = name.EndsWith("AtLeast") ? true : name.EndsWith("AtMost") ? false : null;
        var stem = atLeast is null ? name : name[..^(atLeast.Value ? "AtLeast".Length : "AtMost".Length)];
        bool? percent = null;
        if (condition.IsPercent() || condition.IsAmount())
        {
            percent = condition.IsPercent();
            stem = stem.Replace("Amount", "");
        }

        var index = Subjects.Select((s, i) => (s.Key, i)).First(s => s.Key == stem).i;
        return (index, atLeast, percent);
    }

    /// <summary>세 칸의 선택을 조건으로. 대상에 없는 칸은 무시하고, 필요한데 없으면 이상·%로 본다.</summary>
    public static Condition Compose(int subject, bool atLeast, bool percent)
    {
        var key = Subjects[subject].Key;
        if (Enum.TryParse<Condition>(key, out var plain))
        {
            return plain;
        }

        var compare = atLeast ? "AtLeast" : "AtMost";
        var isHpMp = key.EndsWith("Hp") || key.EndsWith("Mp");
        return Enum.Parse<Condition>(isHpMp && !percent ? $"{key}Amount{compare}" : $"{key}{compare}");
    }

    /// <summary>
    /// "자신 HP 50% 이하"처럼 조건과 값을 한 문장으로. 효과 조건은 <paramref name="effectName"/>(그 줄 행동이 거는 첫 효과)이
    /// 있으면 "자신에게 방패 벽 없음"처럼 효과 이름을 넣는다.
    /// </summary>
    public static string Sentence(Condition condition, int value, string? effectName = null)
    {
        var (subject, atLeast, _) = Split(condition);
        var label = Subjects[subject].Label;
        var effect = effectName ?? "이 행동의 효과";
        var compare = atLeast == true ? "이상" : "이하";
        return condition switch
        {
            Condition.Always => "항상",
            Condition.MaxUses => $"최대 {value}회",
            Condition.FromTurn => $"{value}번째 턴부터",
            Condition.UntilTurn => $"{value}번째 턴까지",
            Condition.OnTurn => $"{value}번째 턴에만",
            Condition.EveryNthTurn => $"{value}턴마다",
            Condition.SelfLacksActionEffect => $"자신에게 {effect} 없음",
            Condition.AnyAllyLacksActionEffect => $"{effect} 없는 아군 있음",
            Condition.AnyEnemyLacksActionEffect => $"{effect} 없는 적 있음",
            Condition.AnyAllyHasDebuff => "디버프 걸린 아군 있음",
            // "살아있는 적 수 2명 이상"이 아니라 "살아있는 적 2명 이상"
            _ when condition.IsEnemyCount() || condition.IsAllyCount() => $"{label[..^2]} {value}명 {compare}",
            _ => $"{label} {value}{(condition.IsPercent() ? "%" : "")} {compare}",
        };
    }

    /// <summary>전술 한 줄의 모든 조건: "4턴마다 그리고 자신 MP 35 이상".</summary>
    public static string Sentence(Tactic tactic, string? effectName = null) =>
        string.Join(" 그리고 ", tactic.Conditions.Select(c => Sentence(c.Condition, c.Value, effectName)));
}
