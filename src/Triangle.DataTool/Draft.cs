using System.Diagnostics;
using Triangle.Core.Balance;
using Triangle.Core.Data;

namespace Triangle.DataTool;

/// <summary>
/// draft 명령: 로그라이트 재편(설계)의 드래프트 시뮬레이션. 봇과 시작 줄 상한마다 판 여러 개를 돌려 클리어율, 막별 탈락,
/// 쓰러짐과 사망, 첫 봉인, 빌드 차이를 보여 준다. 앞을 보는 봇과 무작위 봇의 차이가 "고르기가 결과를 바꾸는가"다.
/// </summary>
internal static class Draft
{
    public static int Run(Args options, string dataDirectory, GameData data)
    {
        var path = options.Get("draft") ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dataDirectory))!, "tools", "draft", "draft.json");
        DraftDefinition draft;
        try
        {
            draft = DraftDefinition.Load(path, data);
        }
        catch (Exception e) when (e is GameDataException or IOException)
        {
            Console.WriteLine(e is GameDataException g ? $"시뮬레이션 정의 오류 {g.Errors.Count}개:\n  - " + string.Join("\n  - ", g.Errors) : e.Message);
            return 1;
        }

        if (Bench.Number(options, "runs", 200, 1) is not { } runs || Bench.Number(options, "lookahead", 4, 1) is not { } lookahead)
        {
            return 2;
        }

        DraftPicker[]? bots = options.Get("bot") switch
        {
            null or "both" => [DraftPicker.Random, DraftPicker.Greedy],
            "random" => [DraftPicker.Random],
            "greedy" => [DraftPicker.Greedy],
            _ => null,
        };
        var startLines = options.Get("start-lines")?.Split(',', StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var n) && n >= 0 && n <= draft.MaxLines ? n : -1).ToList() ?? [draft.StartLines];
        if (bots is null || startLines.Contains(-1))
        {
            Console.Error.WriteLine(bots is null ? "--bot은 random, greedy, both 중 하나다." : $"--start-lines는 0~{draft.MaxLines}의 정수 목록이다.");
            return 2;
        }

        var acts = draft.Acts.Select(a => $"{data.Zones[a.ZoneId].Name} {a.Fights}+보스(T{a.GearTier})");
        Console.WriteLine($"파티: {string.Join(", ", draft.Party.Select(id => draft.Class(id).Name))} · 막: {string.Join(" → ", acts)} · 판 {runs}개");
        Console.WriteLine($"보상: 후보 {draft.Offer.Cards}장 (액티브 {draft.Offer.Active} : 패시브 {draft.Offer.Passive} : 줄 상한 {draft.Offer.Line}) · 막이 끝나면 봉인 {draft.UnsealAfterAct}개 풀기");
        Console.WriteLine();
        Console.WriteLine(Bench.Pad("봇", 10) + Bench.Pad("시작 줄", 9) + Bench.Pad("클리어", 8, true) + Bench.Pad("평균 승리", 11, true)
            + "  " + Bench.Pad("탈락 (막별)", 24) + Bench.Pad("쓰러짐/판", 10, true) + Bench.Pad("사망/판", 9, true)
            + Bench.Pad("첫 봉인", 9, true) + Bench.Pad("봉인 없음", 11, true) + Bench.Pad("빌드 차이", 11, true));

        var watch = Stopwatch.StartNew();
        var details = new List<(DraftPicker Bot, int Lines, IReadOnlyList<DraftRunResult> Results)>();
        foreach (var lines in startLines)
        {
            foreach (var bot in bots)
            {
                var results = new DraftSimulation(draft, data, new DraftOptions { Picker = bot, LookaheadSeeds = lookahead, StartLines = lines }).RunMany(runs);
                details.Add((bot, lines, results));
                var failed = Enumerable.Range(0, draft.Acts.Count).Select(a => 100.0 * results.Count(r => !r.Cleared && r.Act == a) / runs);
                var sealedRuns = results.Where(r => r.FirstSeal is not null).ToList();
                Console.WriteLine(
                    Bench.Pad(Name(bot), 10) + Bench.Pad($"{lines}", 9)
                    + Bench.Pad($"{100.0 * results.Count(r => r.Cleared) / runs:F1}%", 8, true)
                    + Bench.Pad($"{results.Average(r => r.FightsWon):F1}", 11, true)
                    + "  " + Bench.Pad(string.Join(" · ", failed.Select((f, a) => $"{a + 1}막 {f:F0}%")), 24)
                    + Bench.Pad($"{results.Average(r => r.Downs):F2}", 10, true)
                    + Bench.Pad($"{results.Average(r => r.Deaths):F2}", 9, true)
                    + Bench.Pad(sealedRuns.Count == 0 ? "-" : $"{sealedRuns.Average(r => r.FirstSeal!.Value):F1}", 9, true)
                    + Bench.Pad($"{100.0 * (runs - sealedRuns.Count) / runs:F0}%", 11, true)
                    + Bench.Pad($"{Diversity(results):F2}", 11, true));
            }
        }

        Console.WriteLine($"({watch.Elapsed.TotalSeconds:F0}초) 첫 봉인 = 처음 봉인된 전투 번호의 평균(봉인된 판만). 빌드 차이 = 두 판이 고른 액티브 카드 집합의 자카드 거리 평균 (0 같음, 1 겹침 없음).");
        foreach (var (bot, lines, results) in details)
        {
            Console.WriteLine();
            Console.WriteLine($"{Name(bot)} · 시작 줄 {lines}: 판당 많이 고른 카드");
            foreach (var (label, count) in results.SelectMany(r => r.Picks).GroupBy(p => Label(draft, data, p.Card)).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).Take(10))
            {
                Console.WriteLine($"  {(double)count / runs,5:F2}  {label}");
            }

            var ends = results.Where(r => !r.Cleared).GroupBy(r => r.Encounter).OrderByDescending(g => g.Count()).Take(6).ToList();
            if (ends.Count > 0)
            {
                Console.WriteLine("  판을 끝낸 적 (그중 무승부): " + string.Join(", ", ends.Select(g =>
                    $"{data.Encounters[g.Key].Name} {g.Count()}" + (g.Any(r => r.Outcome == Triangle.Core.Combat.CombatOutcome.Draw) ? $" ({g.Count(r => r.Outcome == Triangle.Core.Combat.CombatOutcome.Draw)})" : ""))));
            }
        }

        return 0;
    }

    private static string Name(DraftPicker bot) => bot == DraftPicker.Random ? "무작위" : "앞을 봄";

    private static string Label(DraftDefinition draft, GameData data, DraftCard card) => card.Kind switch
    {
        DraftCardKind.Active => $"{draft.Class(card.ClassId!).Name} · {data.Actions[card.Id!].Name}",
        DraftCardKind.Passive => $"{draft.Class(card.ClassId!).Name} · 패시브 {data.Skills[card.Id!].Name}",
        _ => "줄 상한 +1",
    };

    /// <summary>두 판이 고른 액티브 카드 집합(클래스:행동)의 자카드 거리 평균.</summary>
    private static double Diversity(IReadOnlyList<DraftRunResult> results)
    {
        var sets = results.Select(r => r.Picks.Where(p => p.Card.Kind == DraftCardKind.Active).Select(p => $"{p.Card.ClassId}:{p.Card.Id}").ToHashSet()).ToList();
        var (sum, pairs) = (0.0, 0);
        for (var i = 0; i < sets.Count; i++)
        {
            for (var j = i + 1; j < sets.Count; j++)
            {
                var union = sets[i].Union(sets[j]).Count();
                sum += union == 0 ? 0 : 1 - (double)sets[i].Intersect(sets[j]).Count() / union;
                pairs++;
            }
        }

        return pairs == 0 ? 0 : sum / pairs;
    }
}
