using Triangle.Core.Balance;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Runs;

namespace Triangle.Core.Tests.Balance;

/// <summary>판 시뮬레이션(`triangle-data runs`)의 규칙. 게임의 판 규칙을 그대로 쓰므로 결과 수치는 보지 않고 기록이 맞는지만 본다.</summary>
public class RunSimulationTests
{
    private static readonly GameData Data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static string Describe(RunSimResult r) =>
        $"{r.Cleared} {r.Act} {r.Floor} {r.Deaths} {r.Skipped} {r.GoldEarned} {r.GoldLeft} {r.Members} {r.Joined} {r.Copies} {r.Placed} {r.Lines} "
        + string.Join(",", r.Fights.Select(f => $"{f.Act}/{f.Floor}/{f.Kind}/{f.EncounterId}/{f.Outcome}/{f.Downs}"))
        + " | " + string.Join(",", r.Picks.Select(p => $"{p.Kind}:{p.Id}"))
        + " | " + string.Join(",", r.Spent.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));

    [Theory]
    [InlineData(RunBot.Random)]
    [InlineData(RunBot.Smart)]
    public void Runs_are_deterministic(RunBot bot)
    {
        var simulation = new RunSimulation(Data, new RunSimOptions { Bot = bot, LookaheadSeeds = 1 });
        Assert.Equal(Describe(simulation.Play(3)), Describe(simulation.Play(3)));
        Assert.Equal(simulation.RunMany(3, 5).Select(Describe), Enumerable.Range(5, 3).Select(seed => Describe(simulation.Play(seed))));
    }

    [Theory]
    [InlineData(RunBot.Random)]
    [InlineData(RunBot.Smart)]
    public void Runs_play_to_the_end_and_the_books_balance(RunBot bot)
    {
        var unlocks = RunUnlocks.For(Data, new HashSet<string>());
        var simulation = new RunSimulation(Data, new RunSimOptions { Bot = bot, LookaheadSeeds = 1, Unlocks = unlocks });
        foreach (var r in simulation.RunMany(6))
        {
            // 번 골드에서 쓴 골드를 빼면 남은 골드다.
            Assert.Equal(Data.Run!.StartGold + unlocks.StartGold + r.GoldEarned - r.Spent.Values.Sum(), r.GoldLeft);
            Assert.InRange(r.Placed, 0, r.Copies);
            Assert.NotNull(r.Last);

            // 마지막 전투만 이기지 못할 수 있고, 클리어는 마지막 막 보스를 이긴 것이다.
            Assert.All(r.Fights.SkipLast(1), f => Assert.Equal(CombatOutcome.Victory, f.Outcome));
            Assert.Equal(r.Cleared, r.Last!.Outcome == CombatOutcome.Victory);
            if (r.Cleared)
            {
                Assert.Equal((RunNodeKind.Boss, Data.Run.Acts.Count - 1), (r.Last.Kind, r.Last.Act));
                Assert.Equal(Data.Run.Acts.Count, r.Act);
            }
            else
            {
                Assert.Equal(r.Last.Act, r.Act);
            }

            // 상점에서는 나온 물건만 사고, 쓴 골드만큼 골드가 준다.
            foreach (var shop in r.Shops)
            {
                Assert.All(shop.Bought, b => Assert.Contains(shop.Offered, o => o.Id == b.Id && o.Kind == b.Kind));
                Assert.Equal(shop.GoldIn - shop.Bought.Sum(b => b.Price) - shop.Unseals * Data.Run.Shop.UnsealPrice, shop.GoldOut);
            }

            // 막마다 첫 층에서 시작해 층을 하나씩 올라 보스로 간다.
            foreach (var act in r.Fights.GroupBy(f => f.Act))
            {
                Assert.Equal(act.Select(f => f.Floor).Order(), act.Select(f => f.Floor));
                Assert.All(act.Where(f => f.Kind == RunNodeKind.Boss), f => Assert.Equal(Data.Run.Acts[act.Key].Floors, f.Floor));
            }
        }
    }
}
