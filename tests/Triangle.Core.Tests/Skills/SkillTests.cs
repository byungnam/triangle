using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Items;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Skills;

public class SkillTests
{
    private static SkillDefinition Skill(string id, BonusKind kind, int percent, string? tag = null, int rank = 1) => new()
    {
        Id = id, Name = id, Mastery = "any", Rank = rank, Bonuses = [new SkillBonus(kind, percent, tag)],
    };

    private static readonly Dictionary<string, SkillDefinition> Skills = new[]
    {
        Skill("archery", BonusKind.PowerPercent, 5, "bow"),
        Skill("rapid_fire", BonusKind.DelayReductionPercent, 10, "bow"),
        Skill("mana_efficiency", BonusKind.MpCostReductionPercent, 10),
        Skill("healing", BonusKind.HealPercent, 10),
        Skill("endurance", BonusKind.MaxHpPercent, 5),
        Skill("defense", BonusKind.DamageTakenReductionPercent, 10),
    }.ToDictionary(s => s.Id);

    private static readonly ActionDefinition Shot = new() { Id = "shot", Name = "Shot", Power = 10, Tags = ["bow"] };
    private static readonly ActionDefinition Punch = new() { Id = "punch", Name = "Punch", Power = 10 };
    private static readonly ActionDefinition Costly = new() { Id = "costly", Name = "Costly", Power = 10, MpCost = 100 };
    private static readonly ActionDefinition Mend = new()
    {
        Id = "mend", Name = "Mend", Effect = ActionEffect.Heal, Power = 100, Side = TargetSide.Self,
    };
    private static readonly ActionDefinition Locked = new()
    {
        Id = "locked", Name = "Locked", Power = 10, Requirements = [new SkillRequirement("archery", 3)],
    };
    private static readonly ActionDefinition BowOnly = new() { Id = "bow_only", Name = "BowOnly", Power = 10 };

    private static readonly CombatCatalog Catalog = new(
        new[] { Shot, Punch, Costly, Mend, Locked, BowOnly }.ToDictionary(a => a.Id), Skills);

    private static Dictionary<string, int> Levels(params (string Id, int Level)[] levels) =>
        levels.ToDictionary(l => l.Id, l => l.Level);

    private static CombatantSetup Unit(
        string id, Dictionary<string, int>? skills = null, int str = 10, int vital = 20, string? weapon = null, params Tactic[] tactics) =>
        new(id, id, new Stats(str, 10, vital, 10, 10), Row.Front, weapon, null, skills ?? [], tactics);

    private static Tactic Always(string actionId) => new(1, Condition.Always, 0, actionId);

    private static CombatResult Run(CombatantSetup ally, CombatantSetup enemy, int maxActions = 100) =>
        CombatSimulator.Run([ally], [enemy], Catalog, seed: 1, new CombatRules { MaxActions = maxActions });

    // ── 보너스 합계, 요구 조건 ─────────────────────────────

    [Fact]
    public void Bonus_multiplies_by_level_and_respects_tags()
    {
        var set = new SkillSet(Levels(("archery", 4), ("mana_efficiency", 2)), Skills);

        Assert.Equal(20, set.Bonus(BonusKind.PowerPercent, ["bow"]));
        Assert.Equal(0, set.Bonus(BonusKind.PowerPercent, ["melee"]));
        Assert.Equal(0, set.Bonus(BonusKind.PowerPercent));
        Assert.Equal(20, set.Bonus(BonusKind.MpCostReductionPercent, ["anything"]));
    }

    [Fact]
    public void Requirements_compare_levels()
    {
        var set = new SkillSet(Levels(("archery", 2)), Skills);
        var requirements = new[] { new SkillRequirement("archery", 3), new SkillRequirement("healing", 1) };

        Assert.False(set.Meets(requirements));
        Assert.Equal(requirements, set.Missing(requirements));
        Assert.True(set.Meets([new SkillRequirement("archery", 2)]));
    }

    // ── 전투에 적용 ────────────────────────────────────────

    [Fact]
    public void Power_bonus_applies_only_to_tagged_actions()
    {
        // 위력 10 × 1.5(근력 10) = 15, 활 숙련 4 → +20% = 18. 방어 0.
        var shot = Run(Unit("a", Levels(("archery", 4)), tactics: Always("shot")), Unit("e", str: 0, vital: 100));
        var punch = Run(Unit("a", Levels(("archery", 4)), tactics: Always("punch")), Unit("e", str: 0, vital: 100));

        Assert.Equal(18, shot.Events.OfType<Damaged>().First().Amount);
        Assert.Equal(15, punch.Events.OfType<Damaged>().First().Amount);
    }

    [Fact]
    public void Damage_taken_reduction_applies_after_defense()
    {
        // 15 × (100 − 30)% = 10.5 → 11
        var result = Run(Unit("a", tactics: Always("punch")), Unit("e", Levels(("defense", 3)), str: 0, vital: 100));

        Assert.Equal(11, result.Events.OfType<Damaged>().First().Amount);
    }

    [Fact]
    public void Mp_cost_reduction_lowers_the_cost_paid()
    {
        var result = Run(Unit("a", Levels(("mana_efficiency", 5)), tactics: Always("costly")), Unit("e", vital: 100));

        var use = result.Events.OfType<ActionUsed>().First(u => u.ActorId == "a");
        var maxMp = result.Combatants.Single(c => c.Id == "a").MaxMp;
        Assert.Equal(maxMp - 50, use.ActorMp);
    }

    [Fact]
    public void Heal_bonus_increases_healing()
    {
        // 위력 100 × 1.5(지능 10) = 150, 치유술 2 → +20% = 180. HP 상한에 걸리지 않게 먼저 맞는다.
        var healer = Unit("a", Levels(("healing", 2)), vital: 100, tactics: [new Tactic(1, Condition.SelfHpAtMost, 99, "mend")]);
        var result = Run(healer, Unit("e", str: 1000, vital: 1000, tactics: Always("punch")));

        Assert.Equal(180, result.Events.OfType<Healed>().First().Amount);
    }

    [Fact]
    public void Max_hp_bonus_raises_max_hp()
    {
        var result = Run(Unit("a", Levels(("endurance", 4))), Unit("e"), maxActions: 1);

        Assert.Equal(20 * 28 * 120 / 100, result.Combatants.Single(c => c.Id == "a").MaxHp);
    }

    [Fact]
    public void Delay_reduction_applies_after_tagged_actions()
    {
        // 속사 5 → 활 행동 뒤 대기 −50%. 같은 속도의 상대보다 두 배 자주 행동한다.
        var result = Run(Unit("a", Levels(("rapid_fire", 5)), tactics: Always("shot")), Unit("e", str: 0, vital: 1000), maxActions: 30);

        var turns = result.Events.OfType<TurnStarted>().GroupBy(t => t.ActorId).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(20, turns["a"]);
        Assert.Equal(10, turns["e"]);
    }

    [Fact]
    public void Weapon_bonus_tags_apply_to_generic_actions_and_granted_actions_gate_actions()
    {
        // 활을 들면 기본 공격(태그 없음)에도 "활 위력" 보너스가 붙는다.
        var armed = Run(Unit("a", Levels(("archery", 4)), weapon: "bow", tactics: Always("punch")), Unit("e", str: 0, vital: 100));
        Assert.Equal(18, armed.Events.OfType<Damaged>().First().Amount);

        // 아이템에서 고른 행동만 쓸 수 있다 (GrantedActions가 null이면 적처럼 제한이 없다).
        var notGranted = Unit("a", weapon: "bow", tactics: Always("bow_only")) with { GrantedActions = new HashSet<string> { "shot" } };
        Assert.Throws<ArgumentException>(() => Run(notGranted, Unit("e")));
        var granted = notGranted with { GrantedActions = new HashSet<string> { "bow_only" } };
        var ok = Run(granted, Unit("e"), maxActions: 2);
        Assert.Contains(ok.Events, e => e is ActionUsed { ActionId: "bow_only" });

        // 공용 행동은 고르지 않아도 쓴다.
        var catalog = Catalog with { Actions = Catalog.Actions.Values.Select(a => a.Id == "punch" ? a with { Universal = true } : a).ToDictionary(a => a.Id) };
        var universal = Unit("a", tactics: Always("punch")) with { GrantedActions = new HashSet<string>() };
        Assert.IsType<CombatEnded>(CombatSimulator.Run([universal], [Unit("e")], catalog, seed: 1, new CombatRules { MaxActions = 2 }).Events[^1]);
    }

    [Fact]
    public void Item_bonuses_add_to_skill_bonuses()
    {
        // 아이템 위력 +20%는 활 숙련 4(활 태그 +20%)를 활로 쓴 것과 같다.
        var skill = Run(Unit("a", Levels(("archery", 4)), weapon: "bow", tactics: Always("punch")), Unit("e", str: 0, vital: 100));
        var item = Run(Unit("a", tactics: Always("punch")) with { ItemBonuses = [new ItemBonus(BonusKind.PowerPercent, 20)] }, Unit("e", str: 0, vital: 100));
        Assert.Equal(skill.Events.OfType<Damaged>().First().Amount, item.Events.OfType<Damaged>().First().Amount);

        // 최대 HP %.
        var sturdy = Run(Unit("a") with { ItemBonuses = [new ItemBonus(BonusKind.MaxHpPercent, 20)] }, Unit("e"), maxActions: 1);
        Assert.Equal(20 * 28 * 120 / 100, sturdy.Combatants.Single(c => c.Id == "a").MaxHp);
    }

    [Fact]
    public void Defense_percent_from_armor_reduces_damage_taken()
    {
        int Taken(int defensePercent)
        {
            var target = Unit("e", str: 20, vital: 100) with { ItemBonuses = [new ItemBonus(BonusKind.DefensePercent, defensePercent)] };
            return Run(Unit("a", str: 30, tactics: Always("punch")), target, maxActions: 2)
                .Events.OfType<Damaged>().First(d => d.TargetId == "e").Amount;
        }

        // 방어 20 → 40: 피해 × 100 / (100 + 방어 × 2).
        Assert.True(Taken(100) < Taken(0));
        Assert.Equal(Ratio.DivideRounded((long)Taken(0) * 140, 180), Taken(100));
    }

    [Fact]
    public void Locked_actions_and_unknown_skills_are_rejected_before_combat()
    {
        Assert.Throws<ArgumentException>(() => Run(Unit("a", Levels(("archery", 2)), tactics: Always("locked")), Unit("e")));
        Assert.Throws<ArgumentException>(() => Run(Unit("a", Levels(("no_such_skill", 1))), Unit("e")));

        var ok = Run(Unit("a", Levels(("archery", 3)), tactics: Always("locked")), Unit("e"), maxActions: 2);
        Assert.Contains(ok.Events, e => e is ActionUsed { ActionId: "locked" });
    }
}
