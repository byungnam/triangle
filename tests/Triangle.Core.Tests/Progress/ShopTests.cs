using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Progress;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

/// <summary>상점: T1~T2 구매, 40% 판매, 원정 중 금지.</summary>
public class ShopTests
{
    private static readonly GameData Data = GameDataLoader.Parse(
        """[ { "id": "sword", "name": "검", "kind": "Weapon" } ]""",
        "[]",
        """[ { "id": "strike", "name": "공격", "power": 10, "universal": true } ]""",
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword", "price": 40 },
          { "id": "steel_sword", "name": "강철 검", "slot": "MainHand", "mastery": "sword", "tier": 2, "price": 121 },
          { "id": "knight_sword", "name": "기사 검", "slot": "MainHand", "mastery": "sword", "tier": 3, "price": 320 },
          { "id": "ore", "name": "광석", "slot": "Material", "price": 7 } ]
        """);

    private static Company NewCompany(int gold) =>
        new([new PartyMember("a", "A", new Stats(10, 10, 10, 10, 10), Row.Front, TestGear.Of("old_sword"),
                new Dictionary<string, int>(), new Dictionary<string, int>(), [])],
            ["a"], gold, new Dictionary<string, int>(), 0, 1);

    [Fact]
    public void Stock_is_tier_one_and_two_equipment()
    {
        Assert.Equal(["old_sword", "steel_sword"], Shop.Stock(Data).Select(i => i.Id));
    }

    [Fact]
    public void Buy_spends_gold_and_fills_the_stash()
    {
        var company = NewCompany(200);

        Assert.True(company.Buy("steel_sword", Data));
        Assert.Equal((79, 1), (company.Gold, company.StashCount("steel_sword")));
        Assert.Equal("골드가 모자랍니다", company.WhyCannotBuy("steel_sword", Data));
        Assert.False(company.Buy("steel_sword", Data));
        Assert.Equal("상점에서 팔지 않습니다", company.WhyCannotBuy("knight_sword", Data));
        Assert.Equal("상점에서 팔지 않습니다", company.WhyCannotBuy("ore", Data));
        Assert.Equal(79, company.Gold);
    }

    [Fact]
    public void Sell_pays_forty_percent_rounded_down_for_any_stash_item()
    {
        var company = NewCompany(0);
        company.AddToStash("steel_sword");
        company.AddToStash("knight_sword");
        company.AddToStash("ore", 2);

        Assert.True(company.Sell("steel_sword", Data)); // 121 × 40% = 48.4 → 48
        Assert.True(company.Sell("knight_sword", Data)); // 128
        Assert.True(company.Sell("ore", Data)); // 2.8 → 2
        Assert.Equal(178, company.Gold);
        Assert.Equal(1, company.StashCount("ore"));
        Assert.False(company.Sell("steel_sword", Data)); // 창고에 없다
        Assert.False(company.Sell("old_sword", Data)); // 낀 장비는 팔지 않는다
    }

    [Fact]
    public void Shop_is_closed_on_an_expedition()
    {
        var company = NewCompany(500);
        company.AddToStash("ore");
        company.Expedition = new Triangle.Core.Expeditions.Expedition("z", 1, 0, [], 0, new Dictionary<string, int>(), null);

        Assert.Equal("원정 중에는 상점을 쓸 수 없습니다", company.WhyCannotBuy("old_sword", Data));
        Assert.False(company.Buy("old_sword", Data));
        Assert.False(company.Sell("ore", Data));
        Assert.Equal(500, company.Gold);
    }
}
