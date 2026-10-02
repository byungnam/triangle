namespace Triangle.Core.Combat;

/// <summary>전투 진행 기록. 화면은 이 목록을 순서대로 재생한다.</summary>
public abstract record CombatEvent;

public sealed record TurnStarted(int ActionNumber, string ActorId, long Time) : CombatEvent;

public enum WaitReason
{
    /// <summary>조건이 참인 전술이 없다.</summary>
    NoMatchingTactic,

    /// <summary>선택된 전술의 행동 비용을 낼 수 없다.</summary>
    NotEnoughResource,

    /// <summary>선택된 전술의 행동에 맞는 대상이 없다.</summary>
    NoTarget,
}

/// <summary>행동하지 못하고 턴을 넘겼다. <paramref name="TacticPriority"/>는 선택됐지만 실패한 전술이다.</summary>
public sealed record Waited(string ActorId, WaitReason Reason, int? TacticPriority) : CombatEvent;

public sealed record ActionUsed(string ActorId, string ActionId, int TacticPriority, int ActorHp, int ActorMp) : CombatEvent;

/// <summary>후위인 <paramref name="ProtectedId"/>를 노린 공격을 전위 <paramref name="CoverId"/>가 대신 받았다.</summary>
public sealed record Covered(string ProtectedId, string CoverId) : CombatEvent;

public sealed record Damaged(string TargetId, int Amount, int HpAfter) : CombatEvent;

public sealed record Healed(string TargetId, int Amount, int HpAfter) : CombatEvent;

public sealed record Died(string UnitId) : CombatEvent;

public sealed record MpRestored(string TargetId, int Amount, int MpAfter) : CombatEvent;

/// <param name="Refreshed">이미 걸려 있던 효과의 지속시간을 새로 시작했다.</param>
public sealed record EffectApplied(string TargetId, string EffectId, int Duration, bool Refreshed) : CombatEvent;

/// <summary>턴 시작의 지속 피해·회복. <paramref name="HpChange"/>가 음수면 피해.</summary>
public sealed record EffectTicked(string TargetId, string EffectId, int HpChange, int HpAfter) : CombatEvent;

public sealed record EffectExpired(string TargetId, string EffectId) : CombatEvent;

public sealed record CombatEnded(CombatOutcome Outcome) : CombatEvent;
