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

/// <summary>적 팀의 유닛 한 명. 직업은 ID로 참조한다.</summary>
public sealed record EncounterUnitDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ClassId { get; init; }
    public required Stats Stats { get; init; }
    public required Row Row { get; init; }
    public IReadOnlyList<Tactic> Tactics { get; init; } = [];
}
