using Triangle.Core.Data;

namespace Triangle.Core.Tests.Data;

public class DataLintTests
{
    private const string Masteries = """
        [
          { "id": "sword", "name": "검", "kind": "Weapon" },
          { "id": "plate", "name": "판금", "kind": "Armor" }
        ]
        """;

    private const string Skills = """
        [ { "id": "swordsmanship", "name": "검술", "mastery": "sword" } ]
        """;

    private const string Actions = """
        [
          { "id": "strike", "name": "공격", "power": 10, "universal": true },
          { "id": "slash", "name": "베기", "power": 15, "applies": [ { "effectId": "bleed", "duration": 2 } ] },
          { "id": "orphan", "name": "아무도 못 씀", "power": 99 },
          { "id": "spirit_bite", "name": "정령 물기", "power": 5, "summonOnly": true }
        ]
        """;

    private const string Effects = """
        [
          { "id": "bleed", "name": "출혈", "kind": "Debuff", "tickHpPercent": -5 },
          { "id": "unused_buff", "name": "안 쓰는 버프", "kind": "Buff" }
        ]
        """;

    private const string Encounters = """
        [
          { "id": "camp", "name": "야영지", "units": [ { "id": "e1", "name": "적", "row": "Front", "weapon": "sword",
            "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 },
            "tactics": [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" } ] } ] },
          { "id": "lonely", "name": "아무 지역에도 없음", "units": [ { "id": "e1", "name": "적", "row": "Front",
            "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 } } ] }
        ]
        """;

    private const string Items = """
        [
          { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword", "tier": 1, "actions": [ "slash" ] },
          { "id": "relic_sword", "name": "잃어버린 검", "slot": "MainHand", "mastery": "sword", "tier": 4 },
          { "id": "crafted_sword", "name": "만든 검", "slot": "MainHand", "mastery": "sword", "tier": 4 },
          { "id": "iron", "name": "쇳조각", "slot": "Material" },
          { "id": "pebble", "name": "조약돌", "slot": "Material" }
        ]
        """;

    private const string Zones = """
        [ { "id": "forest", "name": "숲", "maxBattles": 3, "encounters": [ { "encounterId": "camp", "weight": 1 } ],
            "rewards": { "itemDrops": [ { "itemId": "iron", "chance": 10 } ] } } ]
        """;

    private const string Recipes = """
        [ { "result": "crafted_sword", "gold": 10, "materials": [ { "itemId": "iron", "count": 2 } ] } ]
        """;

    private static GameData Load() =>
        GameDataLoader.Parse(Masteries, Skills, Actions, Encounters, Effects, Items, Zones, recipesJson: Recipes);

    [Fact]
    public void Warns_about_unreachable_content()
    {
        var warnings = DataLint.Check(Load());

        Assert.Equal(
            [
                "actions.json 'orphan': no item grants it and no enemy uses it",
                "actions.json 'spirit_bite': summonOnly but no summon uses it",
                "effects.json 'unused_buff': no action applies it",
                "encounters.json 'lonely': no zone uses it",
                "items.json 'relic_sword': cannot be obtained (not sold, dropped, crafted or given to recruits)",
                "items.json 'pebble': cannot be obtained (not sold, dropped, crafted or given to recruits)",
                "items.json 'pebble': material used by no recipe",
            ],
            warnings);
    }

    [Fact]
    public void Repository_data_has_no_warnings()
    {
        var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

        Assert.Empty(DataLint.Check(data));
    }

    [Fact]
    public void Finds_every_reference_to_an_id()
    {
        var data = Load();

        Assert.Equal(["actions.json 'slash': applies"], DataReferences.Find(data, "bleed"));
        Assert.Equal(["recipes.json 'crafted_sword': result"], DataReferences.Find(data, "crafted_sword"));
        Assert.Equal(
            ["zones.json 'forest': itemDrops", "recipes.json 'crafted_sword': materials"],
            DataReferences.Find(data, "iron"));
        Assert.Equal(
            [
                "items.json 'old_sword': mastery",
                "items.json 'relic_sword': mastery",
                "items.json 'crafted_sword': mastery",
                "encounters.json 'camp' unit 'e1': equipment",
            ],
            DataReferences.Find(data, "sword").Where(r => !r.StartsWith("skills.json")).ToList());
        Assert.Empty(DataReferences.Find(data, "nothing"));
    }
}
