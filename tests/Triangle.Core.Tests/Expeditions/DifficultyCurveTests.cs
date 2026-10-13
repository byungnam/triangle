using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Xunit.Abstractions;

namespace Triangle.Core.Tests.Expeditions;

/// <summary>
/// 지역마다 원정을 시드 여러 개로 끝까지 돌려 난이도 곡선을 잰다. 마지막 전투는 지역 보스다.
/// 시작 회사와, 같은 회사를 키운 성장 상태(<see cref="Growth"/>)로 잰다. 원정 사이의 성장은 없다.
/// </summary>
public class DifficultyCurveTests(ITestOutputHelper output)
{
    private static readonly GameData Data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static readonly (string Name, int Tier, int Level)[] States = [("시작", 0, 0), ("T2 Lv5", 2, 5), ("T3 Lv12", 3, 12), ("T4 Lv20", 4, 20)];

    /// <param name="BossReachRate">보스 전투까지 간 비율 (%).</param>
    public sealed record ZoneStats(string ZoneId, double ClearRate, double BossReachRate, double AverageDowned, double WipeRate);

    public static ZoneStats Measure(GameData data, string zoneId, int seeds, int tacticSet = 0, int tier = 0, int masteryLevel = 0)
    {
        var (clears, wipes, downed, reached) = (0, 0, 0, 0);
        for (var seed = 0; seed < seeds; seed++)
        {
            var company = StartingCompany.Create(data, seed);
            company.ActiveTacticSet = tacticSet;
            if (tier > 0)
            {
                Growth.Grow(company, data, tier, masteryLevel);
            }

            Growth.UnlockAll(company, data);
            var expedition = ExpeditionRules.Start(company, data, zoneId);
            ExpeditionSummary? summary = null;
            while (summary is null)
            {
                reached += ExpeditionRules.IsBossBattle(expedition, data) ? 1 : 0;
                summary = ExpeditionRules.ApplyResult(company, data, ExpeditionRules.Fight(company, data));
            }

            clears += summary.End == ExpeditionEnd.Cleared ? 1 : 0;
            wipes += summary.End == ExpeditionEnd.Wiped ? 1 : 0;
            downed += expedition.Members.Count(m => m.Down);
        }

        return new ZoneStats(zoneId, 100.0 * clears / seeds, 100.0 * reached / seeds, (double)downed / seeds, 100.0 * wipes / seeds);
    }

    [Fact]
    public void Each_depth_needs_tactics_or_growth()
    {
        const int seeds = 200;
        var zones = Depths.Ordered(Data);
        var rates = new Dictionary<(string Zone, string State, int Set), double>();
        foreach (var zone in zones)
        {
            foreach (var (name, tier, level) in States)
            {
                var line = $"{zone.Name} · {name}:";
                foreach (var set in new[] { 0, 1 })
                {
                    var s = Measure(Data, zone.Id, seeds, set, tier, level);
                    rates[(zone.Id, name, set)] = s.ClearRate;
                    line += $" 세트 {set + 1} 클리어 {s.ClearRate:F0}% (보스까지 {s.BossReachRate:F0}%, 쓰러짐 {s.AverageDowned:F2})";
                }

                output.WriteLine(line);
            }
        }

        // 수치는 임시다. 모양만 지킨다: 깊이 n은 그 깊이의 성장 상태(깊이 1은 시작, 2는 T2, 3은 T3)의 기본 전술(세트 2)로는
        // 쉽게 클리어하지 못하고, 한 단계 더 자란 상태는 대체로 클리어한다. 다듬은 전술(세트 1)은 그 사이에 있다.
        var expected = new[] { ("시작", "T2 Lv5"), ("T2 Lv5", "T3 Lv12"), ("T3 Lv12", "T4 Lv20") };
        for (var i = 0; i < zones.Count; i++)
        {
            var (current, grown) = expected[i];
            Assert.True(rates[(zones[i].Id, current, 1)] < 50, $"{zones[i].Id} should not fall to {current} basic tactics");
            Assert.True(rates[(zones[i].Id, grown, 0)] >= 70, $"{zones[i].Id} should fall to {grown} growth");
        }
    }
}
