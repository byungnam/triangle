using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Effects;
using Triangle.Core.Masteries;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Combat;

/// <summary>행동의 특수 규칙: 연타, 연쇄, 처형, 기절, 넘어뜨림, 줄 이동, 보호막, 도발, 영창, 소환, 무기 위력 배율.</summary>
public class ActionMechanicsTests
{
    private static readonly Dictionary<string, EffectDefinition> Effects = new[]
    {
        new EffectDefinition { Id = "bleed", Name = "bleed", Kind = EffectKind.Debuff, TickHpPercent = -1 },
        new EffectDefinition { Id = "stun", Name = "stun", Kind = EffectKind.Debuff, Stun = true },
        new EffectDefinition { Id = "taunt", Name = "taunt", Kind = EffectKind.Buff, Taunt = true },
        new EffectDefinition { Id = "broken", Name = "broken", Kind = EffectKind.Debuff, NoCover = true },
    }.ToDictionary(e => e.Id);

    private static ActionDefinition A(string id) => new() { Id = id, Name = id, Power = 10 };

    private static readonly ActionDefinition[] AllActions =
    [
        A("hit"),
        A("double") with { Hits = 2 },
        A("chain") with { ExtraTargets = 2 },
        A("execute") with { MissingHpBonusPercent = 100 },
        A("bleed_hit") with { Applies = [new EffectApplication("bleed", 3)] },
        A("open_wound") with { BonusAgainst = new EffectBonus("bleed", 100) },
        A("stun_hit") with { Applies = [new EffectApplication("stun", 1)] },
        A("trip") with { PushBack = 5000 },
        A("burn_mp") with { MpDamage = 30 },
        A("hook") with { Rule = TargetRule.BackFirst, IgnoresCover = true, MoveTo = Row.Front },
        A("cleanse") with { Effect = ActionEffect.None, Side = TargetSide.Ally, Rule = TargetRule.WithDebuffFirst, RemovesDebuffs = true },
        A("ward") with { Effect = ActionEffect.Shield, Power = 100, Side = TargetSide.Self },
        A("provoke") with { Effect = ActionEffect.None, Side = TargetSide.Self, Applies = [new EffectApplication("taunt", 100)] },
        A("snipe") with { Rule = TargetRule.BackFirst },
        A("break_line") with { Scope = TargetScope.All, Rows = RowRestriction.FrontOnly, Applies = [new EffectApplication("broken", 5)] },
        A("big_spell") with { Power = 200, Chant = 3 },
        A("once") with { OncePerBattle = true },
        A("fire_bolt") with { Tags = ["fire"], Applies = [new EffectApplication("bleed", 2)] },
        A("mana_gift") with { Effect = ActionEffect.RestoreMp, Power = 10, Side = TargetSide.Ally, Rule = TargetRule.LowestMpRatio },
        A("spirit_bite") with { SummonOnly = true },
        A("call_spirit") with
        {
            Effect = ActionEffect.Summon, Tags = ["fire"], Power = 0,
            Summon = new SummonDefinition("spirit", Row.Back, new Stats(10, 10, 10, 10, 10), "spirit_bite", Duration: 2,
                StatSkill: "communion", StatPercentPerLevel: 10),
        },
        A("call_lasting_spirit") with
        {
            Effect = ActionEffect.Summon, Power = 0,
            Summon = new SummonDefinition("lasting", Row.Front, new Stats(10, 10, 10, 10, 10), "spirit_bite"),
        },
    ];

    private static readonly CombatCatalog Catalog = new(
        AllActions.ToDictionary(a => a.Id),
        new Dictionary<string, SkillDefinition>
        {
            ["communion"] = new() { Id = "communion", Name = "communion", Mastery = "fire" },
        }) { Effects = Effects };

    private static CombatantSetup Unit(string id, Row row = Row.Front, int vital = 100, int speed = 10, params string[] actions) =>
        new(id, id, new Stats(10, 10, vital, 10, speed), row, null, null, SkillSet.NoSkills,
            actions.Select((a, i) => new Tactic(i + 1, Condition.Always, 0, a)).ToList());

    private static Tactic Once(string actionId, int priority = 1) => new(priority, Condition.MaxUses, 1, actionId);

    private static CombatResult Run(CombatantSetup[] allies, CombatantSetup[] enemies, int maxActions = 20, int seed = 1) =>
        CombatSimulator.Run(allies, enemies, Catalog, seed, new CombatRules { MaxActions = maxActions });

    private static List<T> Of<T>(CombatResult r) => r.Events.OfType<T>().ToList();

    [Fact]
    public void Hits_strike_the_same_target_several_times()
    {
        var result = Run([Unit("a", actions: "double")], [Unit("e")], maxActions: 2);
        Assert.Equal(2, Of<Damaged>(result).Count(d => d.TargetId == "e"));
    }

    [Fact]
    public void Extra_targets_hit_other_candidates_once_each()
    {
        var result = Run([Unit("a", actions: "chain")], [Unit("e1"), Unit("e2"), Unit("e3"), Unit("e4")], maxActions: 5);
        var hit = Of<Damaged>(result).Select(d => d.TargetId).ToList();
        Assert.Equal(3, hit.Count);
        Assert.Equal(3, hit.Distinct().Count());
    }

    [Fact]
    public void Execute_deals_more_to_wounded_targets()
    {
        CombatantSetup Target(int? hp) => Unit("e") with { StartHp = hp };
        int Dealt(int? hp) => Of<Damaged>(Run([Unit("a", actions: "execute")], [Target(hp)], maxActions: 2)).Single().Amount;

        var full = Dealt(null);
        var half = Dealt(1400); // 최대 2800의 절반
        Assert.Equal(Ratio.ApplyPercent(full, 150), half, tolerance: 1);
    }

    [Fact]
    public void Bonus_against_an_effect_applies_only_when_the_target_has_it()
    {
        var plain = Of<Damaged>(Run([Unit("a", actions: "open_wound")], [Unit("e")], maxActions: 2)).Single().Amount;
        var setup = Unit("a") with { Tactics = [Once("bleed_hit"), new Tactic(2, Condition.Always, 0, "open_wound")] };
        var result = Run([setup with { Stats = setup.Stats with { Speed = 100 } }], [Unit("e", speed: 1)], maxActions: 3);
        var wounded = Of<Damaged>(result).Last().Amount;
        Assert.Equal(plain * 2, wounded, tolerance: 1);
    }

    [Fact]
    public void Stunned_units_lose_their_turn()
    {
        var result = Run([Unit("a", speed: 100, actions: "stun_hit")], [Unit("e", speed: 10, actions: "hit")], maxActions: 30);
        Assert.Contains(Of<Waited>(result), w => w is { ActorId: "e", Reason: WaitReason.Stunned });
        Assert.True(Of<ActionUsed>(result).Count(u => u.ActorId == "e") <= 1); // 시각 0에 먼저 움직였을 때만
    }

    [Fact]
    public void Push_back_delays_the_next_turn()
    {
        var result = Run([Unit("a", actions: "trip")], [Unit("e", actions: "hit")], maxActions: 10);
        var delayed = Of<Delayed>(result).First();
        Assert.Equal("e", delayed.TargetId);
        Assert.Equal(500, delayed.Amount);
        Assert.True(result.Events.OfType<TurnStarted>().Count(t => t.ActorId == "a") > result.Events.OfType<TurnStarted>().Count(t => t.ActorId == "e"));
    }

    [Fact]
    public void Mp_damage_burns_mana()
    {
        var result = Run([Unit("a", actions: "burn_mp")], [Unit("e")], maxActions: 2);
        Assert.Equal(new MpBurned("e", 30, 70), Of<MpBurned>(result).Single());
    }

    [Fact]
    public void Hook_pulls_a_back_row_unit_to_the_front()
    {
        var result = Run([Unit("a", actions: "hook")], [Unit("front"), Unit("back", Row.Back)], maxActions: 3);
        Assert.Equal(new Moved("back", Row.Front), Of<Moved>(result).Single());
        Assert.Equal(Row.Front, result.Combatants.Single(c => c.Id == "back").Row);
        Assert.Equal(Row.Back, result.Combatants.Single(c => c.Id == "back").StartRow);
    }

    [Fact]
    public void Cleanse_removes_debuffs_from_an_afflicted_ally()
    {
        var enemy = Unit("e", speed: 100) with { Tactics = [Once("bleed_hit")] };
        var result = Run([Unit("a", speed: 10, actions: "cleanse"), Unit("b", speed: 1)], [enemy], maxActions: 20);
        var bled = Of<EffectApplied>(result).Single().TargetId;
        Assert.Contains(Of<EffectExpired>(result), x => x.TargetId == bled && x.EffectId == "bleed");
    }

    [Fact]
    public void Shield_absorbs_damage_before_hp()
    {
        var result = Run([Unit("a", speed: 100) with { Tactics = [Once("ward")] }], [Unit("e", speed: 10, actions: "hit")], maxActions: 14);
        var events = result.Events.ToList();
        var gained = Of<ShieldGained>(result).Single();
        var first = events.FindIndex(e => e is ShieldAbsorbed);
        var absorbed = (ShieldAbsorbed)events[first];
        Assert.Equal("a", absorbed.TargetId);
        Assert.Equal(gained.ShieldAfter - absorbed.Amount, absorbed.ShieldAfter);
        Assert.Equal(new Damaged("a", 0, ((Damaged)events[first + 1]).HpAfter), events[first + 1]);
    }

    [Fact]
    public void Taunt_draws_single_target_attacks_past_cover_rules()
    {
        var tank = Unit("tank", Row.Back, speed: 100) with { Tactics = [Once("provoke")] };
        var result = Run([tank, Unit("front", speed: 1)], [Unit("e", speed: 10, actions: "hit")], maxActions: 30);
        var events = result.Events.ToList();
        var taunted = events.FindIndex(e => e is EffectApplied { EffectId: "taunt" });
        var after = events.Skip(taunted).OfType<Damaged>().ToList();
        Assert.NotEmpty(after);
        Assert.All(after, d => Assert.Equal("tank", d.TargetId));
        Assert.DoesNotContain(events.Skip(taunted), e => e is Covered);
    }

    [Fact]
    public void Units_with_no_cover_do_not_protect_the_back_row()
    {
        var breaker = Unit("a", speed: 100) with { Tactics = [Once("break_line"), new Tactic(2, Condition.Always, 0, "snipe")] };
        var result = Run([breaker], [Unit("front", speed: 1), Unit("back", Row.Back, speed: 1)], maxActions: 4);
        Assert.Empty(Of<Covered>(result));
        Assert.Equal("back", Of<Damaged>(result).Last().TargetId);
    }

    [Fact]
    public void Chant_needs_consecutive_uses_and_is_broken_by_other_actions()
    {
        var result = Run([Unit("a", vital: 1000, actions: "big_spell")], [Unit("e", vital: 1000)], maxActions: 14);
        var chants = Of<Chanting>(result).Where(c => c.ActorId == "a").Select(c => c.Count).ToList();
        Assert.Equal([1, 2, 3, 1, 2, 3], chants.Take(6));
        Assert.Equal(2, Of<Damaged>(result).Count(d => d.TargetId == "e"));

        var broken = Run([Unit("a", vital: 1000) with { Tactics = [new Tactic(1, Condition.EveryNthTurn, 2, "hit"), new Tactic(2, Condition.Always, 0, "big_spell")] }],
            [Unit("e", vital: 1000)], maxActions: 16);
        Assert.Contains(Of<ChantBroken>(broken), b => b.ActorId == "a");
        Assert.DoesNotContain(Of<Chanting>(broken), c => c.Count == 3);
    }

    [Fact]
    public void Chant_is_broken_by_push_back()
    {
        // e는 세 번째 차례(시각 166)에 넘어뜨린다. a는 0, 100에 영창 1, 2를 한 상태다.
        var caster = Unit("a", speed: 10, vital: 1000, actions: "big_spell");
        var tripper = Unit("e", speed: 12, vital: 1000) with
        {
            Tactics = [new Tactic(1, Condition.OnTurn, 3, "trip"), new Tactic(2, Condition.Always, 0, "hit")],
        };
        var result = Run([caster], [tripper], maxActions: 30);
        Assert.Contains(Of<ChantBroken>(result), b => b.ActorId == "a");
    }

    [Fact]
    public void Once_per_battle_actions_wait_after_use()
    {
        var result = Run([Unit("a", actions: "once")], [Unit("e", vital: 1000)], maxActions: 6);
        Assert.Single(Of<ActionUsed>(result), u => u.ActorId == "a");
        Assert.Contains(Of<Waited>(result), w => w is { ActorId: "a", Reason: WaitReason.AlreadyUsed });
    }

    [Fact]
    public void Weapon_multiplier_and_effect_bonus_apply_only_to_actions_tagged_with_the_weapon()
    {
        CombatantSetup Caster(string action, int multiplier) =>
            Unit("a", actions: action) with { Weapon = "fire", PowerMultiplierPercent = multiplier, EffectDurationBonus = 1 };

        var wand = Of<Damaged>(Run([Caster("fire_bolt", 100)], [Unit("e")], maxActions: 2)).Single().Amount;
        var staffResult = Run([Caster("fire_bolt", 200)], [Unit("e")], maxActions: 2);
        Assert.Equal(wand * 2, Of<Damaged>(staffResult).Single().Amount, tolerance: 1);
        Assert.Equal(3, Of<EffectApplied>(staffResult).Single().Duration);

        var untagged = Of<Damaged>(Run([Caster("hit", 200)], [Unit("e")], maxActions: 2)).Single().Amount;
        Assert.Equal(wand, untagged);
    }

    [Fact]
    public void Lowest_mp_ratio_picks_the_ally_with_the_least_mana()
    {
        var result = Run([Unit("a", actions: "mana_gift"), Unit("b") with { StartMp = 10 }], [Unit("e")], maxActions: 3);
        Assert.Equal("b", Of<MpRestored>(result).Single().TargetId);
    }

    [Fact]
    public void Summons_join_the_fight_scale_with_weapon_and_skill_and_leave_after_their_duration()
    {
        var caster = Unit("a", vital: 1000, actions: "call_spirit") with
        {
            Weapon = "fire", PowerMultiplierPercent = 200,
            Skills = new Dictionary<string, int> { ["communion"] = 5 },
            Tactics = [Once("call_spirit"), new Tactic(2, Condition.Always, 0, "hit")],
        };
        var result = Run([caster], [Unit("e", vital: 1000, actions: "hit")], maxActions: 12);

        var summoned = Of<Summoned>(result).Single();
        var spirit = result.Combatants.Single(c => c.Id == summoned.UnitId);
        Assert.True(spirit.IsSummon);
        Assert.Equal(new Stats(30, 30, 30, 30, 10), spirit.Stats); // 10 × 200% × 150%, 속도는 그대로
        Assert.Equal(2, Of<ActionUsed>(result).Count(u => u.ActorId == spirit.Id));
        Assert.Contains(Of<Dismissed>(result), d => d.UnitId == spirit.Id);
        Assert.False(spirit.IsAlive);
    }

    [Fact]
    public void Summons_vanish_with_their_owner_and_do_not_count_for_the_outcome()
    {
        var caster = Unit("a", vital: 1, speed: 100) with { Tactics = [Once("call_lasting_spirit")] };
        var result = Run([caster], [Unit("e", vital: 1000, speed: 10, actions: "hit")], maxActions: 100);

        Assert.Equal(CombatOutcome.Defeat, result.Outcome);
        var spiritId = Of<Summoned>(result).Single().UnitId;
        Assert.Contains(Of<Dismissed>(result), d => d.UnitId == spiritId);
        Assert.DoesNotContain(MasteryGain.ForAllies(result), x => x.CombatantId == spiritId);
    }
}
