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

/// <summary>적 팀의 유닛 한 명. 적은 훈련하지 않으므로 스킬을 레벨로 바로 정한다.</summary>
public sealed record EncounterUnitDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Stats Stats { get; init; }
    public required Row Row { get; init; }

    /// <summary>스킬 ID별 레벨 (1–5).</summary>
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();

    public IReadOnlyList<Tactic> Tactics { get; init; } = [];
}
