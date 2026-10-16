using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Progress;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

/// <summary>장비 한 점: 번호와 아이템 레벨이 장착·해제·판매를 거쳐도 그대로 따라간다.</summary>
public class ItemInstanceTests
{
    private static readonly GameData Data = GameDataLoader.Parse(
        """[ { "id": "sword", "name": "검", "kind": "Weapon" }, { "id": "plate", "name": "판금", "kind": "Armor" } ]""",
        "[]",
        """[ { "id": "strike", "name": "공격", "power": 10, "universal": true } ]""",
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword", "price": 40 },
          { "id": "helm", "name": "투구", "slot": "Head", "mastery": "plate", "tier": 2, "price": 100 },
          { "id": "ore", "name": "광석", "slot": "Material", "price": 5 } ]
        """);

    private static PartyMember Member(string id, IReadOnlyDictionary<EquipmentSlot, ItemInstance> gear) =>
        new(id, id, new Stats(10, 10, 10, 10, 10), Row.Front, gear, new Dictionary<string, int>(), new Dictionary<string, int>(), []);

    private static Company NewCompany() =>
        new([Member("a", new Dictionary<EquipmentSlot, ItemInstance> { [EquipmentSlot.MainHand] = new(1, "old_sword", 1) })],
            ["a"], 0, new Dictionary<string, int>(), 0, 1);

    [Fact]
    public void Each_new_piece_gets_its_own_uid_and_a_level()
    {
        var company = NewCompany();

        var made = company.AddToStash("helm", Data, 2, level: 7);
        var bought = company.AddToStash("helm", Data);
        company.AddToStash("ore", Data, 3);

        Assert.Equal([new ItemInstance(2, "helm", 7), new ItemInstance(3, "helm", 7)], made);
        Assert.Equal(new ItemInstance(4, "helm", 2), Assert.Single(bought)); // 레벨을 주지 않으면 티어
        Assert.Equal((3, 3, 6), (company.StashCount("helm"), company.StashCount("ore"), company.StashTotal));
        Assert.Equal(5, company.NextItemUid);
    }

    [Fact]
    public void Equip_and_unequip_move_the_same_piece()
    {
        var company = NewCompany();
        var piece = Assert.Single(company.AddToStash("helm", Data, level: 5));

        Assert.True(company.Equip("a", piece.Uid, Data));
        Assert.Same(piece, company.Member("a").GearIn(EquipmentSlot.Head));
        Assert.Null(company.StashGear(piece.Uid));
        Assert.Null(company.WhyCannotEquip("a", piece.Uid, Data)); // 이미 낀 장비

        Assert.True(company.Unequip("a", EquipmentSlot.Head, Data));
        Assert.Same(piece, company.StashGear(piece.Uid));
        Assert.Equal("창고에 없습니다", company.WhyCannotEquip("a", 99, Data));
    }

    [Fact]
    public void Swapping_returns_the_worn_piece_with_its_uid_and_level()
    {
        var company = NewCompany();
        var better = Assert.Single(company.AddToStash("old_sword", Data, level: 4));

        Assert.True(company.Equip("a", better.Uid, Data));

        Assert.Equal(better, company.Member("a").GearIn(EquipmentSlot.MainHand));
        Assert.Equal(new ItemInstance(1, "old_sword", 1), Assert.Single(company.Gear));
    }

    [Fact]
    public void Best_and_worst_pick_by_level_and_selling_takes_only_that_piece()
    {
        var company = NewCompany();
        company.AddToStash("helm", Data, level: 2);
        company.AddToStash("helm", Data, level: 6);
        company.AddToStash("helm", Data, level: 2);

        Assert.Equal(new ItemInstance(3, "helm", 6), company.BestGear("helm"));
        Assert.Equal(new ItemInstance(4, "helm", 2), company.WorstGear("helm")); // 같은 레벨이면 나중에 들어온 것

        Assert.True(company.Sell(4, Data));
        Assert.False(company.Sell(4, Data));
        Assert.Equal([2, 3], company.Gear.Select(g => g.Uid));
        Assert.Equal(40, company.Gold); // 100의 40%
        Assert.Null(company.BestGear("ore"));
    }

    [Fact]
    public void A_company_rejects_a_uid_used_twice_and_keeps_new_uids_above_the_used_ones()
    {
        var worn = new Dictionary<EquipmentSlot, ItemInstance> { [EquipmentSlot.MainHand] = new(5, "old_sword", 1) };

        Assert.Throws<ArgumentException>(() => new Company(
            [Member("a", worn)], ["a"], 0, new Dictionary<string, int>(), 0, 1, gear: [new ItemInstance(5, "helm", 2)]));

        var company = new Company(
            [Member("a", worn)], ["a"], 0, new Dictionary<string, int>(), 0, 1, gear: [new ItemInstance(9, "helm", 2)], nextItemUid: 3);
        Assert.Equal(10, company.NextItemUid);
    }
}
