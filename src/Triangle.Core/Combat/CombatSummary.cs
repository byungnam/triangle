using Triangle.Core.Tactics;

namespace Triangle.Core.Combat;

/// <summary>전술 한 줄이 쓰인 횟수와, 조건이 참이라 골랐지만 쓰지 못한 횟수(이유별).</summary>
public sealed record TacticUsage(Tactic Tactic, int Used, IReadOnlyDictionary<WaitReason, int> Blocked)
{
    public int BlockedTotal => Blocked.Values.Sum();
}

/// <summary>
/// 행동하지 못하고 넘긴 차례. <paramref name="Turn"/>은 그 유닛의 몇 번째 차례인가(1부터).
/// <paramref name="TacticPriority"/>는 골랐지만 쓰지 못한 전술이다(맞는 전술이 없었거나 기절이면 null).
/// </summary>
public sealed record LostTurn(int Turn, WaitReason Reason, int? TacticPriority);

/// <summary>
/// 유닛 하나의 전투 결과. 소환 유닛은 따로 두지 않고 소환자의 <see cref="SummonDamage"/>·<see cref="SummonHealing"/>에 더한다.
/// </summary>
/// <param name="Turns">차례가 온 횟수 (행동 + 잃은 차례).</param>
/// <param name="Actions">행동한 횟수 (영창 중인 차례도 센다).</param>
/// <param name="DamageDealt">준 HP 피해. 건 지속 피해(<paramref name="DotDamage"/>)를 포함한다. 보호막이 막은 양은 넣지 않는다.</param>
/// <param name="Healing">회복한 HP. 건 지속 회복을 포함한다.</param>
/// <param name="Shielding">준 보호막.</param>
/// <param name="MpRestored">채워 준 MP (자기 자신 포함).</param>
/// <param name="DamageTaken">받은 HP 피해 (지속 피해 포함).</param>
/// <param name="DownedAt">쓰러진 전투 차례 번호(<see cref="TurnStarted.ActionNumber"/>). 끝까지 섰으면 null.</param>
/// <param name="Tactics">전술 순서대로의 사용 횟수.</param>
public sealed record UnitSummary(
    string Id,
    CombatSide Side,
    int Turns,
    int Actions,
    int DamageDealt,
    int DotDamage,
    int Healing,
    int Shielding,
    int MpRestored,
    int DamageTaken,
    int SummonDamage,
    int SummonHealing,
    int? DownedAt,
    IReadOnlyList<TacticUsage> Tactics,
    IReadOnlyList<LostTurn> LostTurns)
{
    /// <summary>이 유닛의 행동이 면역에 막힌 횟수 (대상 하나당 한 번, 피해와 디버프 모두, 2026-10-17).</summary>
    public int Nullified { get; init; }

    /// <summary>이 유닛의 회복이 치유 봉쇄에 막힌 횟수 (2026-10-17).</summary>
    public int HealsBlocked { get; init; }
}

/// <summary>전투 기록(이벤트 목록)에서 유닛별 결과와 전술별 사용 횟수를 센다. 전투 기록 화면의 요약이 쓴다.</summary>
public static class CombatSummary
{
    /// <summary>
    /// 소환 유닛을 뺀 모든 유닛의 결과를 <see cref="CombatResult.Combatants"/> 순서로 돌려준다.
    /// 피해·회복·보호막·MP는 바로 앞의 <see cref="ActionUsed"/>를 한 유닛이 준 것으로 보고,
    /// 지속 피해·회복은 효과를 건 유닛(<see cref="EffectTicked.SourceId"/>)이 준 것으로 본다.
    /// 우선순위가 같은 전술이 둘 이상이면 앞에 놓인 전술에 센다(파티 전술은 우선순위가 겹치지 않는다).
    /// </summary>
    public static IReadOnlyList<UnitSummary> From(CombatResult result)
    {
        var tallies = result.Combatants.Where(c => !c.IsSummon).ToDictionary(c => c.Id, c => new Tally());
        var owners = result.Combatants.Where(c => c.IsSummon).ToDictionary(c => c.Id, c => c.OwnerId!);
        string? actor = null;
        var actionNumber = 0;

        Tally? Of(string id) => tallies.GetValueOrDefault(id);

        // 소환 유닛이 준 피해·회복은 소환자에게 따로 센다.
        void CreditDamage(string? sourceId, int amount, bool overTime)
        {
            if (sourceId is null)
            {
                return;
            }

            if (Of(sourceId) is { } source)
            {
                source.DamageDealt += amount;
                source.DotDamage += overTime ? amount : 0;
            }
            else if (owners.TryGetValue(sourceId, out var ownerId) && Of(ownerId) is { } owner)
            {
                owner.SummonDamage += amount;
            }
        }

        void CreditHealing(string? sourceId, int amount)
        {
            if (sourceId is null)
            {
                return;
            }

            if (Of(sourceId) is { } source)
            {
                source.Healing += amount;
            }
            else if (owners.TryGetValue(sourceId, out var ownerId) && Of(ownerId) is { } owner)
            {
                owner.SummonHealing += amount;
            }
        }

        foreach (var e in result.Events)
        {
            switch (e)
            {
                case TurnStarted t:
                    actor = null;
                    actionNumber = t.ActionNumber;
                    if (Of(t.ActorId) is { } turnTaker)
                    {
                        turnTaker.Turns++;
                    }

                    break;
                case ActionUsed a:
                    actor = a.ActorId;
                    if (Of(a.ActorId) is { } user)
                    {
                        user.Actions++;
                        user.Usage(a.TacticPriority).Used++;
                    }

                    break;
                case Waited w:
                    actor = null;
                    if (Of(w.ActorId) is { } waiter)
                    {
                        waiter.LostTurns.Add(new LostTurn(waiter.Turns, w.Reason, w.TacticPriority));
                        if (w.TacticPriority is { } priority)
                        {
                            var blocked = waiter.Usage(priority).Blocked;
                            blocked[w.Reason] = blocked.GetValueOrDefault(w.Reason) + 1;
                        }
                    }

                    break;
                case Damaged d:
                    CreditDamage(actor, d.Amount, overTime: false);
                    if (Of(d.TargetId) is { } hit)
                    {
                        hit.DamageTaken += d.Amount;
                    }

                    break;
                case Healed h:
                    CreditHealing(actor, h.Amount);
                    break;
                case ShieldGained g when actor is not null && Of(actor) is { } shielder:
                    shielder.Shielding += g.Amount;
                    break;
                case MpRestored m when actor is not null && Of(actor) is { } restorer:
                    restorer.MpRestored += m.Amount;
                    break;
                case EffectTicked { HpChange: < 0 } tick:
                    CreditDamage(tick.SourceId, -tick.HpChange, overTime: true);
                    if (Of(tick.TargetId) is { } ticked)
                    {
                        ticked.DamageTaken -= tick.HpChange;
                    }

                    break;
                case EffectTicked { HpChange: > 0 } tick:
                    CreditHealing(tick.SourceId, tick.HpChange);
                    break;
                case Immune when actor is not null && Of(actor) is { } nullified:
                    nullified.Nullified++;
                    break;
                case HealBlocked when actor is not null && Of(actor) is { } healer:
                    healer.HealsBlocked++;
                    break;
                case Died d when Of(d.UnitId) is { } downed:
                    downed.DownedAt = actionNumber;
                    break;
            }
        }

        return result.Combatants
            .Where(c => !c.IsSummon)
            .Select(c => tallies[c.Id].ToSummary(c))
            .ToList();
    }

    private sealed class Tally
    {
        private readonly Dictionary<int, Use> _usage = [];

        public int Turns;
        public int Actions;
        public int DamageDealt;
        public int DotDamage;
        public int Healing;
        public int Shielding;
        public int MpRestored;
        public int DamageTaken;
        public int SummonDamage;
        public int SummonHealing;
        public int? DownedAt;
        public int Nullified;
        public int HealsBlocked;
        public readonly List<LostTurn> LostTurns = [];

        public Use Usage(int priority)
        {
            if (!_usage.TryGetValue(priority, out var use))
            {
                use = new Use();
                _usage[priority] = use;
            }

            return use;
        }

        public UnitSummary ToSummary(Combatant c)
        {
            var counted = new HashSet<int>();
            var tactics = c.Tactics
                .Select(t => counted.Add(t.Priority) && _usage.TryGetValue(t.Priority, out var use)
                    ? new TacticUsage(t, use.Used, use.Blocked)
                    : new TacticUsage(t, 0, new Dictionary<WaitReason, int>()))
                .ToList();

            return new UnitSummary(
                c.Id, c.Side, Turns, Actions, DamageDealt, DotDamage, Healing, Shielding, MpRestored, DamageTaken,
                SummonDamage, SummonHealing, DownedAt, tactics, LostTurns)
            {
                Nullified = Nullified,
                HealsBlocked = HealsBlocked,
            };
        }
    }

    private sealed class Use
    {
        public int Used;
        public readonly Dictionary<WaitReason, int> Blocked = [];
    }
}
