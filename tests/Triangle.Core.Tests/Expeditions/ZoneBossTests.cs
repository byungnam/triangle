using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Expeditions;

public class ZoneBossTests
{
    private static readonly GameData Data = GameDataLoader.Parse(
        """
        [ { "id": "sword", "name": "검", "kind": "Weapon" },
          { "id": "plate", "name": "판금", "kind": "Armor" } ]
        """,
        "[]",
        """[ { "id": "strike", "name": "공격", "power": 10, "universal": true } ]""",
        """
        [ { "id": "dummy", "name": "허수아비", "units": [ { "id": "d", "name": "적", "row": "Front",
            "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 10 } } ] },
          { "id": "boss", "name": "보스", "units": [ { "id": "b", "name": "보스", "row": "Front",
            "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 10 } } ] } ]
        """,
        itemsJson: """
        [ { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword" },
          { "id": "steel_sword", "name": "강철 검", "slot": "MainHand", "mastery": "sword", "tier": 2 },
          { "id": "ore", "name": "광석", "slot": "Material" } ]
        """,
        zonesJson: """
        [ { "id": "outer", "name": "바깥", "difficulty": 1, "maxBattles": 2, "encounters": [ { "encounterId": "dummy", "weight": 1 } ],
            "rewards": { "goldMin": 10, "goldMax": 10, "clearBonusGold": 5 },
            "boss": { "encounterId": "boss", "clue": "선이 곧다.", "firstClearGold": 100, "firstClearItems": [ "ore", "ore" ] } },
          { "id": "middle", "name": "중간", "difficulty": 2, "maxBattles": 1, "encounters": [ { "encounterId": "dummy", "weight": 1 } ] },
          { "id": "inner", "name": "안쪽", "difficulty": 3, "maxBattles": 1, "encounters": [ { "encounterId": "dummy", "weight": 1 } ] } ]
        """);

    private static PartyMember Member(string id, string weapon = "old_sword") =>
        new(id, id.ToUpperInvariant(), new Stats(10, 10, 20, 10, 10), Row.Front, TestGear.Of(mainHand: weapon),
            new Dictionary<string, int>(), new Dictionary<string, int>(), [[new Tactic(1, Condition.Always, 0, "strike")]]);

    private static Company NewCompany(params PartyMember[] members) =>
        new(members, members.Select(m => m.Id), 0, new Dictionary<string, int>(), activeTacticSet: 0, nextSeed: 42);

    private static CombatResult Result(CombatOutcome outcome, Company company, params (string Id, int Hp)[] allies)
    {
        var combatants = allies.Select(a =>
        {
            var setup = company.Member(a.Id).ToCombatantSetup(Data, 0);
            return new Combatant(setup, CombatSide.Ally, CombatRules.Default, new SkillSet(setup.Skills, Data.Skills)) { Hp = a.Hp };
        }).ToList();
        return new CombatResult(outcome, 0, [], combatants);
    }

    /// <summary>바깥 원정을 두 전투 모두 이긴다 (두 번째가 보스). 끝난 원정의 요약을 돌려준다.</summary>
    private static ExpeditionSummary ClearOuter(Company company, params (string Id, int Hp)[] allies)
    {
        ExpeditionRules.Start(company, Data, "outer");
        Assert.Null(ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, allies)));
        return ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, allies.Where(a => a.Hp > 0).ToArray()))!;
    }

    [Fact]
    public void Last_battle_is_always_the_boss()
    {
        var company = NewCompany(Member("a"));
        var expedition = ExpeditionRules.Start(company, Data, "outer");

        Assert.False(ExpeditionRules.IsBossBattle(expedition, Data));
        Assert.Equal("dummy", ExpeditionRules.NextEncounter(expedition, Data));

        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50)));

        Assert.True(ExpeditionRules.IsBossBattle(expedition, Data));
        Assert.Equal("boss", ExpeditionRules.NextEncounter(expedition, Data));
        Assert.Equal(["b"], ExpeditionRules.Fight(company, Data).Combatants.Where(c => c.Side == CombatSide.Enemy).Select(c => c.Id));
    }

    [Fact]
    public void Next_depth_opens_only_after_the_previous_zone_is_cleared()
    {
        var company = NewCompany(Member("a"));

        Assert.Null(Depths.WhyZoneLocked(company, Data, "outer"));
        Assert.Equal("바깥 클리어가 필요합니다", ExpeditionRules.WhyCannotStart(company, Data, "middle"));
        Assert.Throws<InvalidOperationException>(() => ExpeditionRules.Start(company, Data, "middle"));

        // 보스에게 지면 클리어가 아니다.
        ExpeditionRules.Start(company, Data, "outer");
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50)));
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Defeat, company, ("a", 0)));
        Assert.False(company.HasCleared("outer"));
        Assert.NotNull(ExpeditionRules.WhyCannotStart(company, Data, "middle"));

        ClearOuter(company, ("a", 50));

        Assert.Null(ExpeditionRules.WhyCannotStart(company, Data, "middle"));
        Assert.Equal("중간 클리어가 필요합니다", ExpeditionRules.WhyCannotStart(company, Data, "inner"));
    }

    [Fact]
    public void First_clear_pays_the_boss_reward_and_later_clears_only_update_records()
    {
        var company = NewCompany(Member("a"), Member("b", weapon: "steel_sword"));

        var first = ClearOuter(company, ("a", 0), ("b", 50));

        Assert.Equal(ExpeditionEnd.Cleared, first.End);
        Assert.Equal(new ClearResult(Members: 2, GearTier: 2, Downed: 1), first.Clear!.Result);
        Assert.True(first.Clear.FirstClear);
        Assert.Empty(first.Clear.Improved);
        Assert.Equal(10 + 10 + 5 + 100, first.Gold); // 전투 골드 둘 + 클리어 보너스 + 첫 클리어 보상
        Assert.Equal(2, company.StashCount("ore"));
        Assert.Equal(new ClearRecord(1, 2, 2, 1), company.ClearRecords["outer"]);

        // 강철 검을 든 b를 빼고 a 혼자 쓰러지지 않고 클리어하면: 인원·등급·쓰러짐 기록이 모두 좋아진다. 보스 보상은 없다.
        company.RemoveFromLineup("b");
        var second = ClearOuter(company, ("a", 50));

        Assert.False(second.Clear!.FirstClear);
        Assert.Equal([ClearRecordKind.FewestMembers, ClearRecordKind.LowestGearTier, ClearRecordKind.FewestDowned], second.Clear.Improved);
        Assert.Equal(10 + 10 + 5, second.Gold);
        Assert.Equal(2, company.StashCount("ore"));
        Assert.Equal(new ClearRecord(2, 1, 1, 0), company.ClearRecords["outer"]);
    }

    [Fact]
    public void Zones_without_a_boss_are_cleared_by_winning_the_last_battle()
    {
        var company = NewCompany(Member("a"));
        company.RecordClear("outer", new ClearResult(1, 1, 0));

        ExpeditionRules.Start(company, Data, "middle");
        var summary = ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50)))!;

        Assert.True(summary.Clear!.FirstClear);
        Assert.True(company.HasCleared("middle"));
    }

    [Fact]
    public void Clear_record_keeps_the_best_of_each_kind()
    {
        var (record, improved) = ClearRecord.First(new ClearResult(3, 2, 0)).Add(new ClearResult(4, 1, 2));

        Assert.Equal(new ClearRecord(2, 3, 1, 0), record);
        Assert.Equal([ClearRecordKind.LowestGearTier], improved);
    }
}
