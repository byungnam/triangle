using System.Text.Json;
using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Data;

public class GameDataLoaderTests
{
    private const string Masteries = """
        [
          { "id": "bow", "name": "활", "kind": "Weapon" },
          { "id": "sword", "name": "검", "kind": "Weapon" },
          { "id": "plate", "name": "판금", "kind": "Armor" }
        ]
        """;

    private const string Skills = """
        [
          { "id": "archery", "name": "활 숙련", "mastery": "bow",
            "bonuses": [ { "kind": "PowerPercent", "percentPerLevel": 5, "tag": "bow" } ] },
          { "id": "precision", "name": "정밀 사격", "mastery": "bow", "rank": 3,
            "prerequisites": [ { "skillId": "archery", "level": 4 } ] }
        ]
        """;

    private const string Actions = """
        [
          { "id": "strike", "name": "공격", "power": 10, "rule": "FrontFirst", "tags": [ "melee" ], "universal": true },
          { "id": "shot", "name": "화살", "power": 10,
            "requirements": [ { "skillId": "archery", "level": 1 } ] },
          { "id": "heal", "name": "치료", "effect": "Heal", "power": 10, "side": "Ally", "rule": "LowestHpRatio", "mpCost": 5 }
        ]
        """;

    private static string Encounters(
        string tactics = """{ "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" }""",
        string skills = "{}",
        string weapon = "bow") => $$"""
        [
          {
            "id": "camp",
            "name": "야영지",
            "units": [
              {
                "id": "e1", "name": "적", "row": "Back", "weapon": "{{weapon}}", "armor": "plate",
                "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 },
                "skills": {{skills}},
                "tactics": [ {{tactics}} ]
              }
            ]
          }
        ]
        """;

    private static GameDataException Fails(string skills, string actions, string encounters, string masteries = Masteries) =>
        Assert.Throws<GameDataException>(() => GameDataLoader.Parse(masteries, skills, actions, encounters));

    [Fact]
    public void Shipped_data_files_are_valid_and_playable()
    {
        var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

        Assert.NotEmpty(data.Masteries);
        Assert.NotEmpty(data.Skills);
        Assert.NotEmpty(data.Actions);
        Assert.NotEmpty(data.Encounters);

        // 세이브 v5 변환: v5 기본 아이템 ID가 그대로 주무기·몸통 T1 아이템이어야 한다.
        foreach (var (id, slot) in new[]
                 {
                     ("old_sword", EquipmentSlot.MainHand), ("hunting_bow", EquipmentSlot.MainHand), ("apprentice_staff", EquipmentSlot.MainHand),
                     ("wooden_relic", EquipmentSlot.MainHand), ("plate_armor", EquipmentSlot.Body), ("leather_armor", EquipmentSlot.Body),
                     ("cloth_robe", EquipmentSlot.Body),
                 })
        {
            Assert.Equal((slot, 1), (data.Items[id].Slot, data.Items[id].Tier));
        }

        // 공용 행동이 아닌 행동은 모두 어떤 아이템의 행동 칸에 있어야 한다 (아니면 아군이 쓸 수 없다).
        Assert.All(data.Actions.Values.Where(a => !a.Universal), a => Assert.NotEmpty(data.ItemsGranting(a.Id)));

        // 시작 회사가 데이터와 맞아야 한다 (세이브로 왕복해서 검증).
        var company = StartingCompany.Create(data, seed: 1);
        Assert.Equal(company.Roster.Count, SaveGame.Deserialize(SaveGame.Serialize(company), data).Roster.Count);
        Assert.Null(company.WhyLineupCannotFight(data));
        Assert.All(company.Roster, m => Assert.Empty(m.LockedTacticIndexes(data, 1)));

        // 모든 적 팀이 실제 전투에 들어갈 수 있어야 한다.
        var basic = data.Actions.Values.First(a => a.Requirements.Count == 0 && a.Universal);
        var ally = new CombatantSetup(
            "ally", "아군", new Stats(15, 15, 25, 20, 13), Row.Front, null, null, SkillSet.NoSkills,
            [new Tactic(1, Condition.Always, 0, basic.Id)]);
        foreach (var id in data.Encounters.Keys)
        {
            var result = CombatSimulator.Run([ally], data.CreateEncounterTeam(id), data.Catalog, seed: 1);
            Assert.IsType<CombatEnded>(result.Events[^1]);
        }
    }

    [Fact]
    public void Starting_company_fights_every_encounter_of_every_zone_to_the_end()
    {
        var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        var company = StartingCompany.Create(data, seed: 1);

        Assert.NotEmpty(data.Zones);
        Assert.NotEmpty(data.Recruits);
        foreach (var zone in data.Zones.Values)
        {
            foreach (var encounter in zone.Encounters)
            {
                var result = CombatSimulator.Run(company.LineupSetups(data), data.CreateEncounterTeam(encounter.EncounterId), data.Catalog, seed: 1);
                Assert.IsType<CombatEnded>(result.Events[^1]);
            }
        }
    }

    [Fact]
    public void Rejects_bad_zones_and_recruit_templates()
    {
        const string zones = """
            [ { "id": "z", "name": "지역", "maxBattles": 0, "equipmentDestroyChance": 120,
                "encounters": [ { "encounterId": "ghost", "weight": 0 } ],
                "rewards": { "goldMin": 10, "goldMax": 5, "itemDrops": [ { "itemId": "ghost_item", "chance": 101 } ] } },
              { "id": "empty", "name": "빈 지역", "maxBattles": 1, "encounters": [] } ]
            """;
        const string recruits = """
            [ { "id": "r", "name": "신입", "names": [], "row": "Front", "price": -1,
                "statsMin": { "str": 5, "dex": 1, "vital": 1, "intel": 1, "speed": 1 },
                "statsMax": { "str": 4, "dex": 1, "vital": 1, "intel": 1, "speed": 1 },
                "equipment": { "MainHand": "plate_mail", "OffHand": "buckler", "Head": "ghost" },
                "tactics": [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "shot" } ] },
              { "id": "r2", "name": "신입 궁수", "names": [ "가" ], "row": "Back", "price": 1,
                "statsMin": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 },
                "statsMax": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 },
                "equipment": { "MainHand": "long_bow", "OffHand": "buckler" } } ]
            """;
        const string items = """
            [ { "id": "plate_mail", "name": "판금", "slot": "Body", "mastery": "plate" },
              { "id": "buckler", "name": "방패", "slot": "OffHand", "mastery": "sword" },
              { "id": "long_bow", "name": "장궁", "slot": "MainHand", "mastery": "bow", "twoHanded": true, "tier": 2,
                "requirements": [ { "skillId": "archery", "level": 1 } ] } ]
            """;

        var errors = Assert.Throws<GameDataException>(() => GameDataLoader.Parse(
            Masteries, Skills, Actions, Encounters(), itemsJson: items, zonesJson: zones, recruitsJson: recruits)).Errors;

        Assert.Contains("zones.json 'z': maxBattles must be at least 1, got 0", errors);
        Assert.Contains("zones.json 'z': equipmentDestroyChance must be 0-100, got 120", errors);
        Assert.Contains("zones.json 'z': unknown encounter 'ghost'", errors);
        Assert.Contains("zones.json 'z': weight of 'ghost' must be at least 1, got 0", errors);
        Assert.Contains("zones.json 'z': goldMax 5 is less than goldMin 10", errors);
        Assert.Contains("zones.json 'z': drops unknown item 'ghost_item'", errors);
        Assert.Contains("zones.json 'z': chance of 'ghost_item' must be 0-100, got 101", errors);
        Assert.Contains("zones.json 'empty': needs at least one encounter", errors);
        Assert.Contains("recruits.json 'r': needs at least one name", errors);
        Assert.Contains("recruits.json 'r': statsMax must be at least statsMin for every stat", errors);
        Assert.Contains("recruits.json 'r': price must not be negative, got -1", errors);
        Assert.Contains("recruits.json 'r': equipment: MainHand: 'plate_mail' is Body", errors);
        Assert.Contains("recruits.json 'r': equipment: Head: unknown item 'ghost'", errors);
        Assert.Contains("recruits.json 'r2': a new recruit cannot wear 'long_bow'", errors);
        Assert.Contains("recruits.json 'r2': an off-hand item cannot be worn with a two-handed weapon", errors);
        Assert.Contains("recruits.json 'r' tactic 1: a new recruit cannot use 'shot'", errors);
    }

    [Fact]
    public void Rejects_bad_items()
    {
        const string items = """
            [ { "id": "stick", "name": "막대", "slot": "MainHand", "mastery": "ghost" },
              { "id": "bow1", "name": "활", "slot": "MainHand", "mastery": "bow", "price": -1, "tier": 5 },
              { "id": "bow1", "name": "활", "slot": "MainHand", "mastery": "bow" },
              { "id": "helm", "name": "검 투구", "slot": "Head", "mastery": "sword", "twoHanded": true },
              { "id": "naked", "name": "계열 없음", "slot": "Body" },
              { "id": "ore", "name": "광석", "slot": "Material", "mastery": "plate" },
              { "id": "wand", "name": "막대기", "slot": "MainHand", "mastery": "bow",
                "requirements": [ { "skillId": "ghost", "level": 1 } ],
                "abilities": [ { "options": [] }, { "options": [ "shot", "shot", "ghost", "strike" ] } ] } ]
            """;

        var errors = Assert.Throws<GameDataException>(() =>
            GameDataLoader.Parse(Masteries, Skills, Actions, Encounters(), itemsJson: items)).Errors;

        Assert.Contains("items.json 'stick': mastery: unknown mastery 'ghost'", errors);
        Assert.Contains("items.json 'bow1': price must not be negative, got -1", errors);
        Assert.Contains("items.json 'bow1': tier must be 1-4, got 5", errors);
        Assert.Contains("items.json: duplicate id 'bow1'", errors);
        Assert.Contains("items.json 'helm': mastery: 'sword' is Weapon, not Armor", errors);
        Assert.Contains("items.json 'helm': only a main-hand item can be twoHanded", errors);
        Assert.Contains("items.json 'naked': equipment needs a mastery", errors);
        Assert.Contains("items.json 'ore': a material has no mastery, twoHanded, requirements, bonuses or abilities", errors);
        Assert.Contains("items.json 'wand' requirement: requires unknown skill 'ghost'", errors);
        Assert.Contains("items.json 'wand': ability 1 needs at least one option", errors);
        Assert.Contains("items.json 'wand': ability 2 has unknown action 'ghost'", errors);
        Assert.Contains("items.json 'wand': ability 2 offers universal action 'strike'", errors);
        Assert.Contains("items.json 'wand': ability 2 lists 'shot' more than once", errors);
    }

    [Fact]
    public void Parses_skills_actions_and_encounters()
    {
        var data = GameDataLoader.Parse(Masteries, Skills, Actions, Encounters(skills: """{ "archery": 2 }"""));

        Assert.Equal(MasteryKind.Armor, data.Masteries["plate"].Kind);
        var archery = data.Skills["archery"];
        Assert.Equal(("bow", 1), (archery.Mastery, archery.Rank));
        Assert.Equal(new SkillBonus(BonusKind.PowerPercent, 5, "bow"), Assert.Single(archery.Bonuses));
        Assert.Equal(new SkillRequirement("archery", 4), Assert.Single(data.Skills["precision"].Prerequisites));

        var heal = data.Actions["heal"];
        Assert.Equal(ActionEffect.Heal, heal.Effect);
        Assert.Equal(TargetSide.Ally, heal.Side);
        Assert.Equal(5, heal.MpCost);
        Assert.Equal(TargetScope.Single, heal.Scope); // 생략하면 기본값
        Assert.True(data.Actions["strike"].Universal);
        Assert.False(data.Actions["shot"].Universal);

        var unit = Assert.Single(data.CreateEncounterTeam("camp"));
        Assert.Equal("적", unit.Name);
        Assert.Equal(Row.Back, unit.Row);
        Assert.Equal(("bow", "plate"), (unit.Weapon, unit.Armor));
        Assert.Equal(2, unit.Skills["archery"]);
        Assert.Equal(new Tactic(1, Condition.Always, 0, "strike"), Assert.Single(unit.Tactics));
    }

    [Fact]
    public void Serialized_data_round_trips_with_readable_korean()
    {
        var data = GameDataLoader.Parse(Masteries, Skills, Actions, Encounters());
        var json = JsonSerializer.Serialize(data.Actions.Values.ToList(), GameDataJson.Options);

        Assert.Contains("\"name\": \"치료\"", json);
        Assert.Contains("\"rule\": \"LowestHpRatio\"", json);

        var again = GameDataLoader.Parse(Masteries, Skills, json, Encounters());
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
        Assert.StartsWith("skills.json", Assert.Single(Fails("""[ { "name": "이름만", "mastery": "bow" } ]""", Actions, Encounters()).Errors));
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
              { "id": "a", "name": "A", "mastery": "bow", "prerequisites": [ { "skillId": "b", "level": 1 } ] },
              { "id": "b", "name": "B", "mastery": "bow", "prerequisites": [ { "skillId": "c", "level": 1 } ] },
              { "id": "c", "name": "C", "mastery": "bow", "prerequisites": [ { "skillId": "a", "level": 1 } ] }
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
    public void Validates_enemy_equipment_kinds_and_tree_membership()
    {
        var skills = """
            [ { "id": "archery", "name": "활 숙련", "mastery": "bow" },
              { "id": "cross", "name": "다른 트리 선행", "mastery": "sword",
                "prerequisites": [ { "skillId": "archery", "level": 1 } ] },
              { "id": "lost", "name": "없는 트리", "mastery": "nowhere" } ]
            """;
        var actions = """
            [ { "id": "strike", "name": "공격" },
              { "id": "shot", "name": "화살" } ]
            """;
        // 적은 아이템이 없으므로 화살을 검으로도 쓴다. 장비는 계열의 종류만 맞으면 된다.
        var tactics = """{ "priority": 1, "condition": "Always", "value": 0, "actionId": "shot" }""";

        var errors = Fails(skills, actions, Encounters(tactics, weapon: "plate")).Errors;

        Assert.Contains("skills.json 'cross': prerequisite 'archery' belongs to another mastery tree", errors);
        Assert.Contains("skills.json 'lost': unknown mastery 'nowhere'", errors);
        Assert.Contains("encounters.json 'camp' unit 'e1': weapon: 'plate' is Armor, not Weapon", errors);
        Assert.DoesNotContain(errors, e => e.Contains("tactic 1"));
    }

    [Fact]
    public void Missing_files_are_reported_by_name()
    {
        var dir = Path.Combine(Path.GetTempPath(), "triangle-missing-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, GameDataLoader.MasteriesFile), Masteries);
            File.WriteAllText(Path.Combine(dir, GameDataLoader.SkillsFile), Skills);

            var errors = Assert.Throws<GameDataException>(() => GameDataLoader.LoadDirectory(dir)).Errors;

            Assert.Equal(6, errors.Count);
            Assert.Contains(errors, e => e.StartsWith("items.json: cannot read"));
            Assert.Contains(errors, e => e.StartsWith("zones.json: cannot read"));
            Assert.Contains(errors, e => e.StartsWith("recruits.json: cannot read"));
            Assert.Contains(errors, e => e.StartsWith("actions.json: cannot read"));
            Assert.Contains(errors, e => e.StartsWith("effects.json: cannot read"));
            Assert.Contains(errors, e => e.StartsWith("encounters.json: cannot read"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
