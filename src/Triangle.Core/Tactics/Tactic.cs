using System.Text.Json.Serialization;

namespace Triangle.Core.Tactics;

/// <summary>전술 조건 하나와 그 값.</summary>
public sealed record TacticCondition(Condition Condition, int Value);

/// <summary>
/// "조건이 모두 참이면 행동을 쓴다" 한 줄. 낮은 <see cref="Priority"/>가 먼저 평가된다.
/// 대상은 전술이 아니라 행동이 정한다.
/// </summary>
/// <remarks>
/// 첫 조건은 <see cref="Condition"/>·<see cref="Value"/>이고, 더 거는 조건은 <see cref="And"/>에 둔다(2026-10-15).
/// 모두 참이어야 하고, 합쳐 <see cref="MaxConditions"/>개까지다. "또는"은 같은 행동을 쓰는 줄을 하나 더 둔다.
/// JSON에 <c>and</c>가 없으면 조건 하나짜리다. 그래서 이전 세이브와 데이터가 그대로 읽힌다.
/// </remarks>
public sealed record Tactic(int Priority, Condition Condition, int Value, string ActionId)
{
    public const int MaxConditions = 3;

    /// <summary>첫 조건에 더해 모두 참이어야 하는 조건. 없으면 null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TacticCondition>? And { get; init; }

    /// <summary>첫 조건부터 순서대로 모든 조건.</summary>
    [JsonIgnore]
    public IReadOnlyList<TacticCondition> Conditions => [new(Condition, Value), .. And ?? []];

    /// <param name="conditions">1개 이상. 첫 조건이 <see cref="Condition"/>·<see cref="Value"/>가 된다.</param>
    public static Tactic Create(int priority, IReadOnlyList<TacticCondition> conditions, string actionId) =>
        new(priority, conditions[0].Condition, conditions[0].Value, actionId)
        {
            And = conditions.Count > 1 ? conditions.Skip(1).ToList() : null,
        };
}
