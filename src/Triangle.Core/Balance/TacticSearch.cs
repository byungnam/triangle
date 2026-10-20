using Triangle.Core.Data;
using Triangle.Core.Tactics;

namespace Triangle.Core.Balance;

/// <summary>
/// 전술 자동 탐색: 출발 세트에서 시작해 전술 줄을 조금씩 바꿔 보며(행동·조건·값 바꾸기, 조건·줄 넣고 빼기, 줄 순서 바꾸기)
/// 여러 맞대결의 평균 점수(<see cref="BenchScore.Fitness"/>)가 오르는 쪽으로 간다. (1+λ) 진화 전략이다.
/// </summary>
/// <remarks>
/// 사람이 짠 세트보다 나은 세트가 있는지 보려는 도구다. 맞대결 하나로 돌리면 그 적에 맞춘 세트를,
/// 여러 개로 돌리면 그 적들을 함께 상대하는 공통 세트를 찾는다. 둘의 차이가 그 적의 특색이다.
/// 탐색은 훈련 시드(<see cref="Options.FirstSeed"/>부터)로 하고, 결과는 다른 시드로 다시 재야 한다(훈련 시드에 맞춘 운을 빼려고).
/// 같은 입력과 <see cref="Options.RandomSeed"/>면 같은 결과가 나온다.
/// </remarks>
public static class TacticSearch
{
    public sealed record Options
    {
        public int Generations { get; init; } = 120;

        /// <summary>세대마다 만들어 보는 변형 수.</summary>
        public int Offspring { get; init; } = 8;

        /// <summary>후보 하나를 잴 때 맞대결마다 쓰는 시드 수.</summary>
        public int Seeds { get; init; } = 60;

        /// <summary>훈련 시드의 시작. 측정(0부터)과 겹치지 않게 떨어뜨린다.</summary>
        public int FirstSeed { get; init; } = 100_000;

        public int RandomSeed { get; init; } = 1;

        /// <summary>멤버 한 명의 전술 줄 수 상한.</summary>
        public int MaxLines { get; init; } = 8;
    }

    /// <param name="Fitness">훈련 시드로 잰 평균 점수.</param>
    public sealed record Result(IReadOnlyDictionary<string, IReadOnlyList<Tactic>> Tactics, double Fitness, int Evaluations);

    /// <param name="progress">세대마다 (세대 번호, 지금 점수).</param>
    public static Result Run(
        GameData data,
        IReadOnlyList<PreparedMatchup> matchups,
        IReadOnlyDictionary<string, IReadOnlyList<Tactic>> start,
        Options? options = null,
        Action<int, double>? progress = null)
    {
        options ??= new Options();
        var search = new Search(data, matchups, options);
        var current = Candidate.From(start, matchups[0].Party.Select(p => p.Id));
        var fitness = search.Evaluate(current);
        var random = new Random(options.RandomSeed);
        for (var generation = 1; generation <= options.Generations; generation++)
        {
            var children = Enumerable.Range(0, options.Offspring).Select(_ => search.Mutate(current, random)).ToList();
            foreach (var child in children)
            {
                var score = search.Evaluate(child);
                // 같은 점수도 받아들여 평평한 곳을 건너간다.
                if (score >= fitness)
                {
                    (current, fitness) = (child, score);
                }
            }

            progress?.Invoke(generation, fitness);
        }

        (current, fitness) = search.Prune(current, fitness);
        return new Result(current.ToTactics(), fitness, search.Evaluations);
    }

    /// <summary>줄 하나: 조건(1~3개, 모두 참)과 행동.</summary>
    private sealed record Line(IReadOnlyList<TacticCondition> Conditions, string ActionId);

    private sealed class Candidate
    {
        private Candidate(Dictionary<string, List<Line>> lines) => Lines = lines;

        public Dictionary<string, List<Line>> Lines { get; }

        public static Candidate From(IReadOnlyDictionary<string, IReadOnlyList<Tactic>> tactics, IEnumerable<string> members) =>
            new(members.ToDictionary(
                id => id,
                id => tactics.GetValueOrDefault(id, []).OrderBy(t => t.Priority).Select(t => new Line(t.Conditions, t.ActionId)).ToList()));

        public Candidate Clone() => new(Lines.ToDictionary(p => p.Key, p => p.Value.ToList()));

        public IReadOnlyDictionary<string, IReadOnlyList<Tactic>> ToTactics() =>
            Lines.ToDictionary(
                p => p.Key,
                p => (IReadOnlyList<Tactic>)p.Value.Select((l, i) => Tactic.Create(i + 1, l.Conditions, l.ActionId)).ToList());

        public string Key() =>
            string.Join("|", Lines.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
                p.Key + ":" + string.Join(";", p.Value.Select(l => string.Join("&", l.Conditions.Select(c => $"{c.Condition}={c.Value}")) + ">" + l.ActionId))));
    }

    private sealed class Search(GameData data, IReadOnlyList<PreparedMatchup> matchups, Options options)
    {
        // 수치 조건(실제 HP·MP)은 백분율 조건과 겹치므로 탐색하지 않는다 (출발 세트에 있으면 그대로 둔다).
        private static readonly Condition[] Conditions = Enum.GetValues<Condition>().Where(c => !c.IsAmount()).ToArray();

        private readonly Dictionary<string, double> _memo = [];

        /// <summary>멤버마다 어느 맞대결 상태에서든 쓸 수 있는 행동.</summary>
        private readonly Dictionary<string, string[]> _actions = matchups[0].Party.ToDictionary(
            p => p.Id,
            p => matchups.SelectMany(m => m.UsableActions[p.Id]).Distinct().Order(StringComparer.Ordinal).ToArray());

        public int Evaluations { get; private set; }

        public double Evaluate(Candidate candidate)
        {
            var key = candidate.Key();
            if (_memo.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Evaluations++;
            var tactics = candidate.ToTactics();
            var score = matchups.Average(m => TacticBench.Measure(m, tactics, data, options.Seeds, options.FirstSeed).Fitness);
            _memo[key] = score;
            return score;
        }

        /// <summary>한 멤버의 줄을 1~3번 바꾼다.</summary>
        public Candidate Mutate(Candidate parent, Random random)
        {
            var child = parent.Clone();
            var members = child.Lines.Keys.Order(StringComparer.Ordinal).ToList();
            var memberId = members[random.Next(members.Count)];
            var lines = child.Lines[memberId];
            var actions = _actions[memberId];
            for (var step = random.Next(1, 4); step > 0; step--)
            {
                if (lines.Count == 0)
                {
                    lines.Add(new Line([RandomCondition(random)], actions[random.Next(actions.Length)]));
                    continue;
                }

                var i = random.Next(lines.Count);
                var line = lines[i];
                var c = random.Next(line.Conditions.Count);
                switch (random.Next(8))
                {
                    case 0:
                        lines[i] = line with { ActionId = actions[random.Next(actions.Length)] };
                        break;
                    case 1:
                        lines[i] = line with { Conditions = Replace(line.Conditions, c, RandomCondition(random)) };
                        break;
                    case 2:
                        lines[i] = line with { Conditions = Replace(line.Conditions, c, Nudge(line.Conditions[c], random)) };
                        break;
                    case 3 when line.Conditions.Count < Tactic.MaxConditions:
                        lines[i] = line with { Conditions = [.. line.Conditions, RandomCondition(random)] };
                        break;
                    case 4 when line.Conditions.Count > 1:
                        lines[i] = line with { Conditions = line.Conditions.Where((_, k) => k != c).ToList() };
                        break;
                    case 5 when lines.Count < options.MaxLines:
                        lines.Insert(random.Next(lines.Count + 1), new Line([RandomCondition(random)], actions[random.Next(actions.Length)]));
                        break;
                    case 6 when lines.Count > 1:
                        lines.RemoveAt(i);
                        break;
                    case 7 when lines.Count > 1:
                        lines.RemoveAt(i);
                        lines.Insert(random.Next(lines.Count + 1), line);
                        break;
                }
            }

            return child;
        }

        /// <summary>점수가 떨어지지 않는 한 줄과 덧붙인 조건을 지워 결과를 읽기 쉽게 한다.</summary>
        public (Candidate, double) Prune(Candidate candidate, double fitness)
        {
            foreach (var memberId in candidate.Lines.Keys.Order(StringComparer.Ordinal).ToList())
            {
                for (var i = candidate.Lines[memberId].Count - 1; i >= 0; i--)
                {
                    var lines = candidate.Lines[memberId];
                    if (lines.Count > 1 && TryWith(candidate, fitness, c => c.Lines[memberId].RemoveAt(i)) is { } shorter)
                    {
                        (candidate, fitness) = shorter;
                        continue;
                    }

                    for (var k = lines[i].Conditions.Count - 1; k >= 1; k--)
                    {
                        var at = (i, k);
                        if (TryWith(candidate, fitness, c => c.Lines[memberId][at.i] = c.Lines[memberId][at.i] with
                            {
                                Conditions = c.Lines[memberId][at.i].Conditions.Where((_, n) => n != at.k).ToList(),
                            }) is { } simpler)
                        {
                            (candidate, fitness) = simpler;
                        }
                    }
                }
            }

            return (candidate, fitness);
        }

        private (Candidate, double)? TryWith(Candidate candidate, double fitness, Action<Candidate> change)
        {
            var next = candidate.Clone();
            change(next);
            var score = Evaluate(next);
            return score >= fitness ? (next, score) : null;
        }

        private static IReadOnlyList<TacticCondition> Replace(IReadOnlyList<TacticCondition> conditions, int index, TacticCondition with) =>
            conditions.Select((c, k) => k == index ? with : c).ToList();

        private static TacticCondition RandomCondition(Random random)
        {
            var condition = Conditions[random.Next(Conditions.Length)];
            return new TacticCondition(condition, RandomValue(condition, random));
        }

        private static int RandomValue(Condition condition, Random random) => condition switch
        {
            _ when condition.IsPercent() => 10 * random.Next(1, 10),
            _ when condition.IsEnemyCount() => random.Next(0, 6),
            _ when condition.IsAllyCount() => random.Next(0, 4),
            Condition.MaxUses => random.Next(1, 4),
            Condition.EveryNthTurn => random.Next(2, 6),
            Condition.FromTurn or Condition.UntilTurn or Condition.OnTurn => random.Next(1, 9),
            _ => 0,
        };

        /// <summary>값을 한 칸 옮긴다 (백분율은 10, 나머지는 1). 범위를 벗어나지 않는다.</summary>
        private static TacticCondition Nudge(TacticCondition c, Random random)
        {
            if (!c.Condition.HasValue())
            {
                return c;
            }

            var step = c.Condition.IsPercent() ? 10 : 1;
            var value = c.Value + (random.Next(2) == 0 ? -step : step);
            var min = c.Condition is Condition.EveryNthTurn or Condition.FromTurn or Condition.UntilTurn or Condition.OnTurn or Condition.MaxUses ? 1 : 0;
            var max = c.Condition.IsPercent() ? 100 : int.MaxValue;
            return c with { Value = Math.Clamp(value, min, max) };
        }
    }
}
