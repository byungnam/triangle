using Triangle.Core.Actions;
using Triangle.Core.Effects;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Combat;

/// <summary>
/// 자동 전투를 끝까지 진행한다. 같은 입력과 시드면 항상 같은 결과를 낸다.
/// </summary>
/// <remarks>
/// 행동 순서는 ATB 방식이다. 다음 행동 시각이 가장 이른 유닛이 행동하고,
/// 행동 후 TimeConstant / speed 만큼(스킬의 대기 감소 적용) 뒤로 밀린다.
/// 턴이 오면 전술을 우선순위 순으로 훑어 조건이 참인 첫 전술의 행동을 쓴다.
/// 그 행동의 비용을 낼 수 없거나 대상이 없으면 턴을 잃는다.
/// 스킬은 패시브 보너스(위력, 회복, MP 소모, 대기, 최대 HP/MP, 받는 피해)로만 작용한다.
/// </remarks>
public sealed class CombatSimulator
{
    private readonly CombatRules _rules;
    private readonly CombatCatalog _catalog;
    private readonly Random _random;
    private readonly List<Combatant> _combatants;
    private readonly List<CombatEvent> _events = [];
    private long _nextTieBreak;

    private CombatSimulator(
        IReadOnlyList<CombatantSetup> allies,
        IReadOnlyList<CombatantSetup> enemies,
        CombatCatalog catalog,
        int seed,
        CombatRules rules)
    {
        _rules = rules;
        _catalog = catalog;
        _random = new Random(seed);
        _combatants =
        [
            .. allies.Select(s => new Combatant(s, CombatSide.Ally, rules, new SkillSet(s.Skills, catalog.Skills))),
            .. enemies.Select(s => new Combatant(s, CombatSide.Enemy, rules, new SkillSet(s.Skills, catalog.Skills))),
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
        CombatCatalog catalog,
        int seed,
        CombatRules? rules = null)
    {
        Validate(allies, enemies, catalog);
        return new CombatSimulator(allies, enemies, catalog, seed, rules ?? CombatRules.Default).Run();
    }

    /// <summary>
    /// 잘못된 입력은 전투 규칙으로 처리하지 않고 거부한다. 잠긴 행동(요구 스킬 미달)도 여기서 막는다.
    /// 데이터 로더와 편집 화면이 미리 걸러 주므로, 여기서 걸리면 호출하는 쪽의 버그다.
    /// </summary>
    private static void Validate(
        IReadOnlyList<CombatantSetup> allies,
        IReadOnlyList<CombatantSetup> enemies,
        CombatCatalog catalog)
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
            foreach (var skillId in setup.Skills.Keys.Where(id => !catalog.Skills.ContainsKey(id)))
            {
                throw new ArgumentException($"Combatant '{setup.Id}' has unknown skill '{skillId}'.");
            }

            var skills = new SkillSet(setup.Skills, catalog.Skills);
            foreach (var tactic in setup.Tactics)
            {
                if (!catalog.Actions.TryGetValue(tactic.ActionId, out var action))
                {
                    throw new ArgumentException($"Combatant '{setup.Id}' uses unknown action '{tactic.ActionId}'.");
                }

                if (!action.IsUsableBy(setup.Weapon, skills))
                {
                    throw new ArgumentException($"Combatant '{setup.Id}' cannot use locked action '{tactic.ActionId}'.");
                }

                foreach (var applied in action.Applies.Where(a => !catalog.Effects.ContainsKey(a.EffectId)))
                {
                    throw new ArgumentException($"Action '{action.Id}' applies unknown effect '{applied.EffectId}'.");
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

            // 지속 피해·회복은 턴 시작에 들어간다. 지속 피해로 쓰러지면 행동하지 못한다.
            TickEffects(actor);
            if (!actor.IsAlive)
            {
                if (CheckOutcome() is { } afterTick)
                {
                    return Finish(afterTick, action);
                }

                continue;
            }

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

            EndTurnEffects(actor);
            actor.NextActionTime += ActionDelay(actor, plan?.Action);
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

    /// <summary>행동 뒤 대기. 대기 감소 보너스는 태그 없는 것(전체 속도)과 방금 쓴 행동의 태그 것을 더한다.</summary>
    private long ActionDelay(Combatant c, ActionDefinition? used)
    {
        var speed = Math.Max(1, c.Stats.Speed);
        var baseDelay = _rules.TimeConstant / speed;
        var reduction = SignedReduction(
            c.Skills.Bonus(BonusKind.DelayReductionPercent, used?.BonusTags(c.Weapon))
            + c.EffectModifier(EffectModifierKind.DelayReductionPercent));
        var delay = (long)baseDelay * (100 - reduction) / 100;
        return Math.Max(1, delay);
    }

    /// <summary>감소 보너스를 0 ~ 상한 사이로 자른다.</summary>
    private int Reduction(int percent) => Math.Clamp(percent, 0, _rules.MaxReductionPercent);

    /// <summary>디버프로 음수가 될 수 있는 감소(대기, 받는 피해). 최대 2배(−100%)까지 늘어난다.</summary>
    private int SignedReduction(int percent) => Math.Clamp(percent, -100, _rules.MaxReductionPercent);

    // ── 효과 ───────────────────────────────────────────────

    private void TickEffects(Combatant unit)
    {
        foreach (var effect in unit.Effects.ToList())
        {
            var percent = effect.Definition.TickHpPercent;
            if (percent == 0)
            {
                continue;
            }

            var amount = Math.Max(1, Ratio.ApplyPercent(unit.MaxHp, Math.Abs(percent)));
            var change = percent < 0 ? -Math.Min(unit.Hp, amount) : Math.Min(unit.MaxHp - unit.Hp, amount);
            unit.Hp += change;
            _events.Add(new EffectTicked(unit.Id, effect.Definition.Id, change, unit.Hp));
            if (!unit.IsAlive)
            {
                Die(unit);
                return;
            }
        }
    }

    /// <summary>턴이 끝나면 남은 횟수를 줄이고, 다 된 효과를 지운다.</summary>
    private void EndTurnEffects(Combatant unit)
    {
        foreach (var effect in unit.Effects.ToList())
        {
            if (--effect.Remaining <= 0)
            {
                unit.Effects.Remove(effect);
                _events.Add(new EffectExpired(unit.Id, effect.Definition.Id));
            }
        }
    }

    private void ApplyEffects(ActionDefinition action, Combatant target)
    {
        foreach (var application in action.Applies)
        {
            var definition = _catalog.Effects[application.EffectId];
            var existing = target.Effects.FirstOrDefault(e => e.Definition.Id == definition.Id);
            if (existing is not null)
            {
                existing.Remaining = application.Duration;
            }
            else
            {
                target.Effects.Add(new ActiveEffect(definition, application.Duration));
            }

            _events.Add(new EffectApplied(target.Id, definition.Id, application.Duration, existing is not null));
        }
    }

    /// <summary>쓰러지면 효과는 모두 사라진다 (부활이 없으므로 따로 기록하지 않는다).</summary>
    private void Die(Combatant unit)
    {
        unit.Effects.Clear();
        _events.Add(new Died(unit.Id));
    }

    // ── 전술 ───────────────────────────────────────────────

    private sealed record Plan(Tactic Tactic, ActionDefinition Action, IReadOnlyList<Combatant> Targets);

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

            var action = _catalog.Actions[tactic.ActionId];
            if (!CanPay(actor, action))
            {
                return (null, new Waited(actor.Id, WaitReason.NotEnoughResource, tactic.Priority));
            }

            var targets = Candidates(actor, action);
            if (targets.Count == 0)
            {
                return (null, new Waited(actor.Id, WaitReason.NoTarget, tactic.Priority));
            }

            return (new Plan(tactic, action, targets), null);
        }

        return (null, new Waited(actor.Id, WaitReason.NoMatchingTactic, null));
    }

    private bool ConditionHolds(Combatant actor, int tacticIndex, Tactic tactic)
    {
        var percent = tactic.Value;
        var amount = tactic.Value;
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

            Condition.SelfHpAmountAtLeast => actor.Hp >= amount,
            Condition.SelfHpAmountAtMost => actor.Hp <= amount,
            Condition.SelfMpAmountAtLeast => actor.Mp >= amount,
            Condition.SelfMpAmountAtMost => actor.Mp <= amount,

            Condition.AnyAllyHpAmountAtLeast => allies.Any(a => a.Hp >= amount),
            Condition.AnyAllyHpAmountAtMost => allies.Any(a => a.Hp <= amount),
            Condition.AnyAllyMpAmountAtLeast => allies.Any(a => a.Mp >= amount),
            Condition.AnyAllyMpAmountAtMost => allies.Any(a => a.Mp <= amount),

            // 평균 ≷ 값 ⇔ 합계 ≷ 값 × 인원 (정수 비교)
            Condition.AllyAverageHpAmountAtLeast => allies.Sum(a => (long)a.Hp) >= (long)amount * allies.Count,
            Condition.AllyAverageHpAmountAtMost => allies.Sum(a => (long)a.Hp) <= (long)amount * allies.Count,
            Condition.AllyAverageMpAmountAtLeast => allies.Sum(a => (long)a.Mp) >= (long)amount * allies.Count,
            Condition.AllyAverageMpAmountAtMost => allies.Sum(a => (long)a.Mp) <= (long)amount * allies.Count,

            _ => throw new InvalidOperationException($"Unknown condition {tactic.Condition}."),
        };
    }

    private bool CanPay(Combatant actor, ActionDefinition action) =>
        actor.Mp >= MpCost(actor, action) && (action.HpCost == 0 || actor.Hp > action.HpCost);

    private int MpCost(Combatant actor, ActionDefinition action) =>
        Ratio.ApplyPercent(action.MpCost, 100 - Reduction(actor.Skills.Bonus(BonusKind.MpCostReductionPercent, action.BonusTags(actor.Weapon))));

    // ── 대상 ───────────────────────────────────────────────

    private IEnumerable<Combatant> Living(CombatSide side) =>
        _combatants.Where(c => c.Side == side && c.IsAlive);

    private static CombatSide Opposite(CombatSide side) =>
        side == CombatSide.Ally ? CombatSide.Enemy : CombatSide.Ally;

    /// <summary>줄 제한까지 통과한 후보. 전체 공격이면 이 목록이 곧 대상이다.</summary>
    private List<Combatant> Candidates(Combatant actor, ActionDefinition action)
    {
        if (action.Side == TargetSide.Self)
        {
            return [actor];
        }

        var side = action.Side == TargetSide.Enemy ? Opposite(actor.Side) : actor.Side;
        return Living(side)
            .Where(c => action.Rows switch
            {
                RowRestriction.FrontOnly => c.Row == Row.Front,
                RowRestriction.BackOnly => c.Row == Row.Back,
                _ => true,
            })
            .ToList();
    }

    private Combatant PickOne(IReadOnlyList<Combatant> candidates, ActionDefinition action)
    {
        switch (action.Rule)
        {
            case TargetRule.WithoutEffectFirst when action.Applies.Count > 0:
                var effectId = action.Applies[0].EffectId;
                var without = candidates.Where(c => c.Effects.All(e => e.Definition.Id != effectId)).ToList();
                return PickRandom(without.Count > 0 ? without : candidates);
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
        var action = plan.Action;
        actor.Hp -= action.HpCost;
        actor.Mp -= MpCost(actor, action);
        _events.Add(new ActionUsed(actor.Id, action.Id, plan.Tactic.Priority, actor.Hp, actor.Mp));

        IReadOnlyList<Combatant> targets = action.Scope == TargetScope.All
            ? plan.Targets
            : [ApplyCover(action, PickOne(plan.Targets, action))];

        foreach (var target in targets)
        {
            Apply(actor, action, target);
        }
    }

    private Combatant ApplyCover(ActionDefinition action, Combatant target)
    {
        if (action.Side != TargetSide.Enemy || action.IgnoresCover || target.Row != Row.Back)
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

    private void Apply(Combatant actor, ActionDefinition action, Combatant target)
    {
        switch (action.Effect)
        {
            case ActionEffect.Damage:
            {
                var amount = Math.Min(target.Hp, DamageAmount(actor, action, target));
                target.Hp -= amount;
                _events.Add(new Damaged(target.Id, amount, target.Hp));
                if (!target.IsAlive)
                {
                    Die(target);
                }

                break;
            }
            case ActionEffect.RestoreMp:
            {
                var amount = Math.Min(target.MaxMp - target.Mp, Scaled(action.Power, actor.Stats.Intel));
                target.Mp += amount;
                _events.Add(new MpRestored(target.Id, amount, target.Mp));
                break;
            }
            case ActionEffect.Heal:
            {
                var tags = action.BonusTags(actor.Weapon);
                var bonus = actor.Skills.Bonus(BonusKind.HealPercent, tags) + actor.Skills.Bonus(BonusKind.PowerPercent, tags);
                var heal = Ratio.ApplyPercent(Scaled(action.Power, actor.Stats.Intel), 100 + bonus);
                var amount = Math.Min(target.MaxHp - target.Hp, heal);
                target.Hp += amount;
                _events.Add(new Healed(target.Id, amount, target.Hp));
                break;
            }
        }

        if (target.IsAlive)
        {
            ApplyEffects(action, target);
        }
    }

    /// <summary>
    /// 위력 × 스탯 보정 × (스킬·효과 위력 보너스) → (효과 반영) 방어 경감 → 받는 피해 감소(스킬·효과).
    /// 각 단계에서 사사오입한다.
    /// </summary>
    private int DamageAmount(Combatant actor, ActionDefinition action, Combatant target)
    {
        var (attackStat, defense) = action.DamageType == DamageType.Physical
            ? (actor.Stats.Str, target.Defense)
            : (actor.Stats.Intel, target.MagicDefense);

        var powerBonus = actor.Skills.Bonus(BonusKind.PowerPercent, action.BonusTags(actor.Weapon))
            + actor.EffectModifier(EffectModifierKind.PowerPercent);
        var raw = Ratio.ApplyPercent(Scaled(action.Power, attackStat), Math.Max(0, 100 + powerBonus));
        var effectiveDefense = Ratio.ApplyPercent(defense, Math.Max(0, 100 + target.EffectModifier(EffectModifierKind.DefensePercent)));
        var mitigated = Ratio.DivideRounded((long)raw * 100, 100 + (long)effectiveDefense * _rules.DefenseReductionPercentPerPoint);
        var taken = target.Skills.Bonus(BonusKind.DamageTakenReductionPercent)
            + target.EffectModifier(EffectModifierKind.DamageTakenReductionPercent);
        return Ratio.ApplyPercent(mitigated, 100 - SignedReduction(taken));
    }

    private int Scaled(int power, int stat) =>
        Ratio.ApplyPercent(power, 100 + stat * _rules.PowerScalingPercentPerPoint);
}
