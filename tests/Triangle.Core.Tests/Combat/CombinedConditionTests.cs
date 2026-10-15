using System.Text.Json;
using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Effects;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Combat;

/// <summary>
/// 한 줄에 조건 여러 개(모두 참이어야 한다)와 2026-10-15에 더한 조건:
/// 적 누군가 HP, 쓰러진 아군 수, 행동의 효과가 없는 자신·아군·적, 디버프 걸린 아군.
/// </summary>
public class CombinedConditionTests
{
    private static readonly Dictionary<string, EffectDefinition> Effects = new[]
    {
        new EffectDefinition { Id = "guard", Name = "guard", Kind = EffectKind.Buff },
        new EffectDefinition { Id = "might", Name = "might", Kind = EffectKind.Buff },
        new EffectDefinition { Id = "poison", Name = "poison", Kind = EffectKind.Debuff },
        new EffectDefinition { Id = "weak", Name = "weak", Kind = EffectKind.Debuff },
    }.ToDictionary(e => e.Id);

    private static ActionDefinition A(string id) => new() { Id = id, Name = id, Power = 10 };

    private static readonly CombatCatalog Catalog = new(
        new[]
        {
            A("punch"),
            A("smash") with { Power = 1000 },
            A("mark") with { Effect = ActionEffect.None, Side = TargetSide.Self },
            A("drain") with { Power = 1, MpCost = 50 },
            A("guard_self") with { Effect = ActionEffect.None, Side = TargetSide.Self, Applies = [new EffectApplication("guard", 2)] },
            A("bless") with
            {
                Effect = ActionEffect.None, Side = TargetSide.Ally, Rule = TargetRule.WithoutEffectFirst,
                Applies = [new EffectApplication("might", 20)],
            },
            A("poison_hit") with
            {
                Effect = ActionEffect.None, Rule = TargetRule.WithoutEffectFirst, Applies = [new EffectApplication("poison", 20)],
            },
            A("curse") with { Effect = ActionEffect.None, Applies = [new EffectApplication("weak", 20)] },
            A("cleanse") with { Effect = ActionEffect.None, Side = TargetSide.Ally, Rule = TargetRule.WithDebuffFirst, RemovesDebuffs = true },
        }.ToDictionary(a => a.Id),
        new Dictionary<string, SkillDefinition>()) { Effects = Effects };

    private static CombatantSetup Unit(string id, int str = 10, int vital = 100, int speed = 10, Row row = Row.Front, params Tactic[] tactics) =>
        new(id, id, new Stats(str, 10, vital, 10, speed), row, null, null, SkillSet.NoSkills, tactics);

    private static Tactic Line(int priority, string actionId, params (Condition Condition, int Value)[] conditions) =>
        Tactic.Create(priority, conditions.Select(c => new TacticCondition(c.Condition, c.Value)).ToList(), actionId);

    private static CombatResult Run(CombatantSetup[] allies, CombatantSetup[] enemies, int maxActions = 20) =>
        CombatSimulator.Run(allies, enemies, Catalog, seed: 1, new CombatRules { MaxActions = maxActions });

    private static List<string> ActionsOf(CombatResult result, string id) =>
        result.Events.OfType<ActionUsed>().Where(u => u.ActorId == id).Select(u => u.ActionId).ToList();

    [Fact]
    public void A_line_fires_only_when_all_of_its_conditions_hold()
    {
        // MP 100으로 drain(MP 50)을 두 번 쓰고, 그 뒤로는 "MP 50 이상"이 거짓이라 다음 줄(punch)로 간다.
        var guarded = Run(
            [Unit("a", tactics: [Line(1, "drain", (Condition.Always, 0), (Condition.SelfMpAmountAtLeast, 50)), Line(2, "punch", (Condition.Always, 0))])],
            [Unit("e", vital: 1000)]);
        var actions = ActionsOf(guarded, "a");
        Assert.Equal(["drain", "drain"], actions.Take(2));
        Assert.All(actions.Skip(2), a => Assert.Equal("punch", a));
        Assert.DoesNotContain(guarded.Events.OfType<Waited>(), w => w.ActorId == "a");

        // 조건이 하나뿐이면 MP가 모자란 차례마다 차례를 잃는다(규칙은 그대로다).
        var single = Run([Unit("a", tactics: [Line(1, "drain", (Condition.Always, 0)), Line(2, "punch", (Condition.Always, 0))])], [Unit("e", vital: 1000)]);
        Assert.Contains(single.Events.OfType<Waited>(), w => w is { ActorId: "a", Reason: WaitReason.NotEnoughResource });
    }

    [Fact]
    public void One_false_condition_keeps_the_line_from_firing()
    {
        var result = Run(
            [Unit("a", tactics: [Line(1, "mark", (Condition.FromTurn, 1), (Condition.EnemyAliveAtLeast, 5)), Line(2, "punch", (Condition.Always, 0))])],
            [Unit("e", vital: 1000)], maxActions: 8);
        Assert.All(ActionsOf(result, "a"), a => Assert.Equal("punch", a));
    }

    [Theory]
    [InlineData(Condition.AnyEnemyHpAtMost, 30, true)]
    [InlineData(Condition.AnyEnemyHpAtMost, 5, false)]
    [InlineData(Condition.AnyEnemyHpAtLeast, 100, true)]
    [InlineData(Condition.AnyEnemyHpAmountAtMost, 300, true)]
    [InlineData(Condition.AnyEnemyHpAmountAtMost, 100, false)]
    [InlineData(Condition.AnyEnemyHpAmountAtLeast, 2800, true)]
    [InlineData(Condition.AnyEnemyHpAmountAtLeast, 2801, false)]
    public void Any_enemy_hp_looks_at_every_living_enemy(Condition condition, int value, bool expected)
    {
        // e1은 HP 가득(2800), e2는 280 (10%).
        var a = Unit("a", tactics: [Line(1, "mark", (condition, value)), Line(2, "punch", (Condition.Always, 0))]);
        var result = Run([a], [Unit("e1"), Unit("e2") with { StartHp = 280 }], maxActions: 3);
        Assert.Equal(expected, ActionsOf(result, "a")[0] == "mark");
    }

    [Fact]
    public void Ally_down_counts_fallen_allies()
    {
        // 적 k가 첫 차례(시각 0)에 HP 1인 전위 b를 쓰러뜨린다(후위 a를 노려도 b가 엄호한다). a는 그 뒤에만 mark를 쓴다.
        var a = Unit("a", row: Row.Back, tactics: [Line(1, "mark", (Condition.AllyDownAtLeast, 1)), Line(2, "punch", (Condition.Always, 0))]);
        var b = Unit("b") with { StartHp = 1 };
        var k = Unit("k", speed: 30, tactics: [Line(1, "punch", (Condition.MaxUses, 1)), Line(2, "mark", (Condition.Always, 0))]);
        var result = Run([a, b], [k], maxActions: 12);

        var events = result.Events.ToList();
        var died = events.FindIndex(e => e is Died { UnitId: "b" });
        Assert.True(died >= 0);
        Assert.DoesNotContain(events.Take(died).OfType<ActionUsed>(), u => u is { ActorId: "a", ActionId: "mark" });
        Assert.Equal("mark", events.Skip(died).OfType<ActionUsed>().First(u => u.ActorId == "a").ActionId);
    }

    [Fact]
    public void Self_lacks_effect_recasts_a_self_buff_only_when_it_has_worn_off()
    {
        // guard는 2턴: 건 차례가 끝나면 1, 다음 차례가 끝나면 사라진다. 그래서 걸기 → 공격 → 걸기 → 공격.
        var a = Unit("a", tactics: [Line(1, "guard_self", (Condition.SelfLacksActionEffect, 0)), Line(2, "punch", (Condition.Always, 0))]);
        var result = Run([a], [Unit("e", vital: 1000)], maxActions: 10);
        Assert.Equal(["guard_self", "punch", "guard_self", "punch"], ActionsOf(result, "a").Take(4));
    }

    [Fact]
    public void Any_enemy_lacks_effect_stops_once_every_enemy_has_it()
    {
        var a = Unit("a", speed: 100, tactics: [Line(1, "poison_hit", (Condition.AnyEnemyLacksActionEffect, 0)), Line(2, "punch", (Condition.Always, 0))]);
        var result = Run([a], [Unit("e1", vital: 1000), Unit("e2", vital: 1000)], maxActions: 8);
        Assert.Equal(["poison_hit", "poison_hit", "punch"], ActionsOf(result, "a").Take(3));
    }

    [Fact]
    public void Any_ally_lacks_effect_stops_once_every_ally_has_it()
    {
        var a = Unit("a", speed: 100, tactics: [Line(1, "bless", (Condition.AnyAllyLacksActionEffect, 0)), Line(2, "punch", (Condition.Always, 0))]);
        var result = Run([a, Unit("b")], [Unit("e", vital: 1000)], maxActions: 8);
        Assert.Equal(["bless", "bless", "punch"], ActionsOf(result, "a").Take(3));
    }

    [Fact]
    public void Effect_conditions_are_false_for_an_action_that_applies_no_effect()
    {
        var a = Unit("a", tactics: [Line(1, "mark", (Condition.SelfLacksActionEffect, 0)), Line(2, "punch", (Condition.Always, 0))]);
        var result = Run([a], [Unit("e", vital: 1000)], maxActions: 4);
        Assert.All(ActionsOf(result, "a"), x => Assert.Equal("punch", x));
    }

    [Fact]
    public void Any_ally_has_debuff_triggers_a_cleanse_after_the_enemy_curses()
    {
        var a = Unit("a", tactics: [Line(1, "cleanse", (Condition.AnyAllyHasDebuff, 0)), Line(2, "punch", (Condition.Always, 0))]);
        var e = Unit("e", vital: 1000, tactics: [Line(1, "curse", (Condition.OnTurn, 2)), Line(2, "mark", (Condition.Always, 0))]);
        var result = Run([a], [e], maxActions: 12);

        var events = result.Events.ToList();
        var cursed = events.FindIndex(x => x is EffectApplied { TargetId: "a", EffectId: "weak" });
        Assert.True(cursed >= 0);
        Assert.DoesNotContain(events.Take(cursed).OfType<ActionUsed>(), u => u is { ActorId: "a", ActionId: "cleanse" });
        Assert.Equal("cleanse", events.Skip(cursed).OfType<ActionUsed>().First(u => u.ActorId == "a").ActionId);
    }

    [Fact]
    public void Validation_checks_every_condition_and_the_count()
    {
        var actions = Catalog.Actions;
        var errors = new List<string>();
        DataValidation.ValidateTactic(Line(1, "punch", (Condition.Always, 0), (Condition.SelfHpAtMost, 150)), "t", actions, errors);
        Assert.Contains(errors, e => e.Contains("percent"));

        errors.Clear();
        var four = Line(1, "punch", (Condition.Always, 0), (Condition.FromTurn, 1), (Condition.UntilTurn, 9), (Condition.SelfHpAtMost, 50));
        DataValidation.ValidateTactic(four, "t", actions, errors);
        Assert.Contains(errors, e => e.Contains("at most 3"));

        errors.Clear();
        DataValidation.ValidateTactic(Line(1, "punch", (Condition.SelfLacksActionEffect, 0), (Condition.AllyDownAtLeast, 1)), "t", actions, errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void Json_keeps_single_condition_tactics_as_before_and_round_trips_extra_conditions()
    {
        var single = new Tactic(1, Condition.Always, 0, "punch");
        Assert.DoesNotContain("\"and\"", JsonSerializer.Serialize(single, GameDataJson.Options));

        var combined = Line(2, "drain", (Condition.EveryNthTurn, 4), (Condition.SelfMpAmountAtLeast, 35));
        var json = JsonSerializer.Serialize(combined, GameDataJson.Options);
        Assert.Contains("\"and\"", json);
        Assert.DoesNotContain("\"conditions\"", json);
        var back = JsonSerializer.Deserialize<Tactic>(json, GameDataJson.Options)!;
        Assert.Equal(combined.Conditions, back.Conditions);

        var old = JsonSerializer.Deserialize<Tactic>("""{ "priority": 1, "condition": "Always", "value": 0, "actionId": "punch" }""", GameDataJson.Options)!;
        Assert.Null(old.And);
        Assert.Single(old.Conditions);
    }

    [Fact]
    public void Tactic_list_edits_conditions_in_place()
    {
        var list = new TacticList([new Tactic(1, Condition.EveryNthTurn, 4, "drain")]);
        Assert.True(list.AddCondition(0, Condition.SelfMpAmountAtLeast, 35));
        Assert.True(list.AddCondition(0, Condition.EnemyAliveAtLeast, 2));
        Assert.False(list.AddCondition(0, Condition.Always, 0)); // 3개까지
        Assert.Equal(3, list[0].Conditions.Count);

        list.SetCondition(0, 1, Condition.SelfMpAmountAtLeast, 50);
        list.SetAction(0, "punch");
        Assert.Equal(new TacticCondition(Condition.SelfMpAmountAtLeast, 50), list[0].Conditions[1]);
        Assert.Equal("punch", list[0].ActionId);

        // 첫 조건을 빼면 다음 조건이 첫 조건이 된다. 마지막 하나는 뺄 수 없다.
        Assert.True(list.RemoveCondition(0, 0));
        Assert.Equal(Condition.SelfMpAmountAtLeast, list[0].Condition);
        Assert.True(list.RemoveCondition(0, 1));
        Assert.False(list.RemoveCondition(0, 0));
        Assert.Null(list[0].And);
    }
}
