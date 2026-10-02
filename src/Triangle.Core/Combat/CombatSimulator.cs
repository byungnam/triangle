using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Combat;

/// <summary>
/// 자동 전투를 끝까지 진행한다. 같은 입력과 시드면 항상 같은 결과를 낸다.
/// </summary>
/// <remarks>
/// 행동 순서는 ATB 방식이다. 다음 행동 시각이 가장 이른 유닛이 행동하고,
/// 행동 후 TimeConstant / (직업 계수 × speed) 만큼 뒤로 밀린다.
/// 턴이 오면 전술을 우선순위 순으로 훑어 조건이 참인 첫 전술의 스킬을 쓴다.
/// 그 스킬의 비용을 낼 수 없거나 대상이 없으면 턴을 잃는다.
/// </remarks>
public sealed class CombatSimulator
{
    private readonly CombatRules _rules;
    private readonly IReadOnlyDictionary<string, SkillDefinition> _skills;
    private readonly Random _random;
    private readonly List<Combatant> _combatants;
    private readonly List<CombatEvent> _events = [];
    private long _nextTieBreak;

    private CombatSimulator(
        IReadOnlyList<CombatantSetup> allies,
        IReadOnlyList<CombatantSetup> enemies,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        int seed,
        CombatRules rules)
    {
        _rules = rules;
        _skills = skills;
        _random = new Random(seed);
        _combatants =
        [
            .. allies.Select(s => new Combatant(s, CombatSide.Ally, rules)),
            .. enemies.Select(s => new Combatant(s, CombatSide.Enemy, rules)),
        ];

        // 모두 시각 0에서 시작하므로 첫 행동 순서는 무작위로 정한다.
        foreach (var c in _combatants.OrderBy(_ => _random.Next()))
        {
            c.TieBreak = _nextTieBreak++;
        }
    }

    public static CombatResult Run(
        IReadOnlyList<CombatantSetup> allies,
        IReadOnlyList<CombatantSetup> enemies,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        int seed,
        CombatRules? rules = null)
    {
        Validate(allies, enemies, skills);
        return new CombatSimulator(allies, enemies, skills, seed, rules ?? CombatRules.Default).Run();
    }

    private static void Validate(
        IReadOnlyList<CombatantSetup> allies,
        IReadOnlyList<CombatantSetup> enemies,
        IReadOnlyDictionary<string, SkillDefinition> skills)
    {
        if (allies.Count == 0 || enemies.Count == 0)
        {
            throw new ArgumentException("Both sides need at least one combatant.");
        }

        var duplicate = allies.Concat(enemies).GroupBy(s => s.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate combatant id '{duplicate.Key}'.");
        }

        foreach (var setup in allies.Concat(enemies))
        {
            foreach (var tactic in setup.Tactics)
            {
                if (!skills.ContainsKey(tactic.SkillId))
                {
                    throw new ArgumentException($"Combatant '{setup.Id}' uses unknown skill '{tactic.SkillId}'.");
                }
            }
        }
    }

    private CombatResult Run()
    {
        for (var action = 1; action <= _rules.MaxActions; action++)
        {
            var actor = _combatants
                .Where(c => c.IsAlive)
                .MinBy(c => (c.NextActionTime, c.TieBreak))!;

            actor.TurnCount++;
            _events.Add(new TurnStarted(action, actor.Id, actor.NextActionTime));

            var (plan, waited) = ChooseAction(actor);
            if (plan is not null)
            {
                Execute(actor, plan);
            }
            else
            {
                _events.Add(waited!);
            }

            if (CheckOutcome() is { } outcome)
            {
                return Finish(outcome, action);
            }

            actor.NextActionTime += ActionDelay(actor);
            actor.TieBreak = _nextTieBreak++;
        }

        return Finish(_rules.ActionLimitOutcome, _rules.MaxActions);
    }

    private CombatResult Finish(CombatOutcome outcome, int actionCount)
    {
        _events.Add(new CombatEnded(outcome));
        return new CombatResult(outcome, actionCount, _events, _combatants);
    }

    private CombatOutcome? CheckOutcome()
    {
        if (!_combatants.Any(c => c.Side == CombatSide.Enemy && c.IsAlive))
        {
            return CombatOutcome.Victory;
        }

        if (!_combatants.Any(c => c.Side == CombatSide.Ally && c.IsAlive))
        {
            return CombatOutcome.Defeat;
        }

        return null;
    }

    private long ActionDelay(Combatant c)
    {
        var speed = Math.Max(1, c.Stats.Speed);
        var speedPercent = Math.Max(1, c.Class.SpeedPercent);
        var delay = (long)_rules.TimeConstant * 100 / ((long)speedPercent * speed);
        return Math.Max(1, delay);
    }

    // ── 전술 ───────────────────────────────────────────────

    private sealed record Plan(Tactic Tactic, SkillDefinition Skill, IReadOnlyList<Combatant> Targets);

    /// <summary>
    /// 조건이 참인 첫 전술을 고른다. 그 전술의 비용을 낼 수 없거나 대상이 없으면
    /// 다음 전술로 넘어가지 않고 턴을 잃는다 (레거시 동작).
    /// </summary>
    private (Plan? Plan, Waited? Waited) ChooseAction(Combatant actor)
    {
        for (var i = 0; i < actor.Tactics.Count; i++)
        {
            var tactic = actor.Tactics[i];
            if (!ConditionHolds(actor, i, tactic))
            {
                continue;
            }

            // 레거시와 같이 조건이 참으로 판정된 시점에 센다. 이후 턴을 잃어도 1회로 친다.
            actor.AddUse(i);

            var skill = _skills[tactic.SkillId];
            if (!CanPay(actor, skill))
            {
                return (null, new Waited(actor.Id, WaitReason.NotEnoughResource, tactic.Priority));
            }

            var targets = Candidates(actor, skill);
            if (targets.Count == 0)
            {
                return (null, new Waited(actor.Id, WaitReason.NoTarget, tactic.Priority));
            }

            return (new Plan(tactic, skill, targets), null);
        }

        return (null, new Waited(actor.Id, WaitReason.NoMatchingTactic, null));
    }

    private bool ConditionHolds(Combatant actor, int tacticIndex, Tactic tactic)
    {
        var percent = tactic.Value;
        var allies = Living(actor.Side).ToList();

        int Hp(Combatant c) => Ratio.CompareToPercent(c.Hp, c.MaxHp, percent);
        int Mp(Combatant c) => Ratio.CompareToPercent(c.Mp, c.MaxMp, percent);
        int AverageHp() => Ratio.CompareAverageToPercent(allies.Select(a => (a.Hp, a.MaxHp)).ToList(), percent);
        int AverageMp() => Ratio.CompareAverageToPercent(allies.Select(a => (a.Mp, a.MaxMp)).ToList(), percent);

        return tactic.Condition switch
        {
            Condition.Always => true,

            Condition.SelfHpAtLeast => Hp(actor) >= 0,
            Condition.SelfHpAtMost => Hp(actor) <= 0,
            Condition.SelfMpAtLeast => Mp(actor) >= 0,
            Condition.SelfMpAtMost => Mp(actor) <= 0,

            Condition.AnyAllyHpAtLeast => allies.Any(a => Hp(a) >= 0),
            Condition.AnyAllyHpAtMost => allies.Any(a => Hp(a) <= 0),
            Condition.AnyAllyMpAtLeast => allies.Any(a => Mp(a) >= 0),
            Condition.AnyAllyMpAtMost => allies.Any(a => Mp(a) <= 0),

            Condition.AllyAverageHpAtLeast => AverageHp() >= 0,
            Condition.AllyAverageHpAtMost => AverageHp() <= 0,
            Condition.AllyAverageMpAtLeast => AverageMp() >= 0,
            Condition.AllyAverageMpAtMost => AverageMp() <= 0,

            Condition.MaxUses => actor.GetUses(tacticIndex) < tactic.Value,
            Condition.FromTurn => actor.TurnCount >= tactic.Value,
            Condition.UntilTurn => actor.TurnCount <= tactic.Value,
            Condition.OnTurn => actor.TurnCount == tactic.Value,
            Condition.EveryNthTurn => tactic.Value > 0 && actor.TurnCount % tactic.Value == 0,

            _ => throw new InvalidOperationException($"Unknown condition {tactic.Condition}."),
        };
    }

    private static bool CanPay(Combatant actor, SkillDefinition skill) =>
        actor.Mp >= skill.MpCost && (skill.HpCost == 0 || actor.Hp > skill.HpCost);

    // ── 대상 ───────────────────────────────────────────────

    private IEnumerable<Combatant> Living(CombatSide side) =>
        _combatants.Where(c => c.Side == side && c.IsAlive);

    private static CombatSide Opposite(CombatSide side) =>
        side == CombatSide.Ally ? CombatSide.Enemy : CombatSide.Ally;

    /// <summary>줄 제한까지 통과한 후보. 전체 공격이면 이 목록이 곧 대상이다.</summary>
    private List<Combatant> Candidates(Combatant actor, SkillDefinition skill)
    {
        if (skill.Side == TargetSide.Self)
        {
            return [actor];
        }

        var side = skill.Side == TargetSide.Enemy ? Opposite(actor.Side) : actor.Side;
        return Living(side)
            .Where(c => skill.Rows switch
            {
                RowRestriction.FrontOnly => c.Row == Row.Front,
                RowRestriction.BackOnly => c.Row == Row.Back,
                _ => true,
            })
            .ToList();
    }

    private Combatant PickOne(IReadOnlyList<Combatant> candidates, TargetRule rule)
    {
        switch (rule)
        {
            case TargetRule.FrontFirst:
                return PickRandom(PreferRow(candidates, Row.Front));
            case TargetRule.BackFirst:
                return PickRandom(PreferRow(candidates, Row.Back));
            case TargetRule.LowestHpRatio:
                // 동률이면 앞쪽(입력 순서)을 고른다.
                return candidates.Aggregate((best, c) =>
                    Ratio.Compare(c.Hp, c.MaxHp, best.Hp, best.MaxHp) < 0 ? c : best);
            default:
                return PickRandom(candidates);
        }
    }

    private static IReadOnlyList<Combatant> PreferRow(IReadOnlyList<Combatant> candidates, Row row)
    {
        var inRow = candidates.Where(c => c.Row == row).ToList();
        return inRow.Count > 0 ? inRow : candidates;
    }

    private Combatant PickRandom(IReadOnlyList<Combatant> list) => list[_random.Next(list.Count)];

    // ── 실행 ───────────────────────────────────────────────

    private void Execute(Combatant actor, Plan plan)
    {
        var skill = plan.Skill;
        actor.Hp -= skill.HpCost;
        actor.Mp -= skill.MpCost;
        _events.Add(new SkillUsed(actor.Id, skill.Id, plan.Tactic.Priority, actor.Hp, actor.Mp));

        IReadOnlyList<Combatant> targets = skill.Scope == TargetScope.All
            ? plan.Targets
            : [ApplyCover(skill, PickOne(plan.Targets, skill.Rule))];

        foreach (var target in targets)
        {
            Apply(actor, skill, target);
        }
    }

    private Combatant ApplyCover(SkillDefinition skill, Combatant target)
    {
        if (skill.Side != TargetSide.Enemy || skill.IgnoresCover || target.Row != Row.Back)
        {
            return target;
        }

        var fronts = Living(target.Side).Where(c => c.Row == Row.Front).ToList();
        if (fronts.Count == 0)
        {
            return target;
        }

        var cover = PickRandom(fronts);
        _events.Add(new Covered(target.Id, cover.Id));
        return cover;
    }

    private void Apply(Combatant actor, SkillDefinition skill, Combatant target)
    {
        switch (skill.Effect)
        {
            case SkillEffect.Damage:
            {
                var amount = Math.Min(target.Hp, DamageAmount(actor, skill, target));
                target.Hp -= amount;
                _events.Add(new Damaged(target.Id, amount, target.Hp));
                if (!target.IsAlive)
                {
                    _events.Add(new Died(target.Id));
                }

                break;
            }
            case SkillEffect.Heal:
            {
                var amount = Math.Min(target.MaxHp - target.Hp, Scaled(skill.Power, actor.Stats.Intel));
                target.Hp += amount;
                _events.Add(new Healed(target.Id, amount, target.Hp));
                break;
            }
        }
    }

    private int DamageAmount(Combatant actor, SkillDefinition skill, Combatant target)
    {
        var (attackStat, defense) = skill.DamageType == DamageType.Physical
            ? (actor.Stats.Str, target.Defense)
            : (actor.Stats.Intel, target.MagicDefense);

        var raw = Scaled(skill.Power, attackStat);
        return Ratio.DivideRounded((long)raw * 100, 100 + (long)defense * _rules.DefenseReductionPercentPerPoint);
    }

    private int Scaled(int power, int stat) =>
        Ratio.ApplyPercent(power, 100 + stat * _rules.PowerScalingPercentPerPoint);
}
