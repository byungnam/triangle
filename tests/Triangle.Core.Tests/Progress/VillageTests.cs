using Triangle.Core.Data;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

/// <summary>마을에서 골드를 쓰는 일: 쉬기(부상 회복), 치료소, 패시브 되돌리기 (2026-10-16).</summary>
public class VillageTests
{
    private static readonly GameData Data = GameDataLoader.Parse(
        """
        [ { "id": "bow", "name": "활", "kind": "Weapon" },
          { "id": "plate", "name": "판금", "kind": "Armor" } ]
        """,
        """
        [ { "id": "archery", "name": "활 숙련", "mastery": "bow" },
          { "id": "rapid", "name": "속사", "mastery": "bow", "rank": 2, "prerequisites": [ { "skillId": "archery", "level": 2 } ] },
          { "id": "defense", "name": "방어 기술", "mastery": "plate" } ]
        """,
        """
        [ { "id": "strike", "name": "공격", "power": 10, "universal": true },
          { "id": "shot", "name": "화살", "power": 10 } ]
        """,
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "short_bow", "name": "짧은 활", "slot": "MainHand", "mastery": "bow", "actions": [ "shot" ] },
          { "id": "long_bow", "name": "긴 활", "slot": "MainHand", "mastery": "bow", "tier": 2,
            "requirements": [ { "skillId": "archery", "level": 1 } ], "actions": [ "shot" ] },
          { "id": "plate_mail", "name": "판금 갑옷", "slot": "Body", "mastery": "plate" } ]
        """);

    private static PartyMember Member(string id, int injury = 0) =>
        new(id, id.ToUpperInvariant(), new Stats(10, 10, 10, 10, 10), Row.Front, TestGear.Of("short_bow", "plate_mail"),
            new Dictionary<string, int> { ["bow"] = MasteryProgression.XpForLevel(6), ["plate"] = MasteryProgression.XpForLevel(2) },
            new Dictionary<string, int>(), [[new Tactic(1, Condition.Always, 0, "strike")]], injury);

    private static Company NewCompany(int gold, params PartyMember[] roster) =>
        new(roster, [], gold, new Dictionary<string, int>(), activeTacticSet: 0, nextSeed: 1);

    [Fact]
    public void Resting_costs_gold_per_injured_member_and_heals_one_step()
    {
        var company = NewCompany(100, Member("a", injury: 2), Member("b", injury: 1), Member("c"));

        Assert.Equal(80, company.RestCost);
        var recovered = company.Rest();

        Assert.Equal(["b"], recovered.Select(m => m.Id));
        Assert.Equal(20, company.Gold);
        Assert.Equal((1, 0, 0), (company.Member("a").Injury, company.Member("b").Injury, company.Member("c").Injury));
        Assert.Equal(40, company.RestCost);
    }

    [Fact]
    public void Resting_with_too_little_gold_spends_what_is_left_so_the_company_never_gets_stuck()
    {
        var company = NewCompany(10, Member("a", injury: 1));
        Assert.Single(company.Rest());
        Assert.Equal(0, company.Gold);
    }

    [Fact]
    public void Cannot_rest_without_injuries()
    {
        var company = NewCompany(100, Member("a"));
        Assert.Equal("다친 멤버가 없습니다", company.WhyCannotRest());
        Assert.Empty(company.Rest());
        Assert.Equal(100, company.Gold);
    }

    [Fact]
    public void Infirmary_levels_cost_gold_and_speed_up_recovery()
    {
        var company = NewCompany(3100, Member("a", injury: 3), Member("b", injury: 3));
        var infirmary = Facilities.Find(Facilities.Infirmary)!;
        Assert.Equal((1, 1, 80), (company.ExpeditionRecovery, company.RestRecovery, company.RestCost));

        Assert.True(company.Upgrade(Facilities.Infirmary));
        Assert.Equal(3100 - infirmary.Levels[0].Price, company.Gold);
        Assert.Equal((30, 60), (company.RestCostPerInjured, company.RestCost)); // 25% 할인

        Assert.True(company.Upgrade(Facilities.Infirmary));
        Assert.Equal(2, company.ExpeditionRecovery);

        Assert.True(company.Upgrade(Facilities.Infirmary));
        Assert.Equal((2, 2, 40), (company.ExpeditionRecovery, company.RestRecovery, company.RestCost));
        Assert.Equal(3, company.FacilityLevel(Facilities.Infirmary));
        Assert.Equal("최고 레벨입니다", company.WhyCannotUpgrade(Facilities.Infirmary));
        Assert.False(company.Upgrade(Facilities.Infirmary));
    }

    [Fact]
    public void Cannot_upgrade_without_gold_or_an_unknown_facility()
    {
        var company = NewCompany(10, Member("a"));
        Assert.Equal("골드가 모자랍니다", company.WhyCannotUpgrade(Facilities.Infirmary));
        Assert.Equal("없는 시설입니다", company.WhyCannotUpgrade("tavern"));
        Assert.False(company.Upgrade(Facilities.Infirmary));
        Assert.Equal(0, company.FacilityLevel(Facilities.Infirmary));
    }

    [Fact]
    public void Resetting_a_skill_tree_refunds_its_points_for_gold()
    {
        var company = NewCompany(1000, Member("a"));
        var a = company.Member("a");
        a.Learn("archery", Data);
        a.Learn("archery", Data);
        a.Learn("rapid", Data); // 2 + 2 = 4포인트
        a.Learn("defense", Data);
        Assert.Equal(2, a.PointsAvailable("bow", Data));

        Assert.Equal(200, company.ResetSkillTreeCost("a", "bow", Data));
        Assert.True(company.ResetSkillTree("a", "bow", Data));

        Assert.Equal(800, company.Gold);
        Assert.Equal(6, a.PointsAvailable("bow", Data));
        Assert.False(a.SkillLevels.ContainsKey("archery"));
        Assert.False(a.SkillLevels.ContainsKey("rapid"));
        Assert.Equal(1, a.SkillLevels["defense"]); // 다른 트리는 그대로
        Assert.Equal(MasteryProgression.XpForLevel(6), a.MasteryXp["bow"]); // 경험치도 그대로
        Assert.Equal("배운 패시브가 없습니다", company.WhyCannotResetSkillTree("a", "bow", Data));
    }

    [Fact]
    public void Resetting_needs_enough_gold_and_can_leave_gear_unwearable()
    {
        var company = NewCompany(1000, Member("a"));
        var a = company.Member("a");
        a.Learn("archery", Data);
        company.AddToStash("long_bow", Data);
        Assert.True(company.Equip("a", "long_bow", Data));

        company.Gold = 10;
        Assert.Equal("골드가 모자랍니다", company.WhyCannotResetSkillTree("a", "bow", Data));

        company.Gold = 50;
        Assert.True(company.ResetSkillTree("a", "bow", Data));
        Assert.NotEmpty(a.UnwearableSlots(Data)); // 긴 활의 요구(활 숙련 1)가 사라졌다
        Assert.NotNull(company.WhyLineupCannotFight(Data) ?? (company.AddToLineup("a") ? company.WhyLineupCannotFight(Data) : null));
    }
}
