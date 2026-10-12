namespace Triangle.Core.Progress;

/// <summary>
/// 관문 보스 하나의 도전 기록. 이긴 판마다 갱신하고, 항목마다 가장 좋은 값만 남긴다.
/// 성장이 아니라 전술로 넘은 것을 인정하려는 기록이다: 적은 인원, 낮은 장비 등급, 쓰러진 대원 없음.
/// </summary>
/// <param name="Wins">이긴 횟수 (1 이상: 기록이 있으면 관문을 넘은 것이다).</param>
/// <param name="FewestMembers">이겼을 때 출전한 가장 적은 인원.</param>
/// <param name="LowestGearTier">이겼을 때 가장 낮은 장비 등급 (출전 멤버가 낀 장비 중 가장 높은 티어, 아무것도 안 꼈으면 0).</param>
/// <param name="FewestDowned">이겼을 때 가장 적게 쓰러진 대원 수 (0이면 쓰러진 대원 없이 이겼다).</param>
public sealed record GateRecord(int Wins, int FewestMembers, int LowestGearTier, int FewestDowned)
{
    /// <summary>첫 승리의 기록.</summary>
    public static GateRecord First(GateResult result) => new(1, result.Members, result.GearTier, result.Downed);

    /// <summary>이긴 판을 더한 기록과 이번에 좋아진 항목.</summary>
    public (GateRecord Record, IReadOnlyList<GateRecordKind> Improved) Add(GateResult result)
    {
        var improved = new List<GateRecordKind>();
        if (result.Members < FewestMembers)
        {
            improved.Add(GateRecordKind.FewestMembers);
        }

        if (result.GearTier < LowestGearTier)
        {
            improved.Add(GateRecordKind.LowestGearTier);
        }

        if (result.Downed < FewestDowned)
        {
            improved.Add(GateRecordKind.FewestDowned);
        }

        var record = new GateRecord(
            Wins + 1,
            Math.Min(FewestMembers, result.Members),
            Math.Min(LowestGearTier, result.GearTier),
            Math.Min(FewestDowned, result.Downed));
        return (record, improved);
    }
}

/// <summary>관문 보스를 이긴 한 판.</summary>
/// <param name="Members">출전 인원.</param>
/// <param name="GearTier">출전 멤버가 낀 장비 중 가장 높은 티어 (아무것도 안 꼈으면 0).</param>
/// <param name="Downed">쓰러진 대원 수.</param>
public sealed record GateResult(int Members, int GearTier, int Downed);

public enum GateRecordKind
{
    FewestMembers,
    LowestGearTier,
    FewestDowned,
}
