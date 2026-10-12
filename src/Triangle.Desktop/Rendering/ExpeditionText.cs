using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;

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

    /// <summary>"T1 낡은 검 2, T2 강철 판금 갑옷"처럼 아이템 목록 (장비는 티어를 붙인다).</summary>
    public static string Items(GameData data, IEnumerable<string> itemIds) =>
        string.Join(", ", itemIds.GroupBy(id => id).Select(g => g.Count() == 1 ? Name(data, g.Key) : $"{Name(data, g.Key)} {g.Count()}"));

    public static string Items(GameData data, IReadOnlyDictionary<string, int> counts) =>
        string.Join(", ", counts.Select(p => p.Value == 1 ? Name(data, p.Key) : $"{Name(data, p.Key)} {p.Value}"));

    private static string Name(GameData data, string itemId) => ItemText.ShortName(data.Items[itemId]);

    /// <summary>전투 하나의 결과: 경험치, 전리품, 쓰러짐.</summary>
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

        if (report.Downed.Count > 0)
        {
            lines.Add(new LogLine($"쓰러짐 (남은 전투에 못 나감): {string.Join(", ", report.Downed)}", Theme.Damage));
        }

        return lines;
    }

    /// <summary>끝난 원정 한 줄 요약 (마을 안내). 관문 도전이면 승패와 첫 보상, 좋아진 기록.</summary>
    public static (string Text, Microsoft.Xna.Framework.Color Color) Summary(GameData data, ExpeditionSummary summary)
    {
        var zone = data.Zones[summary.ZoneId];
        var parts = new List<string>();
        if (summary.Gate)
        {
            var boss = data.Encounters[zone.Gate!.EncounterId].Name;
            parts.Add(summary.GateWin switch
            {
                { FirstWin: true } => $"관문 돌파: {boss}",
                { } win => win.Improved.Count > 0 ? $"{boss} 승리 · 새 기록: {Improved(win)}" : $"{boss} 승리",
                null => $"{boss}에게 패배",
            });
        }
        else
        {
            var end = summary.End switch
            {
                ExpeditionEnd.Cleared => "클리어",
                ExpeditionEnd.ForcedReturn => "무승부로 귀환",
                _ => "전멸",
            };
            parts.Add($"{zone.Name} 원정 {end} ({summary.Battles}전)");
        }

        if (summary.Gold > 0 || !summary.Gate)
        {
            parts.Add($"골드 +{summary.Gold}");
        }

        if (summary.Items.Count > 0)
        {
            parts.Add($"아이템: {Items(data, summary.Items)}");
        }

        var color = summary.End == ExpeditionEnd.Cleared || (!summary.Gate && summary.End == ExpeditionEnd.ForcedReturn) ? Theme.Heal : Theme.Enemy;
        return (string.Join(" · ", parts), color);
    }

    /// <summary>관문을 이긴 판의 결과 줄: 이번 판의 기록, 좋아진 기록, 처음이면 단서.</summary>
    public static IReadOnlyList<LogLine> GateWin(GameData data, ExpeditionSummary summary)
    {
        if (summary.GateWin is not { } win)
        {
            return [];
        }

        var gate = data.Zones[summary.ZoneId].Gate!;
        var r = win.Result;
        var lines = new List<LogLine>
        {
            new($"이번 판: {r.Members}명 · 장비 {Tier(r.GearTier)} · 쓰러짐 {r.Downed}명", Theme.TextDim),
        };
        if (win.FirstWin)
        {
            lines.Add(new LogLine("관문을 처음 넘었습니다. 다음 깊이가 열립니다.", Theme.Heal));
            if (gate.Clue is { } clue)
            {
                lines.Add(new LogLine($"단서: {clue}", Theme.Cover));
            }
        }
        else if (win.Improved.Count > 0)
        {
            lines.Add(new LogLine($"새 기록: {Improved(win)}", Theme.Heal));
        }

        return lines;
    }

    /// <summary>"최소 인원 3명 · 최저 장비 T1 · 최소 쓰러짐 0명".</summary>
    public static string GateRecord(GateRecord record) =>
        $"최소 인원 {record.FewestMembers}명 · 최저 장비 {Tier(record.LowestGearTier)} · 최소 쓰러짐 {record.FewestDowned}명";

    /// <summary>관문 첫 보상: "골드 150, 쇠붙이, 질긴 가죽".</summary>
    public static string GateReward(GameData data, GateDefinition gate)
    {
        var parts = new List<string>();
        if (gate.FirstWinGold > 0)
        {
            parts.Add($"골드 {gate.FirstWinGold}");
        }

        if (gate.FirstWinItems.Count > 0)
        {
            parts.Add(Items(data, gate.FirstWinItems));
        }

        return parts.Count == 0 ? "다음 깊이가 열립니다" : string.Join(", ", parts);
    }

    private static string Tier(int tier) => tier == 0 ? "없음" : $"T{tier}";

    private static string Improved(GateWin win) => string.Join(", ", win.Improved.Select(k => k switch
    {
        GateRecordKind.FewestMembers => $"{win.Result.Members}명",
        GateRecordKind.LowestGearTier => $"장비 {Tier(win.Result.GearTier)}",
        _ => $"쓰러짐 {win.Result.Downed}명",
    }));
}
