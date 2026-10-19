using System.Text.Json.Serialization;

namespace Triangle.Core.Runs;

public enum RunNodeKind
{
    Fight,
    Elite,
    Shop,
    Rest,
    Event,
    Boss,
}

/// <summary>지도의 지점. <paramref name="Next"/>는 다음 층에서 갈 수 있는 지점 ID다. 보스는 마지막 층 다음에 하나 있다.</summary>
public sealed record RunNode(int Id, int Floor, int Lane, RunNodeKind Kind, IReadOnlyList<int> Next);

/// <summary>
/// 막 하나의 갈림길 지도 (Slay the Spire식). 층(<see cref="RunNode.Floor"/>) × 칸(<see cref="RunNode.Lane"/>) 격자에서 경로 몇 개를
/// 첫 층부터 아래로 긋고(한 층 내려갈 때 칸을 -1, 0, +1 중 하나만큼 옮긴다), 그 경로들이 지난 자리를 지점으로, 이은 선을 길로 삼는다.
/// 길은 서로 엇갈리지 않는다. 마지막 층의 지점은 모두 보스로 이어진다. 첫 층은 늘 전투, 보스 바로 앞 층은 늘 휴식이다.
/// </summary>
public sealed record RunMap(int Floors, IReadOnlyList<RunNode> Nodes)
{
    [JsonIgnore]
    public RunNode Boss => Nodes[^1];

    [JsonIgnore]
    public IEnumerable<RunNode> Starts => Nodes.Where(n => n.Floor == 0);

    public RunNode Node(int id) => Nodes[id];

    /// <param name="hasElites">정예 후보가 없는 막이면 정예 지점을 두지 않는다.</param>
    public static RunMap Generate(RunMapRules rules, int floors, bool hasElites, int seed)
    {
        var random = new Random(seed);
        var width = Math.Max(1, rules.Width);
        var edges = new HashSet<(int Floor, int From, int To)>();
        var spots = new HashSet<(int Floor, int Lane)>();
        var firstLane = 0;
        for (var path = 0; path < Math.Max(1, rules.Paths); path++)
        {
            // 처음 두 경로는 서로 다른 칸에서 시작해 갈림길이 생기게 한다.
            var lane = path == 1 && width > 1 ? (firstLane + 1 + random.Next(width - 1)) % width : random.Next(width);
            firstLane = path == 0 ? lane : firstLane;
            spots.Add((0, lane));
            for (var floor = 1; floor < floors; floor++)
            {
                // 다른 경로의 길과 엇갈리면 곧게 내려간다 (곧은 길은 엇갈리지 않는다).
                var next = Math.Clamp(lane + random.Next(-1, 2), 0, width - 1);
                var from = lane;
                var to = next;
                if (edges.Any(e => e.Floor == floor - 1 && Math.Sign(e.From - from) * Math.Sign(e.To - to) < 0))
                {
                    next = lane;
                }

                edges.Add((floor - 1, lane, next));
                spots.Add((floor, next));
                lane = next;
            }
        }

        var ordered = spots.OrderBy(s => s.Floor).ThenBy(s => s.Lane).ToList();
        var ids = ordered.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
        var bossId = ordered.Count;
        var nodes = new List<RunNode>();
        foreach (var (floor, lane) in ordered)
        {
            var next = floor == floors - 1
                ? [bossId]
                : edges.Where(e => e.Floor == floor && e.From == lane).Select(e => ids[(floor + 1, e.To)]).Distinct().Order().ToList();
            nodes.Add(new RunNode(ids[(floor, lane)], floor, lane, Kind(rules, floor, floors, hasElites, random), next));
        }

        nodes.Add(new RunNode(bossId, floors, width / 2, RunNodeKind.Boss, []));
        return new RunMap(floors, nodes);
    }

    private static RunNodeKind Kind(RunMapRules rules, int floor, int floors, bool hasElites, Random random)
    {
        if (floor == 0)
        {
            return RunNodeKind.Fight;
        }

        if (floor == floors - 1)
        {
            return RunNodeKind.Rest;
        }

        // 정예와 휴식은 너무 이르지 않게, 휴식은 보스 앞 휴식 바로 전 층에도 두지 않는다.
        var special = floor >= rules.SpecialFrom;
        var weights = new (RunNodeKind Kind, int Weight)[]
        {
            (RunNodeKind.Fight, rules.Fight),
            (RunNodeKind.Elite, special && hasElites ? rules.Elite : 0),
            (RunNodeKind.Shop, rules.Shop),
            (RunNodeKind.Rest, special && floor < floors - 2 ? rules.Rest : 0),
            (RunNodeKind.Event, rules.Event),
        };
        var roll = random.Next(Math.Max(1, weights.Sum(w => w.Weight)));
        foreach (var (kind, weight) in weights)
        {
            if ((roll -= weight) < 0)
            {
                return kind;
            }
        }

        return RunNodeKind.Fight;
    }
}
