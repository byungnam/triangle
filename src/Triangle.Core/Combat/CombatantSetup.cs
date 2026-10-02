using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Combat;

/// <summary>전투에 들어가는 유닛 한 명의 입력.</summary>
/// <param name="Skills">스킬 ID별 레벨 (1–5). 없는 스킬은 0레벨.</param>
public sealed record CombatantSetup(
    string Id,
    string Name,
    Stats Stats,
    Row Row,
    IReadOnlyDictionary<string, int> Skills,
    IReadOnlyList<Tactic> Tactics);
