using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Expeditions;

public class ExpeditionTests
{
    private static string Enemy(string id, int str, int vital, bool attacks)
    {
        var tactics = attacks ? """{ "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" }""" : "";
        return $$"""
            { "id": "{{id}}", "name": "{{id}}", "units": [ { "id": "{{id}}_1", "name": "적", "row": "Front",
              "stats": { "str": {{str}}, "dex": 1, "vital": {{vital}}, "intel": 1, "speed": 10 }, "tactics": [ {{tactics}} ] } ] }
            """;
    }

    private const string ItemsJson = """
        [ { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword" },
          { "id": "old_shield", "name": "낡은 방패", "slot": "OffHand", "mastery": "sword" },
          { "id": "plate_helm", "name": "판금 투구", "slot": "Head", "mastery": "plate" },
          { "id": "plate_armor", "name": "판금 갑옷", "slot": "Body", "mastery": "plate" },
          { "id": "plate_boots", "name": "판금 장화", "slot": "Feet", "mastery": "plate" },
          { "id": "steel_sword", "name": "강철 검", "slot": "MainHand", "mastery": "sword", "tier": 2 },
          { "id": "steel_helm", "name": "강철 투구", "slot": "Head", "mastery": "plate", "tier": 2 },
          { "id": "ore", "name": "광석", "slot": "Material" } ]
        """;

    private static readonly GameData Data = GameDataLoader.Parse(
        """
        [ { "id": "sword", "name": "검", "kind": "Weapon" },
          { "id": "plate", "name": "판금", "kind": "Armor" } ]
        """,
        "[]",
        """[ { "id": "strike", "name": "공격", "power": 10, "universal": true } ]""",
        "[ " + string.Join(", ",
            Enemy("dummy", 1, 1, attacks: false),
            Enemy("wall", 1, 100_000, attacks: false),
            Enemy("brute", 10, 100_000, attacks: true)) + " ]",
        itemsJson: ItemsJson,
        zonesJson: """
        [ { "id": "forest", "name": "숲", "maxBattles": 3, "encounters": [ { "encounterId": "dummy", "weight": 1 } ],
            "rewards": { "goldMin": 10, "goldMax": 10, "depthBonusPercent": 50, "clearBonusGold": 100,
                         "itemDrops": [ { "itemId": "plate_armor", "chance": 100 } ] } },
          { "id": "mixed", "name": "갈림길", "maxBattles": 3,
            "encounters": [ { "encounterId": "dummy", "weight": 1 }, { "encounterId": "wall", "weight": 3 } ] },
          { "id": "grave", "name": "묘지", "maxBattles": 3, "permadeath": true,
            "encounters": [ { "encounterId": "dummy", "weight": 1 } ], "rewards": { "goldMin": 5, "goldMax": 5 } },
          { "id": "vault", "name": "보물고", "maxBattles": 3, "encounters": [ { "encounterId": "dummy", "weight": 1 } ],
            "rewards": { "equipmentDrop": { "chance": 100, "minTier": 2, "maxTier": 2 } } },
          { "id": "arena", "name": "투기장", "maxBattles": 3, "encounters": [ { "encounterId": "brute", "weight": 1 } ] } ]
        """,
        recruitsJson: """
        [ { "id": "rookie", "name": "신입", "names": [ "가", "나", "다" ], "row": "Front",
            "statsMin": { "str": 10, "dex": 10, "vital": 10, "intel": 10, "speed": 10 },
            "statsMax": { "str": 12, "dex": 12, "vital": 12, "intel": 12, "speed": 12 },
            "equipment": { "MainHand": "old_sword", "Body": "plate_armor" },
            "tactics": [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" } ], "price": 100 } ]
        """);

    /// <summary>부위 5개 모두 (부위 순서).</summary>
    private static readonly string[] FullGearItems = ["old_sword", "old_shield", "plate_helm", "plate_armor", "plate_boots"];

    private static readonly Dictionary<Triangle.Core.Items.EquipmentSlot, string> FullGear =
        TestGear.Of(mainHand: "old_sword", offHand: "old_shield", head: "plate_helm", body: "plate_armor", feet: "plate_boots");

    private static PartyMember Member(string id, int vital = 20) =>
        new(id, id.ToUpperInvariant(), new Stats(10, 10, vital, 10, 10), Row.Front, FullGear, null,
            new Dictionary<string, int>(), new Dictionary<string, int>(), [[new Tactic(1, Condition.Always, 0, "strike")]]);

    private static Company NewCompany(int gold = 0) =>
        new([Member("a"), Member("b")], ["a", "b"], gold, new Dictionary<string, int>(), activeTacticSet: 0, nextSeed: 42);

    private static int MaxHp(PartyMember m) => CombatRules.Default.MaxHp(m.Stats, new SkillSet(SkillSet.NoSkills, Data.Skills));

    /// <summary>전투 결과를 직접 만든다 (아군별 남은 HP·MP).</summary>
    private static CombatResult Result(CombatOutcome outcome, Company company, params (string Id, int Hp, int Mp)[] allies)
    {
        var combatants = allies.Select(a =>
        {
            var setup = company.Member(a.Id).ToCombatantSetup(Data, 0);
            return new Combatant(setup, CombatSide.Ally, CombatRules.Default, new SkillSet(setup.Skills, Data.Skills)) { Hp = a.Hp, Mp = a.Mp };
        }).ToList();
        return new CombatResult(outcome, 0, [], combatants);
    }

    [Fact]
    public void Start_fills_hp_and_mp_and_blocks_village_actions()
    {
        var company = NewCompany(gold: 1000);
        company.RerollRecruits(Data);

        var expedition = ExpeditionRules.Start(company, Data, "forest");

        Assert.Same(expedition, company.Expedition);
        Assert.All(expedition.Members, m => Assert.Equal(MaxHp(company.Member(m.Id)), m.Hp));
        Assert.Equal(100, expedition.Members[0].Mp);
        Assert.False(company.RemoveFromLineup("a"));
        Assert.False(company.Equip("a", "old_sword", Data));
        Assert.Null(company.Hire(0, Data));
        Assert.Equal("이미 원정 중입니다", ExpeditionRules.WhyCannotStart(company, Data, "forest"));
    }

    [Fact]
    public void Cannot_start_with_an_empty_lineup()
    {
        var company = NewCompany();
        company.RemoveFromLineup("a");
        company.RemoveFromLineup("b");

        Assert.Equal("출전 명단이 비어 있습니다", ExpeditionRules.WhyCannotStart(company, Data, "forest"));
    }

    [Fact]
    public void Encounter_choice_is_deterministic_and_follows_weights()
    {
        var walls = 0;
        const int runs = 2000;
        for (var seed = 0; seed < runs; seed++)
        {
            var expedition = new Expedition("mixed", seed, 0, [], 0, new Dictionary<string, int>(), null);
            var encounter = ExpeditionRules.NextEncounter(expedition, Data);
            Assert.Equal(encounter, ExpeditionRules.NextEncounter(expedition, Data));
            walls += encounter == "wall" ? 1 : 0;
        }

        // 가중치 1:3 → wall 75%.
        Assert.InRange(walls * 100 / runs, 70, 80);
    }

    [Fact]
    public void Hp_and_mp_carry_over_and_are_clamped_to_max()
    {
        var company = NewCompany();
        ExpeditionRules.Start(company, Data, "forest");
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 123, 45), ("b", 300, 7)));

        var setups = ExpeditionRules.AllySetups(company, Data);
        Assert.Equal((123, 45), (setups[0].StartHp, setups[0].StartMp));

        var result = ExpeditionRules.Fight(company, Data);
        var a = result.Combatants.Single(c => c.Id == "a");
        Assert.Equal((123, 45), (a.StartHp, a.StartMp));

        // 최대치를 넘는 시작 HP·MP는 최대치로 자른다.
        var big = CombatSimulator.Run([setups[0] with { StartHp = 1_000_000, StartMp = 1_000_000 }], Data.CreateEncounterTeam("dummy"), Data.Catalog, 1);
        var unit = big.Combatants.Single(c => c.Id == "a");
        Assert.Equal((unit.MaxHp, unit.MaxMp), (unit.StartHp, unit.StartMp));
    }

    [Fact]
    public void Downed_members_sit_out_the_rest_and_recover_in_safe_zones()
    {
        var company = NewCompany();
        ExpeditionRules.Start(company, Data, "forest");

        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 0, 0), ("b", 100, 10)));

        Assert.True(company.Expedition!.MemberState("a")!.Down);
        Assert.Equal(["b"], ExpeditionRules.AllySetups(company, Data).Select(s => s.Id));
        Assert.Equal(["b"], ExpeditionRules.Fight(company, Data).Combatants.Where(c => c.Side == CombatSide.Ally).Select(c => c.Id));
        Assert.Equal(["A"], company.Expedition.LastBattle!.Downed);

        var summary = ExpeditionRules.Return(company, Data);

        Assert.Equal(ExpeditionEnd.Returned, summary.End);
        Assert.Equal(2, company.Roster.Count);
        Assert.Equal(["a", "b"], company.Lineup);
        var next = ExpeditionRules.Start(company, Data, "forest");
        Assert.All(next.Members, m => Assert.False(m.Down));
        Assert.Equal(MaxHp(company.Member("a")), next.MemberState("a")!.Hp);
    }

    [Fact]
    public void Mastery_xp_is_granted_every_battle_even_if_the_expedition_is_lost()
    {
        var company = NewCompany();
        ExpeditionRules.Start(company, Data, "forest");

        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Defeat, company, ("a", 0, 0), ("b", 0, 0)));

        Assert.Equal(30, company.Member("a").MasteryXp["sword"]);
        Assert.Equal(30, company.Member("b").MasteryXp["plate"]);
    }

    [Fact]
    public void Victory_loot_is_carried_then_confirmed_on_return()
    {
        var company = NewCompany(gold: 7);
        ExpeditionRules.Start(company, Data, "forest");

        Assert.Null(ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50, 5), ("b", 50, 5))));
        Assert.Null(ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50, 5), ("b", 50, 5))));

        // 골드 10, 깊이 보너스 50%: 10 + 15.
        var expedition = company.Expedition!;
        Assert.Equal(25, expedition.CarriedGold);
        Assert.Equal(2, expedition.CarriedItems["plate_armor"]);
        Assert.Equal(7, company.Gold);
        Assert.True(ExpeditionRules.CanContinue(company, Data));

        var summary = ExpeditionRules.Return(company, Data);

        Assert.Equal((ExpeditionEnd.Returned, 2, 25), (summary.End, summary.Battles, summary.Gold));
        Assert.Equal(32, company.Gold);
        Assert.Equal(2, company.StashCount("plate_armor"));
        Assert.Null(company.Expedition);
    }

    [Fact]
    public void Clearing_the_last_battle_adds_the_bonus_and_returns()
    {
        var company = NewCompany();
        ExpeditionRules.Start(company, Data, "forest");

        ExpeditionSummary? summary = null;
        for (var i = 0; i < 3; i++)
        {
            summary = ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50, 5), ("b", 50, 5)));
            Assert.Equal(i == 2, summary is not null);
        }

        // 10 + 15 + 20 + 클리어 100.
        Assert.Equal(ExpeditionEnd.Cleared, summary!.End);
        Assert.Equal(145, company.Gold);
        Assert.Equal(3, company.StashCount("plate_armor"));
        Assert.Null(company.Expedition);
    }

    [Fact]
    public void Wipe_loses_carried_loot()
    {
        var company = NewCompany(gold: 7);
        ExpeditionRules.Start(company, Data, "forest");
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50, 5), ("b", 50, 5)));

        var summary = ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Defeat, company, ("a", 0, 0), ("b", 0, 0)));

        Assert.Equal((ExpeditionEnd.Wiped, 0), (summary!.End, summary.Gold));
        Assert.Equal(7, company.Gold);
        Assert.Empty(company.Stash);
        Assert.Equal(2, company.Roster.Count); // 초보 지역: 아무도 죽지 않는다
    }

    [Fact]
    public void Draw_is_a_forced_return_that_keeps_loot()
    {
        var company = NewCompany();
        ExpeditionRules.Start(company, Data, "forest");
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50, 5), ("b", 50, 5)));

        var summary = ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Draw, company, ("a", 50, 5), ("b", 50, 5)));

        Assert.Equal(ExpeditionEnd.ForcedReturn, summary!.End);
        Assert.Equal(10, company.Gold);
        Assert.Equal(1, company.StashCount("plate_armor"));
    }

    [Fact]
    public void Real_draw_against_an_unkillable_wall_ends_the_expedition()
    {
        var company = NewCompany();
        ExpeditionRules.Start(company, Data, "mixed");
        while (company.Expedition is { } e && ExpeditionRules.NextEncounter(e, Data) == "dummy")
        {
            ExpeditionRules.ApplyResult(company, Data, ExpeditionRules.Fight(company, Data));
        }

        Assert.NotNull(company.Expedition);
        var result = ExpeditionRules.Fight(company, Data);
        Assert.Equal(CombatOutcome.Draw, result.Outcome);
        Assert.Equal(ExpeditionEnd.ForcedReturn, ExpeditionRules.ApplyResult(company, Data, result)!.End);
    }

    [Fact]
    public void Equipment_drops_come_from_the_zone_tier_range()
    {
        var seen = new HashSet<string>();
        for (var seed = 0; seed < 40; seed++)
        {
            var company = new Company([Member("a")], ["a"], 0, new Dictionary<string, int>(), 0, seed);
            ExpeditionRules.Start(company, Data, "vault");
            ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50, 5)));
            var drop = Assert.Single(company.Expedition!.LastBattle!.Drops);
            Assert.Equal(2, Data.Items[drop].Tier);
            seen.Add(drop);
        }

        // T2 장비 둘 다 나온다 (재료와 T1은 나오지 않는다).
        Assert.Equal(new HashSet<string> { "steel_sword", "steel_helm" }, seen);
    }

    [Fact]
    public void Permadeath_deletes_the_fallen_and_rolls_each_piece_of_their_gear()
    {
        var company = NewCompany();
        ExpeditionRules.Start(company, Data, "grave");

        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 0, 0), ("b", 50, 5)));

        Assert.Equal(["b"], company.Roster.Select(m => m.Id));
        Assert.Equal(["b"], company.Lineup);
        var expedition = company.Expedition!;
        Assert.Equal(["b"], expedition.Members.Select(m => m.Id));
        var report = expedition.LastBattle!;
        Assert.Equal(["A"], report.Deaths);
        // 아이템마다 파괴 아니면 회수, 둘 중 하나다.
        Assert.Equal(FullGearItems.Order(), report.Destroyed.Concat(report.Recovered).Order());

        var summary = ExpeditionRules.Return(company, Data);

        Assert.Equal(["A"], summary.Deaths);
        Assert.All(report.Recovered, item => Assert.Equal(1, company.StashCount(item)));
        Assert.All(report.Destroyed, item => Assert.Equal(0, company.StashCount(item)));
    }

    [Fact]
    public void Destroy_chance_is_the_same_constant_rolled_per_item_with_the_expedition_seed()
    {
        // 결과는 시드가 같으면 같고, 여러 시드에 걸쳐 아이템마다 대략 상수 확률(50%)로 파괴된다.
        IReadOnlyList<string> Destroyed(int seed)
        {
            var company = new Company([Member("a"), Member("b")], ["a", "b"], 0, new Dictionary<string, int>(), 0, seed);
            ExpeditionRules.Start(company, Data, "grave");
            ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 0, 0), ("b", 50, 5)));
            return company.Expedition!.LastBattle!.Destroyed;
        }

        Assert.Equal(Destroyed(3), Destroyed(3));
        var counts = Enumerable.Range(0, 500).Select(seed => Destroyed(seed).Count).ToList();
        var percent = counts.Sum() * 100 / (500 * FullGearItems.Length);
        Assert.InRange(percent, ExpeditionRules.EquipmentDestroyChance - 5, ExpeditionRules.EquipmentDestroyChance + 5);
        // 독립적으로 굴리므로 전부 파괴와 전부 회수가 모두 나온다.
        Assert.Contains(FullGearItems.Length, counts);
        Assert.Contains(0, counts);
    }

    [Fact]
    public void Permadeath_wipe_deletes_everyone_and_the_cheapest_recruit_becomes_free()
    {
        var company = NewCompany(gold: 30);
        ExpeditionRules.Start(company, Data, "grave");
        ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Victory, company, ("a", 50, 5), ("b", 50, 5)));

        var summary = ExpeditionRules.ApplyResult(company, Data, Result(CombatOutcome.Defeat, company, ("a", 0, 0), ("b", 0, 0)));

        Assert.Equal(ExpeditionEnd.Wiped, summary!.End);
        Assert.Equal(["A", "B"], summary.Deaths);
        Assert.Empty(company.Roster);
        Assert.Empty(company.Lineup);
        Assert.Empty(company.Stash); // 회수한 장비도 함께 잃는다
        Assert.Equal(30, company.Gold);

        Assert.Equal(0, company.HirePrice(0));
        Assert.Equal(100, company.HirePrice(1));
        var hired = company.Hire(0, Data);
        Assert.NotNull(hired);
        Assert.Equal(30, company.Gold);
        Assert.Equal([hired.Id], company.Lineup);
        Assert.Equal(100, company.HirePrice(0)); // 이제 로스터가 있다
    }

    [Fact]
    public void Real_battles_against_a_brute_run_to_the_end()
    {
        var company = NewCompany();
        ExpeditionRules.Start(company, Data, "arena");

        var summaries = new List<ExpeditionSummary?>();
        while (company.Expedition is not null)
        {
            var before = company.Expedition.Standing.Count();
            var result = ExpeditionRules.Fight(company, Data);
            Assert.Equal(before, result.Combatants.Count(c => c.Side == CombatSide.Ally));
            summaries.Add(ExpeditionRules.ApplyResult(company, Data, result));
        }

        Assert.NotNull(summaries[^1]);
    }

    [Fact]
    public void Same_state_gives_the_same_battle()
    {
        var a = NewCompany();
        var b = NewCompany();
        ExpeditionRules.Start(a, Data, "arena");
        ExpeditionRules.Start(b, Data, "arena");

        var first = ExpeditionRules.Fight(a, Data);
        var second = ExpeditionRules.Fight(b, Data);

        Assert.Equal(first.Events, second.Events);
    }
}
