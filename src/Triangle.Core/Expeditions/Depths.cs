using Triangle.Core.Data;
using Triangle.Core.Progress;

namespace Triangle.Core.Expeditions;

/// <summary>
/// 깊이: 지역을 난이도순으로 늘어놓은 것. 첫 지역은 처음부터 열려 있고, 그다음 지역은 바로 앞 지역의
/// 관문 보스를 넘어야 열린다(앞 지역에 관문이 없으면 그냥 열려 있다). 관문 보스는 그 지역을 한 번 클리어한 뒤에 도전한다.
/// </summary>
public static class Depths
{
    /// <summary>지역을 깊이 순서로 (난이도순, 같으면 데이터 순서).</summary>
    public static IReadOnlyList<ZoneDefinition> Ordered(GameData data) => data.Zones.Values.OrderBy(z => z.Difficulty).ToList();

    /// <summary>지역이 잠긴 이유. null이면 열려 있다.</summary>
    public static string? WhyZoneLocked(Company company, GameData data, string zoneId)
    {
        var ordered = Ordered(data);
        var index = ordered.ToList().FindIndex(z => z.Id == zoneId);
        if (index <= 0)
        {
            return null;
        }

        var previous = ordered[index - 1];
        return previous.Gate is null || company.HasPassedGate(previous.Id)
            ? null
            : $"{previous.Name}의 관문을 넘어야 열립니다";
    }

    /// <summary>관문 보스에 도전할 수 없는 이유. null이면 도전할 수 있다.</summary>
    public static string? WhyGateLocked(Company company, GameData data, string zoneId)
    {
        var zone = data.Zones[zoneId];
        if (zone.Gate is null)
        {
            return "관문이 없는 지역입니다";
        }

        if (WhyZoneLocked(company, data, zoneId) is { } locked)
        {
            return locked;
        }

        return company.HasCleared(zoneId) ? null : $"{zone.Name} 원정을 한 번 클리어해야 도전할 수 있습니다";
    }
}
