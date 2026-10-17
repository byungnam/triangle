using Triangle.Core.Balance;
using Triangle.Core.Data;
using Triangle.Core.Tactics;
using Xunit.Abstractions;

namespace Triangle.Core.Tests.Balance;

/// <summary>
/// 전술 측정(`tools/bench`)의 정의와 시제품 적 조합. 수치는 임시다. 모양만 지킨다: 시제품은 사람이 짠 만능 세트를 깨고
/// 그 적에 맞춘 대응 세트에는 지며, 겉모습이 같은 방벽과 수도회는 서로의 대응 세트에 진다.
/// </summary>
public class TacticBenchTests(ITestOutputHelper output)
{
    private const int Seeds = 200;

    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "data");

    private static readonly (BenchDefinition Bench, GameData Data) Loaded =
        BenchDefinition.Load(Path.Combine(AppContext.BaseDirectory, "bench", "bench.json"), DataDirectory);

    private double WinRate(string encounterId, string setId)
    {
        var (bench, data) = Loaded;
        var prepared = TacticBench.Prepare(bench, data, bench.Matchups.First(m => m.EncounterId == encounterId));
        var rate = TacticBench.Measure(prepared, bench.TacticsOf(setId), data, Seeds).WinRate;
        output.WriteLine($"{data.Encounters[encounterId].Name} · {setId}: {rate:F1}%");
        return rate;
    }

    [Fact]
    public void Bench_measures_every_shipped_encounter_and_keeps_prototypes_out_of_the_game_data()
    {
        var (bench, data) = Loaded;
        var shipped = GameDataLoader.LoadDirectory(DataDirectory);

        Assert.Equal(shipped.Encounters.Keys.Order(), bench.Matchups.Select(m => m.EncounterId).Where(shipped.Encounters.ContainsKey).Order());
        Assert.All(bench.Matchups.Where(m => m.Counter is not null), m => Assert.False(shipped.Encounters.ContainsKey(m.EncounterId)));
        Assert.All(bench.Matchups, m => Assert.True(data.Encounters.ContainsKey(m.EncounterId)));
    }

    [Fact]
    public void Prototypes_break_the_universal_set_and_fall_to_their_counter()
    {
        foreach (var matchup in Loaded.Bench.Matchups.Where(m => m.Counter is not null))
        {
            Assert.True(WinRate(matchup.EncounterId, "universal") < 30, $"{matchup.EncounterId} should beat the universal set");
            Assert.True(WinRate(matchup.EncounterId, matchup.Counter!) >= 70, $"{matchup.EncounterId} should fall to {matchup.Counter}");
        }
    }

    [Fact]
    public void Bulwark_and_penitents_look_alike_but_need_opposite_sets()
    {
        var data = Loaded.Data;
        Assert.Equal(Rows(data, "oath_bulwark"), Rows(data, "penitent_order"));

        Assert.True(WinRate("penitent_order", "vs_bulwark") < 30);
        Assert.True(WinRate("oath_bulwark", "vs_penitents") < 30);
    }

    [Fact]
    public void Lines_the_state_cannot_use_are_dropped_like_on_an_expedition()
    {
        var (bench, data) = Loaded;
        var start = TacticBench.Prepare(bench, data, new BenchMatchup("training", "start"));
        var grown = TacticBench.Prepare(bench, data, new BenchMatchup("training", "t2"));

        // 탱커의 강타는 검술 2가 필요하다. 시작 상태에는 없고, T2 성장(검 숙련 Lv5)에서 배운다.
        Assert.Contains(TacticBench.Dropped(start, bench.TacticsOf("universal")), d => d is ("tank", { ActionId: "heavy_strike" }));
        Assert.DoesNotContain(TacticBench.Dropped(grown, bench.TacticsOf("universal")), d => d.MemberId == "tank");
        Assert.DoesNotContain(TacticBench.Allies(start, bench.TacticsOf("universal")).Single(a => a.Id == "tank").Tactics, t => t.ActionId == "heavy_strike");
    }

    [Fact]
    public void Search_is_deterministic_and_never_worse_than_its_start()
    {
        var (bench, data) = Loaded;
        var prepared = TacticBench.Prepare(bench, data, bench.Matchups.First(m => m.EncounterId == "shadow_hunters"));
        var options = new TacticSearch.Options { Generations = 4, Offspring = 3, Seeds = 8 };
        var start = TacticBench.Measure(prepared, bench.TacticsOf("universal"), data, options.Seeds, options.FirstSeed).Fitness;

        var first = TacticSearch.Run(data, [prepared], bench.TacticsOf("universal"), options);
        var second = TacticSearch.Run(data, [prepared], bench.TacticsOf("universal"), options);

        Assert.True(first.Fitness >= start);
        Assert.Equal(first.Fitness, second.Fitness);
        Assert.Equal(Describe(first.Tactics), Describe(second.Tactics));
    }

    [Fact]
    public void Extra_encounters_get_the_same_checks_as_the_game_data()
    {
        var duplicate = """[ { "id": "training", "name": "또 훈련", "units": [] } ]""";
        var unknownItem = """
            [ { "id": "proto", "name": "시제품", "units": [ { "id": "p1", "name": "적", "row": "Front",
                "stats": { "str": 10, "dex": 10, "vital": 10, "intel": 10, "speed": 10 }, "equipment": { "MainHand": "no_such_item" } } ] } ]
            """;

        Assert.Contains(Assert.Throws<GameDataException>(() => GameDataLoader.LoadDirectory(DataDirectory, duplicate)).Errors, e => e.Contains("training"));
        Assert.Contains(Assert.Throws<GameDataException>(() => GameDataLoader.LoadDirectory(DataDirectory, unknownItem)).Errors, e => e.Contains("no_such_item"));
        Assert.Contains(Assert.Throws<GameDataException>(() => GameDataLoader.LoadDirectory(DataDirectory, "{}")).Errors, e => e.StartsWith("extra encounters"));
    }

    private static string Rows(GameData data, string encounterId) =>
        string.Join(",", data.Encounters[encounterId].Units.Select(u => u.Row).Order());

    private static string Describe(IReadOnlyDictionary<string, IReadOnlyList<Tactic>> tactics) =>
        string.Join("|", tactics.OrderBy(p => p.Key).Select(p => p.Key + ":" + string.Join(";", p.Value.Select(t =>
            string.Join("&", t.Conditions.Select(c => $"{c.Condition}={c.Value}")) + ">" + t.ActionId))));
}
