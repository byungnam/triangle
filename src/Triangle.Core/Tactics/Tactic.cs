namespace Triangle.Core.Tactics;

/// <summary>
/// "조건이 참이면 행동을 쓴다" 한 줄. 낮은 <see cref="Priority"/>가 먼저 평가된다.
/// 대상은 전술이 아니라 행동이 정한다.
/// </summary>
public sealed record Tactic(int Priority, Condition Condition, int Value, string ActionId);
