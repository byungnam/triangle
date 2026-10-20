using Triangle.Core.Data;

namespace Triangle.Core.Tests.Data;

public class DataLintTests
{
    private const string Masteries = """
        [
          { "id": "sword", "name": "검", "kind": "Weapon" },
          { "id": "bow", "name": "활", "kind": "Weapon" },
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
          { "id": "camp", "name": "야영지", "units": [ { "id": "e1", "name": "적", "row": "Front", "equipment": { "MainHand": "old_sword", "Body": "worn_mail" },
            "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 },
            "tactics": [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" } ] } ] },
          { "id": "lonely", "name": "아무 지역에도 없음", "units": [ { "id": "e1", "name": "적", "row": "Front",
            "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 } } ] }
        ]
        """;

    private const string Items = """
        [
          { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword", "tier": 1, "actions": [ "slash" ] },
          { "id": "rare_sword", "name": "상점에 안 나오는 검", "slot": "MainHand", "mastery": "sword", "tier": 2, "shop": true },
          { "id": "lost_bow", "name": "쓸 클래스가 없는 활", "slot": "MainHand", "mastery": "bow", "tier": 1, "shop": true },
          { "id": "old_mail", "name": "팔지 않는 갑옷", "slot": "Body", "mastery": "plate", "tier": 3 },
          { "id": "worn_mail", "name": "적이 입은 갑옷", "slot": "Body", "mastery": "plate", "tier": 3 },
          { "id": "ring", "name": "반지", "slot": "Trinket", "tier": 1, "shop": true }
        ]
        """;

    private const string Zones = """
        [ { "id": "forest", "name": "숲", "encounters": [ { "encounterId": "camp", "weight": 1 } ] } ]
        """;

    /// <summary>검 클래스 하나.</summary>
    private const string Classes = """
        [ { "id": "knight", "name": "기사", "row": "Front", "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 },
            "families": [ "sword" ], "weapon": "old_sword", "armor": "worn_mail", "startingSkill": "slash", "skills": [ "slash" ], "passives": [] } ]
        """;

    /// <summary>막 하나, 상점은 티어 1까지 판다.</summary>
    private const string Run = """
        { "acts": [ { "zoneId": "forest", "floors": 2, "bosses": [ "camp" ], "shopTier": 1 } ] }
        """;

    private static GameData Load() =>
        GameDataLoader.Parse(Masteries, Skills, Actions, Encounters, Effects, Items, Zones, classesJson: Classes, runJson: Run);

    [Fact]
    public void Warns_about_unreachable_content()
    {
        var warnings = DataLint.Check(Load());

        Assert.Equal(
            [
                "actions.json 'orphan': no item grants it",
                "actions.json 'spirit_bite': summonOnly but no summon uses it",
                "effects.json 'unused_buff': no action applies it",
                "encounters.json 'lonely': no zone or run act uses it",
                "items.json 'rare_sword': sold in a run but no act shop sells tier 2",
                "items.json 'lost_bow': sold in a run but no class wields 'bow'",
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
        Assert.Equal(["zones.json 'forest': encounters", "run.json acts[0]: bosses"], DataReferences.Find(data, "camp"));
        Assert.Equal(
            [
                "items.json 'old_sword': mastery",
                "items.json 'rare_sword': mastery",
                "classes.json 'knight': families",
            ],
            DataReferences.Find(data, "sword").Where(r => !r.StartsWith("skills.json")).ToList());
        Assert.Equal(["encounters.json 'camp' unit 'e1': equipment", "classes.json 'knight': armor"], DataReferences.Find(data, "worn_mail"));
        Assert.Contains("encounters.json 'camp' unit 'e1': equipment", DataReferences.Find(data, "old_sword"));
        Assert.Empty(DataReferences.Find(data, "nothing"));
    }
}
