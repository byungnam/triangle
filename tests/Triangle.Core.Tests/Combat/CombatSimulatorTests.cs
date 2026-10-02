using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Combat;

public class CombatSimulatorTests
{
    private static readonly ActionDefinition Strike = new()
    {
        Id = "strike", Name = "Strike", Power = 10, Rule = TargetRule.FrontFirst,
    };

    private static readonly ActionDefinition Snipe = new()
    {
        Id = "snipe", Name = "Snipe", Power = 10, Rule = TargetRule.BackFirst,
    };

    private static readonly ActionDefinition PiercingSnipe = Snipe with { Id = "piercing", IgnoresCover = true };

    private static readonly ActionDefinition Sweep = new()
    {
        Id = "sweep", Name = "Sweep", Power = 10, Scope = TargetScope.All,
    };

    private static readonly ActionDefinition Heal = new()
    {
        Id = "heal", Name = "Heal", Effect = ActionEffect.Heal, Power = 10,
        Side = TargetSide.Ally, Rule = TargetRule.LowestHpRatio,
    };

    private static readonly ActionDefinition FrontOnlyStrike = Strike with { Id = "front_only", Rows = RowRestriction.FrontOnly };

    private static readonly ActionDefinition Expensive = Strike with { Id = "expensive", MpCost = 100_000 };

    private static readonly Dictionary<string, ActionDefinition> Actions =
        new[] { Strike, Snipe, PiercingSnipe, Sweep, Heal, FrontOnlyStrike, Expensive }.ToDictionary(s => s.Id);

    private static readonly CombatCatalog Catalog = new(Actions, new Dictionary<string, SkillDefinition>());

    private static CombatantSetup Unit(
        string id,
        Row row = Row.Front,
        int str = 10,
        int vital = 20,
        int intel = 10,
        int speed = 10,
        params Tactic[] tactics) =>
        new(id, id, new Stats(str, 10, vital, intel, speed), row, null, null, SkillSet.NoSkills, tactics);

    private static Tactic Always(string actionId, int priority = 1) => new(priority, Condition.Always, 0, actionId);

    private static CombatResult Run(CombatantSetup[] allies, CombatantSetup[] enemies, int seed = 1, CombatRules? rules = null) =>
        CombatSimulator.Run(allies, enemies, Catalog, seed, rules);

    [Fact]
    public void Damage_is_scaled_by_attack_stat_and_reduced_by_defense()
    {
        // 위력 10 × (1 + 10 × 0.05) = 15, 방어 25 → 15 / (1 + 25 × 0.02) = 10
        var result = Run(
            [Unit("a", str: 10, tactics: Always("strike"))],
            [Unit("e", str: 25)]);

        var hit = result.Events.OfType<Damaged>().First();
        Assert.Equal("e", hit.TargetId);
        Assert.Equal(10, hit.Amount);
        Assert.Equal(20 * 28 - 10, hit.HpAfter);
    }

    [Fact]
    public void Faster_units_act_proportionally_more_often()
    {
        var result = Run(
            [Unit("fast", speed: 20)],
            [Unit("slow", speed: 10)],
            rules: new CombatRules { MaxActions = 30 });

        var turns = result.Events.OfType<TurnStarted>().GroupBy(t => t.ActorId).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(20, turns["fast"]);
        Assert.Equal(10, turns["slow"]);
    }

    [Fact]
    public void Tactics_are_evaluated_in_priority_order_regardless_of_input_order()
    {
        var result = Run(
            [Unit("a", tactics: [Always("sweep", 3), Always("snipe", 1), Always("strike", 2)])],
            [Unit("e", vital: 100)]);

        var used = result.Events.OfType<ActionUsed>().Where(s => s.ActorId == "a").Select(s => s.ActionId).Distinct();
        Assert.Equal(["snipe"], used);
    }

    [Fact]
    public void Heal_targets_lowest_hp_ratio_not_lowest_absolute_hp()
    {
        var tank = Unit("tank", Row.Front, vital: 100);
        var mage = Unit("mage", Row.Back, vital: 10);
        var healer = Unit("healer", Row.Back, tactics: new Tactic(1, Condition.AnyAllyHpAtMost, 99, "heal"));
        var enemy = Unit("enemy", vital: 1000, tactics: Always("strike"));

        var result = Run([tank, mage, healer], [enemy]);

        var heal = result.Events.OfType<Healed>().First();
        Assert.Equal("tank", heal.TargetId);
    }

    [Fact]
    public void MaxUses_limits_how_often_a_tactic_is_chosen()
    {
        var result = Run(
            [Unit("a", tactics: [new Tactic(1, Condition.MaxUses, 2, "snipe"), Always("strike", 2)])],
            [Unit("e", vital: 1000)],
            rules: new CombatRules { MaxActions = 20 });

        var used = result.Events.OfType<ActionUsed>().Where(s => s.ActorId == "a").ToList();
        Assert.Equal(2, used.Count(s => s.ActionId == "snipe"));
        Assert.True(used.Count(s => s.ActionId == "strike") > 0);
    }

    [Fact]
    public void MaxUses_counts_selections_that_lost_the_turn()
    {
        var result = Run(
            [Unit("a", tactics: [new Tactic(1, Condition.MaxUses, 2, "expensive"), Always("strike", 2)])],
            [Unit("e", vital: 1000)],
            rules: new CombatRules { MaxActions = 20 });

        var mine = result.Events.Where(e => e is Waited { ActorId: "a" } or ActionUsed { ActorId: "a" }).ToList();
        Assert.Equal(new Waited("a", WaitReason.NotEnoughResource, 1), mine[0]);
        Assert.Equal(new Waited("a", WaitReason.NotEnoughResource, 1), mine[1]);
        Assert.All(mine.Skip(2), e => Assert.Equal("strike", Assert.IsType<ActionUsed>(e).ActionId));
    }

    [Fact]
    public void Front_row_covers_attacks_aimed_at_back_row()
    {
        var result = Run(
            [Unit("a", tactics: Always("snipe"))],
            [Unit("front", Row.Front, vital: 1000), Unit("back", Row.Back, vital: 1000)],
            rules: new CombatRules { MaxActions = 10 });

        Assert.All(result.Events.OfType<Covered>(), c =>
        {
            Assert.Equal("back", c.ProtectedId);
            Assert.Equal("front", c.CoverId);
        });
        Assert.NotEmpty(result.Events.OfType<Covered>());
        Assert.All(result.Events.OfType<Damaged>(), d => Assert.Equal("front", d.TargetId));
    }

    [Fact]
    public void Cover_picks_randomly_among_living_front_units()
    {
        var hit = new HashSet<string>();
        for (var seed = 0; seed < 20; seed++)
        {
            var result = Run(
                [Unit("a", tactics: Always("snipe"))],
                [Unit("f1", Row.Front, vital: 1000), Unit("f2", Row.Front, vital: 1000), Unit("back", Row.Back, vital: 1000)],
                seed,
                new CombatRules { MaxActions = 10 });
            hit.UnionWith(result.Events.OfType<Covered>().Select(c => c.CoverId));
        }

        Assert.Equal(["f1", "f2"], hit.Order());
    }

    [Fact]
    public void Skills_that_ignore_cover_hit_the_back_row()
    {
        var result = Run(
            [Unit("a", tactics: Always("piercing"))],
            [Unit("front", Row.Front, vital: 1000), Unit("back", Row.Back, vital: 1000)],
            rules: new CombatRules { MaxActions = 10 });

        Assert.Empty(result.Events.OfType<Covered>());
        Assert.All(result.Events.OfType<Damaged>(), d => Assert.Equal("back", d.TargetId));
    }

    [Fact]
    public void Back_row_stays_in_back_and_is_hit_directly_once_front_row_falls()
    {
        var result = Run(
            [Unit("a", str: 100, tactics: Always("snipe"))],
            [Unit("front", Row.Front, vital: 1, str: 0), Unit("back", Row.Back, vital: 1000)],
            rules: new CombatRules { MaxActions = 10 });

        var events = result.Events.ToList();
        var death = events.FindIndex(e => e is Died { UnitId: "front" });
        Assert.True(death >= 0);
        Assert.Contains(events.Take(death), e => e is Covered);

        var after = events.Skip(death + 1).ToList();
        Assert.Empty(after.OfType<Covered>());
        Assert.Contains(after, e => e is Damaged { TargetId: "back" });
        Assert.Equal(Row.Back, result.Combatants.Single(c => c.Id == "back").Row);
    }

    [Fact]
    public void Area_skills_hit_every_candidate_without_cover()
    {
        var result = Run(
            [Unit("a", tactics: Always("sweep"))],
            [Unit("front", Row.Front, vital: 1000), Unit("back", Row.Back, vital: 1000)],
            rules: new CombatRules { MaxActions = 3 });

        var firstUse = result.Events.SkipWhile(e => e is not ActionUsed).Skip(1).TakeWhile(e => e is Damaged).Cast<Damaged>();
        Assert.Equal(["back", "front"], firstUse.Select(d => d.TargetId).Order());
        Assert.Empty(result.Events.OfType<Covered>());
    }

    [Fact]
    public void Tactic_without_valid_target_loses_the_turn()
    {
        var result = Run(
            [Unit("a", tactics: [Always("front_only", 1), Always("snipe", 2)])],
            [Unit("back", Row.Back, vital: 1000)],
            rules: new CombatRules { MaxActions = 5 });

        Assert.DoesNotContain(result.Events, e => e is ActionUsed { ActorId: "a" });
        Assert.All(
            result.Events.OfType<Waited>().Where(w => w.ActorId == "a"),
            w => Assert.Equal(new Waited("a", WaitReason.NoTarget, 1), w));
    }

    [Fact]
    public void Unaffordable_tactic_loses_the_turn()
    {
        var result = Run(
            [Unit("a", tactics: [Always("expensive", 1), Always("strike", 2)])],
            [Unit("e", vital: 1000)],
            rules: new CombatRules { MaxActions = 5 });

        Assert.DoesNotContain(result.Events, e => e is ActionUsed { ActorId: "a" });
        Assert.All(
            result.Events.OfType<Waited>().Where(w => w.ActorId == "a"),
            w => Assert.Equal(new Waited("a", WaitReason.NotEnoughResource, 1), w));
    }

    [Fact]
    public void Lower_priority_tactic_is_used_when_higher_condition_is_false()
    {
        var result = Run(
            [Unit("a", tactics: [new Tactic(1, Condition.SelfHpAtMost, 50, "expensive"), Always("strike", 2)])],
            [Unit("e", vital: 1000)],
            rules: new CombatRules { MaxActions = 5 });

        var used = result.Events.OfType<ActionUsed>().Where(s => s.ActorId == "a").Select(s => s.ActionId).Distinct();
        Assert.Equal(["strike"], used);
    }

    [Fact]
    public void Unit_with_no_usable_tactic_waits()
    {
        var result = Run([Unit("a")], [Unit("e")], rules: new CombatRules { MaxActions = 4 });

        Assert.Equal(4, result.Events.OfType<Waited>().Count(w => w.Reason == WaitReason.NoMatchingTactic));
    }

    [Fact]
    public void Killing_every_enemy_is_victory_and_losing_every_ally_is_defeat()
    {
        var win = Run([Unit("a", str: 100, tactics: Always("strike"))], [Unit("e", vital: 1, str: 0)]);
        Assert.Equal(CombatOutcome.Victory, win.Outcome);

        var loss = Run([Unit("a", vital: 1, str: 0)], [Unit("e", str: 100, tactics: Always("strike"))]);
        Assert.Equal(CombatOutcome.Defeat, loss.Outcome);
        Assert.IsType<CombatEnded>(loss.Events[^1]);
    }

    [Fact]
    public void Reaching_the_action_limit_uses_configured_outcome()
    {
        var result = Run([Unit("a")], [Unit("e")], rules: new CombatRules { MaxActions = 7 });

        Assert.Equal(CombatOutcome.Draw, result.Outcome);
        Assert.Equal(7, result.ActionCount);
    }

    [Fact]
    public void Same_seed_produces_identical_combat()
    {
        CombatantSetup[] allies = [Unit("a1", tactics: Always("strike")), Unit("a2", Row.Back, tactics: Always("snipe"))];
        CombatantSetup[] enemies = [Unit("e1", tactics: Always("strike")), Unit("e2", Row.Back, tactics: Always("sweep"))];

        var first = Run(allies, enemies, seed: 42);
        var second = Run(allies, enemies, seed: 42);

        Assert.Equal(first.Events, second.Events);
    }

    [Fact]
    public void Unknown_action_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Run([Unit("a", tactics: Always("nope"))], [Unit("e")]));
    }
}
