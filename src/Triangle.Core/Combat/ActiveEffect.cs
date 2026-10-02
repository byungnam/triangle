using Triangle.Core.Effects;

namespace Triangle.Core.Combat;

/// <summary>유닛에게 걸려 있는 효과 하나와 남은 행동 횟수.</summary>
internal sealed class ActiveEffect(EffectDefinition definition, int remaining)
{
    public EffectDefinition Definition { get; } = definition;
    public int Remaining { get; set; } = remaining;
}
