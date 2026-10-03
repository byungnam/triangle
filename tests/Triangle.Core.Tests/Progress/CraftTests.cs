using Triangle.Core.Data;
using Triangle.Core.Progress;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

/// <summary>제작: 재료와 골드를 내고 장비를 창고에 받는다.</summary>
public class CraftTests
{
    private static readonly GameData Data = GameDataLoader.Parse(
        """[ { "id": "sword", "name": "검", "kind": "Weapon" } ]""",
        "[]",
        """[ { "id": "strike", "name": "공격", "power": 10, "universal": true } ]""",
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword" },
          { "id": "knight_sword", "name": "기사 검", "slot": "MainHand", "mastery": "sword", "tier": 3 },
          { "id": "iron", "name": "철 조각", "slot": "Material" },
          { "id": "shard", "name": "고대 파편", "slot": "Material" } ]
        """,
        recipesJson: """
        [ { "result": "knight_sword", "materials": [ { "itemId": "iron", "count": 3 }, { "itemId": "shard", "count": 1 } ], "gold": 50 } ]
        """);

    private static Company NewCompany(int gold, int iron, int shard)
    {
        var company = new Company(
            [new PartyMember("a", "A", new Stats(10, 10, 10, 10, 10), Row.Front, TestGear.Of("old_sword"), null,
                new Dictionary<string, int>(), new Dictionary<string, int>(), [])],
            ["a"], gold, new Dictionary<string, int>(), 0, 1);
        company.AddToStash("iron", iron);
        company.AddToStash("shard", shard);
        return company;
    }

    [Fact]
    public void Craft_spends_materials_and_gold()
    {
        var company = NewCompany(gold: 60, iron: 4, shard: 1);

        Assert.True(company.Craft("knight_sword", Data));

        Assert.Equal(10, company.Gold);
        Assert.Equal((1, 0, 1), (company.StashCount("iron"), company.StashCount("shard"), company.StashCount("knight_sword")));
    }

    [Fact]
    public void Craft_needs_every_material_and_enough_gold()
    {
        Assert.Equal("재료가 모자랍니다: 철 조각 2/3, 고대 파편 0/1", NewCompany(100, 2, 0).WhyCannotCraft("knight_sword", Data));
        Assert.Equal("골드가 모자랍니다", NewCompany(49, 3, 1).WhyCannotCraft("knight_sword", Data));
        Assert.Equal("제작법이 없습니다", NewCompany(100, 3, 1).WhyCannotCraft("old_sword", Data));

        var company = NewCompany(100, 2, 1);
        Assert.False(company.Craft("knight_sword", Data));
        Assert.Equal((100, 2, 1), (company.Gold, company.StashCount("iron"), company.StashCount("shard")));
    }

    [Fact]
    public void Crafting_is_closed_on_an_expedition()
    {
        var company = NewCompany(100, 3, 1);
        company.Expedition = new Triangle.Core.Expeditions.Expedition("z", 1, 0, [], 0, new Dictionary<string, int>(), null);

        Assert.Equal("원정 중에는 제작할 수 없습니다", company.WhyCannotCraft("knight_sword", Data));
        Assert.False(company.Craft("knight_sword", Data));
    }
}
