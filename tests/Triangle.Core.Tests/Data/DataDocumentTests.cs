using System.Text.Json.Nodes;
using Triangle.Core.Data;

namespace Triangle.Core.Tests.Data;

public class DataDocumentTests
{
    private const string Masteries = """
        [
          { "id": "sword", "name": "검", "kind": "Weapon" }
        ]
        """;

    private const string Actions = """
        [
          { "id": "strike", "name": "공격", "power": 10, "universal": true },
          { "id": "slash", "name": "베기", "power": 15 },
          { "id": "cleave", "name": "내려찍기", "power": 30 }
        ]
        """;

    private const string Items = """
        [
          { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "type": "한손검", "mastery": "sword", "tier": 1, "price": 40, "actions": [ "slash" ] },
          { "id": "steel_sword", "name": "강철 검", "slot": "MainHand", "type": "한손검", "mastery": "sword", "tier": 2, "price": 125, "actions": [ "slash" ] },
          { "id": "old_greatsword", "name": "낡은 대검", "slot": "MainHand", "type": "양손검", "mastery": "sword", "tier": 1, "price": 45, "twoHanded": true }
        ]
        """;

    private const string Encounters = """
        [
          { "id": "camp", "name": "야영지", "units": [ { "id": "e1", "name": "적", "row": "Front",
            "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 } } ] }
        ]
        """;

    private static DataDocument Load() => DataDocument.FromJson(new Dictionary<string, string>
    {
        ["masteries"] = Masteries,
        ["actions"] = Actions,
        ["items"] = Items,
        ["encounters"] = Encounters,
    });

    private static JsonObject Item(DataDocument document, string id) =>
        document.Entries("items").Single(e => DataDocument.IdOf("items", e) == id);

    [Fact]
    public void Select_matches_wildcards_lists_and_conditions()
    {
        var document = Load();

        IReadOnlyList<string> Ids(string pattern, params string[] where) =>
            document.Select("items", pattern, where).Select(e => DataDocument.IdOf("items", e)).ToList();

        Assert.Equal(["old_sword", "old_greatsword"], Ids("old_*"));
        Assert.Equal(["old_sword", "steel_sword"], Ids("*_sword"));
        Assert.Equal(["steel_sword", "old_greatsword"], Ids("steel_sword,old_greatsword"));
        Assert.Equal(["old_sword", "steel_sword"], Ids("*", "type=한손검"));
        Assert.Equal(["steel_sword"], Ids("*", "tier>=2"));
        Assert.Equal(["old_greatsword"], Ids("*", "price<100", "twoHanded=true"));
        Assert.Equal(["old_sword", "steel_sword"], Ids("*", "actions=slash"));
        Assert.Equal(["old_greatsword"], Ids("*", "actions!=slash"));
    }

    [Fact]
    public void Set_parses_json_strings_and_arithmetic()
    {
        var document = Load();
        var steel = Item(document, "steel_sword");

        Assert.Equal(125, DataPath.Set(steel, "price", "*=1.2")!.GetValue<int>());
        Assert.Equal(150, DataPath.Get(steel, "price")!.GetValue<long>());

        DataPath.Set(steel, "price", "-=5");
        Assert.Equal(145, DataPath.Get(steel, "price")!.GetValue<long>());

        DataPath.Set(steel, "price", "-5");
        Assert.Equal(-5, DataPath.Get(steel, "price")!.GetValue<int>());

        Assert.Null(DataPath.Set(steel, "description", "임시 수치"));
        Assert.Equal("임시 수치", DataPath.Get(steel, "description")!.GetValue<string>());

        DataPath.Set(steel, "requirements", """[ { "skillId": "swordsmanship", "level": 2 } ]""");
        Assert.Equal(2, DataPath.Get(steel, "requirements.0.level")!.GetValue<int>());

        Assert.Throws<ArgumentException>(() => DataPath.Set(steel, "name", "*=2"));
    }

    [Fact]
    public void Paths_pick_array_elements_by_index_or_id()
    {
        var document = Load();
        var camp = document.Entries("encounters").Single();

        DataPath.Set(camp, "units.e1.stats.str", "+=3");
        Assert.Equal(13, DataPath.Get(camp, "units.0.stats.str")!.GetValue<long>());
        Assert.Throws<ArgumentException>(() => DataPath.Set(camp, "units.e9.stats.str", "1"));
        Assert.Throws<ArgumentException>(() => DataPath.Set(camp, "units.5.stats.str", "1"));
    }

    [Fact]
    public void Add_remove_and_unset_change_arrays_and_properties()
    {
        var document = Load();
        var greatsword = Item(document, "old_greatsword");

        Assert.True(DataPath.Add(greatsword, "actions", "cleave"));
        Assert.False(DataPath.Add(greatsword, "actions", "cleave"));
        Assert.Equal("""["cleave"]""", DataPath.Get(greatsword, "actions")!.ToJsonString());
        Assert.Equal(1, DataPath.Remove(greatsword, "actions", "cleave"));
        Assert.Equal(0, DataPath.Remove(greatsword, "actions", "cleave"));

        Assert.True(DataPath.Unset(greatsword, "twoHanded")!.GetValue<bool>());
        Assert.Null(DataPath.Get(greatsword, "twoHanded"));
        Assert.Null(DataPath.Unset(greatsword, "twoHanded"));
    }

    [Fact]
    public void Validate_catches_broken_references_from_edits()
    {
        var document = Load();
        DataPath.Add(Item(document, "old_sword"), "actions", "nope");

        var error = Assert.Throws<GameDataException>(document.Validate);
        Assert.Contains("items.json 'old_sword': unknown action 'nope'", error.Errors);
    }

    [Fact]
    public void Insert_copy_and_remove_keep_data_order()
    {
        var document = Load();
        var copy = Item(document, "old_sword").DeepClone().AsObject();
        copy["id"] = "rusty_sword";
        document.Insert("items", copy, afterId: "old_sword");
        document.Remove("items", "steel_sword");

        Assert.Equal(
            ["old_sword", "rusty_sword", "old_greatsword"],
            document.Entries("items").Select(e => DataDocument.IdOf("items", e)));
        Assert.Throws<ArgumentException>(() => document.Insert("items", copy.DeepClone().AsObject()));
        Assert.Equal(["items"], document.Changed);
        document.Validate();
    }

    [Fact]
    public void Repository_files_round_trip_unchanged()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "data");
        var document = DataDocument.Load(directory);

        foreach (var (kind, file) in DataDocument.Files)
        {
            Assert.Equal(File.ReadAllText(Path.Combine(directory, file)), document.ToJson(kind));
        }
    }
}
