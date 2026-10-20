using System.Diagnostics;
using Triangle.Core.Balance;
using Triangle.Core.Data;
using Triangle.Core.Tactics;

namespace Triangle.DataTool;

/// <summary>
/// bench 명령: 기준 파티가 전술 세트마다 적 조합과 싸운 승률 표. --search면 전술 자동 탐색으로
/// 공통 최선 세트와 적마다 맞춘 세트를 찾아 그 차이(특색)를 보여 준다.
/// </summary>
internal static class Bench
{
    public static int Run(Args options, string dataDirectory)
    {
        var path = options.Get("bench") ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dataDirectory))!, "tools", "bench", "bench.json");
        BenchDefinition bench;
        GameData data;
        try
        {
            (bench, data) = BenchDefinition.Load(path, dataDirectory);
        }
        catch (Exception e) when (e is GameDataException or IOException)
        {
            Console.WriteLine(e is GameDataException g ? $"측정 정의 오류 {g.Errors.Count}개:\n  - " + string.Join("\n  - ", g.Errors) : e.Message);
            return 1;
        }

        var encounters = Ids(options.Get("encounter"));
        var matchups = bench.Matchups.Where(m => encounters is null || encounters.Any(id => Matches(m.EncounterId, id))).ToList();
        var setIds = Ids(options.Get("set")) ?? bench.Sets.Select(s => s.Id).ToList();
        if (matchups.Count == 0 || setIds.Any(id => bench.Sets.All(s => s.Id != id)))
        {
            Console.Error.WriteLine(matchups.Count == 0 ? "고른 맞대결이 없다." : $"모르는 세트: {string.Join(", ", setIds.Where(id => bench.Sets.All(s => s.Id != id)))}");
            return 2;
        }

        if (Number(options, "seeds", 200, 1) is not { } seeds
            || Number(options, "generations", 120, 0) is not { } generations
            || Number(options, "train-seeds", 60, 1) is not { } trainSeeds)
        {
            return 2;
        }

        // 세트가 장비를 바꾸면 파티도 다르므로 세트마다 준비한다.
        var preparedBy = new Dictionary<string, List<PreparedMatchup>>();
        List<PreparedMatchup> Prepared(string setId) =>
            preparedBy.TryGetValue(setId, out var list)
                ? list
                : preparedBy[setId] = matchups.Select(m => TacticBench.Prepare(bench, data, m, setId)).ToList();

        Console.WriteLine($"기준 파티: {string.Join(", ", bench.Party.Select(m => m.Name))} · 시드 {seeds}개 · 승률 % (무승부는 진 것으로 센다)");
        Console.WriteLine();
        PrintTable(bench, data, matchups, setIds, Prepared, seeds);
        PrintDropped(bench, data, setIds, Prepared);

        if (!options.Has("search"))
        {
            return 0;
        }

        var search = new TacticSearch.Options { Generations = generations, Seeds = trainSeeds };
        var starts = options.Get("from") is "all" ? bench.Sets.Select(s => s.Id).ToList() : Ids(options.Get("from")) ?? [bench.Sets[0].Id];
        if (starts.FirstOrDefault(id => bench.Sets.All(s => s.Id != id)) is { } unknown)
        {
            Console.Error.WriteLine($"모르는 출발 세트: {unknown}");
            return 2;
        }

        Console.WriteLine();
        Console.WriteLine($"자동 탐색: 출발 {string.Join(", ", starts)}, 세대 {search.Generations} × 변형 {search.Offspring}, 훈련 시드 {search.Seeds}개 (표와 다른 시드)");
        var watch = Stopwatch.StartNew();

        // 공통 세트는 출발 세트마다 찾아 훈련 점수가 가장 높은 것을 쓴다 (한 출발점의 지역 최적에 갇히지 않게).
        // 탐색은 전술만 바꾸고, 장비는 출발 세트의 것을 그대로 쓴다.
        var (joint, jointStart) = starts
            .Select(start =>
            {
                var found = TacticSearch.Run(data, Prepared(start), bench.TacticsOf(start), search, Progress($"공통 세트 ({start}에서)"));
                Console.WriteLine();
                return (Result: found, Start: start);
            })
            .ToList()
            .MaxBy(r => r.Result.Fitness);
        var prepared = Prepared(jointStart);

        var rows = new List<(PreparedMatchup Matchup, TacticSearch.Result Own)>();
        foreach (var matchup in prepared)
        {
            rows.Add((matchup, TacticSearch.Run(data, [matchup], joint.Tactics, search, Progress(Label(data, matchup)))));
            Console.WriteLine();
        }

        Console.WriteLine($"({watch.Elapsed.TotalSeconds:F0}초)");
        Console.WriteLine();
        Console.WriteLine("특색 = 그 적에 맞춘 세트 승률 − 모든 적을 함께 상대하는 공통 세트 승률 (표와 같은 시드로 다시 잼)");
        foreach (var (matchup, own) in rows)
        {
            var common = TacticBench.Measure(matchup, joint.Tactics, data, seeds).WinRate;
            var fitted = TacticBench.Measure(matchup, own.Tactics, data, seeds).WinRate;
            Console.WriteLine($"  {Pad(Label(data, matchup), 28)} 공통 {common,5:F1}  맞춤 {fitted,5:F1}  특색 {fitted - common,6:+0.0;-0.0;0.0}");
        }

        Console.WriteLine();
        Console.WriteLine($"공통 세트 ('{jointStart}'의 장비):");
        PrintTactics(bench, data, joint.Tactics, null);
        foreach (var (matchup, own) in rows)
        {
            Console.WriteLine($"{Label(data, matchup)}에 맞춘 세트 (공통 세트와 다른 멤버만):");
            PrintTactics(bench, data, own.Tactics, joint.Tactics);
        }

        return 0;
    }

    /// <summary>정수 옵션. 없으면 기본값, 잘못됐으면 알리고 null.</summary>
    internal static int? Number(Args options, string name, int fallback, int min)
    {
        var text = options.Get(name);
        if (text is null)
        {
            return fallback;
        }

        if (int.TryParse(text, out var value) && value >= min)
        {
            return value;
        }

        Console.Error.WriteLine($"--{name}은(는) {min} 이상의 정수여야 한다: '{text}'");
        return null;
    }

    /// <summary>터미널 폭으로 맞춘다. 한글 등 전각 글자는 두 칸이다.</summary>
    internal static string Pad(string text, int width, bool right = false)
    {
        var gap = new string(' ', Math.Max(0, width - Width(text)));
        return right ? gap + text : text + gap;
    }

    private static int Width(string text) => text.Sum(c => IsWide(c) ? 2 : 1);

    private static bool IsWide(char c) =>
        c is >= '\u1100' and <= '\u115F' or >= '\u2E80' and <= '\uA4CF' or >= '\uAC00' and <= '\uD7A3' or >= '\uF900' and <= '\uFAFF' or >= '\uFF00' and <= '\uFF60';

    private static List<string>? Ids(string? value) => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static bool Matches(string id, string pattern) =>
        pattern.EndsWith('*') ? id.StartsWith(pattern[..^1], StringComparison.Ordinal) : id == pattern;

    private static string Label(GameData data, PreparedMatchup m) => $"{data.Encounters[m.Matchup.EncounterId].Name} ({m.State.Name})";

    private static void PrintTable(
        BenchDefinition bench,
        GameData data,
        List<BenchMatchup> matchups,
        List<string> setIds,
        Func<string, List<PreparedMatchup>> prepared,
        int seeds)
    {
        // 열 너비는 세트 이름에 맞춘다 (최소 8칸).
        var widths = setIds.ToDictionary(id => id, id => Math.Max(8, Width(bench.Sets.First(s => s.Id == id).Name)) + 2);
        Console.WriteLine(Pad("적 조합 (상태)", 28) + string.Concat(setIds.Select(id => Pad(bench.Sets.First(s => s.Id == id).Name, widths[id], right: true))));
        for (var i = 0; i < matchups.Count; i++)
        {
            var cells = setIds.Select(id =>
            {
                var score = TacticBench.Measure(prepared(id)[i], bench.TacticsOf(id), data, seeds);
                return Pad($"{score.WinRate:F1}", widths[id], right: true);
            });
            Console.WriteLine(Pad(Label(data, prepared(setIds[0])[i]), 28) + string.Concat(cells));
        }
    }

    private static void PrintDropped(BenchDefinition bench, GameData data, List<string> setIds, Func<string, List<PreparedMatchup>> prepared)
    {
        var dropped = setIds
            .SelectMany(id => prepared(id).SelectMany(m => TacticBench.Dropped(m, bench.TacticsOf(id)).Select(d => (Set: id, m.State.Name, d.MemberId, d.Tactic.ActionId))))
            .Distinct()
            .ToList();
        if (dropped.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("그 상태에서 쓸 수 없어 빼고 싸운 줄 (장비나 패시브가 아직 없음):");
        foreach (var group in dropped.GroupBy(d => (d.Set, d.Name)))
        {
            Console.WriteLine($"  {group.Key.Set} · {group.Key.Name}: " + string.Join(", ", group.Select(d => $"{d.MemberId} {data.Actions[d.ActionId].Name}")));
        }
    }

    private static void PrintTactics(
        BenchDefinition bench,
        GameData data,
        IReadOnlyDictionary<string, IReadOnlyList<Tactic>> tactics,
        IReadOnlyDictionary<string, IReadOnlyList<Tactic>>? onlyDifferentFrom)
    {
        foreach (var member in bench.Party)
        {
            var lines = tactics.GetValueOrDefault(member.Id, []);
            if (onlyDifferentFrom is not null && Same(lines, onlyDifferentFrom.GetValueOrDefault(member.Id, [])))
            {
                continue;
            }

            Console.WriteLine($"  {member.Name}");
            foreach (var t in lines)
            {
                var conditions = string.Join(" 그리고 ", t.Conditions.Select(c => c.Condition.HasValue() ? $"{c.Condition} {c.Value}" : $"{c.Condition}"));
                Console.WriteLine($"    {t.Priority}. {conditions} → {data.Actions[t.ActionId].Name} ({t.ActionId})");
            }
        }
    }

    private static bool Same(IReadOnlyList<Tactic> a, IReadOnlyList<Tactic> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.ActionId == p.Second.ActionId && p.First.Conditions.SequenceEqual(p.Second.Conditions));

    private static Action<int, double> Progress(string label) => (generation, fitness) =>
    {
        if (generation % 10 == 0)
        {
            Console.Write($"\r  {label}: 세대 {generation}, 점수 {fitness:F3}   ");
        }
    };
}
