namespace Triangle.Core.Progress;

/// <summary>
/// 마을 시설의 한 레벨이 주는 효과. 레벨마다 누적된 값이다(이전 레벨 효과에 더하지 않는다).
/// </summary>
/// <param name="Price">이 레벨로 올리는 데 드는 골드.</param>
/// <param name="ExpeditionRecovery">원정이 한 번 끝날 때 나가지 않은 부상 멤버가 더 낫는 단계.</param>
/// <param name="RestRecovery">쉬기 한 번에 더 낫는 단계.</param>
/// <param name="RestDiscountPercent">쉬기 비용 할인 (%).</param>
public sealed record FacilityLevel(int Price, int ExpeditionRecovery = 0, int RestRecovery = 0, int RestDiscountPercent = 0);

/// <param name="Levels">레벨 1부터 차례로. 레벨 0(짓지 않음)은 효과가 없다.</param>
public sealed record FacilityDefinition(string Id, string Name, string Description, IReadOnlyList<FacilityLevel> Levels)
{
    public int MaxLevel => Levels.Count;

    /// <summary>그 레벨의 효과. 레벨 0이면 효과 없음.</summary>
    public FacilityLevel At(int level) => level <= 0 ? new FacilityLevel(0) : Levels[Math.Min(level, MaxLevel) - 1];
}

/// <summary>
/// 마을 시설 목록 (2026-10-16). 지금은 치료소 하나라서 코드에 둔다. 시설이 늘면 데이터 파일로 옮긴다. 수치는 임시다.
/// </summary>
public static class Facilities
{
    public const string Infirmary = "infirmary";

    public static readonly IReadOnlyList<FacilityDefinition> All =
    [
        new(Infirmary, "치료소", "부상이 더 빨리 낫고 쉬기 비용이 준다.",
        [
            new(300, RestDiscountPercent: 25),
            new(800, ExpeditionRecovery: 1, RestDiscountPercent: 25),
            new(2000, ExpeditionRecovery: 1, RestRecovery: 1, RestDiscountPercent: 50),
        ]),
    ];

    public static FacilityDefinition? Find(string id) => All.FirstOrDefault(f => f.Id == id);
}

/// <summary>마을에서 골드를 쓰는 규칙의 기본 수치 (임시).</summary>
public static class VillageRules
{
    /// <summary>쉬기 한 번에 부상 멤버 한 명당 드는 골드 (치료소 할인 전).</summary>
    public const int RestCostPerInjured = 40;

    /// <summary>패시브를 되돌릴 때 돌려받는 포인트 하나당 드는 골드.</summary>
    public const int RespecCostPerPoint = 50;

    /// <summary>원정이 한 번 끝날 때, 그리고 쉬기 한 번에 낫는 기본 단계.</summary>
    public const int BaseRecovery = 1;
}
