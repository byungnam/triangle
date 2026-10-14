using Triangle.Core.Effects;

namespace Triangle.Core.Combat;

/// <summary>유닛에게 걸려 있는 효과 하나와 남은 행동 횟수, 그 효과를 (마지막으로) 건 유닛.</summary>
internal sealed class ActiveEffect(EffectDefinition definition, int remaining, string sourceId)
{
    public EffectDefinition Definition { get; } = definition;
    public int Remaining { get; set; } = remaining;

    /// <summary>다시 걸리면 새로 건 유닛으로 바뀐다. 지속 피해·회복은 이 유닛이 준 것으로 기록한다.</summary>
    public string SourceId { get; set; } = sourceId;
}
