using System.Diagnostics;
using Triangle.Core.Balance;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Runs;

namespace Triangle.DataTool;

/// <summary>
/// runs 명령: 판 시뮬레이션. 게임의 판 규칙 그대로 봇이 판 여러 개를 끝까지 두고, 클리어율, 어디서 지는지, 전투가 얼마나 빠듯한지,
/// 카드와 골드가 얼마나 들어오고 어디에 쓰이는지, 판이 끝날 때 노는 사본이 얼마인지를 보여 준다. 숫자 맞추기의 측정용이다.
/// </summary>
internal static class RunReport
{
    public static int Run(Args options, GameData data)
    {
        if (data.Run is not { } rules)
        {
            Console.WriteLine("run.json이 없다.");
            return 1;
        }

        if (Bench.Number(options, "runs", 200, 1) is not { } runs || Bench.Number(options, "lookahead", 3, 1) is not { } lookahead
            || Bench.Number(options, "seed", 0, 0) is not { } seed)
        {
            return 2;
        }

        RunBot[]? bots = options.Get("bot") switch
        {
            null or "both" => [RunBot.Random, RunBot.Smart],
            "random" => [RunBot.Random],
            "smart" => [RunBot.Smart],
            _ => null,
        };
        RunUnlocks? unlocks = options.Get("unlocks") switch
        {
            null or "none" => RunUnlocks.For(data, new HashSet<string>()),
            "all" => RunUnlocks.All,
            _ => null,
        };
        var party = options.Get("party")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            ?? ["knight", "archer", "elementalist", "priest"];
        if (bots is null || unlocks is null)
        {
            Console.Error.WriteLine(bots is null ? "--bot은 random, smart, both 중 하나다." : "--unlocks는 none, all 중 하나다.");
            return 2;
        }

        if (Triangle.Core.Runs.Run.WhyCannotStart(data, party, unlocks) is { } reason)
        {
            Console.Error.WriteLine($"--party: {reason}");
            return 2;
        }

        var acts = rules.Acts.Select(a => $"{data.Zones[a.ZoneId].Name} {a.Floors}층");
        Console.WriteLine($"파티: {string.Join(", ", party.Select(id => data.Classes[id].Name))} · 해금: {(unlocks == RunUnlocks.All ? "모두 열림" : "없음 (처음 판)")} · 판 {runs}개");
        Console.WriteLine($"막: {string.Join(" → ", acts)} · 줄 상한 시작 {rules.StartLines}, 최대 {rules.MaxLines} · 시작 골드 {rules.StartGold + unlocks.StartGold}");
        Console.WriteLine();
        Console.WriteLine(Bench.Pad("봇", 8) + Bench.Pad("클리어", 8, true) + "  " + Bench.Pad("탈락 (막별)", 26) + Bench.Pad("전투/판", 9, true)
            + Bench.Pad("쓰러짐/판", 11, true) + Bench.Pad("사망/판", 9, true) + Bench.Pad("카드/판", 9, true) + Bench.Pad("번 골드", 9, true)
            + Bench.Pad("쓴 골드", 9, true) + Bench.Pad("합류/판", 9, true));

        var watch = Stopwatch.StartNew();
        var details = new List<(RunBot Bot, IReadOnlyList<RunSimResult> Results)>();
        foreach (var bot in bots)
        {
            var results = new RunSimulation(data, new RunSimOptions { Bot = bot, Party = party, Unlocks = unlocks, LookaheadSeeds = lookahead }).RunMany(runs, seed);
            details.Add((bot, results));
            var failed = Enumerable.Range(0, rules.Acts.Count).Select(a => 100.0 * results.Count(r => !r.Cleared && r.Act == a) / runs);
            Console.WriteLine(
                Bench.Pad(Name(bot), 8)
                + Bench.Pad($"{100.0 * results.Count(r => r.Cleared) / runs:F1}%", 8, true)
                + "  " + Bench.Pad(string.Join(" · ", failed.Select((f, a) => $"{a + 1}막 {f:F0}%")), 26)
                + Bench.Pad($"{results.Average(r => r.Fights.Count):F1}", 9, true)
                + Bench.Pad($"{results.Average(r => r.Fights.Sum(f => f.Downs)):F2}", 11, true)
                + Bench.Pad($"{results.Average(r => r.Deaths):F2}", 9, true)
                + Bench.Pad($"{results.Average(r => r.Picks.Count):F1}", 9, true)
                + Bench.Pad($"{results.Average(r => r.GoldEarned):F0}", 9, true)
                + Bench.Pad($"{results.Average(r => r.Spent.Values.Sum()):F0}", 9, true)
                + Bench.Pad($"{results.Average(r => r.Joined):F2}", 9, true));
        }

        Console.WriteLine($"({watch.Elapsed.TotalSeconds:F0}초) 쓰러짐 = 이긴 전투에서 쓰러진 횟수(봉인이 된다). 카드 = 고른 보상 카드(전투, 단련, 이벤트).");
        Console.WriteLine("전투 시간 = 전투 시계(속도 10인 유닛이 보통 행동 한 번에 100씩 간다).");
        foreach (var (bot, results) in details)
        {
            Details(data, rules, bot, results);
        }

        return 0;
    }

    private static void Details(GameData data, RunDefinition rules, RunBot bot, IReadOnlyList<RunSimResult> results)
    {
        var runs = results.Count;
        var fights = results.SelectMany(r => r.Fights).ToList();
        Console.WriteLine();
        Console.WriteLine($"[{Name(bot)}]");
        Console.WriteLine("  " + Bench.Pad("막", 6) + Bench.Pad("전투/판 (보통·정예·보스)", 26) + Bench.Pad("이긴 뒤 남은 HP", 16, true)
            + Bench.Pad("쓰러짐/전투", 13, true) + Bench.Pad("진 전투", 9, true) + Bench.Pad("전투 시간", 11, true));
        for (var act = 0; act < rules.Acts.Count; act++)
        {
            var inAct = fights.Where(f => f.Act == act).ToList();
            if (inAct.Count == 0)
            {
                continue;
            }

            var won = inAct.Where(f => f.Outcome == CombatOutcome.Victory).ToList();
            double PerRun(RunNodeKind kind) => (double)inAct.Count(f => f.Kind == kind) / runs;
            Console.WriteLine("  " + Bench.Pad($"{act + 1}막", 6)
                + Bench.Pad($"{(double)inAct.Count / runs:F1} ({PerRun(RunNodeKind.Fight):F1}·{PerRun(RunNodeKind.Elite):F1}·{PerRun(RunNodeKind.Boss):F2})", 26)
                + Bench.Pad(won.Count == 0 ? "-" : $"{100 * won.Average(f => f.AllyHp):F0}%", 16, true)
                + Bench.Pad($"{won.Sum(f => f.Downs) / (double)Math.Max(1, won.Count):F2}", 13, true)
                + Bench.Pad($"{100.0 * (inAct.Count - won.Count) / inAct.Count:F1}%", 9, true)
                + Bench.Pad(won.Count == 0 ? "-" : $"{won.Average(f => f.Time):F0}", 11, true));
        }

        Console.WriteLine();
        Console.WriteLine("  적 조합별 (싸운 수 · 진 비율 · 이긴 뒤 남은 HP · 쓰러짐/전투 · 이긴 전투의 시간 평균과 가장 긴 것):");
        foreach (var group in fights.GroupBy(f => (f.Act, f.Kind, f.EncounterId)).OrderBy(g => g.Key.Act).ThenBy(g => g.Key.Kind).ThenByDescending(g => g.Count()))
        {
            var won = group.Where(f => f.Outcome == CombatOutcome.Victory).ToList();
            var kind = group.Key.Kind switch { RunNodeKind.Elite => "정예", RunNodeKind.Boss => "보스", _ => "보통" };
            Console.WriteLine("    " + Bench.Pad($"{group.Key.Act + 1}막 {kind} {data.Encounters[group.Key.EncounterId].Name}", 30)
                + Bench.Pad($"{group.Count()}", 7, true)
                + Bench.Pad($"{100.0 * (group.Count() - won.Count) / group.Count():F1}%", 9, true)
                + Bench.Pad(won.Count == 0 ? "-" : $"{100 * won.Average(f => f.AllyHp):F0}%", 8, true)
                + Bench.Pad($"{won.Sum(f => f.Downs) / (double)Math.Max(1, won.Count):F2}", 8, true)
                + Bench.Pad(won.Count == 0 ? "-" : $"{won.Average(f => f.Time):F0} / {won.Max(f => f.Time)}", 14, true));
        }

        // 보스 대비: 똑똑한 봇이 보스마다 고른 전술 방식과 줄 배치 (많이 고른 셋).
        var plans = fights.Where(f => f.Plan is not null).GroupBy(f => (f.Act, f.EncounterId)).OrderBy(g => g.Key.Act).ToList();
        if (plans.Count > 0)
        {
            Console.WriteLine("  보스 대비 (보스마다 고른 전술 방식 · 줄 배치, 많이 고른 셋):");
            foreach (var g in plans)
            {
                var top = g.GroupBy(f => f.Plan!).OrderByDescending(p => p.Count()).Take(3).Select(p => $"{p.Key} {100.0 * p.Count() / g.Count():F0}%");
                Console.WriteLine($"    {g.Key.Act + 1}막 {data.Encounters[g.Key.EncounterId].Name}: {string.Join(", ", top)}");
            }
        }

        var ends = results.Where(r => !r.Cleared && r.Last is not null).GroupBy(r => r.Last!.EncounterId).OrderByDescending(g => g.Count()).Take(6).ToList();
        if (ends.Count > 0)
        {
            Console.WriteLine("  판을 끝낸 적 (그중 무승부): " + string.Join(", ", ends.Select(g =>
                $"{data.Encounters[g.Key].Name} {g.Count()}" + (g.Any(r => r.Last!.Outcome == CombatOutcome.Draw) ? $" ({g.Count(r => r.Last!.Outcome == CombatOutcome.Draw)})" : ""))));
        }

        // 시간 초과는 적이 세서가 아니라 싸움이 길어서 진 것이다 (소환·회복으로 버티는 적). 위의 여섯에 들지 않아도 모두 적는다.
        var draws = results.Where(r => !r.Cleared && r.Last?.Outcome == CombatOutcome.Draw).GroupBy(r => r.Last!.EncounterId).OrderByDescending(g => g.Count()).ToList();
        if (draws.Count > 0)
        {
            Console.WriteLine("  시간 초과로 판을 끝낸 적: " + string.Join(", ", draws.Select(g => $"{data.Encounters[g.Key].Name} {g.Count()}")));
        }

        Console.WriteLine();
        double Avg(Func<RunSimResult, double> f) => results.Average(f);
        Console.WriteLine($"  카드/판: 액티브 {Avg(r => r.Picks.Count(p => p.Kind == RunCardKind.Active)):F1} · 패시브 {Avg(r => r.Picks.Count(p => p.Kind == RunCardKind.Passive)):F1}"
            + $" · 줄 상한 {Avg(r => r.Picks.Count(p => p.Kind == RunCardKind.Line)):F1} · 고를 것이 없어 건너뜀 {Avg(r => r.Skipped):F1}");
        var spends = Enum.GetValues<RunSpend>().Select(s => $"{Spend(s)} {Avg(r => r.Spent.GetValueOrDefault(s)):F0}");
        Console.WriteLine($"  골드/판: 번 것 {Avg(r => r.GoldEarned):F0} · 쓴 곳 {string.Join(" · ", spends)} · 남긴 것 {Avg(r => r.GoldLeft):F0}");
        Shops(data, rules, results);
        foreach (var (label, group) in new[] { ("모든 판", results), ("클리어한 판", results.Where(r => r.Cleared).ToList()) })
        {
            if (group.Count == 0)
            {
                continue;
            }

            double G(Func<RunSimResult, double> f) => group.Average(f);
            Console.WriteLine($"  끝날 때 ({label} {group.Count}): 멤버 {G(r => r.Members):F1}명 · 사본 {G(r => r.Copies):F1}장 중 켜진 줄에 {G(r => r.Placed):F1}장"
                + $" (놀고 있음 {G(r => r.Copies - r.Placed):F1}) · 줄 상한 합 {G(r => r.Lines):F1} · 줄 상한 최대({rules.MaxLines}) 멤버 {G(r => r.Maxed):F1}명");
        }

        Console.WriteLine("  판당 많이 고른 카드:");
        foreach (var (name, count) in results.SelectMany(r => r.Picks).GroupBy(p => Label(data, p)).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).Take(8))
        {
            Console.WriteLine($"    {(double)count / runs,5:F2}  {name}");
        }
    }

    /// <summary>상점: 막마다 들를 때 골드, 쓴 골드, 산 수, 살 수 있는 것이 없던 방문, 종류별로 나온 것 중 산 비율과 값.</summary>
    private static void Shops(GameData data, RunDefinition rules, IReadOnlyList<RunSimResult> results)
    {
        var visits = results.SelectMany(r => r.Shops).ToList();
        if (visits.Count == 0)
        {
            return;
        }

        string KindOf(RunStock s) => s.Kind switch
        {
            RunStockKind.Item => data.Items[s.Id].Slot switch
            {
                Triangle.Core.Items.EquipmentSlot.MainHand => "무기",
                Triangle.Core.Items.EquipmentSlot.Body => "방어구",
                _ => "장신구",
            },
            RunStockKind.Skill => "스킬",
            RunStockKind.Upgrade => "강화",
            RunStockKind.Line => "줄 상한",
            _ => "합류",
        };
        string[] kinds = ["무기", "방어구", "장신구", "스킬", "강화", "줄 상한", "합류"];
        Console.WriteLine();
        Console.WriteLine($"  상점 (판당 {(double)visits.Count / results.Count:F2}번). 종류 칸은 나온 것 중 산 비율과 값의 평균:");
        Console.WriteLine("    " + Bench.Pad("막", 5) + Bench.Pad("방문/판", 9, true) + Bench.Pad("들를 때", 9, true) + Bench.Pad("쓴 골드", 9, true)
            + Bench.Pad("산 수", 7, true) + Bench.Pad("빈손", 7, true) + string.Concat(kinds.Select(k => Bench.Pad(k, 13, true))) + Bench.Pad("봉인 풀기", 11, true));
        for (var act = 0; act < rules.Acts.Count; act++)
        {
            var inAct = visits.Where(v => v.Act == act).ToList();
            if (inAct.Count == 0)
            {
                continue;
            }

            var cells = kinds.Select(k =>
            {
                var offered = inAct.SelectMany(v => v.Offered).Where(o => KindOf(o) == k).ToList();
                var bought = inAct.Sum(v => v.Bought.Count(b => KindOf(b) == k));
                return offered.Count == 0 ? "-" : $"{100.0 * bought / offered.Count:F0}% {offered.Average(o => o.Price):F0}G";
            });
            var broke = inAct.Count(v => v.Offered.All(o => o.Price > v.GoldIn) && (v.Sealed == 0 || rules.Shop.UnsealPrice > v.GoldIn));
            Console.WriteLine("    " + Bench.Pad($"{act + 1}막", 5)
                + Bench.Pad($"{(double)inAct.Count / results.Count:F2}", 9, true)
                + Bench.Pad($"{inAct.Average(v => v.GoldIn):F0}", 9, true)
                + Bench.Pad($"{inAct.Average(v => v.GoldIn - v.GoldOut):F0}", 9, true)
                + Bench.Pad($"{inAct.Average(v => v.Bought.Count + v.Unseals):F2}", 7, true)
                + Bench.Pad($"{100.0 * broke / inAct.Count:F0}%", 7, true)
                + string.Concat(cells.Select(c => Bench.Pad(c, 13, true)))
                + Bench.Pad($"{inAct.Average(v => v.Unseals):F2}", 11, true));
        }

        Console.WriteLine("    빈손 = 들를 때 골드로 아무것도(봉인이 있으면 봉인 풀기도) 살 수 없던 방문.");

        // 똑똑한 봇만: 들어가자마자 살 수 있던 것마다 사 본 값어치 (미리 싸운 점수가 나아진 정도, 이기면 1점대).
        var values = visits.SelectMany(v => v.Values.Select(x => (v.Act, Value: x))).ToList();
        if (values.Count > 0)
        {
            Console.WriteLine("  값어치 (막별, 살 수 있던 것마다 사 봤을 때 점수가 나아진 평균 · 100골드당):");
            foreach (var act in values.Select(x => x.Act).Distinct().Order())
            {
                var cells = kinds.Append("봉인 풀기").Select(k =>
                {
                    var xs = values.Where(x => x.Act == act && (x.Value.Stock is { } st ? KindOf(st) : "봉인 풀기") == k).Select(x => x.Value).ToList();
                    return xs.Count == 0 ? $"{k} -" : $"{k} {xs.Average(x => x.Gain):F3} · {100 * xs.Average(x => x.Gain / Math.Max(1, x.Price)):F3}";
                });
                Console.WriteLine($"    {act + 1}막  " + string.Join("   ", cells));
            }

            // 아이템마다: 나온 횟수, 값어치 평균(가장 잘 맞는 멤버에게 낀 것), 산 비율.
            var items = values.Where(x => x.Value.Stock is { Kind: RunStockKind.Item }).GroupBy(x => x.Value.Stock!.Id)
                .Select(g => (Id: g.Key, Count: g.Count(), Gain: g.Average(x => x.Value.Gain),
                    Rate: (double)visits.Sum(v => v.Bought.Count(b => b.Id == g.Key)) / Math.Max(1, visits.Sum(v => v.Offered.Count(o => o.Id == g.Key)))))
                .OrderByDescending(x => x.Gain)
                .ToList();
            Console.WriteLine("  아이템별 값어치 (나온 수 · 값어치 평균 · 산 비율):");
            foreach (var (id, count, gain, rate) in items)
            {
                Console.WriteLine("    " + Bench.Pad($"{data.Items[id].Name} ({KindOf(new RunStock(RunStockKind.Item, id, 0))} T{data.Items[id].Tier} {data.Items[id].Price}G)", 36)
                    + Bench.Pad($"{count}", 6, true) + Bench.Pad($"{gain:F3}", 9, true) + Bench.Pad($"{100 * rate:F0}%", 7, true));
            }

            // 강화 종류마다: 나온 횟수, 값어치 평균, 산 비율, 값의 평균.
            var upgrades = values.Where(x => x.Value.Stock is { Kind: RunStockKind.Upgrade }).GroupBy(x => x.Value.Stock!.Upgrade!.Value).OrderBy(g => g.Key).ToList();
            if (upgrades.Count > 0)
            {
                Console.WriteLine("  강화별 값어치 (나온 수 · 값어치 평균 · 산 비율 · 값 평균):");
                foreach (var g in upgrades)
                {
                    var offered = visits.Sum(v => v.Offered.Count(o => o.Upgrade == g.Key));
                    var bought = visits.Sum(v => v.Bought.Count(b => b.Upgrade == g.Key));
                    Console.WriteLine("    " + Bench.Pad(RunUpgrades.Name(g.Key), 10) + Bench.Pad($"{g.Count()}", 6, true) + Bench.Pad($"{g.Average(x => x.Value.Gain):F3}", 9, true)
                        + Bench.Pad($"{100.0 * bought / Math.Max(1, offered):F0}%", 7, true) + Bench.Pad($"{g.Average(x => x.Value.Price):F0}G", 7, true));
                }
            }
        }
    }

    private static string Name(RunBot bot) => bot == RunBot.Random ? "아무렇게나" : "똑똑함";

    private static string Spend(RunSpend spend) => spend switch
    {
        RunSpend.Item => "아이템",
        RunSpend.Skill => "스킬",
        RunSpend.Recruit => "합류",
        RunSpend.Unseal => "봉인 풀기",
        RunSpend.Upgrade => "강화",
        RunSpend.Line => "줄 상한",
        _ => "이벤트",
    };

    private static string Label(GameData data, RunCard card) => card.Kind switch
    {
        RunCardKind.Active => $"{data.Classes[card.ClassId!].Name} · {data.Actions[card.Id!].Name}",
        RunCardKind.Passive => $"{data.Classes[card.ClassId!].Name} · 패시브 {data.Skills[card.Id!].Name}",
        _ => "줄 상한 +1",
    };
}
