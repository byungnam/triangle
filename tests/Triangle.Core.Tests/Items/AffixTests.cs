using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Items;

/// <summary>무작위 옵션: 붙는 곳, 등급, 값, 전투 보너스, 제작품, 분해.</summary>
public class AffixTests
{
    private static readonly GameData Data = GameDataLoader.Parse(
        """
        [ { "id": "sword", "name": "검", "kind": "Weapon" },
          { "id": "shield", "name": "방패", "kind": "Weapon" },
          { "id": "tome", "name": "마도서", "kind": "Weapon" },
          { "id": "plate", "name": "판금", "kind": "Armor" } ]
        """,
        "[]",
        """[ { "id": "strike", "name": "공격", "power": 10, "universal": true } ]""",
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword" },
          { "id": "tower_shield", "name": "탑 방패", "slot": "OffHand", "mastery": "shield" },
          { "id": "old_tome", "name": "낡은 마도서", "slot": "OffHand", "mastery": "tome" },
          { "id": "helm", "name": "투구", "slot": "Head", "mastery": "plate" },
          { "id": "boots", "name": "장화", "slot": "Feet", "mastery": "plate" },
          { "id": "steel_helm", "name": "강철 투구", "slot": "Head", "mastery": "plate", "tier": 2 },
          { "id": "iron", "name": "철 조각", "slot": "Material" },
          { "id": "shard", "name": "고대 파편", "slot": "Material" } ]
        """,
        zonesJson: """
        [ { "id": "deep", "name": "심연", "difficulty": 4, "maxBattles": 1, "encounters": [ { "encounterId": "camp", "weight": 1 } ] } ]
        """,
        recipesJson: """
        [ { "result": "steel_helm", "materials": [ { "itemId": "iron", "count": 3 }, { "itemId": "shard", "count": 1 } ], "gold": 10 } ]
        """,
        affixesJson: """
        [ { "id": "weapon_power", "name": "계열 위력", "kind": "PowerPercent", "itemMasteryTag": true,
            "on": [ { "slot": "MainHand" } ], "min": 3, "max": 6, "perLevel": 1.5 },
          { "id": "defense", "name": "방어", "kind": "DefensePercent",
            "on": [ { "slot": "Head" }, { "slot": "Feet" }, { "slot": "OffHand", "masteries": [ "shield" ] } ], "min": 4, "max": 8, "perLevel": 2 },
          { "id": "haste", "name": "행동 간격 감소", "kind": "DelayReductionPercent",
            "on": [ { "slot": "MainHand" }, { "slot": "Head" }, { "slot": "Feet" } ], "min": 1, "max": 2, "perLevel": 0.5, "memberCap": 5 } ]
        """);

    private static AffixDefinition Affix(string id) => Data.Affixes[id];

    private static ItemDefinition Item(string id) => Data.Items[id];

    private static Company NewCompany(IReadOnlyDictionary<EquipmentSlot, ItemInstance>? gear = null) =>
        new([new PartyMember("a", "A", new Stats(10, 10, 10, 10, 10), Row.Front, gear ?? TestGear.Of("old_sword"),
                new Dictionary<string, int>(), new Dictionary<string, int>(), [])],
            ["a"], 100, new Dictionary<string, int>(), 0, 1);

    [Fact]
    public void Options_fit_by_slot_and_listed_mastery()
    {
        Assert.True(Affix("weapon_power").Fits(Item("old_sword")));
        Assert.False(Affix("weapon_power").Fits(Item("helm")));
        Assert.True(Affix("defense").Fits(Item("tower_shield")));
        Assert.False(Affix("defense").Fits(Item("old_tome"))); // 보조 손 중 방패에만
        Assert.False(Affix("defense").Fits(Item("iron")));

        Assert.Equal(["weapon_power", "haste"], Affixes.For(Item("old_sword"), Data).Select(a => a.Id));
        Assert.Empty(Affixes.For(Item("old_tome"), Data));
    }

    [Fact]
    public void Values_grow_with_item_level_and_round_half_away_from_zero()
    {
        var power = Affix("weapon_power");

        Assert.Equal(4, power.ValueAt(1, 4));
        Assert.Equal(6, power.ValueAt(2, 4)); // 4 + 1.5 → 5.5 → 6
        Assert.Equal(7, power.ValueAt(3, 4));
        Assert.Equal(4, power.ValueAt(0, 4)); // 레벨은 1부터
        Assert.Equal("sword", power.TagFor(Item("old_sword")));
        Assert.Null(Affix("defense").TagFor(Item("helm")));
    }

    [Fact]
    public void Rolled_options_are_distinct_in_data_order_and_in_range()
    {
        var order = Data.Affixes.Keys.ToList();
        var counts = new HashSet<int>();
        for (var seed = 0; seed < 300; seed++)
        {
            var options = Affixes.Roll(Item("helm"), 5, Data, new Random(seed));

            Assert.True(options.Count <= 2); // 투구에 붙을 수 있는 옵션은 방어·행동 간격 둘뿐
            Assert.Equal(options.Select(o => o.AffixId).Distinct(), options.Select(o => o.AffixId));
            Assert.Equal(options.Select(o => o.AffixId).OrderBy(order.IndexOf), options.Select(o => o.AffixId));
            Assert.All(options, o =>
            {
                var affix = Affix(o.AffixId);
                Assert.InRange(o.Percent, affix.ValueAt(5, affix.Min), affix.ValueAt(5, affix.Max));
            });
            counts.Add(options.Count);
        }

        Assert.Equal(new HashSet<int> { 0, 1, 2 }, counts);
        Assert.Equal(2, Affixes.Roll(Item("helm"), 1, Data, new Random(1), count: 3).Count);
        Assert.Single(Affixes.Roll(Item("helm"), 1, Data, new Random(1), count: 1));
        Assert.Empty(Affixes.Roll(Item("old_tome"), 1, Data, new Random(1), count: 3));
    }

    [Fact]
    public void Rarity_follows_the_weights()
    {
        var random = new Random(7);
        var counts = new int[4];
        const int Rolls = 20_000;
        for (var i = 0; i < Rolls; i++)
        {
            counts[Affixes.RollCount(random)]++;
        }

        for (var count = 0; count < 4; count++)
        {
            Assert.InRange(counts[count] * 100.0 / Rolls, Affixes.RarityWeights[count] - 1.5, Affixes.RarityWeights[count] + 1.5);
        }

        Assert.Equal(ItemRarity.Common, new ItemInstance(1, "helm", 1).Rarity);
        Assert.Equal(ItemRarity.Rare, new ItemInstance(1, "helm", 1, [new("defense", 4), new("haste", 1)]).Rarity);
    }

    [Fact]
    public void Option_bonuses_reach_combat_without_mastery_power_and_capped_sums_are_cut()
    {
        var company = NewCompany(new Dictionary<EquipmentSlot, ItemInstance>
        {
            [EquipmentSlot.MainHand] = new(1, "old_sword", 3, [new("weapon_power", 5), new("haste", 4)]),
            [EquipmentSlot.Head] = new(2, "helm", 3, [new("defense", 6), new("haste", 4)]),
        });
        var member = company.Member("a");
        member.AddMasteryXp("sword", MasteryProgression.XpForLevel(25));

        // 행동 간격 감소는 한 멤버 합계 5%까지: 4 + 1. 숙련 아이템 파워(Lv25 +50%)는 옵션에 붙지 않는다.
        Assert.Equal(
            [new ItemBonus(BonusKind.PowerPercent, 5, "sword"), new ItemBonus(BonusKind.DelayReductionPercent, 4),
             new ItemBonus(BonusKind.DelayReductionPercent, 1), new ItemBonus(BonusKind.DefensePercent, 6)],
            member.ToCombatantSetup(Data, 0).ItemBonuses);
        var skills = member.CombatSkills(Data);
        Assert.Equal(5, skills.Bonus(BonusKind.DelayReductionPercent));
        Assert.Equal(5, skills.Bonus(BonusKind.PowerPercent, ["sword"]));
        Assert.Equal(0, skills.Bonus(BonusKind.PowerPercent, ["bow"]));
    }

    [Fact]
    public void Crafting_makes_a_magic_piece_at_the_deepest_cleared_depth()
    {
        var company = NewCompany();
        company.AddToStash("iron", Data, 6);
        company.AddToStash("shard", Data, 2);

        var first = company.Craft("steel_helm", Data)!;
        company.RecordClear("deep", new ClearResult(1, 1, 0));
        var second = company.Craft("steel_helm", Data)!;

        Assert.Equal((1, 4), (first.Level, second.Level)); // 아무것도 깨지 않았으면 1, 깊이 4를 깼으면 4
        Assert.All([first, second], made => Assert.Equal(ItemRarity.Magic, made.Rarity));
        Assert.Equal(4, company.DeepestDepth(Data));
    }

    [Fact]
    public void Dismantling_gives_more_material_for_higher_rarity()
    {
        var company = NewCompany();
        var common = company.MintGear("helm", 1, null);
        var magic = company.MintGear("boots", 1, [new("defense", 4)]);
        var rare = company.MintGear("helm", 2, [new("defense", 4), new("haste", 1)]);
        foreach (var gear in new[] { common, magic, rare })
        {
            company.StoreGear(gear);
        }

        // 투구는 같은 부위·계열(판금 머리)의 제작법인 강철 투구에서 가장 많이 드는 철 조각을 준다. 판금 장화는 제작법이 없어 분해할 수 없다.
        Assert.Equal(("iron", 1), company.Dismantle(common.Uid, Data));
        Assert.Equal(("iron", 3), company.Dismantle(rare.Uid, Data));
        Assert.Equal("분해해도 나오는 재료가 없습니다", company.WhyCannotDismantle(magic.Uid, Data));
        Assert.Null(company.Dismantle(magic.Uid, Data));
        Assert.Equal(4, company.StashCount("iron"));
        Assert.Equal([magic], company.Gear);
        Assert.Equal("창고에 없습니다", company.WhyCannotDismantle(common.Uid, Data));

        Assert.Equal(("iron", 2), Affixes.DismantleYield(new ItemInstance(99, "steel_helm", 1, [new("haste", 1)]), Data));
        Assert.Equal(("iron", 3), Affixes.DismantleYield(new ItemInstance(99, "steel_helm", 1, [new("defense", 4), new("haste", 1), new("weapon_power", 3)]), Data));

        company.Expedition = new Triangle.Core.Expeditions.Expedition("deep", 1, 0, [], 0, new Dictionary<string, int>(), null);
        Assert.Equal("원정 중에는 분해할 수 없습니다", company.WhyCannotDismantle(magic.Uid, Data));
    }
}
