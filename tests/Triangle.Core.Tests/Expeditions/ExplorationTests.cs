using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Expeditions;

public class ExplorationTests
{
    private static readonly GameData Data = GameDataLoader.Parse(
        """[ { "id": "sword", "name": "검", "kind": "Weapon" } ]""",
        "[]",
        """
        [ { "id": "strike", "name": "공격", "power": 10, "universal": true },
          { "id": "focus", "name": "정신 집중", "effect": "RestoreMp", "power": 10, "universal": true } ]
        """,
        """
        [ { "id": "dogs", "name": "들개", "units": [ { "id": "d", "name": "들개", "row": "Front",
            "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 10 },
            "tactics": [ { "priority": 2, "condition": "Always", "value": 0, "actionId": "strike" },
                         { "priority": 1, "condition": "SelfMpAtMost", "value": 10, "actionId": "focus" },
                         { "priority": 3, "condition": "Always", "value": 0, "actionId": "strike" } ] } ] },
          { "id": "thieves", "name": "도적", "units": [ { "id": "t", "name": "도적", "row": "Front",
            "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 10 } } ] },
          { "id": "boss", "name": "보스", "units": [ { "id": "b", "name": "보스", "row": "Front",
            "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 10 } } ] } ]
        """,
        itemsJson: """[ { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword" } ]""",
        zonesJson: """
        [ { "id": "outer", "name": "바깥", "difficulty": 1, "maxBattles": 2,
            "encounters": [ { "encounterId": "dogs", "weight": 1 }, { "encounterId": "thieves", "weight": 1 } ],
            "rewards": { "goldMin": 10, "goldMax": 10 },
            "boss": { "encounterId": "boss" } } ]
        """);

    private static Company NewCompany()
    {
        var member = new PartyMember("a", "A", new Stats(10, 10, 20, 10, 10), Row.Front, TestGear.Of(mainHand: "old_sword"),
            new Dictionary<string, int>(), new Dictionary<string, int>(), [[new Tactic(1, Condition.Always, 0, "strike")]]);
        return new Company([member], ["a"], 0, new Dictionary<string, int>(), activeTacticSet: 0, nextSeed: 42);
    }

    private static CombatResult Result(CombatOutcome outcome, Company company, int hp)
    {
        var setup = company.Member("a").ToCombatantSetup(Data, 0);
        var ally = new Combatant(setup, CombatSide.Ally, CombatRules.Default, new SkillSet(setup.Skills, Data.Skills)) { Hp = hp };
        return new CombatResult(outcome, 0, [], [ally]);
    }

    [Fact]
    public void Zone_entries_list_encounters_then_the_boss()
    {
        Assert.Equal(
            [new ZoneEntry("dogs", false), new ZoneEntry("thieves", false), new ZoneEntry("boss", true)],
            Exploration.Entries(Data.Zones["outer"]));
    }

    [Fact]
    public void Fought_encounters_are_recorded_win_or_lose()
    {
        var company = NewCompany();
        var expedition = ExpeditionRules.Start(company, Data, "outer");
        var first = ExpeditionRules.NextEncounter(expedition, Data);
        Assert.Equal((0, 3), Exploration.Progress(company, Data.Zones["outer"]));

        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, 50));

        Assert.True(expedition.LastBattle!.FirstMeet);
        Assert.Equal(new EncounterRecord(1, 1), company.EncounterRecords[first]);

        // 보스에게 진다: 져도 만난 것이다.
        Assert.NotNull(ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Defeat, company, 0)));
        Assert.Equal(new EncounterRecord(1, 0), company.EncounterRecords["boss"]);
        Assert.Equal((2, 3), Exploration.Progress(company, Data.Zones["outer"]));
    }

    [Fact]
    public void Meeting_again_is_not_a_first_meet()
    {
        var company = NewCompany();
        var expedition = ExpeditionRules.Start(company, Data, "outer");
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, 50));
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, 50));

        expedition = ExpeditionRules.Start(company, Data, "outer");
        expedition.BattleIndex = 1;
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, 50));

        Assert.False(expedition.LastBattle!.FirstMeet);
        Assert.Equal(new EncounterRecord(2, 2), company.EncounterRecords["boss"]);
    }

    [Fact]
    public void Unit_actions_follow_tactic_priority_once_each()
    {
        Assert.Equal(["focus", "strike"], Exploration.UnitActions(Data.Encounters["dogs"].Units[0]));
    }
}
