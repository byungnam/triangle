using Triangle.Core.Items;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Data;

/// <summary>미리 정의된 적 팀.</summary>
public sealed record EncounterDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>편집기용 메모.</summary>
    public string? Description { get; init; }

    public required IReadOnlyList<EncounterUnitDefinition> Units { get; init; }
}

/// <summary>적 팀의 유닛 한 명. 적은 성장하지 않으므로 스킬을 레벨로 바로 정한다. 행동은 <see cref="Actions"/>(없으면 낀 아이템이 주는 것)과 공용 행동만 쓴다.</summary>
public sealed record EncounterUnitDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Stats Stats { get; init; }
    public required Row Row { get; init; }

    /// <summary>
    /// 부위별 아이템 ID (파티와 같은 아이템, 2026-10-07). 낀 아이템의 행동, 보너스, 위력 배율을 그대로 쓴다.
    /// 숙련 아이템 파워는 없다(숙련 레벨 0). 비우면 맨손·맨몸(짐승)이다.
    /// </summary>
    public IReadOnlyDictionary<EquipmentSlot, string> Equipment { get; init; } = new Dictionary<EquipmentSlot, string>();

    /// <summary>스킬 ID별 레벨 (1–5).</summary>
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// 쓰는 행동 ID (공용 행동은 적지 않는다). 있으면 장비가 주는 행동 대신 이것만 쓴다. 없으면 낀 장비가 준다.
    /// 로그라이트 재편에서 아이템은 행동을 주지 않으므로 적도 행동을 직접 가진다 (2026-10-19).
    /// </summary>
    public IReadOnlyList<string>? Actions { get; init; }

    /// <summary>타고난 효과 ID (면역 같은 특성). 전투를 시작할 때 걸려 있고 지워지기 전까지 끝나지 않는다 (2026-10-17).</summary>
    public IReadOnlyList<string> Effects { get; init; } = [];

    public IReadOnlyList<Tactic> Tactics { get; init; } = [];
}
