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
        if (report.FirstMeet)
        {
            lines.Add(new LogLine($"처음 만난 적: {data.Encounters[report.EncounterId].Name} (탐사 기록에 남김)", Theme.Cover));
        }

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

    /// <summary>끝난 원정 한 줄 요약 (마을 안내).</summary>
    public static (string Text, Microsoft.Xna.Framework.Color Color) Summary(GameData data, ExpeditionSummary summary)
    {
        var zone = data.Zones[summary.ZoneId].Name;
        var end = summary.End switch
        {
            ExpeditionEnd.Cleared => summary.Clear is { FirstClear: true } ? "첫 클리어" : "클리어",
            ExpeditionEnd.ForcedReturn => "무승부로 귀환",
            _ => "전멸",
        };
        var parts = new List<string> { $"{zone} 원정 {end} ({summary.Battles}전)", $"골드 +{summary.Gold}" };
        if (summary.Items.Count > 0)
        {
            parts.Add($"아이템: {Items(data, summary.Items)}");
        }

        var color = summary.End == ExpeditionEnd.Wiped ? Theme.Enemy : Theme.Heal;
        return (string.Join(" · ", parts), color);
    }

    /// <summary>클리어한 원정의 결과 줄: 이번 원정의 기록, 처음이면 다음 깊이와 단서, 아니면 새 기록.</summary>
    public static IReadOnlyList<LogLine> Clear(GameData data, ExpeditionSummary summary)
    {
        if (summary.Clear is not { } clear)
        {
            return [];
        }

        var zone = data.Zones[summary.ZoneId];
        var r = clear.Result;
        var lines = new List<LogLine>
        {
            new($"이번 원정: {r.Members}명 · 장비 {Tier(r.GearTier)} · 쓰러짐 {r.Downed}명", Theme.TextDim),
        };
        if (clear.FirstClear)
        {
            if (Depths.Ordered(data).SkipWhile(z => z.Id != zone.Id).Skip(1).FirstOrDefault() is { } next)
            {
                lines.Add(new LogLine($"{Korean.IGa(next.Name)} 열렸습니다.", Theme.Heal));
            }

            if (zone.Boss?.Clue is { } clue)
            {
                lines.Add(new LogLine($"단서: {clue}", Theme.Cover));
            }
        }
        else if (clear.Improved.Count > 0)
        {
            lines.Add(new LogLine($"새 기록: {Improved(clear)}", Theme.Heal));
        }

        return lines;
    }

    /// <summary>"최소 인원 3명 · 최저 장비 T1 · 최소 쓰러짐 0명".</summary>
    public static string ClearRecord(ClearRecord record) =>
        $"최소 인원 {record.FewestMembers}명 · 최저 장비 {Tier(record.LowestGearTier)} · 최소 쓰러짐 {record.FewestDowned}명";

    /// <summary>보스의 첫 클리어 보상: "골드 150, 철 조각, 질긴 가죽". 없으면 null.</summary>
    public static string? FirstClearReward(GameData data, BossDefinition boss)
    {
        var parts = new List<string>();
        if (boss.FirstClearGold > 0)
        {
            parts.Add($"골드 {boss.FirstClearGold}");
        }

        if (boss.FirstClearItems.Count > 0)
        {
            parts.Add(Items(data, boss.FirstClearItems));
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string Tier(int tier) => tier == 0 ? "없음" : $"T{tier}";

    private static string Improved(ZoneClear clear) => string.Join(", ", clear.Improved.Select(k => k switch
    {
        ClearRecordKind.FewestMembers => $"{clear.Result.Members}명",
        ClearRecordKind.LowestGearTier => $"장비 {Tier(clear.Result.GearTier)}",
        _ => $"쓰러짐 {clear.Result.Downed}명",
    }));
}
