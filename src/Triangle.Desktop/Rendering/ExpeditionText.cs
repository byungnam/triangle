using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;

namespace Triangle.Desktop.Rendering;

/// <summary>원정 요약을 화면 줄로 바꾼다 (원정 화면, 전투 기록 끝, 마을 안내).</summary>
internal static class ExpeditionText
{
    public static (string Text, Microsoft.Xna.Framework.Color Color) Outcome(CombatOutcome outcome) => outcome switch
    {
        CombatOutcome.Victory => ("승리", Theme.Heal),
        CombatOutcome.Defeat => ("패배", Theme.Enemy),
        _ => ("무승부", Theme.TextDim),
    };

    /// <summary>"낡은 검 2, 판금 갑옷"처럼 아이템 목록.</summary>
    public static string Items(GameData data, IEnumerable<string> itemIds) =>
        string.Join(", ", itemIds.GroupBy(id => id).Select(g => g.Count() == 1 ? Name(data, g.Key) : $"{Name(data, g.Key)} {g.Count()}"));

    public static string Items(GameData data, IReadOnlyDictionary<string, int> counts) =>
        string.Join(", ", counts.Select(p => p.Value == 1 ? Name(data, p.Key) : $"{Name(data, p.Key)} {p.Value}"));

    private static string Name(GameData data, string itemId) => data.Items[itemId].Name;

    /// <summary>전투 하나의 결과: 경험치, 전리품, 쓰러짐, 사망, 장비 파괴.</summary>
    public static IReadOnlyList<LogLine> Battle(GameData data, BattleReport report)
    {
        var lines = new List<LogLine>();
        foreach (var member in report.Xp.GroupBy(x => x.MemberId))
        {
            var parts = member.Select(x =>
            {
                var name = data.Masteries[x.Mastery].Name;
                return x.LevelsGained > 0 ? $"{name} +{x.Xp} (레벨 업, 포인트 +{x.LevelsGained})" : $"{name} +{x.Xp}";
            });
            var levelUp = member.Any(x => x.LevelsGained > 0);
            lines.Add(new LogLine($"{member.First().MemberName}: {string.Join(" · ", parts)}", levelUp ? Theme.Heal : Theme.TextDim));
        }

        if (report.Gold > 0)
        {
            lines.Add(new LogLine($"골드 +{report.Gold} (들고 있는 전리품)", Theme.Cover));
        }

        if (report.Drops.Count > 0)
        {
            lines.Add(new LogLine($"아이템: {Items(data, report.Drops)}", Theme.Cover));
        }

        if (report.Deaths.Count > 0)
        {
            lines.Add(new LogLine($"사망 (로스터에서 삭제): {string.Join(", ", report.Deaths)}", Theme.Enemy));
        }
        else if (report.Downed.Count > 0)
        {
            lines.Add(new LogLine($"쓰러짐 (남은 전투에 못 나감): {string.Join(", ", report.Downed)}", Theme.Damage));
        }

        if (report.Destroyed.Count > 0)
        {
            lines.Add(new LogLine($"파괴된 장비: {Items(data, report.Destroyed)}", Theme.Enemy));
        }

        if (report.Recovered.Count > 0)
        {
            lines.Add(new LogLine($"회수한 장비: {Items(data, report.Recovered)}", Theme.Cover));
        }

        return lines;
    }

    /// <summary>끝난 원정 한 줄 요약 (마을 안내).</summary>
    public static (string Text, Microsoft.Xna.Framework.Color Color) Summary(GameData data, ExpeditionSummary summary)
    {
        var zone = data.Zones[summary.ZoneId].Name;
        var end = summary.End switch
        {
            ExpeditionEnd.Cleared => "클리어",
            ExpeditionEnd.Returned => "귀환",
            ExpeditionEnd.ForcedReturn => "무승부로 귀환",
            _ => "전멸, 전리품을 잃었습니다",
        };
        var parts = new List<string> { $"{zone} 원정 {end} ({summary.Battles}전)" };
        if (summary.End != ExpeditionEnd.Wiped)
        {
            parts.Add($"골드 +{summary.Gold}");
            if (summary.Items.Count > 0)
            {
                parts.Add($"아이템: {Items(data, summary.Items)}");
            }
        }

        if (summary.Deaths.Count > 0)
        {
            parts.Add($"사망: {string.Join(", ", summary.Deaths)}");
        }

        var color = summary.End == ExpeditionEnd.Wiped || summary.Deaths.Count > 0 ? Theme.Enemy : Theme.Heal;
        return (string.Join(" · ", parts), color);
    }
}
