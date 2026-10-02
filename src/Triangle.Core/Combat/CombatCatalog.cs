using Triangle.Core.Actions;
using Triangle.Core.Effects;
using Triangle.Core.Skills;

namespace Triangle.Core.Combat;

/// <summary>전투에 필요한 정의 묶음: 행동, (보너스 계산용) 스킬, 효과.</summary>
public sealed record CombatCatalog(
    IReadOnlyDictionary<string, ActionDefinition> Actions,
    IReadOnlyDictionary<string, SkillDefinition> Skills)
{
    public IReadOnlyDictionary<string, EffectDefinition> Effects { get; init; } = new Dictionary<string, EffectDefinition>();
}
