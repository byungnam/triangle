using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Skills;

public class SkillTests
{
    private static SkillDefinition Skill(string id, BonusKind kind, int percent, string? tag = null, int rank = 1) => new()
    {
        Id = id, Name = id, Rank = rank, Bonuses = [new SkillBonus(kind, percent, tag)],
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

    private static readonly CombatCatalog Catalog = new(
        new[] { Shot, Punch, Costly, Mend, Locked }.ToDictionary(a => a.Id), Skills);

    private static Dictionary<string, int> Levels(params (string Id, int Level)[] levels) =>
        levels.ToDictionary(l => l.Id, l => l.Level);

    private static CombatantSetup Unit(string id, Dictionary<string, int>? skills = null, int str = 10, int vital = 20, params Tactic[] tactics) =>
        new(id, id, new Stats(str, 10, vital, 10, 10), Row.Front, skills ?? [], tactics);

    private static Tactic Always(string actionId) => new(1, Condition.Always, 0, actionId);

    private static CombatResult Run(CombatantSetup ally, CombatantSetup enemy, int maxActions = 100) =>
        CombatSimulator.Run([ally], [enemy], Catalog, seed: 1, new CombatRules { MaxActions = maxActions });

    // ── 진행 표 ────────────────────────────────────────────

    [Theory]
    [InlineData(1, 1, 250)]
    [InlineData(1, 2, 1_415)]
    [InlineData(1, 5, 256_000)]
    [InlineData(3, 4, 135_765)]
    [InlineData(2, 0, 0)]
    public void SpForLevel_follows_eve_table_times_rank(int rank, int level, int sp)
    {
        Assert.Equal(sp, SkillProgression.SpForLevel(rank, level));
    }

    [Theory]
    [InlineData(2, 0, 0)]
    [InlineData(2, 499, 0)]
    [InlineData(2, 500, 1)]
    [InlineData(2, 2_829, 1)]
    [InlineData(2, 2_830, 2)]
    [InlineData(1, 10_000_000, 5)]
    public void LevelFor_is_the_highest_level_reached(int rank, int sp, int level)
    {
        Assert.Equal(level, SkillProgression.LevelFor(rank, sp));
    }

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
    public void Locked_actions_and_unknown_skills_are_rejected_before_combat()
    {
        Assert.Throws<ArgumentException>(() => Run(Unit("a", Levels(("archery", 2)), tactics: Always("locked")), Unit("e")));
        Assert.Throws<ArgumentException>(() => Run(Unit("a", Levels(("no_such_skill", 1))), Unit("e")));

        var ok = Run(Unit("a", Levels(("archery", 3)), tactics: Always("locked")), Unit("e"), maxActions: 2);
        Assert.Contains(ok.Events, e => e is ActionUsed { ActionId: "locked" });
    }
}
