using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Xunit.Abstractions;

namespace Triangle.Core.Tests.Expeditions;

/// <summary>
/// 관문 보스의 난이도: 시작 회사와 세 성장 상태(<see cref="Growth"/>)로 관문마다 시드 여러 개를 돌려 승률을 잰다.
/// 관문은 성장으로도 넘을 수 있어야 하고(한 단계 자란 상태는 대체로 이긴다), 성장 없이는 쉽게 넘지 못해야 한다.
/// </summary>
public class GateCurveTests(ITestOutputHelper output)
{
    private static readonly GameData Data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static readonly (string Name, int Tier, int Level)[] States = [("시작", 0, 0), ("T2 Lv5", 2, 5), ("T3 Lv12", 3, 12), ("T4 Lv20", 4, 20)];

    /// <summary>관문 하나의 승률 (%). 시드마다 새 회사로 한 번 싸운다.</summary>
    public static double WinRate(GameData data, string zoneId, int seeds, int tier = 0, int masteryLevel = 0, int tacticSet = 0)
    {
        var wins = 0;
        for (var seed = 0; seed < seeds; seed++)
        {
            var company = StartingCompany.Create(data, seed);
            company.ActiveTacticSet = tacticSet;
            if (tier > 0)
            {
                Growth.Grow(company, data, tier, masteryLevel);
            }

            var zone = data.Zones[zoneId];
            var result = CombatSimulator.Run(company.LineupSetups(data), data.CreateEncounterTeam(zone.Gate!.EncounterId), data.Catalog, seed);
            wins += result.Outcome == CombatOutcome.Victory ? 1 : 0;
        }

        return 100.0 * wins / seeds;
    }

    [Fact]
    public void Gates_need_growth_or_tactics()
    {
        const int seeds = 200;
        var zones = Depths.Ordered(Data).Where(z => z.Gate is not null).ToList();
        var rates = new Dictionary<(string, string, int), double>();
        foreach (var zone in zones)
        {
            foreach (var (name, tier, level) in States)
            {
                foreach (var set in new[] { 0, 1 })
                {
                    rates[(zone.Id, name, set)] = WinRate(Data, zone.Id, seeds, tier, level, set);
                }

                output.WriteLine($"{zone.Name} 관문 · {name}: 세트 1 {rates[(zone.Id, name, 0)]:F0}%, 세트 2 {rates[(zone.Id, name, 1)]:F0}%");
            }
        }

        // 수치는 임시다. 모양만 지킨다.
        // 깊이 n의 관문은 그 깊이의 성장 상태(깊이 1은 시작, 2는 T2, 3은 T3)의 기본 전술로는 쉽게 넘지 못하고,
        // 한 단계 더 자란 상태는 대체로 넘는다. 다듬은 전술(세트 1)은 그 사이에 있다.
        Assert.True(rates[(zones[0].Id, "시작", 1)] < 50, "gate 1 should not fall to the starting company's basic tactics");
        Assert.True(rates[(zones[0].Id, "T2 Lv5", 0)] >= 70, "gate 1 should fall to T2 growth");
        Assert.True(rates[(zones[1].Id, "T2 Lv5", 1)] < 50, "gate 2 should not fall to T2 basic tactics");
        Assert.True(rates[(zones[1].Id, "T3 Lv12", 0)] >= 70, "gate 2 should fall to T3 growth");
        Assert.True(rates[(zones[2].Id, "T3 Lv12", 1)] < 70, "gate 3 should not fall easily to T3 basic tactics");
        Assert.True(rates[(zones[2].Id, "T4 Lv20", 0)] >= 70, "gate 3 should fall to T4 growth");
    }
}
