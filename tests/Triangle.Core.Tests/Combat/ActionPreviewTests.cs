using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Items;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Combat;

/// <summary>전투 밖의 미리보기가 전투 엔진과 같은 값을 내는가.</summary>
public class ActionPreviewTests
{
    private static readonly ActionDefinition Bolt = new()
    {
        Id = "bolt", Name = "bolt", Power = 40, MpCost = 20, Delay = 1500, DamageType = DamageType.Magical, Tags = ["fire"],
    };

    private static readonly ActionDefinition Mend = new()
    {
        Id = "mend", Name = "mend", Power = 30, Effect = ActionEffect.Heal, Side = TargetSide.Self, Tags = ["fire"],
    };

    private static readonly CombatCatalog Catalog = new(
        new[] { Bolt, Mend }.ToDictionary(a => a.Id),
        new Dictionary<string, SkillDefinition>
        {
            ["pyro"] = new()
            {
                Id = "pyro", Name = "pyro", Mastery = "fire",
                Bonuses = [new SkillBonus(BonusKind.PowerPercent, 5, "fire"), new SkillBonus(BonusKind.MpCostReductionPercent, 10, "fire")],
            },
            ["haste"] = new() { Id = "haste", Name = "haste", Mastery = "fire", Bonuses = [new SkillBonus(BonusKind.DelayReductionPercent, 7)] },
        });

    private static CombatantSetup Caster(string action) =>
        new("a", "a", new Stats(12, 0, 50, 17, 13), Row.Back, "fire", null,
            new Dictionary<string, int> { ["pyro"] = 3, ["haste"] = 2 },
            [new Tactic(1, Condition.Always, 0, action)])
        {
            PowerMultiplierPercent = 200,
            ItemBonuses = [new ItemBonus(BonusKind.PowerPercent, 8, "fire"), new ItemBonus(BonusKind.MaxHpPercent, 10)],
        };

    // 방어가 0인 과녁: 미리 본 피해가 그대로 들어간다.
    private static CombatantSetup Dummy() => new("e", "e", new Stats(0, 0, 500, 0, 1), Row.Front, null, null, SkillSet.NoSkills, []);

    [Fact]
    public void Damage_cost_and_delay_match_the_simulator()
    {
        var caster = Caster("bolt");
        var preview = ActionPreview.For(caster, Bolt, Catalog);
        var result = CombatSimulator.Run([caster], [Dummy()], Catalog, seed: 3, new CombatRules { MaxActions = 6 });

        var hit = result.Events.OfType<Damaged>().First();
        Assert.Equal(preview.Amount, hit.Amount);

        var used = result.Events.OfType<ActionUsed>().First();
        var maxMp = CombatRules.Default.MaxMp(caster.Stats, new SkillSet(caster.Skills, Catalog.Skills, caster.ItemBonuses));
        Assert.Equal(preview.MpCost, maxMp - used.ActorMp);

        var turns = result.Events.OfType<TurnStarted>().Where(t => t.ActorId == "a").Take(2).ToList();
        Assert.Equal(preview.Delay, turns[1].Time - turns[0].Time);
        Assert.True(preview.WeaponBoosted);
    }

    [Fact]
    public void Heal_matches_the_simulator()
    {
        var caster = Caster("mend") with { StartHp = 1 };
        var preview = ActionPreview.For(caster, Mend, Catalog);
        var result = CombatSimulator.Run([caster], [Dummy()], Catalog, seed: 3, new CombatRules { MaxActions = 4 });

        Assert.Equal(preview.Amount, result.Events.OfType<Healed>().First().Amount);
    }

    [Fact]
    public void Unit_preview_reads_stats_and_bonuses()
    {
        var unit = UnitPreview.For(Caster("bolt"), Catalog);

        Assert.Equal(50 * 28 * 110 / 100, unit.MaxHp);
        Assert.Equal(160, unit.PhysicalPowerPercent);
        Assert.Equal(185, unit.MagicPowerPercent);
        Assert.Equal(1000 / 13 * 86 / 100, unit.BaseDelay);
        Assert.Equal(12, unit.Defense);
        Assert.Equal(19, unit.PhysicalReductionPercent); // 100 / 1.24 = 80.6 → 81
    }
}
