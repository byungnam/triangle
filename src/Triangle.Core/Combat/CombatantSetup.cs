using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Combat;

/// <summary>전투에 들어가는 유닛 한 명의 입력.</summary>
public sealed record CombatantSetup(
    string Id,
    string Name,
    ClassDefinition Class,
    Stats Stats,
    Row Row,
    IReadOnlyList<Tactic> Tactics);
