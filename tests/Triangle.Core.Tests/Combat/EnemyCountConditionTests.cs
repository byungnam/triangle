using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Combat;

public class EnemyCountConditionTests
{
    private static readonly ActionDefinition Mark = new() { Id = "mark", Name = "mark", Effect = ActionEffect.None, Side = TargetSide.Self };
    private static readonly ActionDefinition Punch = new() { Id = "punch", Name = "punch", Power = 10 };

    private static readonly CombatCatalog Catalog = new(
        new[] { Mark, Punch }.ToDictionary(a => a.Id), new Dictionary<string, SkillDefinition>());

    private static CombatantSetup Unit(string id, Row row = Row.Front, int speed = 10, int? startHp = null, params Tactic[] tactics) =>
        new(id, id, new Stats(10, 10, 20, 10, speed), row, null, null, SkillSet.NoSkills, tactics, StartHp: startHp);

    /// <summary>
    /// 적: 전열 2명, 후열 1명 (모두 HP 1). killFirst면 빠른 아군 k가 먼저 한 번 때려 한 명을 쓰러뜨린다.
    /// a의 첫 행동이 "mark"(조건 참)인지 본다.
    /// </summary>
    private static bool FirstChoiceIsMark(Condition condition, int value, bool killFirst = false)
    {
        var a = Unit("a", tactics: [new Tactic(1, condition, value, "mark"), new Tactic(2, Condition.Always, 0, "punch")]);
        var allies = new List<CombatantSetup> { a };
        if (killFirst)
        {
            allies.Add(Unit("k", speed: 1000, tactics: [new Tactic(1, Condition.MaxUses, 1, "punch"), new Tactic(2, Condition.Always, 0, "mark")]));
        }

        CombatantSetup[] enemies = [Unit("e1", startHp: 1), Unit("e2", startHp: 1), Unit("e3", Row.Back, startHp: 1)];
        var result = CombatSimulator.Run(allies, enemies, Catalog, seed: 1, new CombatRules { MaxActions = 6 });
        return result.Events.OfType<ActionUsed>().First(u => u.ActorId == "a").ActionId == "mark";
    }

    [Theory]
    [InlineData(Condition.EnemyAliveAtLeast, 3, true)]
    [InlineData(Condition.EnemyAliveAtLeast, 4, false)]
    [InlineData(Condition.EnemyAliveAtMost, 3, true)]
    [InlineData(Condition.EnemyAliveAtMost, 2, false)]
    [InlineData(Condition.EnemyDeadAtLeast, 1, false)]
    [InlineData(Condition.EnemyDeadAtMost, 0, true)]
    [InlineData(Condition.EnemyFrontAtLeast, 2, true)]
    [InlineData(Condition.EnemyFrontAtMost, 1, false)]
    [InlineData(Condition.EnemyBackAtLeast, 1, true)]
    [InlineData(Condition.EnemyBackAtMost, 0, false)]
    public void Counts_living_enemies_by_row(Condition condition, int value, bool expected)
    {
        Assert.Equal(expected, FirstChoiceIsMark(condition, value));
    }

    [Theory]
    [InlineData(Condition.EnemyDeadAtLeast, 1, true)]
    [InlineData(Condition.EnemyDeadAtLeast, 2, false)]
    [InlineData(Condition.EnemyDeadAtMost, 0, false)]
    [InlineData(Condition.EnemyAliveAtMost, 2, true)]
    [InlineData(Condition.EnemyAliveAtLeast, 3, false)]
    public void Counts_fallen_enemies(Condition condition, int value, bool expected)
    {
        Assert.Equal(expected, FirstChoiceIsMark(condition, value, killFirst: true));
    }

}
