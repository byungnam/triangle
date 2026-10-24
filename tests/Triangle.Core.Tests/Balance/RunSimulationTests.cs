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
    [InlineData(RunBot.Novice)]
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

            // 진 전투는 목숨 하나다: 마지막이 아니면 바로 다음이 같은 적과 다시 싸운 것이고, 다시 싸운 것은 늘 진 전투 다음이다.
            // 클리어는 마지막 막 보스를 이긴 것이다.
            var fights = r.Fights.ToList();
            Assert.InRange(fights.Count(f => f.Outcome != CombatOutcome.Victory), 0, Data.Run.Lives);
            for (var i = 0; i < fights.Count; i++)
            {
                if (fights[i].Outcome != CombatOutcome.Victory && i + 1 < fights.Count)
                {
                    Assert.True(fights[i + 1].Retry);
                    Assert.Equal((fights[i].Act, fights[i].Floor, fights[i].Kind, fights[i].EncounterId), (fights[i + 1].Act, fights[i + 1].Floor, fights[i + 1].Kind, fights[i + 1].EncounterId));
                }

                Assert.Equal(fights[i].Retry, i > 0 && fights[i - 1].Outcome != CombatOutcome.Victory);
            }

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

            // 똑똑한 봇은 보스마다 전술 방식과 줄 배치를 고르고 들어간다 (보스 대비). 다른 전투와 다른 봇은 고르지 않는다.
            // 다시 싸울 때 고친 것은 보스 대비로 세지 않는다.
            Assert.All(r.Fights, f => Assert.Equal(bot == RunBot.Smart && f.Kind == RunNodeKind.Boss && !f.Retry, f.Plan is not null));

            // 상점에서는 나온 물건만 사고, 강화는 상점의 강화 수까지 걸고, 쓴 골드만큼 골드가 준다.
            foreach (var shop in r.Shops)
            {
                Assert.All(shop.Bought, b => Assert.Contains(shop.Offered, o => o.Id == b.Id && o.Kind == b.Kind));
                Assert.InRange(shop.Upgraded.Count, 0, Data.Run.Shop.Upgrades);
                Assert.Equal(shop.GoldIn - shop.Bought.Sum(b => b.Price) - shop.Upgraded.Sum(u => u.Price) - shop.Unseals * Data.Run.Shop.UnsealPrice, shop.GoldOut);
            }

            // 막마다 첫 층에서 시작해 층을 하나씩 올라 보스로 간다.
            foreach (var act in r.Fights.GroupBy(f => f.Act))
            {
                Assert.Equal(act.Select(f => f.Floor).Order(), act.Select(f => f.Floor));
                Assert.All(act.Where(f => f.Kind == RunNodeKind.Boss), f => Assert.Equal(Data.Run.Acts[act.Key].Floors, f.Floor));
            }
        }
    }

    [Fact]
    public void Runs_can_roll_a_chart_each_and_the_books_still_balance()
    {
        // 판마다 지도 등급 2의 지도를 굴린다: 수식어 1~2개와 보상 하나. 같은 시드면 같은 지도다.
        var simulation = new RunSimulation(Data, new RunSimOptions { Bot = RunBot.Novice, Tier = 2, Unlocks = RunUnlocks.For(Data, new HashSet<string>()) });
        var results = simulation.RunMany(8);
        Assert.All(results, r => Assert.InRange(r.Chart.Modifiers.Count, 1, 2));
        Assert.All(results, r => Assert.NotNull(r.Chart.Reward));
        Assert.Equal(results[3].Chart, simulation.Play(3).Chart);
        Assert.True(results.Select(r => r.Chart).Distinct().Count() > 1);

        // 지도의 시작 골드도 셈에 든다.
        Assert.All(results, r => Assert.Equal(Math.Max(0, Data.Run!.StartGold + r.Chart.Terms(Data).StartGold) + r.GoldEarned - r.Spent.Values.Sum(), r.GoldLeft));
    }
}
