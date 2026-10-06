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

/// <summary>적 팀의 유닛 한 명. 적은 성장하지 않으므로 스킬을 레벨로 바로 정한다.</summary>
public sealed record EncounterUnitDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Stats Stats { get; init; }
    public required Row Row { get; init; }

    /// <summary>장착한 무기·방어구 계열 ID.</summary>
    public string? Weapon { get; init; }
    public string? Armor { get; init; }

    /// <summary>무기 위력 배율(%). 마법 지팡이를 든 적은 200 (아이템이 없으므로 직접 적는다).</summary>
    public int PowerMultiplierPercent { get; init; } = 100;

    /// <summary>스킬 ID별 레벨 (1–5).</summary>
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();

    public IReadOnlyList<Tactic> Tactics { get; init; } = [];
}
