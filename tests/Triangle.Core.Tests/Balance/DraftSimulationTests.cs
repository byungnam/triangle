using Triangle.Core.Balance;
using Triangle.Core.Data;
using Triangle.Core.Tactics;

namespace Triangle.Core.Tests.Balance;

/// <summary>
/// 드래프트 시뮬레이션(`tools/draft`)의 규칙. 로그라이트 재편(설계)의 임시 규칙이고 수치도 임시다. 결과 수치는 보지 않고 규칙만 지킨다.
/// </summary>
public class DraftSimulationTests
{
    private static readonly GameData Data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static readonly DraftDefinition Draft = DraftDefinition.Load(Path.Combine(AppContext.BaseDirectory, "draft", "draft.json"), Data);

    [Fact]
    public void Classes_draw_only_from_their_own_families()
    {
        IReadOnlySet<string> Pool(string classId) => DraftSimulation.ActivePool(Data, Draft.Class(classId));

        Assert.Contains("shield_wall", Pool("knight"));
        Assert.Contains("provoke", Pool("knight"));
        Assert.Contains("throwing_knife", Pool("archer"));
        Assert.Contains("fireball", Pool("elementalist"));
        Assert.Contains("heal", Pool("priest"));
        Assert.DoesNotContain("fireball", Pool("knight"));
        Assert.DoesNotContain("basic_attack", Pool("priest"));

        // 재질 행동(도발, 투척 단검 등)도 그 클래스 전용이므로 클래스끼리 겹치는 카드가 없다.
        var classes = Draft.Classes.Select(c => c.Id).ToList();
        Assert.All(classes.SelectMany(a => classes.Where(b => string.CompareOrdinal(a, b) < 0).Select(b => (a, b))),
            pair => Assert.Empty(Pool(pair.a).Intersect(Pool(pair.b))));
    }

    [Fact]
    public void Placement_keeps_the_line_cap_and_always_ends_with_the_basic_attack()
    {
        string[] skills = ["fireball", "ash_storm", "ember", "flame_pillar"];
        for (var lines = 0; lines <= 5; lines++)
        {
            var tactics = DraftTactics.Build(skills, lines, Data);
            Assert.Equal(Math.Min(lines, skills.Length) + 1, tactics.Count);
            Assert.Equal((Condition.Always, DraftTactics.Fallback), (tactics[^1].Condition, tactics[^1].ActionId));
            Assert.Equal(Enumerable.Range(1, tactics.Count), tactics.Select(t => t.Priority));
        }
    }

    [Fact]
    public void Placement_heals_first_and_only_casts_what_it_can_pay_for()
    {
        var tactics = DraftTactics.Build(["fireball", "heal"], 2, Data);

        Assert.Equal(["heal", "fireball"], tactics.Take(2).Select(t => t.ActionId));
        Assert.Contains(new TacticCondition(Condition.SelfMpAmountAtLeast, Data.Actions["fireball"].MpCost), tactics[1].Conditions);
    }

    [Fact]
    public void A_second_heal_copy_gets_a_looser_line_and_a_second_attack_copy_is_wasted()
    {
        var heals = DraftTactics.Build(["heal", "heal"], 2, Data);
        Assert.Equal([(Condition.AnyAllyHpAtMost, 50), (Condition.AnyAllyHpAtMost, 75)], heals.Take(2).Select(t => (t.Condition, t.Value)));

        // 두 번째 화염구는 첫 번째와 같은 줄이라 쓸모가 없어 맨 아래로 가고, 줄 상한 2에서 꺼진다.
        var bolts = DraftTactics.Build(["fireball", "fireball", "heal"], 2, Data);
        Assert.Equal(["heal", "fireball", DraftTactics.Fallback], bolts.Select(t => t.ActionId));
    }

    [Fact]
    public void Runs_are_deterministic()
    {
        foreach (var picker in new[] { DraftPicker.Random, DraftPicker.Greedy })
        {
            var simulation = new DraftSimulation(Draft, Data, new DraftOptions { Picker = picker, LookaheadSeeds = 1 });
            Assert.Equal(Describe(simulation.Run(7)), Describe(simulation.Run(7)));
        }
    }

    [Fact]
    public void Going_down_seals_a_line_and_with_no_line_left_it_kills()
    {
        // 줄 상한 카드가 없으면 상한은 시작 값에서 늘지 않는다. 버려진 요새를 T1 장비로 가면 이긴 전투에서도 자주 쓰러진다.
        var draft = Draft with { Offer = Draft.Offer with { Line = 0 }, Acts = [new DraftAct("abandoned_fort", 6, 1)] };

        // 시작 줄 상한이 0이면 봉인할 줄이 없어 쓰러질 때마다 죽는다.
        var bare = new DraftSimulation(draft, Data, new DraftOptions { Picker = DraftPicker.Random, StartLines = 0 }).RunMany(40);
        Assert.Contains(bare, r => r.Downs > 0);
        Assert.All(bare, r => Assert.Equal((r.Downs, (int?)null), (r.Deaths, r.FirstSeal)));

        // 줄 상한이 있으면 처음 쓰러질 때는 죽지 않고 봉인된다.
        var lined = new DraftSimulation(draft, Data, new DraftOptions { Picker = DraftPicker.Random, StartLines = 2 }).RunMany(40);
        Assert.Contains(lined, r => r.Downs > 0);
        Assert.All(lined.Where(r => r.Downs > 0), r => Assert.NotNull(r.FirstSeal));
        Assert.All(lined, r => Assert.True(r.Deaths < r.Downs || r.Downs == 0));
    }

    [Fact]
    public void Definition_checks_its_references()
    {
        var wrongSkill = Draft with { Classes = [Draft.Classes[0] with { StartingSkill = "fireball" }, .. Draft.Classes.Skip(1)] };
        var unknownClass = Draft with { Party = ["knight", "bard"] };

        Assert.Contains(wrongSkill.Validate(Data), e => e.Contains("starting skill 'fireball'"));
        Assert.Contains(unknownClass.Validate(Data), e => e.Contains("unknown class 'bard'"));
        Assert.Empty(Draft.Validate(Data));
    }

    private static string Describe(DraftRunResult r) =>
        $"{r.Outcome} {r.FightsWon} {r.Downs} {r.Deaths} {r.Encounter} " + string.Join(";", r.Picks.Select(p => $"{p.Card.Kind}:{p.Card.Id}@{p.Member}"));
}
