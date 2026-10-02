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
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static Party SampleParty() => new(
    [
        new PartyMember("a", "율리아", new Stats(10, 11, 21, 24, 12), Row.Back, "relic", "cloth",
            new Dictionary<string, int> { ["relic"] = MasteryProgression.XpForLevel(3) + 40, ["cloth"] = 120 },
            new Dictionary<string, int> { ["healing"] = 2 },
            [new Tactic(1, Condition.AnyAllyHpAtMost, 50, "heal"), new Tactic(2, Condition.Always, 0, "strike")]),
        new PartyMember("b", "마르쿠스", new Stats(15, 12, 25, 20, 13), Row.Front, "sword", null,
            new Dictionary<string, int>(), new Dictionary<string, int>(), []),
    ]);

    private static string Saved(string member) => $$"""{ "version": 3, "party": [ {{member}} ] }""";

    private static string Member(string extra) => $$"""
        { "id": "a", "name": "이름", "row": "Front",
          "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 }{{extra}} }
        """;

    [Fact]
    public void Round_trips_equipment_mastery_skills_and_tactics()
    {
        var original = SampleParty();

        var loaded = SaveGame.Deserialize(SaveGame.Serialize(original), Data);

        Assert.Equal(original.Members.Count, loaded.Members.Count);
        for (var i = 0; i < original.Members.Count; i++)
        {
            var (o, l) = (original.Members[i], loaded.Members[i]);
            Assert.Equal((o.Id, o.Name, o.Stats, o.Row, o.Weapon, o.Armor), (l.Id, l.Name, l.Stats, l.Row, l.Weapon, l.Armor));
            Assert.Equal(o.MasteryXp, l.MasteryXp);
            Assert.Equal(o.SkillLevels, l.SkillLevels);
            Assert.Equal(o.Tactics, l.Tactics);
        }
    }

    [Fact]
    public void Serialized_save_is_readable_json_with_version()
    {
        var json = SaveGame.Serialize(SampleParty());

        Assert.Contains("\"version\": 3", json);
        Assert.Contains("\"name\": \"율리아\"", json);
        Assert.Contains("\"weapon\": \"relic\"", json);
        Assert.Contains("\"healing\": 2", json);
    }

    [Fact]
    public void Rejects_older_and_unknown_versions()
    {
        foreach (var version in new[] { 1, 2, 99 })
        {
            var e = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize($$"""{ "version": {{version}}, "party": [] }""", Data));
            Assert.Contains($"unsupported version {version}", Assert.Single(e.Errors));
        }
    }

    [Fact]
    public void Rejects_references_missing_from_game_data_and_wrong_slots()
    {
        var member = Member("""
            , "weapon": "cloth", "armor": "ghost_armor",
            "masteryXp": { "ghost": 100 },
            "skillLevels": { "ghost_skill": 1 },
            "tactics": [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "removed_action" } ]
            """);

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Saved(member), Data)).Errors;

        Assert.Contains("save member 'a': weapon: 'cloth' is Armor, not Weapon", errors);
        Assert.Contains("save member 'a': armor: unknown mastery 'ghost_armor'", errors);
        Assert.Contains("save member 'a': unknown mastery 'ghost'", errors);
        Assert.Contains("save member 'a': unknown skill 'ghost_skill'", errors);
        Assert.Contains("save member 'a' tactic 1: unknown action 'removed_action'", errors);
    }

    [Fact]
    public void Rejects_skills_without_prerequisites_or_more_points_than_mastery_gives()
    {
        // 신성 마법은 치유술 3 필요. 성구 숙련 Lv 1인데 포인트 1 + 3 = 4를 썼다.
        var member = Member("""
            , "masteryXp": { "relic": 100 },
            "skillLevels": { "healing": 1, "holy": 1 }
            """);

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Saved(member), Data)).Errors;

        Assert.Contains("save member 'a': 'holy' needs 'healing' level 3", errors);
        Assert.Contains("save member 'a': spent 4 points in 'relic' but mastery level is 1", errors);
    }

    [Fact]
    public void Loads_tactics_whose_action_became_locked_and_reports_them()
    {
        // 치료는 성구가 필요한데 검을 들었다 (장비를 바꾼 경우).
        var member = Member("""
            , "weapon": "sword", "masteryXp": { "relic": 300 }, "skillLevels": { "healing": 1 },
            "tactics": [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" },
                         { "priority": 2, "condition": "Always", "value": 0, "actionId": "heal" } ]
            """);

        var party = SaveGame.Deserialize(Saved(member), Data);

        Assert.Equal([1], party.Members[0].LockedTacticIndexes(Data));
        party.Members[0].Weapon = "relic";
        Assert.Empty(party.Members[0].LockedTacticIndexes(Data));
    }

    [Fact]
    public void Rejects_malformed_json_and_duplicate_members()
    {
        Assert.Throws<SaveGameException>(() => SaveGame.Deserialize("{ not json", Data));

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Saved(Member("") + ", " + Member("")), Data)).Errors;
        Assert.Contains("save: duplicate member id 'a'", errors);
    }

    [Fact]
    public void Store_starts_new_game_when_no_save_exists()
    {
        var store = new SaveStore(Path.Combine(_dir, "save.json"));

        var result = store.Load(Data, SampleParty);

        Assert.Equal(LoadStatus.NewGame, result.Status);
        Assert.False(File.Exists(store.Path));
    }

    [Fact]
    public void Store_saves_and_loads_edits()
    {
        var store = new SaveStore(Path.Combine(_dir, "nested", "save.json"));
        var party = SampleParty();
        party.Members[1].ToggleRow();
        party.Members[1].AddTactic(Condition.EveryNthTurn, 3, "strike");

        store.Save(party);
        var result = store.Load(Data, () => throw new InvalidOperationException("should not create new"));

        Assert.Equal(LoadStatus.Loaded, result.Status);
        Assert.Equal(Row.Back, result.Party.Members[1].Row);
        Assert.Equal(new Tactic(1, Condition.EveryNthTurn, 3, "strike"), Assert.Single(result.Party.Members[1].Tactics));
        Assert.False(File.Exists(store.Path + ".tmp"));
    }

    [Fact]
    public void Store_keeps_broken_save_aside_and_starts_new_game()
    {
        Directory.CreateDirectory(_dir);
        var store = new SaveStore(Path.Combine(_dir, "save.json"));
        File.WriteAllText(store.Path, "{ broken");

        var result = store.Load(Data, SampleParty);

        Assert.Equal(LoadStatus.Recovered, result.Status);
        Assert.NotEmpty(result.Errors!);
        Assert.False(File.Exists(store.Path));
        Assert.Equal("{ broken", File.ReadAllText(result.BrokenFilePath!));
    }
}
