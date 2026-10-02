using Triangle.Core.Data;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

public sealed class SaveGameTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "triangle-save-" + Guid.NewGuid());

    private static readonly GameData Data = GameDataLoader.Parse(
        """
        [ { "id": "relic", "name": "성구", "slot": "Weapon" },
          { "id": "sword", "name": "검", "slot": "Weapon" },
          { "id": "cloth", "name": "천", "slot": "Armor" } ]
        """,
        """
        [ { "id": "healing", "name": "치유술", "mastery": "relic" },
          { "id": "holy", "name": "신성 마법", "mastery": "relic", "rank": 3, "prerequisites": [ { "skillId": "healing", "level": 3 } ] } ]
        """,
        """
        [ { "id": "strike", "name": "공격", "power": 10 },
          { "id": "heal", "name": "치료", "effect": "Heal", "weapon": "relic", "requirements": [ { "skillId": "healing", "level": 1 } ] } ]
        """,
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "wooden_relic", "name": "나무 성구", "mastery": "relic" },
          { "id": "silver_relic", "name": "은 성구", "mastery": "relic" },
          { "id": "old_sword", "name": "낡은 검", "mastery": "sword" },
          { "id": "cloth_robe", "name": "천 로브", "mastery": "cloth" } ]
        """);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static Company SampleCompany()
    {
        PartyMember[] roster =
        [
            new("a", "율리아", new Stats(10, 11, 21, 24, 12), Row.Back, "wooden_relic", "cloth_robe",
                new Dictionary<string, int> { ["relic"] = MasteryProgression.XpForLevel(3) + 40, ["cloth"] = 120 },
                new Dictionary<string, int> { ["healing"] = 2 },
                [[new Tactic(1, Condition.AnyAllyHpAtMost, 50, "heal"), new Tactic(2, Condition.Always, 0, "strike")],
                 [new Tactic(1, Condition.Always, 0, "strike")]]),
            new("b", "마르쿠스", new Stats(15, 12, 25, 20, 13), Row.Front, "old_sword", null,
                new Dictionary<string, int>(), new Dictionary<string, int>(), []),
            new("c", "후보", new Stats(9, 9, 9, 9, 9), Row.Front, null, null,
                new Dictionary<string, int>(), new Dictionary<string, int>(), []),
        ];
        return new Company(roster, ["b", "a"], gold: 120, new Dictionary<string, int> { ["silver_relic"] = 2 }, activeTacticSet: 1, nextSeed: 77);
    }

    /// <summary>버전 4 세이브: 파티, 장비는 계열 ID.</summary>
    private static string Version4(string members, int activeTacticSet = 0) =>
        $$"""{ "version": 4, "activeTacticSet": {{activeTacticSet}}, "party": [ {{members}} ] }""";

    private static string Version5(string members, string lineup = "\"a\"", string extra = "") =>
        $$"""{ "version": 5, "gold": 10, "nextSeed": 3, "lineup": [ {{lineup}} ], "roster": [ {{members}} ]{{extra}} }""";

    private static string Member(string extra, string id = "a") => $$"""
        { "id": "{{id}}", "name": "이름", "row": "Front",
          "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 }{{extra}} }
        """;

    [Fact]
    public void Round_trips_company()
    {
        var original = SampleCompany();

        var loaded = SaveGame.Deserialize(SaveGame.Serialize(original), Data);

        Assert.Equal(original.Roster.Count, loaded.Roster.Count);
        for (var i = 0; i < original.Roster.Count; i++)
        {
            var (o, l) = (original.Roster[i], loaded.Roster[i]);
            Assert.Equal((o.Id, o.Name, o.Stats, o.Row, o.Weapon, o.Armor), (l.Id, l.Name, l.Stats, l.Row, l.Weapon, l.Armor));
            Assert.Equal(o.MasteryXp, l.MasteryXp);
            Assert.Equal(o.SkillLevels, l.SkillLevels);
            for (var set = 0; set < PartyMember.TacticSetCount; set++)
            {
                Assert.Equal(o.TacticSets[set], l.TacticSets[set]);
            }
        }

        Assert.Equal(["b", "a"], loaded.Lineup);
        Assert.Equal((120, 1, 77), (loaded.Gold, loaded.ActiveTacticSet, loaded.NextSeed));
        Assert.Equal(2, loaded.StashCount("silver_relic"));
    }

    [Fact]
    public void Serialized_save_is_readable_json_with_version()
    {
        var json = SaveGame.Serialize(SampleCompany());

        Assert.Contains("\"version\": 5", json);
        Assert.Contains("\"roster\"", json);
        Assert.DoesNotContain("\"party\"", json);
        Assert.Contains("\"name\": \"율리아\"", json);
        Assert.Contains("\"weapon\": \"wooden_relic\"", json);
        Assert.Contains("\"silver_relic\": 2", json);
        Assert.Contains("\"healing\": 2", json);
    }

    [Fact]
    public void Rejects_older_and_unknown_versions()
    {
        foreach (var version in new[] { 1, 2, 3, 99 })
        {
            var e = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize($$"""{ "version": {{version}}, "party": [] }""", Data));
            Assert.Contains($"unsupported version {version}", Assert.Single(e.Errors));
        }
    }

    [Fact]
    public void Converts_version_4_party_to_roster_lineup_and_basic_items()
    {
        var members = string.Join(", ", Enumerable.Range(1, 6).Select(i => Member("""
            , "weapon": "relic", "armor": "cloth", "masteryXp": { "relic": 300 }, "skillLevels": { "healing": 1 },
            "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "heal" } ] ]
            """, id: $"m{i}")));

        var company = SaveGame.Deserialize(Version4(members, activeTacticSet: 1), Data);

        Assert.Equal(6, company.Roster.Count);
        Assert.Equal(["m1", "m2", "m3", "m4", "m5"], company.Lineup);
        Assert.Equal(("wooden_relic", "cloth_robe"), (company.Roster[0].Weapon, company.Roster[0].Armor));
        Assert.Equal("relic", company.Roster[0].WeaponMastery(Data));
        Assert.Equal(300, company.Roster[0].MasteryXp["relic"]);
        Assert.Equal(new Tactic(1, Condition.Always, 0, "heal"), Assert.Single(company.Roster[0].TacticSets[0]));
        Assert.Equal((Company.StartingGold, 1), (company.Gold, company.ActiveTacticSet));
        Assert.Empty(company.Stash);
    }

    [Fact]
    public void Rejects_version_4_equipment_that_cannot_be_converted()
    {
        var errors = Assert.Throws<SaveGameException>(() =>
            SaveGame.Deserialize(Version4(Member(""", "weapon": "cloth", "armor": "ghost" """)), Data)).Errors;

        Assert.Contains("save member 'a': weapon: 'cloth' is Armor, not Weapon", errors);
        Assert.Contains("save member 'a': armor: unknown mastery 'ghost'", errors);
    }

    [Fact]
    public void Rejects_references_missing_from_game_data_and_wrong_slots()
    {
        var member = Member("""
            , "weapon": "cloth_robe", "armor": "ghost_armor",
            "masteryXp": { "ghost": 100 },
            "skillLevels": { "ghost_skill": 1 },
            "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "removed_action" } ] ]
            """);

        var errors = Assert.Throws<SaveGameException>(() =>
            SaveGame.Deserialize(Version5(member, extra: """, "stash": { "ghost_item": 1, "old_sword": 0 }"""), Data)).Errors;

        Assert.Contains("save member 'a': weapon: 'cloth_robe' is Armor, not Weapon", errors);
        Assert.Contains("save member 'a': armor: unknown item 'ghost_armor'", errors);
        Assert.Contains("save member 'a': unknown mastery 'ghost'", errors);
        Assert.Contains("save member 'a': unknown skill 'ghost_skill'", errors);
        Assert.Contains("save member 'a' set 1 tactic 1: unknown action 'removed_action'", errors);
        Assert.Contains("save: stash has unknown item 'ghost_item'", errors);
        Assert.Contains("save: stash count of 'old_sword' must be at least 1, got 0", errors);
    }

    [Fact]
    public void Rejects_bad_lineups()
    {
        var two = Member("") + ", " + Member("", id: "b");
        var errors = Assert.Throws<SaveGameException>(() =>
            SaveGame.Deserialize(Version5(two, lineup: "\"a\", \"a\", \"ghost\""), Data)).Errors;
        Assert.Contains("save: lineup lists 'a' more than once", errors);
        Assert.Contains("save: lineup member 'ghost' is not in the roster", errors);

        var six = string.Join(", ", Enumerable.Range(1, 6).Select(i => Member("", id: $"m{i}")));
        var lineup = string.Join(", ", Enumerable.Range(1, 6).Select(i => $"\"m{i}\""));
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Version5(six, lineup), Data)).Errors;
        Assert.Contains("save: lineup has 6 members (at most 5)", errors);
    }

    [Fact]
    public void Empty_lineup_is_allowed_in_the_village()
    {
        var company = SaveGame.Deserialize(Version5(Member(""), lineup: ""), Data);

        Assert.Empty(company.Lineup);
        Assert.Single(company.Roster);
    }

    [Fact]
    public void Rejects_skills_without_prerequisites_or_more_points_than_mastery_gives()
    {
        // 신성 마법은 치유술 3 필요. 성구 숙련 Lv 1인데 포인트 1 + 3 = 4를 썼다.
        var member = Member("""
            , "masteryXp": { "relic": 100 },
            "skillLevels": { "healing": 1, "holy": 1 }
            """);

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Version5(member), Data)).Errors;

        Assert.Contains("save member 'a': 'holy' needs 'healing' level 3", errors);
        Assert.Contains("save member 'a': spent 4 points in 'relic' but mastery level is 1", errors);
    }

    [Fact]
    public void Loads_tactics_whose_action_became_locked_and_reports_them()
    {
        // 치료는 성구가 필요한데 검을 들었다 (장비를 바꾼 경우).
        var member = Member("""
            , "weapon": "old_sword", "masteryXp": { "relic": 300 }, "skillLevels": { "healing": 1 },
            "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" },
                              { "priority": 2, "condition": "Always", "value": 0, "actionId": "heal" } ] ]
            """);

        var company = SaveGame.Deserialize(Version5(member, extra: """, "stash": { "silver_relic": 1 }"""), Data);

        Assert.Equal([1], company.Roster[0].LockedTacticIndexes(Data, 0));
        Assert.True(company.HasLockedTactics(Data));
        Assert.True(company.Equip("a", "silver_relic", Data));
        Assert.Empty(company.Roster[0].LockedTacticIndexes(Data, 0));
    }

    [Fact]
    public void Rejects_bad_tactic_set_data_and_version_mixups()
    {
        var tooMany = Member(""", "tacticSets": [ [], [], [] ]""");
        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Version5(tooMany), Data)).Errors;
        Assert.Contains("save member 'a': at most 2 tactic sets, got 3", errors);

        var badActive = Version5(Member(""), extra: """, "activeTacticSet": 5""");
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(badActive, Data)).Errors;
        Assert.Contains("save: activeTacticSet must be 0-1, got 5", errors);

        var partyInV5 = Version5(Member(""), extra: """, "party": [] """);
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(partyInV5, Data)).Errors;
        Assert.Contains("save: 'party' is a version 4 field; use 'roster' and 'lineup'", errors);
    }

    [Fact]
    public void Rejects_malformed_json_and_duplicate_members()
    {
        Assert.Throws<SaveGameException>(() => SaveGame.Deserialize("{ not json", Data));

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Version5(Member("") + ", " + Member("")), Data)).Errors;
        Assert.Contains("save: duplicate member id 'a'", errors);
    }

    [Fact]
    public void Store_starts_new_game_when_no_save_exists()
    {
        var store = new SaveStore(Path.Combine(_dir, "save.json"));

        var result = store.Load(Data, SampleCompany);

        Assert.Equal(LoadStatus.NewGame, result.Status);
        Assert.False(File.Exists(store.Path));
    }

    [Fact]
    public void Store_saves_and_loads_edits()
    {
        var store = new SaveStore(Path.Combine(_dir, "nested", "save.json"));
        var company = SampleCompany();
        company.Roster[1].ToggleRow();
        company.Roster[1].TacticSets[1].Add(Condition.EveryNthTurn, 3, "strike");
        company.ActiveTacticSet = 0;
        company.Equip("a", "silver_relic", Data);

        store.Save(company);
        var result = store.Load(Data, () => throw new InvalidOperationException("should not create new"));

        Assert.Equal(LoadStatus.Loaded, result.Status);
        Assert.Equal(Row.Back, result.Company.Roster[1].Row);
        Assert.Equal(new Tactic(1, Condition.EveryNthTurn, 3, "strike"), Assert.Single(result.Company.Roster[1].TacticSets[1]));
        Assert.Equal(0, result.Company.ActiveTacticSet);
        Assert.Equal("silver_relic", result.Company.Roster[0].Weapon);
        Assert.Equal(1, result.Company.StashCount("wooden_relic"));
        Assert.False(File.Exists(store.Path + ".tmp"));
    }

    [Fact]
    public void Store_keeps_broken_save_aside_and_starts_new_game()
    {
        Directory.CreateDirectory(_dir);
        var store = new SaveStore(Path.Combine(_dir, "save.json"));
        File.WriteAllText(store.Path, "{ broken");

        var result = store.Load(Data, SampleCompany);

        Assert.Equal(LoadStatus.Recovered, result.Status);
        Assert.NotEmpty(result.Errors!);
        Assert.False(File.Exists(store.Path));
        Assert.Equal("{ broken", File.ReadAllText(result.BrokenFilePath!));
    }
}
