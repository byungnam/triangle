using Triangle.Core.Data;
using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

public sealed class SaveGameTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "triangle-save-" + Guid.NewGuid());

    private static readonly GameData Data = GameDataLoader.Parse(
        """[ { "id": "soldier", "name": "병사" } ]""",
        """[ { "id": "strike", "name": "공격", "power": 10 }, { "id": "heal", "name": "치료", "effect": "Heal" } ]""",
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "classId": "soldier", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static Party SampleParty() => new(
    [
        new PartyMember("a", "마르쿠스", "soldier", new Stats(15, 12, 25, 20, 13), Row.Front,
            [new Tactic(1, Condition.SelfHpAtMost, 30, "heal"), new Tactic(2, Condition.Always, 0, "strike")]),
        new PartyMember("b", "율리아", "soldier", new Stats(10, 11, 21, 24, 12), Row.Back, []),
    ]);

    private static string Saved(string party) => $$"""{ "version": 1, "party": [ {{party}} ] }""";

    private const string ValidMember =
        """{ "id": "a", "name": "이름", "classId": "soldier", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } }""";

    [Fact]
    public void Round_trips_party_members_rows_and_tactics()
    {
        var original = SampleParty();

        var loaded = SaveGame.Deserialize(SaveGame.Serialize(original), Data);

        Assert.Equal(original.Members.Count, loaded.Members.Count);
        for (var i = 0; i < original.Members.Count; i++)
        {
            var (o, l) = (original.Members[i], loaded.Members[i]);
            Assert.Equal((o.Id, o.Name, o.ClassId, o.Stats, o.Row), (l.Id, l.Name, l.ClassId, l.Stats, l.Row));
            Assert.Equal(o.Tactics, l.Tactics);
        }
    }

    [Fact]
    public void Serialized_save_is_readable_json_with_version()
    {
        var json = SaveGame.Serialize(SampleParty());

        Assert.Contains("\"version\": 1", json);
        Assert.Contains("\"name\": \"율리아\"", json);
        Assert.Contains("\"condition\": \"SelfHpAtMost\"", json);
    }

    [Fact]
    public void Rejects_unknown_version()
    {
        var e = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize("""{ "version": 99, "party": [] }""", Data));

        Assert.Contains("unsupported version 99", Assert.Single(e.Errors));
    }

    [Fact]
    public void Rejects_references_missing_from_game_data()
    {
        var member = """
            { "id": "a", "name": "이름", "classId": "ghost", "row": "Front",
              "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 },
              "tactics": [ { "priority": 1, "condition": "Always", "value": 0, "skillId": "removed_skill" } ] }
            """;

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Saved(member), Data)).Errors;

        Assert.Contains("save member 'a': unknown class 'ghost'", errors);
        Assert.Contains("save member 'a' tactic 1: unknown skill 'removed_skill'", errors);
    }

    [Fact]
    public void Rejects_malformed_json_and_duplicate_members()
    {
        Assert.Throws<SaveGameException>(() => SaveGame.Deserialize("{ not json", Data));

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Saved(ValidMember + ", " + ValidMember), Data)).Errors;
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
        Assert.Equal(Row.Front, result.Party.Members[1].Row);
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
