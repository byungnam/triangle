using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

public sealed class SaveGameTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "triangle-save-" + Guid.NewGuid());

    private static readonly GameData Data = GameDataLoader.Parse(
        """
        [ { "id": "relic", "name": "성구", "kind": "Weapon" },
          { "id": "sword", "name": "검", "kind": "Weapon" },
          { "id": "cloth", "name": "천", "kind": "Armor" } ]
        """,
        """
        [ { "id": "healing", "name": "치유술", "mastery": "relic" },
          { "id": "holy", "name": "신성 마법", "mastery": "relic", "rank": 3, "prerequisites": [ { "skillId": "healing", "level": 3 } ] } ]
        """,
        """
        [ { "id": "strike", "name": "공격", "power": 10, "universal": true },
          { "id": "heal", "name": "치료", "effect": "Heal", "requirements": [ { "skillId": "healing", "level": 1 } ] },
          { "id": "smite", "name": "징벌", "power": 10 } ]
        """,
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "wooden_relic", "name": "나무 성구", "slot": "MainHand", "mastery": "relic", "abilities": [ { "options": [ "heal", "smite" ] } ] },
          { "id": "silver_relic", "name": "은 성구", "slot": "MainHand", "mastery": "relic", "tier": 2,
            "requirements": [ { "skillId": "healing", "level": 1 } ], "abilities": [ { "options": [ "heal", "smite" ] } ] },
          { "id": "gold_relic", "name": "금 성구", "slot": "MainHand", "mastery": "relic", "tier": 3,
            "requirements": [ { "skillId": "healing", "level": 3 } ], "bonuses": [ { "kind": "HealPercent", "percent": 30 } ],
            "abilities": [ { "options": [ "heal" ] } ] },
          { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword" },
          { "id": "cloth_robe", "name": "천 로브", "slot": "Body", "mastery": "cloth" } ]
        """);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static Company SampleCompany()
    {
        PartyMember[] roster =
        [
            new("a", "이졸데", new Stats(10, 11, 21, 24, 12), Row.Back, TestGear.Of("wooden_relic", "cloth_robe"),
                new Dictionary<EquipmentSlot, IReadOnlyList<string>> { [EquipmentSlot.MainHand] = ["smite"] },
                new Dictionary<string, int> { ["relic"] = MasteryProgression.XpForLevel(3) + 40, ["cloth"] = 120 },
                new Dictionary<string, int> { ["healing"] = 2 },
                [[new Tactic(1, Condition.AnyAllyHpAtMost, 50, "smite"), new Tactic(2, Condition.Always, 0, "strike")],
                 [new Tactic(1, Condition.Always, 0, "strike")]]),
            new("b", "고드릭", new Stats(15, 12, 25, 20, 13), Row.Front, TestGear.Of("old_sword"), null,
                new Dictionary<string, int>(), new Dictionary<string, int>(), []),
            new("c", "후보", new Stats(9, 9, 9, 9, 9), Row.Front, TestGear.Of(), null,
                new Dictionary<string, int>(), new Dictionary<string, int>(), []),
        ];
        return new Company(roster, ["b", "a"], gold: 120, new Dictionary<string, int> { ["silver_relic"] = 2 }, activeTacticSet: 1, nextSeed: 77);
    }

    /// <summary>버전 5 세이브: 장비는 weapon·armor 아이템 ID.</summary>
    private static string Version5(string members, string lineup = "\"a\"") =>
        $$"""{ "version": 5, "gold": 10, "nextSeed": 3, "lineup": [ {{lineup}} ], "roster": [ {{members}} ] }""";

    private static string Version6(string members, string lineup = "\"a\"", string extra = "") =>
        $$"""{ "version": 6, "gold": 10, "nextSeed": 3, "lineup": [ {{lineup}} ], "roster": [ {{members}} ]{{extra}} }""";

    private static string Member(string extra, string id = "a") => $$"""
        { "id": "{{id}}", "name": "이름", "row": "Front",
          "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 }{{extra}} }
        """;

    [Fact]
    public void Round_trips_company()
    {
        var original = SampleCompany();

        var loaded = SaveGame.Deserialize(SaveGame.Serialize(original), Data);

        Assert.Equal(original.Roster.Count, loaded.Roster.Count);
        for (var i = 0; i < original.Roster.Count; i++)
        {
            var (o, l) = (original.Roster[i], loaded.Roster[i]);
            Assert.Equal((o.Id, o.Name, o.Stats, o.Row), (l.Id, l.Name, l.Stats, l.Row));
            Assert.Equal(o.Equipment, l.Equipment);
            Assert.Equal(o.ChosenAbilities(EquipmentSlot.MainHand, Data), l.ChosenAbilities(EquipmentSlot.MainHand, Data));
            Assert.Equal(o.MasteryXp, l.MasteryXp);
            Assert.Equal(o.SkillLevels, l.SkillLevels);
            for (var set = 0; set < PartyMember.TacticSetCount; set++)
            {
                Assert.Equal(o.TacticSets[set], l.TacticSets[set]);
            }
        }

        Assert.Equal(["b", "a"], loaded.Lineup);
        Assert.Equal((120, 1, 77), (loaded.Gold, loaded.ActiveTacticSet, loaded.NextSeed));
        Assert.Equal(2, loaded.StashCount("silver_relic"));
    }

    [Fact]
    public void Serialized_save_is_readable_json_with_version()
    {
        var json = SaveGame.Serialize(SampleCompany());

        Assert.Contains("\"version\": 6", json);
        Assert.Contains("\"roster\"", json);
        Assert.DoesNotContain("\"party\"", json);
        Assert.Contains("\"name\": \"이졸데\"", json);
        Assert.Contains("\"MainHand\": \"wooden_relic\"", json);
        Assert.Contains("\"abilityChoices\"", json);
        Assert.DoesNotContain("\"weapon\"", json);
        Assert.Contains("\"silver_relic\": 2", json);
        Assert.Contains("\"healing\": 2", json);
    }

    [Fact]
    public void Rejects_older_and_unknown_versions()
    {
        foreach (var version in new[] { 1, 2, 3, 4, 99 })
        {
            var e = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize($$"""{ "version": {{version}}, "party": [] }""", Data));
            Assert.Contains($"unsupported version {version}", Assert.Single(e.Errors));
        }
    }

    [Fact]
    public void Converts_version_5_weapon_and_armor_to_main_hand_and_body()
    {
        var members = Member("""
            , "weapon": "wooden_relic", "armor": "cloth_robe", "masteryXp": { "relic": 300 }, "skillLevels": { "healing": 1 },
            "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "heal" },
                              { "priority": 2, "condition": "Always", "value": 0, "actionId": "smite" } ] ]
            """);

        var company = SaveGame.Deserialize(Version5(members), Data);

        var member = company.Roster[0];
        Assert.Equal(TestGear.Of("wooden_relic", "cloth_robe"), member.Equipment);
        Assert.Equal(["heal"], member.ChosenAbilities(EquipmentSlot.MainHand, Data)); // 첫 옵션
        Assert.Equal([1], member.LockedTacticIndexes(Data, 0)); // 징벌은 고르지 않았으므로 잠긴다
        Assert.Equal((10, 3, 300), (company.Gold, company.NextSeed, member.MasteryXp["relic"]));
    }

    [Fact]
    public void Rejects_version_5_items_that_no_longer_exist_or_moved_slot()
    {
        var errors = Assert.Throws<SaveGameException>(() =>
            SaveGame.Deserialize(Version5(Member(""", "weapon": "cloth_robe", "armor": "ghost" """)), Data)).Errors;

        Assert.Contains("save member 'a': equipment: MainHand: 'cloth_robe' is Body", errors);
        Assert.Contains("save member 'a': equipment: Body: unknown item 'ghost'", errors);
    }

    [Fact]
    public void Unmet_requirements_load_as_unwearable_and_block_fighting()
    {
        // 금 성구는 치유술 3을 요구하는데 1뿐이다 (게임 데이터가 바뀐 경우).
        var member = Member("""
            , "equipment": { "MainHand": "gold_relic" }, "masteryXp": { "relic": 300 }, "skillLevels": { "healing": 1 },
            "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "heal" } ] ]
            """);

        var company = SaveGame.Deserialize(Version6(member, extra: """, "stash": { "wooden_relic": 1 }"""), Data);

        var m = company.Roster[0];
        Assert.Equal([EquipmentSlot.MainHand], m.UnwearableSlots(Data));
        Assert.Empty(m.ItemBonuses(Data));
        Assert.Null(m.WeaponMastery(Data));
        Assert.Equal([0], m.LockedTacticIndexes(Data, 0));
        Assert.Equal("착용할 수 없는 장비를 바꿔야 합니다", company.WhyLineupCannotFight(Data));

        Assert.True(company.Equip("a", "wooden_relic", Data));
        Assert.Null(company.WhyLineupCannotFight(Data));
        Assert.Equal(1, company.StashCount("gold_relic"));
    }

    [Fact]
    public void Ability_choices_that_are_no_longer_options_fall_back_to_the_first()
    {
        var member = Member("""
            , "equipment": { "MainHand": "wooden_relic" }, "abilityChoices": { "MainHand": [ "removed" ] }
            """);

        var company = SaveGame.Deserialize(Version6(member), Data);

        Assert.Equal(["heal"], company.Roster[0].ChosenAbilities(EquipmentSlot.MainHand, Data));
    }

    [Fact]
    public void Rejects_references_missing_from_game_data_and_wrong_slots()
    {
        var member = Member("""
            , "equipment": { "MainHand": "cloth_robe", "Body": "ghost_armor" },
            "masteryXp": { "ghost": 100 },
            "skillLevels": { "ghost_skill": 1 },
            "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "removed_action" } ] ]
            """);

        var errors = Assert.Throws<SaveGameException>(() =>
            SaveGame.Deserialize(Version6(member, extra: """, "stash": { "ghost_item": 1, "old_sword": 0 }"""), Data)).Errors;

        Assert.Contains("save member 'a': equipment: MainHand: 'cloth_robe' is Body", errors);
        Assert.Contains("save member 'a': equipment: Body: unknown item 'ghost_armor'", errors);
        Assert.Contains("save member 'a': unknown mastery 'ghost'", errors);
        Assert.Contains("save member 'a': unknown skill 'ghost_skill'", errors);
        Assert.Contains("save member 'a' set 1 tactic 1: unknown action 'removed_action'", errors);
        Assert.Contains("save: stash has unknown item 'ghost_item'", errors);
        Assert.Contains("save: stash count of 'old_sword' must be at least 1, got 0", errors);
    }

    [Fact]
    public void Rejects_bad_lineups()
    {
        var two = Member("") + ", " + Member("", id: "b");
        var errors = Assert.Throws<SaveGameException>(() =>
            SaveGame.Deserialize(Version6(two, lineup: "\"a\", \"a\", \"ghost\""), Data)).Errors;
        Assert.Contains("save: lineup lists 'a' more than once", errors);
        Assert.Contains("save: lineup member 'ghost' is not in the roster", errors);

        var six = string.Join(", ", Enumerable.Range(1, 6).Select(i => Member("", id: $"m{i}")));
        var lineup = string.Join(", ", Enumerable.Range(1, 6).Select(i => $"\"m{i}\""));
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Version6(six, lineup), Data)).Errors;
        Assert.Contains("save: lineup has 6 members (at most 5)", errors);
    }

    [Fact]
    public void Empty_lineup_is_allowed_in_the_village()
    {
        var company = SaveGame.Deserialize(Version6(Member(""), lineup: ""), Data);

        Assert.Empty(company.Lineup);
        Assert.Single(company.Roster);
    }

    [Fact]
    public void Rejects_skills_without_prerequisites_or_more_points_than_mastery_gives()
    {
        // 신성 마법은 치유술 3 필요. 성구 숙련 Lv 1인데 포인트 1 + 3 = 4를 썼다.
        var member = Member("""
            , "masteryXp": { "relic": 100 },
            "skillLevels": { "healing": 1, "holy": 1 }
            """);

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Version6(member), Data)).Errors;

        Assert.Contains("save member 'a': 'holy' needs 'healing' level 3", errors);
        Assert.Contains("save member 'a': spent 4 points in 'relic' but mastery level is 1", errors);
    }

    [Fact]
    public void Loads_tactics_whose_action_became_locked_and_reports_them()
    {
        // 치료는 성구의 행동 칸에 있는데 검을 들었다 (장비를 바꾼 경우).
        var member = Member("""
            , "equipment": { "MainHand": "old_sword" }, "masteryXp": { "relic": 300 }, "skillLevels": { "healing": 1 },
            "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" },
                              { "priority": 2, "condition": "Always", "value": 0, "actionId": "heal" } ] ]
            """);

        var company = SaveGame.Deserialize(Version6(member, extra: """, "stash": { "silver_relic": 1 }"""), Data);

        Assert.Equal([1], company.Roster[0].LockedTacticIndexes(Data, 0));
        Assert.True(company.HasLockedTactics(Data));
        Assert.True(company.Equip("a", "silver_relic", Data));
        Assert.Empty(company.Roster[0].LockedTacticIndexes(Data, 0));
    }

    [Fact]
    public void Rejects_bad_tactic_set_data_and_version_mixups()
    {
        var tooMany = Member(""", "tacticSets": [ [], [], [] ]""");
        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Version6(tooMany), Data)).Errors;
        Assert.Contains("save member 'a': at most 2 tactic sets, got 3", errors);

        var badActive = Version6(Member(""), extra: """, "activeTacticSet": 5""");
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(badActive, Data)).Errors;
        Assert.Contains("save: activeTacticSet must be 0-1, got 5", errors);

        var weaponInV6 = Version6(Member(""", "weapon": "old_sword" """));
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(weaponInV6, Data)).Errors;
        Assert.Contains("save member 'a': 'weapon' and 'armor' are version 5 fields; use 'equipment'", errors);
    }

    [Fact]
    public void Rejects_malformed_json_and_duplicate_members()
    {
        Assert.Throws<SaveGameException>(() => SaveGame.Deserialize("{ not json", Data));

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Version6(Member("") + ", " + Member("")), Data)).Errors;
        Assert.Contains("save: duplicate member id 'a'", errors);
    }

    [Fact]
    public void Saves_and_restores_an_expedition_in_progress()
    {
        var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        var company = StartingCompany.Create(data, seed: 11);
        var zone = data.Zones.Values.First(z => !z.Permadeath);
        ExpeditionRules.Start(company, data, zone.Id);
        ExpeditionRules.ApplyResult(company, data, ExpeditionRules.Fight(company, data));
        Assert.NotNull(company.Expedition); // 첫 전투로는 끝나지 않는 지역이어야 한다

        var loaded = SaveGame.Deserialize(SaveGame.Serialize(company), data);

        var (a, b) = (company.Expedition!, loaded.Expedition!);
        Assert.Equal((a.ZoneId, a.Seed, a.BattleIndex, a.CarriedGold), (b.ZoneId, b.Seed, b.BattleIndex, b.CarriedGold));
        Assert.Equal(a.Members, b.Members);
        Assert.Equal(a.CarriedItems, b.CarriedItems);
        Assert.Equal(a.LastBattle!.Xp, b.LastBattle!.Xp);
        Assert.Equal((a.LastBattle.Outcome, a.LastBattle.EncounterId, a.LastBattle.Gold), (b.LastBattle.Outcome, b.LastBattle.EncounterId, b.LastBattle.Gold));
        Assert.Equal(company.RecruitOffers, loaded.RecruitOffers);
        Assert.Equal(company.NextSeed, loaded.NextSeed);

        // 불러온 뒤의 다음 전투도 같다.
        Assert.Equal(ExpeditionRules.Fight(company, data).Events, ExpeditionRules.Fight(loaded, data).Events);
    }

    [Fact]
    public void Rejects_inconsistent_expeditions()
    {
        var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        var zone = data.Zones.Values.First();
        var json = SaveGame.Serialize(StartingCompany.Create(data, seed: 1));
        var lineup = StartingCompany.Create(data, seed: 1).Lineup;
        var members = string.Join(", ", lineup.Skip(1).Select(id => $$"""{ "id": "{{id}}", "hp": 10, "mp": -1, "down": true }"""));
        var expedition = $$"""
            "expedition": { "zoneId": "{{zone.Id}}", "seed": 1, "battleIndex": {{zone.MaxBattles}},
              "members": [ {{members}}, { "id": "ghost", "hp": 1, "mp": 1, "down": true } ], "carriedItems": { "ghost_item": 1 } },
            "version"
            """;

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(json.Replace("\"version\"", expedition), data)).Errors;

        Assert.Contains($"save expedition: battleIndex must be 0-{zone.MaxBattles - 1}, got {zone.MaxBattles}", errors);
        Assert.Contains("save expedition: needs at least one standing member", errors);
        Assert.Contains("save expedition: member 'ghost' is not in the lineup", errors);
        Assert.Contains($"save expedition: lineup member '{lineup[0]}' is not on the expedition", errors);
        Assert.Contains($"save expedition member '{lineup[1]}': mp must not be negative, got -1", errors);
        Assert.Contains("save expedition: carries unknown item 'ghost_item'", errors);

        var unknownZone = json.Replace("\"version\"", """ "expedition": { "zoneId": "nowhere", "seed": 1, "battleIndex": 0, "members": [] }, "version" """);
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(unknownZone, data)).Errors;
        Assert.Contains("save expedition: unknown zone 'nowhere'", errors);
    }

    [Fact]
    public void Store_starts_new_game_when_no_save_exists()
    {
        var store = new SaveStore(Path.Combine(_dir, "save.json"));

        var result = store.Load(Data, SampleCompany);

        Assert.Equal(LoadStatus.NewGame, result.Status);
        Assert.False(File.Exists(store.Path));
    }

    [Fact]
    public void Store_saves_and_loads_edits()
    {
        var store = new SaveStore(Path.Combine(_dir, "nested", "save.json"));
        var company = SampleCompany();
        company.Roster[1].ToggleRow();
        company.Roster[1].TacticSets[1].Add(Condition.EveryNthTurn, 3, "strike");
        company.ActiveTacticSet = 0;
        Assert.True(company.Equip("a", "silver_relic", Data));
        Assert.True(company.ChooseAbility("a", EquipmentSlot.MainHand, 0, "smite", Data));

        store.Save(company);
        var result = store.Load(Data, () => throw new InvalidOperationException("should not create new"));

        Assert.Equal(LoadStatus.Loaded, result.Status);
        Assert.Equal(Row.Back, result.Company.Roster[1].Row);
        Assert.Equal(new Tactic(1, Condition.EveryNthTurn, 3, "strike"), Assert.Single(result.Company.Roster[1].TacticSets[1]));
        Assert.Equal(0, result.Company.ActiveTacticSet);
        Assert.Equal("silver_relic", result.Company.Roster[0].ItemIn(EquipmentSlot.MainHand));
        Assert.Equal(["smite"], result.Company.Roster[0].ChosenAbilities(EquipmentSlot.MainHand, Data));
        Assert.Equal(1, result.Company.StashCount("wooden_relic"));
        Assert.False(File.Exists(store.Path + ".tmp"));
    }

    [Fact]
    public void Store_keeps_broken_save_aside_and_starts_new_game()
    {
        Directory.CreateDirectory(_dir);
        var store = new SaveStore(Path.Combine(_dir, "save.json"));
        File.WriteAllText(store.Path, "{ broken");

        var result = store.Load(Data, SampleCompany);

        Assert.Equal(LoadStatus.Recovered, result.Status);
        Assert.NotEmpty(result.Errors!);
        Assert.False(File.Exists(store.Path));
        Assert.Equal("{ broken", File.ReadAllText(result.BrokenFilePath!));
    }
}
