using Triangle.Core.Data;
using Triangle.Core.Progress;

namespace Triangle.Core.Expeditions;

/// <summary>
/// 깊이: 지역을 난이도순으로 늘어놓은 것. 첫 지역은 처음부터 열려 있고, 그다음 지역은 바로 앞 지역을
/// 클리어(마지막 전투의 보스까지 이김)해야 열린다.
/// </summary>
public static class Depths
{
    /// <summary>지역을 깊이 순서로 (난이도순, 같으면 데이터 순서).</summary>
    public static IReadOnlyList<ZoneDefinition> Ordered(GameData data) => data.Zones.Values.OrderBy(z => z.Difficulty).ToList();

    /// <summary>지역이 잠긴 이유. null이면 열려 있다.</summary>
    public static string? WhyZoneLocked(Company company, GameData data, string zoneId)
    {
        var ordered = Ordered(data).ToList();
        var index = ordered.FindIndex(z => z.Id == zoneId);
        if (index <= 0)
        {
            return null;
        }

        var previous = ordered[index - 1];
        return company.HasCleared(previous.Id) ? null : $"{previous.Name} 클리어가 필요합니다";
    }
}
