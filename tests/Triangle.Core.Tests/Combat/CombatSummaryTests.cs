using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Effects;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Combat;

/// <summary>전투 기록 요약: 전술별 사용·막힘, 잃은 차례, 피해·회복 귀속(지속 효과는 건 유닛, 소환 유닛은 소환자), 쓰러진 차례.</summary>
public class CombatSummaryTests
{
    private static readonly Dictionary<string, EffectDefinition> Effects = new[]
    {
        new EffectDefinition { Id = "poison", Name = "poison", Kind = EffectKind.Debuff, TickHpPercent = -10 },
        new EffectDefinition { Id = "regen", Name = "regen", Kind = EffectKind.Buff, TickHpPercent = 10 },
    }.ToDictionary(e => e.Id);

    private static ActionDefinition A(string id) => new() { Id = id, Name = id, Power = 10 };

    private static readonly CombatCatalog Catalog = new(
        new[]
        {
            A("punch"),
            A("drain") with { Power = 1, MpCost = 50 },
            A("poison_hit") with { Effect = ActionEffect.None, Power = 0, Applies = [new EffectApplication("poison", 3)] },
            A("regen_self") with { Effect = ActionEffect.None, Power = 0, Side = TargetSide.Self, Applies = [new EffectApplication("regen", 3)] },
            A("focus") with { Effect = ActionEffect.RestoreMp, Power = 1000, Side = TargetSide.Self },
            A("spirit_bite") with { SummonOnly = true },
            A("call_spirit") with
            {
                Effect = ActionEffect.Summon, Power = 0,
                Summon = new SummonDefinition("spirit", Row.Back, new Stats(10, 10, 10, 10, 10), "spirit_bite"),
            },
        }.ToDictionary(a => a.Id),
        new Dictionary<string, SkillDefinition>()) { Effects = Effects };

    private static CombatantSetup Unit(string id, int str = 10, int vital = 100, int intel = 10, int speed = 10, params Tactic[] tactics) =>
        new(id, id, new Stats(str, 10, vital, intel, speed), Row.Front, null, null, SkillSet.NoSkills, tactics);

    private static Tactic T(int priority, string actionId, Condition condition = Condition.Always, int value = 0) =>
        new(priority, condition, value, actionId);

    private static CombatResult Run(CombatantSetup[] allies, CombatantSetup[] enemies, int maxActions = 20) =>
        CombatSimulator.Run(allies, enemies, Catalog, seed: 1, new CombatRules { MaxActions = maxActions });

    private static UnitSummary Of(CombatResult result, string id) => CombatSummary.From(result).Single(s => s.Id == id);

    [Fact]
    public void Counts_each_tactic_in_order_and_every_turn_and_action()
    {
        var result = Run(
            [Unit("a", tactics: [T(1, "poison_hit", Condition.MaxUses, 1), T(2, "punch"), T(3, "focus")])],
            [Unit("e", vital: 1000)]);

        var a = Of(result, "a");
        var used = result.Events.OfType<ActionUsed>().Count(u => u.ActorId == "a");
        Assert.Equal(result.Events.OfType<TurnStarted>().Count(t => t.ActorId == "a"), a.Turns);
        Assert.Equal(used, a.Actions);
        Assert.Equal([1, 2, 3], a.Tactics.Select(t => t.Tactic.Priority));
        Assert.Equal([1, used - 1, 0], a.Tactics.Select(t => t.Used));
        Assert.All(a.Tactics, t => Assert.Equal(0, t.BlockedTotal));
        Assert.Empty(a.LostTurns);

        var e = Of(result, "e");
        Assert.Equal(result.Events.OfType<TurnStarted>().Count(t => t.ActorId == "e"), e.LostTurns.Count);
        Assert.All(e.LostTurns, l => Assert.Equal((WaitReason.NoMatchingTactic, (int?)null), (l.Reason, l.TacticPriority)));
    }

    [Fact]
    public void A_tactic_that_cannot_be_paid_is_counted_as_blocked_with_the_turn_it_lost()
    {
        // MP 100으로 drain(MP 50)을 두 번 쓰고, 그 뒤로는 MP가 모자라 차례마다 막힌다.
        var result = Run([Unit("a", tactics: T(1, "drain"))], [Unit("e", vital: 1000)]);

        var a = Of(result, "a");
        var tactic = Assert.Single(a.Tactics);
        Assert.Equal(2, tactic.Used);
        Assert.Equal(a.Turns - 2, tactic.Blocked[WaitReason.NotEnoughResource]);
        Assert.Equal(Enumerable.Range(3, a.Turns - 2), a.LostTurns.Select(l => l.Turn));
        Assert.All(a.LostTurns, l => Assert.Equal((WaitReason.NotEnoughResource, (int?)1), (l.Reason, l.TacticPriority)));
    }

    [Fact]
    public void Damage_over_time_belongs_to_whoever_applied_it_and_counts_as_damage_taken()
    {
        // a는 첫 차례에 독만 건다 (피해 없음). 독은 e의 차례 3번 동안 최대 HP(2800)의 10%씩.
        var result = Run([Unit("a", tactics: T(1, "poison_hit", Condition.MaxUses, 1))], [Unit("e")]);

        var a = Of(result, "a");
        Assert.Equal(840, a.DamageDealt);
        Assert.Equal(840, a.DotDamage);
        Assert.Equal(840, Of(result, "e").DamageTaken);
        Assert.Equal(a.Turns - 1, a.LostTurns.Count); // 독을 건 뒤로는 맞는 전술이 없다
    }

    [Fact]
    public void Healing_includes_heal_over_time_and_restored_mp_is_counted()
    {
        var setup = Unit("a", intel: 10, tactics: [T(1, "regen_self", Condition.MaxUses, 1), T(2, "focus")]) with { StartHp = 100, StartMp = 0 };
        var result = Run([setup], [Unit("e")], maxActions: 10);

        var a = Of(result, "a");
        Assert.Equal(result.Events.OfType<EffectTicked>().Where(t => t.TargetId == "a").Sum(t => t.HpChange), a.Healing);
        Assert.True(a.Healing > 0);
        Assert.Equal(100, a.MpRestored); // 최대 MP까지만 채운다
    }

    [Fact]
    public void Summons_are_folded_into_their_owner()
    {
        var owner = Unit("a", tactics: [T(1, "call_spirit", Condition.MaxUses, 1), T(2, "punch")]);
        var result = Run([owner], [Unit("e", vital: 1000)], maxActions: 12);

        var spiritId = result.Events.OfType<Summoned>().Single().UnitId;
        var summary = CombatSummary.From(result);
        Assert.DoesNotContain(summary, s => s.Id == spiritId);

        // 이벤트를 차례 단위로 따라가며 행동한 유닛별 피해를 더한다.
        var dealt = new Dictionary<string, int>();
        string? actor = null;
        foreach (var e in result.Events)
        {
            actor = e switch { ActionUsed u => u.ActorId, TurnStarted or Waited => null, _ => actor };
            if (e is Damaged d && actor is not null)
            {
                dealt[actor] = dealt.GetValueOrDefault(actor) + d.Amount;
            }
        }

        var a = summary.Single(s => s.Id == "a");
        Assert.True(dealt[spiritId] > 0);
        Assert.Equal(dealt[spiritId], a.SummonDamage);
        Assert.Equal(dealt["a"], a.DamageDealt);
    }

    [Fact]
    public void Records_the_combat_turn_on_which_a_unit_went_down()
    {
        var result = Run([Unit("a", str: 100, vital: 1000, tactics: T(1, "punch"))], [Unit("e", vital: 1, tactics: T(1, "punch"))]);

        var events = result.Events.ToList();
        var died = events.FindIndex(e => e is Died { UnitId: "e" });
        var turn = events.Take(died).OfType<TurnStarted>().Last().ActionNumber;
        Assert.Equal(turn, Of(result, "e").DownedAt);
        Assert.Null(Of(result, "a").DownedAt);
        Assert.Equal(Of(result, "e").DamageTaken, Of(result, "a").DamageDealt);
    }
}
