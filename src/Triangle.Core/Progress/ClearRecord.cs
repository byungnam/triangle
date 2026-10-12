namespace Triangle.Core.Progress;

/// <summary>
/// 지역 하나의 클리어 기록. 클리어한 원정마다 갱신하고, 항목마다 가장 좋은 값만 남긴다.
/// 성장이 아니라 전술로 넘은 것을 인정하려는 기록이다: 적은 인원, 낮은 장비 등급, 쓰러진 대원 없음.
/// </summary>
/// <param name="Clears">클리어한 횟수 (1 이상: 기록이 있으면 클리어한 것이다).</param>
/// <param name="FewestMembers">클리어했을 때 출전한 가장 적은 인원.</param>
/// <param name="LowestGearTier">클리어했을 때 가장 낮은 장비 등급 (출전 멤버가 낀 장비 중 가장 높은 티어, 아무것도 안 꼈으면 0).</param>
/// <param name="FewestDowned">클리어했을 때 가장 적게 쓰러진 대원 수 (0이면 쓰러진 대원 없이 클리어했다).</param>
public sealed record ClearRecord(int Clears, int FewestMembers, int LowestGearTier, int FewestDowned)
{
    /// <summary>첫 클리어의 기록.</summary>
    public static ClearRecord First(ClearResult result) => new(1, result.Members, result.GearTier, result.Downed);

    /// <summary>클리어한 원정을 더한 기록과 이번에 좋아진 항목.</summary>
    public (ClearRecord Record, IReadOnlyList<ClearRecordKind> Improved) Add(ClearResult result)
    {
        var improved = new List<ClearRecordKind>();
        if (result.Members < FewestMembers)
        {
            improved.Add(ClearRecordKind.FewestMembers);
        }

        if (result.GearTier < LowestGearTier)
        {
            improved.Add(ClearRecordKind.LowestGearTier);
        }

        if (result.Downed < FewestDowned)
        {
            improved.Add(ClearRecordKind.FewestDowned);
        }

        var record = new ClearRecord(
            Clears + 1,
            Math.Min(FewestMembers, result.Members),
            Math.Min(LowestGearTier, result.GearTier),
            Math.Min(FewestDowned, result.Downed));
        return (record, improved);
    }
}

/// <summary>클리어한 원정 하나.</summary>
/// <param name="Members">출전 인원.</param>
/// <param name="GearTier">출전 멤버가 낀 장비 중 가장 높은 티어 (아무것도 안 꼈으면 0).</param>
/// <param name="Downed">원정 동안 쓰러진 대원 수.</param>
public sealed record ClearResult(int Members, int GearTier, int Downed);

public enum ClearRecordKind
{
    FewestMembers,
    LowestGearTier,
    FewestDowned,
}
