using Triangle.Core.Actions;
using Triangle.Core.Skills;

namespace Triangle.Core.Combat;

/// <summary>전투에 필요한 정의 묶음: 행동과 (보너스 계산용) 스킬.</summary>
public sealed record CombatCatalog(
    IReadOnlyDictionary<string, ActionDefinition> Actions,
    IReadOnlyDictionary<string, SkillDefinition> Skills);
