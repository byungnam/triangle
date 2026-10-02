using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Xunit.Abstractions;

namespace Triangle.Core.Tests.Expeditions;

/// <summary>
/// 시작 회사로 지역마다 원정을 시드 여러 개로 끝까지(귀환하지 않고) 돌려 난이도 곡선을 잰다.
/// 성장(숙련, 패시브, 모집) 없이 처음 상태 그대로 계속 싸운다.
/// </summary>
public class DifficultyCurveTests(ITestOutputHelper output)
{
    private static readonly GameData Data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    public sealed record ZoneStats(string ZoneId, double ClearRate, double AverageDeaths, double AverageBattles, double WipeRate);

    public static ZoneStats Measure(GameData data, string zoneId, int seeds, int tacticSet = 0)
    {
        var (clears, wipes, deaths, battles) = (0, 0, 0, 0);
        for (var seed = 0; seed < seeds; seed++)
        {
            var company = StartingCompany.Create(data, seed);
            company.ActiveTacticSet = tacticSet;
            ExpeditionRules.Start(company, data, zoneId);
            ExpeditionSummary? summary = null;
            while (summary is null)
            {
                summary = ExpeditionRules.ApplyResult(company, data, ExpeditionRules.Fight(company, data));
            }

            clears += summary.End == ExpeditionEnd.Cleared ? 1 : 0;
            wipes += summary.End == ExpeditionEnd.Wiped ? 1 : 0;
            deaths += summary.Deaths.Count;
            battles += summary.Battles;
        }

        return new ZoneStats(zoneId, 100.0 * clears / seeds, (double)deaths / seeds, (double)battles / seeds, 100.0 * wipes / seeds);
    }

    [Fact]
    public void Zones_get_harder_with_difficulty()
    {
        const int seeds = 200;
        var stats = Data.Zones.Values.OrderBy(z => z.Difficulty).Select(z => Measure(Data, z.Id, seeds)).ToList();
        foreach (var s in stats)
        {
            output.WriteLine($"{s.ZoneId}: 클리어 {s.ClearRate:F0}%, 전멸 {s.WipeRate:F0}%, 평균 사망 {s.AverageDeaths:F2}, 평균 전투 {s.AverageBattles:F2}");
        }

        // 수치는 임시다. 곡선의 모양만 지킨다: 초보는 거의 다 클리어하고, 어려울수록 덜 클리어한다.
        Assert.True(stats[0].ClearRate >= 85, $"beginner clear rate {stats[0].ClearRate}%");
        for (var i = 1; i < stats.Count; i++)
        {
            Assert.True(stats[i].ClearRate < stats[i - 1].ClearRate, $"{stats[i].ZoneId} should be harder than {stats[i - 1].ZoneId}");
        }
    }
}
