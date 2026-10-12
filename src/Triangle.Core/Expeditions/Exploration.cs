using Triangle.Core.Data;
using Triangle.Core.Progress;

namespace Triangle.Core.Expeditions;

/// <summary>
/// 탐사 기록 (만난 적 도감). 정찰은 없다: 원정에서 직접 싸운 적 조합만 기록에 남고(<see cref="Company.EncounterRecords"/>),
/// 그 조합의 대원, 줄, 장비, 쓰는 행동을 볼 수 있다. 아직 만나지 않은 조합은 있다는 것만 안다.
/// </summary>
public static class Exploration
{
    /// <summary>지역에서 만날 수 있는 적 조합 (지역 목록 순서, 보스는 마지막).</summary>
    public static IReadOnlyList<ZoneEntry> Entries(ZoneDefinition zone) =>
        zone.Encounters.Select(e => e.EncounterId)
            .Distinct()
            .Select(id => new ZoneEntry(id, IsBoss: false))
            .Concat(zone.Boss is { } boss ? [new ZoneEntry(boss.EncounterId, IsBoss: true)] : [])
            .ToList();

    /// <summary>지역의 적 조합 중 만난 수와 전체 수.</summary>
    public static (int Met, int Total) Progress(Company company, ZoneDefinition zone)
    {
        var entries = Entries(zone);
        return (entries.Count(e => company.HasMet(e.EncounterId)), entries.Count);
    }

    /// <summary>적 유닛이 쓰는 행동 (전술 우선순위 순서, 같은 행동은 한 번).</summary>
    public static IReadOnlyList<string> UnitActions(EncounterUnitDefinition unit) =>
        unit.Tactics.OrderBy(t => t.Priority).Select(t => t.ActionId).Distinct().ToList();
}

/// <summary>지역에서 만날 수 있는 적 조합 하나.</summary>
/// <param name="IsBoss">지역 보스 (원정의 마지막 전투).</param>
public sealed record ZoneEntry(string EncounterId, bool IsBoss);
