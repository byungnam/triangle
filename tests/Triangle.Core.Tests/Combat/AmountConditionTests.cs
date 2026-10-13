using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Combat;

public class AmountConditionTests
{
    private static readonly ActionDefinition Mark = new() { Id = "mark", Name = "mark", Effect = ActionEffect.None, Side = TargetSide.Self };
    private static readonly ActionDefinition Punch = new() { Id = "punch", Name = "punch", Power = 10 };

    private static readonly CombatCatalog Catalog = new(
        new[] { Mark, Punch }.ToDictionary(a => a.Id), new Dictionary<string, SkillDefinition>());

    /// <summary>vital 20 → HP 560, intel 10 → MP 100.</summary>
    private static CombatantSetup Unit(string id, int vital = 20, params Tactic[] tactics) =>
        new(id, id, new Stats(10, 10, vital, 10, 10), Row.Front, null, null, SkillSet.NoSkills, tactics);

    /// <summary>a의 첫 행동이 "mark"(조건 참)인지 "punch"(조건 거짓, 다음 전술)인지 본다.</summary>
    private static bool FirstChoiceIsMark(Condition condition, int value, params CombatantSetup[] extraAllies)
    {
        var a = Unit("a", tactics: [new Tactic(1, condition, value, "mark"), new Tactic(2, Condition.Always, 0, "punch")]);
        var result = CombatSimulator.Run([a, .. extraAllies], [Unit("e", vital: 1000)], Catalog, seed: 1, new CombatRules { MaxActions = 6 });
        return result.Events.OfType<ActionUsed>().First(u => u.ActorId == "a").ActionId == "mark";
    }

    [Theory]
    [InlineData(Condition.SelfHpAmountAtLeast, 560, true)]
    [InlineData(Condition.SelfHpAmountAtLeast, 561, false)]
    [InlineData(Condition.SelfHpAmountAtMost, 560, true)]
    [InlineData(Condition.SelfHpAmountAtMost, 559, false)]
    [InlineData(Condition.SelfMpAmountAtLeast, 100, true)]
    [InlineData(Condition.SelfMpAmountAtMost, 99, false)]
    public void Self_amount_conditions_compare_actual_values(Condition condition, int value, bool expected)
    {
        Assert.Equal(expected, FirstChoiceIsMark(condition, value));
    }

    [Fact]
    public void Any_ally_amount_conditions_look_at_every_living_ally()
    {
        // b의 HP는 280 (vital 10). 아군 누군가 HP 300 이하 → 참.
        Assert.True(FirstChoiceIsMark(Condition.AnyAllyHpAmountAtMost, 300, Unit("b", vital: 10)));
        Assert.False(FirstChoiceIsMark(Condition.AnyAllyHpAmountAtMost, 279, Unit("b", vital: 10)));
        Assert.True(FirstChoiceIsMark(Condition.AnyAllyHpAmountAtLeast, 560, Unit("b", vital: 10)));
    }

    [Fact]
    public void Average_amount_conditions_are_exact_at_the_boundary()
    {
        // HP 560과 280의 평균은 정확히 420.
        Assert.True(FirstChoiceIsMark(Condition.AllyAverageHpAmountAtMost, 420, Unit("b", vital: 10)));
        Assert.True(FirstChoiceIsMark(Condition.AllyAverageHpAmountAtLeast, 420, Unit("b", vital: 10)));
        Assert.False(FirstChoiceIsMark(Condition.AllyAverageHpAmountAtLeast, 421, Unit("b", vital: 10)));
        // MP는 둘 다 100 → 평균 100.
        Assert.False(FirstChoiceIsMark(Condition.AllyAverageMpAmountAtMost, 99, Unit("b", vital: 10)));
    }

    [Fact]
    public void Amount_values_are_not_range_checked_but_percents_are()
    {
        static string Encounter(string condition, int value) => $$"""
            [ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front",
                "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 },
                "tactics": [ { "priority": 1, "condition": "{{condition}}", "value": {{value}}, "actionId": "punch" } ] } ] } ]
            """;
        const string actions = """[ { "id": "punch", "name": "주먹", "universal": true } ]""";

        GameDataLoader.Parse("[]", "[]", actions, Encounter("SelfHpAmountAtMost", 99_999));
        GameDataLoader.Parse("[]", "[]", actions, Encounter("AllyAverageMpAmountAtLeast", -5));

        var error = Assert.Single(Assert.Throws<GameDataException>(() =>
            GameDataLoader.Parse("[]", "[]", actions, Encounter("SelfHpAtMost", 150))).Errors);
        Assert.Contains("value must be a percent 0-100", error);
    }

    [Fact]
    public void Every_condition_is_classified_as_at_most_one_value_kind()
    {
        foreach (var condition in Enum.GetValues<Condition>())
        {
            Assert.False(condition.IsPercent() && condition.IsAmount(), condition.ToString());
        }

        Assert.Equal(12, Enum.GetValues<Condition>().Count(c => c.IsPercent()));
        Assert.Equal(12, Enum.GetValues<Condition>().Count(c => c.IsAmount()));
    }
}
