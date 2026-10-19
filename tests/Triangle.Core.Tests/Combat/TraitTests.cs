using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Effects;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Combat;

/// <summary>극단적인 적의 특성 (2026-10-17): 타고난 효과, 피해·디버프 면역, 치유 봉쇄, 버프 지우기, 밀려남 조건.</summary>
public class TraitTests
{
    private static readonly Dictionary<string, EffectDefinition> Effects = new[]
    {
        new EffectDefinition { Id = "ghost", Name = "ghost", Kind = EffectKind.Buff, ImmuneDamageTypes = [DamageType.Physical] },
        new EffectDefinition { Id = "pure", Name = "pure", Kind = EffectKind.Buff, ImmuneToDebuffs = true },
        new EffectDefinition { Id = "rot", Name = "rot", Kind = EffectKind.Debuff, HealBlock = true },
        new EffectDefinition { Id = "poison", Name = "poison", Kind = EffectKind.Debuff, TickHpPercent = -10 },
        new EffectDefinition { Id = "regen", Name = "regen", Kind = EffectKind.Buff, TickHpPercent = 10 },
    }.ToDictionary(e => e.Id);

    private static readonly CombatCatalog Catalog = new(
        new ActionDefinition[]
        {
            new() { Id = "punch", Name = "punch", Power = 10 },
            new() { Id = "shove", Name = "shove", Power = 10, MoveTo = Row.Back },
            new() { Id = "bolt", Name = "bolt", Power = 10, DamageType = DamageType.Magical },
            new() { Id = "venom", Name = "venom", Power = 10, Applies = [new EffectApplication("poison", 3)] },
            new() { Id = "curse", Name = "curse", Effect = ActionEffect.None, Applies = [new EffectApplication("poison", 3)] },
            new() { Id = "rot_hit", Name = "rot_hit", Power = 1, Applies = [new EffectApplication("rot", 5)] },
            new() { Id = "heal_self", Name = "heal_self", Effect = ActionEffect.Heal, Power = 50, Side = TargetSide.Self },
            new() { Id = "shield_self", Name = "shield_self", Effect = ActionEffect.Shield, Power = 50, Side = TargetSide.Self },
            new() { Id = "regen_self", Name = "regen_self", Effect = ActionEffect.None, Side = TargetSide.Self, Applies = [new EffectApplication("regen", 5)] },
            new() { Id = "dispel", Name = "dispel", Effect = ActionEffect.None, Rule = TargetRule.WithBuffFirst, IgnoresCover = true, RemovesBuffs = true },
            new() { Id = "step_back", Name = "step_back", Effect = ActionEffect.None, Side = TargetSide.Self, MoveTo = Row.Back },
            new() { Id = "wait", Name = "wait", Effect = ActionEffect.None, Side = TargetSide.Self },
        }.ToDictionary(a => a.Id),
        new Dictionary<string, SkillDefinition>()) { Effects = Effects };

    private static CombatantSetup Unit(string id, Row row = Row.Front, int speed = 10, int vital = 20, string[]? traits = null, params Tactic[] tactics) =>
        new(id, id, new Stats(10, 10, vital, 10, speed), row, null, null, SkillSet.NoSkills, tactics) { StartingEffects = traits ?? [] };

    private static Tactic Use(string actionId, int priority = 1, Condition condition = Condition.Always, int value = 0) =>
        new(priority, condition, value, actionId);

    private static CombatResult Run(CombatantSetup[] allies, CombatantSetup[] enemies, int maxActions = 30) =>
        CombatSimulator.Run(allies, enemies, Catalog, seed: 1, new CombatRules { MaxActions = maxActions });

    [Fact]
    public void Starting_effects_are_announced_before_the_first_turn_and_never_run_out()
    {
        var result = Run([Unit("a", tactics: Use("wait"))], [Unit("e", traits: ["ghost"], tactics: Use("wait"))]);

        Assert.Equal(new EffectApplied("e", "ghost", EffectApplied.Permanent, false), result.Events[0]);
        Assert.DoesNotContain(result.Events, e => e is EffectExpired);
        Assert.Contains(result.Combatants.Single(c => c.Id == "e").ActiveEffects, x => x.EffectId == "ghost");
    }

    [Fact]
    public void Immunity_stops_the_damage_type_with_its_effects_and_movement_but_not_the_other_type()
    {
        var result = Run(
            [Unit("a", tactics: [Use("shove", 1, Condition.OnTurn, 1), Use("venom", 2, Condition.OnTurn, 2), Use("bolt", 3)])],
            [Unit("e", vital: 100, traits: ["ghost"], tactics: Use("wait"))]);

        var events = result.Events;
        Assert.Equal(2, events.OfType<Immune>().Count(i => i is { TargetId: "e", EffectId: "ghost", BlockedEffectId: null }));
        Assert.DoesNotContain(events, e => e is Moved or EffectApplied { EffectId: "poison" });
        Assert.All(events.OfType<Damaged>().Where(d => d.TargetId == "e"), d => Assert.True(d.Amount > 0));
        Assert.Contains(events, e => e is Damaged { TargetId: "e" });
        Assert.Equal(Row.Front, result.Combatants.Single(c => c.Id == "e").Row);
    }

    [Fact]
    public void Debuff_immunity_blocks_debuffs_from_any_action_but_not_damage()
    {
        var result = Run(
            [Unit("a", tactics: [Use("curse", 1, Condition.OnTurn, 1), Use("venom", 2, Condition.OnTurn, 2), Use("wait", 3)])],
            [Unit("e", vital: 100, traits: ["pure"], tactics: Use("wait"))]);

        Assert.Equal(2, result.Events.OfType<Immune>().Count(i => i is { TargetId: "e", EffectId: "pure", BlockedEffectId: "poison" }));
        Assert.DoesNotContain(result.Events, e => e is EffectApplied { EffectId: "poison" });
        Assert.Single(result.Events.OfType<Damaged>(), d => d.TargetId == "e");
    }

    [Fact]
    public void Heal_block_stops_heals_and_regeneration_but_not_shields()
    {
        var result = Run(
            [Unit("a", speed: 100, tactics: [Use("rot_hit", 1, Condition.OnTurn, 1), Use("punch", 2, Condition.UntilTurn, 3), Use("wait", 3)])],
            [Unit("e", vital: 100, tactics: [Use("regen_self", 1, Condition.OnTurn, 1), Use("heal_self", 2, Condition.OnTurn, 2), Use("shield_self", 3, Condition.OnTurn, 3), Use("wait", 4)])],
            maxActions: 40);

        var events = result.Events;
        Assert.DoesNotContain(events, e => e is Healed { TargetId: "e" } or EffectTicked { TargetId: "e", HpChange: > 0 });
        Assert.Contains(events, e => e is HealBlocked { TargetId: "e", EffectId: "rot" });
        Assert.Contains(events, e => e is ShieldGained { TargetId: "e" });
        Assert.Equal(1, CombatSummary.From(result).Single(s => s.Id == "e").HealsBlocked);
    }

    [Fact]
    public void Dispel_removes_buffs_including_starting_ones_and_prefers_buffed_targets()
    {
        // 엄호 무시라 후위의 버프 유닛을 고른다. 버프가 사라지면 물리 공격이 들어간다.
        var result = Run(
            [Unit("a", tactics: [Use("dispel", 1, Condition.AnyEnemyHasBuff), Use("punch", 2)])],
            [Unit("front", tactics: Use("wait")), Unit("ghost", row: Row.Back, vital: 5, traits: ["ghost"], tactics: Use("wait"))]);

        var events = result.Events.ToList();
        var dispelled = events.FindIndex(e => e is EffectExpired { TargetId: "ghost", EffectId: "ghost" });
        Assert.True(dispelled > 0);
        Assert.DoesNotContain(events.Take(dispelled), e => e is Damaged);
        Assert.Equal(1, result.Events.OfType<ActionUsed>().Count(a => a.ActionId == "dispel"));
    }

    [Fact]
    public void Displaced_is_true_only_away_from_the_starting_row()
    {
        // 적이 첫 턴에 a를 후위로 민다. a는 밀려났을 때만 제자리로 돌아간다(step_back은 후위로 가므로 여기서는 쓰지 않는다).
        var result = Run(
            [Unit("a", speed: 5, tactics: [Use("punch", 1, Condition.SelfDisplaced), Use("wait", 2)])],
            [Unit("e", speed: 20, vital: 100, tactics: [Use("shove", 1, Condition.OnTurn, 1), Use("wait", 2)])],
            maxActions: 8);

        var events = result.Events.ToList();
        var moved = events.FindIndex(e => e is Moved { TargetId: "a", Row: Row.Back });
        var punches = events.Select((e, i) => (e, i)).Where(x => x.e is ActionUsed { ActorId: "a", ActionId: "punch" }).Select(x => x.i).ToList();
        Assert.True(moved >= 0);
        Assert.NotEmpty(punches);
        Assert.All(punches, i => Assert.True(i > moved));
    }

    [Fact]
    public void Unknown_starting_effect_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Run([Unit("a", tactics: Use("wait"))], [Unit("e", traits: ["nope"], tactics: Use("wait"))]));
    }
}
