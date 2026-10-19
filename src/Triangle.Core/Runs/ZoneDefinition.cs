namespace Triangle.Core.Runs;

/// <summary>
/// 지역 (`data/zones.json`). 판의 막 하나가 지역 하나다(<see cref="RunAct"/>).
/// 보통 전투는 <see cref="Encounters"/>에서 가중치로 적 조합을 뽑고, 막의 보스 후보가 없으면 <see cref="Boss"/>가 막 보스다.
/// </summary>
public sealed record ZoneDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>편집기용 메모.</summary>
    public string? Description { get; init; }

    public required IReadOnlyList<ZoneEncounter> Encounters { get; init; }

    public BossDefinition? Boss { get; init; }
}

/// <summary>지역 보스.</summary>
public sealed record BossDefinition
{
    public required string EncounterId { get; init; }

    /// <summary>이야기 단서 ("왜 삼각형인가"를 조금씩 드러낸다). 지금은 화면에 쓰지 않는다.</summary>
    public string? Clue { get; init; }
}

/// <param name="Weight">가중치 (1 이상). 지역의 가중치 합에 대한 비율로 나온다.</param>
public sealed record ZoneEncounter(string EncounterId, int Weight);
