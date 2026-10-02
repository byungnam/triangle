using System.Text.Json;
using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Data;

public class GameDataLoaderTests
{
    private const string Skills = """
        [
          { "id": "archery", "name": "활 숙련", "group": "궁술", "primary": "Dex", "secondary": "Str",
            "bonuses": [ { "kind": "PowerPercent", "percentPerLevel": 5, "tag": "bow" } ] },
          { "id": "precision", "name": "정밀 사격", "rank": 3,
            "prerequisites": [ { "skillId": "archery", "level": 4 } ] }
        ]
        """;

    private const string Actions = """
        [
          { "id": "strike", "name": "공격", "power": 10, "rule": "FrontFirst", "tags": [ "melee" ] },
          { "id": "shot", "name": "화살", "power": 10, "tags": [ "bow" ],
            "requirements": [ { "skillId": "archery", "level": 1 } ] },
          { "id": "heal", "name": "치료", "effect": "Heal", "power": 10, "side": "Ally", "rule": "LowestHpRatio", "mpCost": 5 }
        ]
        """;

    private static string Encounters(
        string tactics = """{ "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" }""",
        string skills = "{}") => $$"""
        [
          {
            "id": "camp",
            "name": "야영지",
            "units": [
              {
                "id": "e1", "name": "적", "row": "Back",
                "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 },
                "skills": {{skills}},
                "tactics": [ {{tactics}} ]
              }
            ]
          }
        ]
        """;

    private static GameDataException Fails(string skills, string actions, string encounters) =>
        Assert.Throws<GameDataException>(() => GameDataLoader.Parse(skills, actions, encounters));

    [Fact]
    public void Shipped_data_files_are_valid_and_playable()
    {
        var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

        Assert.NotEmpty(data.Skills);
        Assert.NotEmpty(data.Actions);
        Assert.NotEmpty(data.Encounters);

        // 모든 적 팀이 실제 전투에 들어갈 수 있어야 한다.
        var basic = data.Actions.Values.First(a => a.Requirements.Count == 0);
        var ally = new CombatantSetup(
            "ally", "아군", new Stats(15, 15, 25, 20, 13), Row.Front, SkillSet.NoSkills,
            [new Tactic(1, Condition.Always, 0, basic.Id)]);
        foreach (var id in data.Encounters.Keys)
        {
            var result = CombatSimulator.Run([ally], data.CreateEncounterTeam(id), data.Catalog, seed: 1);
            Assert.IsType<CombatEnded>(result.Events[^1]);
        }
    }

    [Fact]
    public void Parses_skills_actions_and_encounters()
    {
        var data = GameDataLoader.Parse(Skills, Actions, Encounters(skills: """{ "archery": 2 }"""));

        var archery = data.Skills["archery"];
        Assert.Equal((Stat.Dex, Stat.Str, 1), (archery.Primary, archery.Secondary, archery.Rank));
        Assert.Equal(new SkillBonus(BonusKind.PowerPercent, 5, "bow"), Assert.Single(archery.Bonuses));
        Assert.Equal(new SkillRequirement("archery", 4), Assert.Single(data.Skills["precision"].Prerequisites));

        var heal = data.Actions["heal"];
        Assert.Equal(ActionEffect.Heal, heal.Effect);
        Assert.Equal(TargetSide.Ally, heal.Side);
        Assert.Equal(5, heal.MpCost);
        Assert.Equal(TargetScope.Single, heal.Scope); // 생략하면 기본값
        Assert.Equal(["bow"], data.Actions["shot"].Tags);

        var unit = Assert.Single(data.CreateEncounterTeam("camp"));
        Assert.Equal("적", unit.Name);
        Assert.Equal(Row.Back, unit.Row);
        Assert.Equal(2, unit.Skills["archery"]);
        Assert.Equal(new Tactic(1, Condition.Always, 0, "strike"), Assert.Single(unit.Tactics));
    }

    [Fact]
    public void Serialized_data_round_trips_with_readable_korean()
    {
        var data = GameDataLoader.Parse(Skills, Actions, Encounters());
        var json = JsonSerializer.Serialize(data.Actions.Values.ToList(), GameDataJson.Options);

        Assert.Contains("\"name\": \"치료\"", json);
        Assert.Contains("\"rule\": \"LowestHpRatio\"", json);

        var again = GameDataLoader.Parse(Skills, json, Encounters());
        Assert.Equal(data.Actions["heal"].Name, again.Actions["heal"].Name);
        Assert.Equal(data.Actions["shot"].Requirements, again.Actions["shot"].Requirements);
    }

    [Fact]
    public void Reports_invalid_json_with_file_and_line()
    {
        var error = Assert.Single(Fails(Skills, "[\n  { \"id\": \"x\",\n    \"name\": \"y\", \"rule\": \"Sideways\" }\n]", Encounters()).Errors);

        Assert.StartsWith("actions.json line 3:", error);
    }

    [Fact]
    public void Rejects_unknown_properties_so_typos_are_not_ignored()
    {
        var error = Assert.Single(Fails(Skills, """[ { "id": "x", "name": "y", "powr": 10 } ]""", Encounters()).Errors);

        Assert.StartsWith("actions.json", error);
        Assert.Contains("powr", error);
    }

    [Fact]
    public void Rejects_missing_required_properties_and_numeric_enums()
    {
        Assert.StartsWith("skills.json", Assert.Single(Fails("""[ { "name": "이름만" } ]""", Actions, Encounters()).Errors));
        Assert.StartsWith("actions.json", Assert.Single(Fails(Skills, """[ { "id": "x" } ]""", Encounters()).Errors));
        Fails(Skills, """[ { "id": "x", "name": "y", "rule": 1 } ]""", Encounters());
    }

    [Fact]
    public void Reports_every_reference_and_range_error_together()
    {
        var actions = """
            [
              { "id": "strike", "name": "공격", "power": 10 },
              { "id": "strike", "name": "중복", "mpCost": -1 },
              { "id": "spell", "name": "주문", "requirements": [ { "skillId": "ghost_skill", "level": 9 } ] }
            ]
            """;
        var tactics = """
            { "priority": 1, "condition": "SelfHpAtMost", "value": 150, "actionId": "strike" },
            { "priority": 2, "condition": "EveryNthTurn", "value": 0, "actionId": "strike" },
            { "priority": 3, "condition": "Always", "value": 0, "actionId": "missing" }
            """;

        var errors = Fails(Skills, actions, Encounters(tactics)).Errors;

        Assert.Contains("actions.json: duplicate id 'strike'", errors);
        Assert.Contains(errors, e => e.StartsWith("actions.json 'strike': mpCost must not be negative"));
        Assert.Contains("actions.json 'spell' requirement: requires unknown skill 'ghost_skill'", errors);
        Assert.Contains(errors, e => e.StartsWith("actions.json 'spell' requirement: required level of 'ghost_skill' must be 1-5"));
        Assert.Contains(errors, e => e.StartsWith("encounters.json 'camp' unit 'e1' tactic 1: SelfHpAtMost value must be a percent"));
        Assert.Contains(errors, e => e.StartsWith("encounters.json 'camp' unit 'e1' tactic 2: EveryNthTurn value must be positive"));
        Assert.Contains("encounters.json 'camp' unit 'e1' tactic 3: unknown action 'missing'", errors);
        Assert.Equal(7, errors.Count);
    }

    [Fact]
    public void Rejects_prerequisite_cycles()
    {
        var skills = """
            [
              { "id": "a", "name": "A", "prerequisites": [ { "skillId": "b", "level": 1 } ] },
              { "id": "b", "name": "B", "prerequisites": [ { "skillId": "c", "level": 1 } ] },
              { "id": "c", "name": "C", "prerequisites": [ { "skillId": "a", "level": 1 } ] }
            ]
            """;

        var error = Assert.Single(Fails(skills, """[ { "id": "strike", "name": "공격" } ]""", Encounters()).Errors);

        Assert.StartsWith("skills.json: prerequisite cycle", error);
    }

    [Fact]
    public void Rejects_enemy_skills_without_prerequisites_and_locked_tactics()
    {
        var tactics = """{ "priority": 1, "condition": "Always", "value": 0, "actionId": "shot" }""";

        var errors = Fails(Skills, Actions, Encounters(tactics, skills: """{ "precision": 1, "unknown": 1 }""")).Errors;

        Assert.Contains("encounters.json 'camp' unit 'e1': 'precision' needs 'archery' level 4", errors);
        Assert.Contains("encounters.json 'camp' unit 'e1': unknown skill 'unknown'", errors);
        Assert.Contains("encounters.json 'camp' unit 'e1' tactic 1: 'shot' needs 'archery' level 1", errors);
    }

    [Fact]
    public void Missing_files_are_reported_by_name()
    {
        var dir = Path.Combine(Path.GetTempPath(), "triangle-missing-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, GameDataLoader.SkillsFile), Skills);

            var errors = Assert.Throws<GameDataException>(() => GameDataLoader.LoadDirectory(dir)).Errors;

            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, e => e.StartsWith("actions.json: cannot read"));
            Assert.Contains(errors, e => e.StartsWith("encounters.json: cannot read"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
