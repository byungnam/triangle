using System.Text.Json;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Data;

public class GameDataLoaderTests
{
    private const string Classes = """
        [ { "id": "soldier", "name": "병사", "speedPercent": 110 } ]
        """;

    private const string Skills = """
        [
          { "id": "strike", "name": "공격", "power": 10, "rule": "FrontFirst" },
          { "id": "heal", "name": "치료", "effect": "Heal", "power": 10, "side": "Ally", "rule": "LowestHpRatio", "mpCost": 5 }
        ]
        """;

    private static string Encounters(string tactics = """{ "priority": 1, "condition": "Always", "value": 0, "skillId": "strike" }""",
        string classId = "soldier") => $$"""
        [
          {
            "id": "camp",
            "name": "야영지",
            "units": [
              {
                "id": "e1", "name": "적", "classId": "{{classId}}", "row": "Back",
                "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 },
                "tactics": [ {{tactics}} ]
              }
            ]
          }
        ]
        """;

    private static GameDataException Fails(string classes, string skills, string encounters) =>
        Assert.Throws<GameDataException>(() => GameDataLoader.Parse(classes, skills, encounters));

    [Fact]
    public void Shipped_data_files_are_valid_and_playable()
    {
        var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

        Assert.NotEmpty(data.Classes);
        Assert.NotEmpty(data.Skills);
        Assert.NotEmpty(data.Encounters);

        // 모든 적 팀이 실제 전투에 들어갈 수 있어야 한다.
        var ally = new CombatantSetup(
            "ally", "아군", data.Classes.Values.First(), new Stats(15, 15, 25, 20, 13), Row.Front,
            [new Tactic(1, Condition.Always, 0, data.Skills.Keys.First())]);
        foreach (var id in data.Encounters.Keys)
        {
            var result = CombatSimulator.Run([ally], data.CreateEncounterTeam(id), data.Skills, seed: 1);
            Assert.IsType<CombatEnded>(result.Events[^1]);
        }
    }

    [Fact]
    public void Parses_definitions_and_resolves_encounter_classes()
    {
        var data = GameDataLoader.Parse(Classes, Skills, Encounters());

        Assert.Equal(new ClassDefinition("soldier", "병사", 110), data.Classes["soldier"]);

        var heal = data.Skills["heal"];
        Assert.Equal(SkillEffect.Heal, heal.Effect);
        Assert.Equal(TargetSide.Ally, heal.Side);
        Assert.Equal(TargetRule.LowestHpRatio, heal.Rule);
        Assert.Equal(5, heal.MpCost);
        Assert.Equal(TargetScope.Single, heal.Scope); // 생략하면 기본값

        var unit = Assert.Single(data.CreateEncounterTeam("camp"));
        Assert.Equal("적", unit.Name);
        Assert.Same(data.Classes["soldier"], unit.Class);
        Assert.Equal(Row.Back, unit.Row);
        Assert.Equal(new Stats(10, 10, 20, 10, 10), unit.Stats);
        Assert.Equal(new Tactic(1, Condition.Always, 0, "strike"), Assert.Single(unit.Tactics));
    }

    [Fact]
    public void Serialized_data_round_trips_with_readable_korean()
    {
        var data = GameDataLoader.Parse(Classes, Skills, Encounters());
        var json = JsonSerializer.Serialize(data.Skills.Values.ToList(), GameDataJson.Options);

        Assert.Contains("\"name\": \"치료\"", json);
        Assert.Contains("\"rule\": \"LowestHpRatio\"", json);

        var again = GameDataLoader.Parse(Classes, json, Encounters());
        Assert.Equal(data.Skills["heal"], again.Skills["heal"]);
    }

    [Fact]
    public void Reports_invalid_json_with_file_and_line()
    {
        var error = Assert.Single(Fails(Classes, "[\n  { \"id\": \"x\",\n    \"name\": \"y\", \"rule\": \"Sideways\" }\n]", Encounters()).Errors);

        Assert.StartsWith("skills.json line 3:", error);
    }

    [Fact]
    public void Rejects_unknown_properties_so_typos_are_not_ignored()
    {
        var error = Assert.Single(Fails(Classes, """[ { "id": "x", "name": "y", "powr": 10 } ]""", Encounters()).Errors);

        Assert.StartsWith("skills.json", error);
        Assert.Contains("powr", error);
    }

    [Fact]
    public void Rejects_missing_required_properties()
    {
        Assert.StartsWith("classes.json", Assert.Single(Fails("""[ { "name": "이름만" } ]""", Skills, Encounters()).Errors));
        Assert.StartsWith("skills.json", Assert.Single(Fails(Classes, """[ { "id": "x" } ]""", Encounters()).Errors));
    }

    [Fact]
    public void Rejects_numeric_enum_values()
    {
        Fails(Classes, """[ { "id": "x", "name": "y", "rule": 1 } ]""", Encounters());
    }

    [Fact]
    public void Reports_every_reference_and_range_error_together()
    {
        var skills = """
            [
              { "id": "strike", "name": "공격", "power": 10 },
              { "id": "strike", "name": "중복", "mpCost": -1 }
            ]
            """;
        var tactics = """
            { "priority": 1, "condition": "SelfHpAtMost", "value": 150, "skillId": "strike" },
            { "priority": 2, "condition": "EveryNthTurn", "value": 0, "skillId": "strike" },
            { "priority": 3, "condition": "Always", "value": 0, "skillId": "missing" }
            """;

        var errors = Fails(Classes, skills, Encounters(tactics, classId: "ghost")).Errors;

        Assert.Contains(errors, e => e == "skills.json: duplicate id 'strike'");
        Assert.Contains(errors, e => e.StartsWith("skills.json 'strike': mpCost must not be negative"));
        Assert.Contains(errors, e => e == "encounters.json 'camp' unit 'e1': unknown class 'ghost'");
        Assert.Contains(errors, e => e.StartsWith("encounters.json 'camp' unit 'e1' tactic 1: SelfHpAtMost value must be a percent"));
        Assert.Contains(errors, e => e.StartsWith("encounters.json 'camp' unit 'e1' tactic 2: EveryNthTurn value must be positive"));
        Assert.Contains(errors, e => e == "encounters.json 'camp' unit 'e1' tactic 3: unknown skill 'missing'");
        Assert.Equal(6, errors.Count);
    }

    [Fact]
    public void Missing_files_are_reported_by_name()
    {
        var dir = Path.Combine(Path.GetTempPath(), "triangle-missing-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, GameDataLoader.ClassesFile), Classes);

            var errors = Assert.Throws<GameDataException>(() => GameDataLoader.LoadDirectory(dir)).Errors;

            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, e => e.StartsWith("skills.json: cannot read"));
            Assert.Contains(errors, e => e.StartsWith("encounters.json: cannot read"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
