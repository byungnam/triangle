namespace Triangle.Core.Combat;

/// <summary>전투 진행 기록. 화면은 이 목록을 순서대로 재생한다.</summary>
public abstract record CombatEvent;

public sealed record TurnStarted(int ActionNumber, string ActorId, long Time) : CombatEvent;

public enum WaitReason
{
    /// <summary>조건이 참인 전술이 없다.</summary>
    NoMatchingTactic,

    /// <summary>선택된 전술의 스킬 비용을 낼 수 없다.</summary>
    NotEnoughResource,

    /// <summary>선택된 전술의 스킬에 맞는 대상이 없다.</summary>
    NoTarget,
}

/// <summary>행동하지 못하고 턴을 넘겼다. <paramref name="TacticPriority"/>는 선택됐지만 실패한 전술이다.</summary>
public sealed record Waited(string ActorId, WaitReason Reason, int? TacticPriority) : CombatEvent;

public sealed record SkillUsed(string ActorId, string SkillId, int TacticPriority, int ActorHp, int ActorMp) : CombatEvent;

/// <summary>후위인 <paramref name="ProtectedId"/>를 노린 공격을 전위 <paramref name="CoverId"/>가 대신 받았다.</summary>
public sealed record Covered(string ProtectedId, string CoverId) : CombatEvent;

public sealed record Damaged(string TargetId, int Amount, int HpAfter) : CombatEvent;

public sealed record Healed(string TargetId, int Amount, int HpAfter) : CombatEvent;

public sealed record Died(string UnitId) : CombatEvent;

public sealed record CombatEnded(CombatOutcome Outcome) : CombatEvent;
