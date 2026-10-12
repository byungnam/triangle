using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Expeditions;

public class GateTests
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
            "gate": { "encounterId": "boss", "clue": "선이 곧다.", "firstWinGold": 100, "firstWinItems": [ "ore", "ore" ] } },
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

    private static void ClearOuter(Company company)
    {
        ExpeditionRules.Start(company, Data, "outer");
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50)));
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50)));
    }

    [Fact]
    public void Next_depth_opens_only_after_the_previous_gate_and_zones_without_a_gate_do_not_block()
    {
        var company = NewCompany(Member("a"));

        Assert.Null(Depths.WhyZoneLocked(company, Data, "outer"));
        Assert.Equal("바깥의 관문을 넘어야 열립니다", ExpeditionRules.WhyCannotStart(company, Data, "middle"));
        Assert.Throws<InvalidOperationException>(() => ExpeditionRules.Start(company, Data, "middle"));

        company.RecordGateWin("outer", new GateResult(1, 1, 0));

        Assert.Null(ExpeditionRules.WhyCannotStart(company, Data, "middle"));
        Assert.Null(ExpeditionRules.WhyCannotStart(company, Data, "inner")); // 중간에는 관문이 없다
    }

    [Fact]
    public void Gate_opens_after_the_zone_is_cleared()
    {
        var company = NewCompany(Member("a"));

        Assert.Equal("바깥 원정을 한 번 클리어해야 도전할 수 있습니다", ExpeditionRules.WhyCannotStart(company, Data, "outer", gate: true));
        Assert.Equal("관문이 없는 지역입니다", Depths.WhyGateLocked(company, Data, "middle"));

        // 지면 클리어가 아니다.
        ExpeditionRules.Start(company, Data, "outer");
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Defeat, company, ("a", 0)));
        Assert.False(company.HasCleared("outer"));

        ClearOuter(company);

        Assert.True(company.HasCleared("outer"));
        Assert.Null(ExpeditionRules.WhyCannotStart(company, Data, "outer", gate: true));
    }

    [Fact]
    public void Gate_challenge_is_one_fight_against_the_boss_with_full_hp()
    {
        var company = NewCompany(Member("a"));
        ClearOuter(company);

        var expedition = ExpeditionRules.Start(company, Data, "outer", gate: true);

        Assert.True(expedition.Gate);
        Assert.Equal(1, ExpeditionRules.MaxBattles(expedition, Data));
        Assert.Equal("boss", ExpeditionRules.NextEncounter(expedition, Data));
        Assert.Equal(["b"], ExpeditionRules.Fight(company, Data).Combatants.Where(c => c.Side == CombatSide.Enemy).Select(c => c.Id));
        var a = ExpeditionRules.AllySetups(company, Data).Single();
        Assert.Equal(CombatRules.Default.MaxHp(company.Member("a").Stats, company.Member("a").CombatSkills(Data)), a.StartHp);
    }

    [Fact]
    public void First_win_pays_the_gate_reward_and_later_wins_only_update_records()
    {
        var company = NewCompany(Member("a"), Member("b", weapon: "steel_sword"));
        ClearOuter(company);
        var goldAfterClear = company.Gold;

        ExpeditionRules.Start(company, Data, "outer", gate: true);
        var first = ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 0), ("b", 50)))!;

        Assert.Equal(ExpeditionEnd.Cleared, first.End);
        Assert.True(first.Gate);
        Assert.Equal(new GateResult(Members: 2, GearTier: 2, Downed: 1), first.GateWin!.Result);
        Assert.True(first.GateWin.FirstWin);
        Assert.Empty(first.GateWin.Improved);
        Assert.Equal(100, first.Gold); // 지역 골드와 클리어 보너스는 없다
        Assert.Equal(2, company.StashCount("ore"));
        Assert.Equal(goldAfterClear + 100, company.Gold);
        Assert.Equal(new GateRecord(1, 2, 2, 1), company.GateRecords["outer"]);
        Assert.Null(ExpeditionRules.WhyCannotStart(company, Data, "middle"));

        // 한 명이 강철 검을 빼고 혼자 나가 쓰러지지 않고 이기면: 인원·등급·쓰러짐 기록이 모두 좋아진다. 보상은 없다.
        company.RemoveFromLineup("b");
        ExpeditionRules.Start(company, Data, "outer", gate: true);
        var second = ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50)))!;

        Assert.False(second.GateWin!.FirstWin);
        Assert.Equal([GateRecordKind.FewestMembers, GateRecordKind.LowestGearTier, GateRecordKind.FewestDowned], second.GateWin.Improved);
        Assert.Equal(0, second.Gold);
        Assert.Empty(second.Items);
        Assert.Equal(new GateRecord(2, 1, 1, 0), company.GateRecords["outer"]);
    }

    [Fact]
    public void Losing_a_gate_gives_nothing_and_records_nothing()
    {
        var company = NewCompany(Member("a"));
        ClearOuter(company);
        var gold = company.Gold;

        ExpeditionRules.Start(company, Data, "outer", gate: true);
        var summary = ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Defeat, company, ("a", 0)))!;

        Assert.Equal(ExpeditionEnd.Wiped, summary.End);
        Assert.Null(summary.GateWin);
        Assert.Equal(gold, company.Gold);
        Assert.False(company.HasPassedGate("outer"));
        Assert.NotNull(ExpeditionRules.WhyCannotStart(company, Data, "middle"));
    }

    [Fact]
    public void Gate_record_keeps_the_best_of_each_kind()
    {
        var (record, improved) = GateRecord.First(new GateResult(3, 2, 0)).Add(new GateResult(4, 1, 2));

        Assert.Equal(new GateRecord(2, 3, 1, 0), record);
        Assert.Equal([GateRecordKind.LowestGearTier], improved);
    }
}
