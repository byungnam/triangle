using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Effects;
using Triangle.Core.Masteries;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Combat;

public class EffectTests
{
    private static EffectDefinition Effect(string id, int tick = 0, EffectKind kind = EffectKind.Debuff, params EffectModifier[] modifiers) =>
        new() { Id = id, Name = id, Kind = kind, TickHpPercent = tick, Modifiers = modifiers };

    private static readonly Dictionary<string, EffectDefinition> Effects = new[]
    {
        Effect("poison", tick: -10),
        Effect("regen", tick: 10, kind: EffectKind.Buff),
        Effect("might", kind: EffectKind.Buff, modifiers: new EffectModifier(EffectModifierKind.PowerPercent, 50)),
        Effect("exposed", modifiers: new EffectModifier(EffectModifierKind.DefensePercent, -100)),
        Effect("vulnerable", modifiers: new EffectModifier(EffectModifierKind.DamageTakenReductionPercent, -50)),
        Effect("haste", kind: EffectKind.Buff, modifiers: new EffectModifier(EffectModifierKind.DelayReductionPercent, 50)),
        Effect("slow", modifiers: new EffectModifier(EffectModifierKind.DelayReductionPercent, -100)),
    }.ToDictionary(e => e.Id);

    private static ActionDefinition Apply(string id, string effectId, int duration, TargetSide side = TargetSide.Enemy,
        TargetRule rule = TargetRule.Random, ActionEffect effect = ActionEffect.None, int power = 0) => new()
    {
        Id = id, Name = id, Effect = effect, Power = power, Side = side, Rule = rule,
        Applies = [new EffectApplication(effectId, duration)],
    };

    private static readonly ActionDefinition Punch = new() { Id = "punch", Name = "punch", Power = 10 };
    private static readonly ActionDefinition Focus = new()
    {
        Id = "focus", Name = "focus", Effect = ActionEffect.RestoreMp, Power = 1000, Side = TargetSide.Self, MpCost = 0,
    };
    private static readonly ActionDefinition Drain = new() { Id = "drain", Name = "drain", Power = 1, MpCost = 50 };

    private static readonly CombatCatalog Catalog = new(
        new[]
        {
            Punch, Focus, Drain,
            Apply("poison_hit", "poison", 3),
            Apply("regen_self", "regen", 3, TargetSide.Self),
            Apply("might_self", "might", 2, TargetSide.Self),
            Apply("expose", "exposed", 5),
            Apply("make_vulnerable", "vulnerable", 5),
            Apply("haste_self", "haste", 100, TargetSide.Self),
            Apply("slow_enemy", "slow", 100),
            Apply("might_ally", "might", 9, TargetSide.Ally, TargetRule.WithoutEffectFirst),
        }.ToDictionary(a => a.Id),
        new Dictionary<string, SkillDefinition>()) { Effects = Effects };

    private static CombatantSetup Unit(string id, int str = 10, int vital = 20, int speed = 10, Row row = Row.Front, params Tactic[] tactics) =>
        new(id, id, new Stats(str, 10, vital, 10, speed), row, null, null, SkillSet.NoSkills, tactics);

    private static Tactic Use(string actionId, int priority = 1, Condition condition = Condition.Always, int value = 0) =>
        new(priority, condition, value, actionId);

    private static CombatResult Run(CombatantSetup[] allies, CombatantSetup[] enemies, int maxActions = 40) =>
        CombatSimulator.Run(allies, enemies, Catalog, seed: 1, new CombatRules { MaxActions = maxActions });

    [Fact]
    public void Damage_over_time_ticks_once_per_target_turn_for_its_duration_then_expires()
    {
        // a는 첫 턴에만 독을 건다 (MaxUses 1). 독은 e의 턴 3번 동안 매번 최대 HP의 10%.
        var result = Run(
            [Unit("a", tactics: Use("poison_hit", condition: Condition.MaxUses, value: 1))],
            [Unit("e", vital: 100)]);

        var ticks = result.Events.OfType<EffectTicked>().Where(t => t.TargetId == "e").ToList();
        Assert.Equal(3, ticks.Count);
        Assert.All(ticks, t => Assert.Equal(-280, t.HpChange));
        Assert.Single(result.Events.OfType<EffectExpired>(), x => x is { TargetId: "e", EffectId: "poison" });

        // 틱은 e의 턴 시작(TurnStarted 바로 다음)에 들어간다.
        var events = result.Events.ToList();
        foreach (var tick in ticks)
        {
            var index = events.IndexOf(tick);
            Assert.IsType<TurnStarted>(events[index - 1]);
            Assert.Equal("e", ((TurnStarted)events[index - 1]).ActorId);
        }
    }

    [Fact]
    public void Reapplying_refreshes_duration_without_stacking()
    {
        var result = Run([Unit("a", tactics: Use("poison_hit"))], [Unit("e", vital: 1000)], maxActions: 6);

        var applied = result.Events.OfType<EffectApplied>().ToList();
        Assert.False(applied[0].Refreshed);
        Assert.All(applied.Skip(1), a => Assert.True(a.Refreshed));
        // 중첩되지 않으므로 e의 턴마다 틱은 한 번뿐이다.
        var turnsOfE = result.Events.OfType<TurnStarted>().Count(t => t.ActorId == "e");
        Assert.True(result.Events.OfType<EffectTicked>().Count() <= turnsOfE);
    }

    [Fact]
    public void Healing_over_time_is_capped_at_max_hp()
    {
        var result = Run([Unit("a", tactics: Use("regen_self"))], [Unit("e")], maxActions: 6);

        Assert.All(result.Events.OfType<EffectTicked>(), t => Assert.Equal(0, t.HpChange)); // 처음부터 가득 차 있다
    }

    [Fact]
    public void Dying_to_damage_over_time_skips_the_turn_and_can_end_the_battle()
    {
        // e의 HP는 28. 독 10%면 틱마다 3 (최소 1), 직접 공격은 없다 → 대신 큰 독으로 시험한다.
        var deadly = Catalog with
        {
            Effects = new Dictionary<string, EffectDefinition>(Effects) { ["poison"] = Effect("poison", tick: -100) },
        };

        var result = CombatSimulator.Run(
            [Unit("a", tactics: Use("poison_hit"))], [Unit("e", vital: 1)], deadly, seed: 1, new CombatRules { MaxActions = 10 });

        Assert.Equal(CombatOutcome.Victory, result.Outcome);
        var events = result.Events.ToList();
        var died = events.FindIndex(e => e is Died { UnitId: "e" });
        Assert.IsType<EffectTicked>(events[died - 1]);
        Assert.DoesNotContain(events, e => e is ActionUsed { ActorId: "e" });
    }

    [Fact]
    public void Power_buff_raises_damage_only_while_active()
    {
        // 힘 버프(2턴) 뒤 공격: 15 × 1.5 = 22.5 → 23. 버프가 끝나면 15.
        var result = Run(
            [Unit("a", tactics: [Use("might_self", 1, Condition.OnTurn, 1), Use("punch", 2)])],
            [Unit("e", str: 0, vital: 100)], maxActions: 12);

        var hits = result.Events.OfType<Damaged>().Where(d => d.TargetId == "e").Select(d => d.Amount).ToList();
        Assert.Equal(23, hits[0]);
        Assert.Equal(15, hits[^1]);
    }

    [Fact]
    public void Defense_and_damage_taken_debuffs_increase_damage()
    {
        // 방어 25 → 15 / 1.5 = 10. 방어 -100% → 15. 받는 피해 -50%(취약) → 15 × 1.5 = 22.5 → 23 (방어 25면 10 × 1.5 = 15).
        var exposed = Run([Unit("a", tactics: [Use("expose", 1, Condition.OnTurn, 1), Use("punch", 2)])], [Unit("e", str: 25, vital: 100)], 6);
        var vulnerable = Run([Unit("a", tactics: [Use("make_vulnerable", 1, Condition.OnTurn, 1), Use("punch", 2)])], [Unit("e", str: 25, vital: 100)], 6);

        Assert.Equal(15, exposed.Events.OfType<Damaged>().First().Amount);
        Assert.Equal(15, vulnerable.Events.OfType<Damaged>().First().Amount);
    }

    [Fact]
    public void Haste_and_slow_change_turn_frequency()
    {
        var hasted = Run([Unit("a", tactics: Use("haste_self", condition: Condition.OnTurn, value: 1))], [Unit("e")], maxActions: 31);
        var turns = hasted.Events.OfType<TurnStarted>().GroupBy(t => t.ActorId).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(turns["a"] * 10 >= turns["e"] * 17, $"haste a={turns["a"]} e={turns["e"]}");

        var slowed = Run([Unit("a", tactics: Use("slow_enemy", condition: Condition.OnTurn, value: 1))], [Unit("e")], maxActions: 31);
        turns = slowed.Events.OfType<TurnStarted>().GroupBy(t => t.ActorId).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(turns["a"] * 10 >= turns["e"] * 17, $"slow a={turns["a"]} e={turns["e"]}");
    }

    [Fact]
    public void Restore_mp_is_capped_at_max_mp()
    {
        var result = Run([Unit("a", tactics: [Use("drain", 1, Condition.SelfMpAtLeast, 50), Use("focus", 2)])], [Unit("e", vital: 1000)], 10);

        // MP 100 → 소모 50 두 번 → 0 → 정신 집중으로 최대치(100)까지만 회복.
        var events = result.Events.ToList();
        var restore = events.OfType<MpRestored>().First();
        var before = ((ActionUsed)events[events.IndexOf(restore) - 1]).ActorMp;
        Assert.Equal(100, restore.MpAfter);
        Assert.Equal(100 - before, restore.Amount);
    }

    [Fact]
    public void Without_effect_first_spreads_buffs_across_allies()
    {
        var result = Run(
            [Unit("a", tactics: Use("might_ally")), Unit("b"), Unit("c")],
            [Unit("e", vital: 1000)], maxActions: 12);

        var targets = result.Events.OfType<EffectApplied>().Take(3).Select(a => a.TargetId).ToList();
        Assert.Equal(3, targets.Distinct().Count());
    }

    [Fact]
    public void Effects_vanish_when_the_unit_dies()
    {
        var result = Run(
            [Unit("a", str: 100, tactics: [Use("poison_hit", 1, Condition.OnTurn, 1), Use("punch", 2)])],
            [Unit("e", vital: 1, str: 0)]);

        var events = result.Events.ToList();
        var died = events.FindIndex(e => e is Died { UnitId: "e" });
        Assert.DoesNotContain(events.Skip(died), e => e is EffectTicked { TargetId: "e" } or EffectExpired { TargetId: "e" });
    }

    [Fact]
    public void Damage_over_time_counts_as_damage_taken_for_armor_mastery()
    {
        CombatantSetup Armored(string id, params Tactic[] tactics) =>
            new(id, id, new Stats(10, 10, 100, 10, 10), Row.Front, null, "plate", SkillSet.NoSkills, tactics);

        var result = CombatSimulator.Run([Armored("a")], [Unit("e", tactics: Use("poison_hit"))], Catalog, seed: 1,
            new CombatRules { MaxActions = 10 });

        var ticked = -result.Events.OfType<EffectTicked>().Where(t => t.TargetId == "a").Sum(t => t.HpChange);
        var gain = Assert.Single(MasteryGain.ForAllies(result));
        Assert.True(ticked > 0);
        Assert.Equal(ticked / MasteryRules.Default.DamageTakenPerXp + MasteryRules.Default.ResultXp(result.Outcome), gain.Armor["plate"]);
    }

    [Fact]
    public void Loader_rejects_unknown_effects_and_bad_durations()
    {
        var errors = Assert.Throws<GameDataException>(() => GameDataLoader.Parse(
            """[ { "id": "bow", "name": "활", "kind": "Weapon" } ]""",
            "[]",
            """
            [ { "id": "a", "name": "A", "applies": [ { "effectId": "ghost", "duration": 2 } ] },
              { "id": "b", "name": "B", "applies": [ { "effectId": "poison", "duration": 0 } ] },
              { "id": "c", "name": "C", "delayPercent": 0 } ]
            """,
            """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
            """[ { "id": "poison", "name": "독", "tickHpPercent": -4 }, { "id": "bad", "name": "과함", "tickHpPercent": -150 } ]""")).Errors;

        Assert.Contains("actions.json 'a': applies unknown effect 'ghost'", errors);
        Assert.Contains("actions.json 'b': duration of 'poison' must be at least 1, got 0", errors);
        Assert.Contains("actions.json 'c': delayPercent must be at least 1, got 0", errors);
        Assert.Contains("effects.json 'bad': tickHpPercent must be -100..100, got -150", errors);
    }
}
