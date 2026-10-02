namespace Triangle.Core.Expeditions;

/// <summary>
/// 전투지역. 원정은 지역 하나를 골라 최대 <see cref="MaxBattles"/>번 연속으로 싸운다.
/// 전투마다 <see cref="Encounters"/>에서 가중치에 따라 적 조합 하나를 무작위로 만난다.
/// </summary>
public sealed record ZoneDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>편집기용 메모. 화면에도 보여준다.</summary>
    public string? Description { get; init; }

    /// <summary>난이도 (표시와 정렬용, 클수록 어렵다).</summary>
    public int Difficulty { get; init; }

    /// <summary>원정 한 번에 싸울 수 있는 최대 전투 수. 마지막 전투에서 이기면 지역 클리어.</summary>
    public required int MaxBattles { get; init; }

    /// <summary>참이면 쓰러진 캐릭터가 로스터에서 삭제된다. 거짓이면 원정이 끝날 때 회복한다.</summary>
    public bool Permadeath { get; init; }

    /// <summary>영구 사망 때 장착 아이템이 하나씩 파괴될 확률 (%). 남은 아이템은 들고 있는 전리품이 된다.</summary>
    public int EquipmentDestroyChance { get; init; }

    public required IReadOnlyList<ZoneEncounter> Encounters { get; init; }

    public ZoneRewards Rewards { get; init; } = new();
}

/// <param name="Weight">가중치 (1 이상). 지역의 가중치 합에 대한 비율로 나온다.</param>
public sealed record ZoneEncounter(string EncounterId, int Weight);

/// <summary>전리품 규칙. 전투에서 이길 때마다 굴리고, 귀환하거나 클리어해야 확정된다.</summary>
public sealed record ZoneRewards
{
    /// <summary>전투당 골드 (최소–최대, 균등).</summary>
    public int GoldMin { get; init; }
    public int GoldMax { get; init; }

    /// <summary>깊이 보너스 (%): n번째 전투(0부터)의 골드에 n × 이 값 %를 더한다.</summary>
    public int DepthBonusPercent { get; init; }

    /// <summary>전투에서 이길 때마다 아이템별로 따로 굴린다.</summary>
    public IReadOnlyList<ItemDrop> ItemDrops { get; init; } = [];

    /// <summary>마지막 전투에서 이기면 더 받는 골드.</summary>
    public int ClearBonusGold { get; init; }
}

/// <param name="Chance">떨어질 확률 (%).</param>
public sealed record ItemDrop(string ItemId, int Chance);
