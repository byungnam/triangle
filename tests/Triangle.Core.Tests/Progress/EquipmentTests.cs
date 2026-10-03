using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

/// <summary>부위 슬롯, 두손 무기, 해제, 착용 조건, 행동 칸 선택.</summary>
public class EquipmentTests
{
    private static readonly GameData Data = GameDataLoader.Parse(
        """
        [ { "id": "sword", "name": "검", "kind": "Weapon" },
          { "id": "bow", "name": "활", "kind": "Weapon" },
          { "id": "plate", "name": "판금", "kind": "Armor" } ]
        """,
        """[ { "id": "archery", "name": "활 숙련", "mastery": "bow" } ]""",
        """
        [ { "id": "strike", "name": "공격", "power": 10, "universal": true },
          { "id": "slash", "name": "베기", "power": 10 },
          { "id": "shot", "name": "화살", "power": 10 },
          { "id": "snipe", "name": "저격", "power": 10 },
          { "id": "poison", "name": "독화살", "power": 10 } ]
        """,
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "sword", "name": "검", "slot": "MainHand", "mastery": "sword", "abilities": [ { "options": [ "slash" ] } ] },
          { "id": "bow", "name": "활", "slot": "MainHand", "mastery": "bow", "twoHanded": true,
            "bonuses": [ { "kind": "PowerPercent", "percent": 5, "tag": "bow" } ],
            "abilities": [ { "options": [ "shot", "snipe" ] }, { "options": [ "poison" ] } ] },
          { "id": "long_bow", "name": "장궁", "slot": "MainHand", "mastery": "bow", "twoHanded": true, "tier": 2,
            "requirements": [ { "skillId": "archery", "level": 1 } ], "abilities": [ { "options": [ "snipe" ] } ] },
          { "id": "shield", "name": "방패", "slot": "OffHand", "mastery": "sword",
            "bonuses": [ { "kind": "DefensePercent", "percent": 10 } ] },
          { "id": "helm", "name": "투구", "slot": "Head", "mastery": "plate",
            "bonuses": [ { "kind": "DefensePercent", "percent": 5 }, { "kind": "MaxHpPercent", "percent": 3 } ] },
          { "id": "ore", "name": "광석", "slot": "Material", "price": 5 } ]
        """);

    private static Company NewCompany(params string[] stash)
    {
        var member = new PartyMember("a", "A", new Stats(10, 10, 20, 10, 10), Row.Front, TestGear.Of("sword", offHand: "shield"), null,
            new Dictionary<string, int> { ["bow"] = 100 }, new Dictionary<string, int>(),
            [[new Tactic(1, Condition.Always, 0, "slash"), new Tactic(2, Condition.Always, 0, "strike")]]);
        var company = new Company([member], ["a"], 0, new Dictionary<string, int>(), 0, 1);
        foreach (var item in stash)
        {
            company.AddToStash(item);
        }

        return company;
    }

    [Fact]
    public void Items_go_to_their_own_slot_and_the_old_item_returns_to_the_stash()
    {
        var company = NewCompany("helm", "ore");

        Assert.True(company.Equip("a", "helm", Data));
        Assert.Equal("helm", company.Member("a").ItemIn(EquipmentSlot.Head));
        Assert.Equal("장비가 아닙니다", company.WhyCannotEquip("a", "ore", Data));
        Assert.False(company.Equip("a", "ore", Data));
        Assert.Equal(1, company.StashCount("ore"));
    }

    [Fact]
    public void Two_handed_weapon_sends_the_off_hand_to_the_stash_and_blocks_it()
    {
        var company = NewCompany("bow");

        Assert.True(company.Equip("a", "bow", Data));

        var member = company.Member("a");
        Assert.Null(member.ItemIn(EquipmentSlot.OffHand));
        Assert.Equal((1, 1), (company.StashCount("sword"), company.StashCount("shield")));
        Assert.Equal("두손 무기를 들고 있습니다", company.WhyCannotEquip("a", "shield", Data));
        Assert.False(company.Equip("a", "shield", Data));
    }

    [Fact]
    public void Unequip_puts_the_item_in_the_stash()
    {
        var company = NewCompany();

        Assert.True(company.Unequip("a", EquipmentSlot.OffHand, Data));
        Assert.False(company.Unequip("a", EquipmentSlot.OffHand, Data));
        Assert.Null(company.Member("a").ItemIn(EquipmentSlot.OffHand));
        Assert.Equal(1, company.StashCount("shield"));

        // 주무기를 빼면 그 행동을 쓰는 전술이 잠긴다. 공용 행동은 그대로 쓴다.
        Assert.True(company.Unequip("a", EquipmentSlot.MainHand, Data));
        Assert.Equal([0], company.Member("a").LockedTacticIndexes(Data, 0));
        Assert.Null(company.Member("a").WeaponMastery(Data));
    }

    [Fact]
    public void Equip_checks_tier_requirements()
    {
        var company = NewCompany("long_bow");

        Assert.Equal("요구: 활 숙련 Lv1", company.WhyCannotEquip("a", "long_bow", Data));
        Assert.False(company.Equip("a", "long_bow", Data));

        Assert.True(company.Member("a").Learn("archery", Data));
        Assert.True(company.Equip("a", "long_bow", Data));
    }

    [Fact]
    public void Only_chosen_options_are_usable_and_changing_a_choice_locks_tactics()
    {
        var company = NewCompany("bow");
        company.Equip("a", "bow", Data);
        var member = company.Member("a");
        member.TacticSets[0].Replace(0, Condition.Always, 0, "shot");
        member.TacticSets[0].Add(Condition.Always, 0, "poison");

        Assert.Equal(["shot", "poison"], member.ChosenAbilities(EquipmentSlot.MainHand, Data));
        Assert.Equal(new HashSet<string> { "shot", "poison" }, member.GrantedActions(Data));
        Assert.Empty(member.LockedTacticIndexes(Data, 0));

        Assert.True(company.ChooseAbility("a", EquipmentSlot.MainHand, 0, "snipe", Data));
        Assert.Equal(["snipe", "poison"], member.ChosenAbilities(EquipmentSlot.MainHand, Data));
        Assert.Equal([0], member.LockedTacticIndexes(Data, 0));
        Assert.True(company.HasLockedTactics(Data));

        Assert.False(company.ChooseAbility("a", EquipmentSlot.MainHand, 0, "poison", Data)); // 그 칸의 후보가 아니다
        Assert.False(company.ChooseAbility("a", EquipmentSlot.MainHand, 2, "shot", Data)); // 없는 칸
        Assert.False(company.ChooseAbility("a", EquipmentSlot.Head, 0, "shot", Data)); // 빈 부위
    }

    [Fact]
    public void Worn_item_bonuses_reach_the_combat_setup()
    {
        var company = NewCompany("helm");
        company.Equip("a", "helm", Data);

        var setup = company.Member("a").ToCombatantSetup(Data, 0);

        Assert.Equal(
            [new ItemBonus(BonusKind.DefensePercent, 10), new ItemBonus(BonusKind.DefensePercent, 5), new ItemBonus(BonusKind.MaxHpPercent, 3)],
            setup.ItemBonuses);
        Assert.Equal(["plate"], setup.ArmorPieces);
        Assert.Equal("sword", setup.Weapon);
        Assert.Equal(new HashSet<string> { "slash" }, setup.GrantedActions);
        Assert.Equal(15, company.Member("a").CombatSkills(Data).Bonus(BonusKind.DefensePercent));
    }

    [Fact]
    public void Mastery_level_raises_item_bonuses_by_two_percent_per_level()
    {
        var company = NewCompany("helm");
        company.Equip("a", "helm", Data);
        var member = company.Member("a");
        member.AddMasteryXp("sword", MasteryProgression.XpForLevel(5));
        member.AddMasteryXp("plate", MasteryProgression.XpForLevel(25));

        // 방패(검 Lv5): 10 × 110% = 11. 투구(판금 Lv25): 5 × 150% = 7.5 → 8, 3 × 150% = 4.5 → 5.
        Assert.Equal(
            [new ItemBonus(BonusKind.DefensePercent, 11), new ItemBonus(BonusKind.DefensePercent, 8), new ItemBonus(BonusKind.MaxHpPercent, 5)],
            member.ItemBonuses(Data));
    }

    [Fact]
    public void Equipment_and_choices_are_locked_on_an_expedition()
    {
        var company = NewCompany("helm");
        company.Expedition = new Triangle.Core.Expeditions.Expedition("z", 1, 0, [], 0, new Dictionary<string, int>(), null);

        Assert.False(company.Equip("a", "helm", Data));
        Assert.False(company.Unequip("a", EquipmentSlot.MainHand, Data));
        Assert.False(company.ChooseAbility("a", EquipmentSlot.MainHand, 0, "slash", Data));
    }
}
