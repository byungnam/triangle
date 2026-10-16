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
          { "id": "fire", "name": "화염", "kind": "Weapon" },
          { "id": "cloth", "name": "천", "kind": "Armor" } ]
        """,
        """
        [ { "id": "healing", "name": "치유술", "mastery": "relic" },
          { "id": "holy", "name": "신성 마법", "mastery": "relic", "rank": 3, "prerequisites": [ { "skillId": "healing", "level": 3 } ] },
          { "id": "pyromancy", "name": "화염 마법", "mastery": "fire" } ]
        """,
        """
        [ { "id": "strike", "name": "공격", "power": 10, "universal": true },
          { "id": "heal", "name": "치료", "effect": "Heal", "requirements": [ { "skillId": "healing", "level": 1 } ] },
          { "id": "smite", "name": "징벌", "power": 10 } ]
        """,
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "wooden_relic", "name": "나무 성구", "slot": "MainHand", "mastery": "relic", "actions": [ "heal", "smite" ] },
          { "id": "silver_relic", "name": "은 성구", "slot": "MainHand", "mastery": "relic", "tier": 2,
            "requirements": [ { "skillId": "healing", "level": 1 } ], "actions": [ "heal", "smite" ] },
          { "id": "gold_relic", "name": "금 성구", "slot": "MainHand", "mastery": "relic", "tier": 3,
            "requirements": [ { "skillId": "healing", "level": 3 } ], "bonuses": [ { "kind": "HealPercent", "percent": 30 } ],
            "actions": [ "heal" ] },
          { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword" },
          { "id": "cloth_robe", "name": "천 로브", "slot": "Body", "mastery": "cloth" },
          { "id": "iron_scrap", "name": "철 조각", "slot": "Material" } ]
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
                new Dictionary<string, int> { ["relic"] = MasteryProgression.XpForLevel(3) + 40, ["cloth"] = 120 },
                new Dictionary<string, int> { ["healing"] = 2 },
                [[new Tactic(1, Condition.AnyAllyHpAtMost, 50, "smite"), new Tactic(2, Condition.Always, 0, "strike")],
                 [new Tactic(1, Condition.Always, 0, "strike")]]),
            new("b", "고드릭", new Stats(15, 12, 25, 20, 13), Row.Front, TestGear.Of("old_sword"),
                new Dictionary<string, int>(), new Dictionary<string, int>(), []),
            new("c", "후보", new Stats(9, 9, 9, 9, 9), Row.Front, TestGear.Of(),
                new Dictionary<string, int>(), new Dictionary<string, int>(), []),
        ];
        var company = new Company(roster, ["b", "a"], gold: 120, new Dictionary<string, int>(), activeTacticSet: 1, nextSeed: 77);
        company.AddToStash("silver_relic", Data, 2);
        return company;
    }

    /// <summary>지금 버전의 세이브.</summary>
    private static string Save(string members, string lineup = "\"a\"", string extra = "") =>
        $$"""{ "version": {{SaveGame.CurrentVersion}}, "gold": 10, "nextSeed": 3, "lineup": [ {{lineup}} ], "roster": [ {{members}} ]{{extra}} }""";

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
            Assert.Equal(o.Gear, l.Gear);
            Assert.Equal(o.MasteryXp, l.MasteryXp);
            Assert.Equal(o.SkillLevels, l.SkillLevels);
            for (var set = 0; set < original.TacticSetNames.Count; set++)
            {
                Assert.Equal(o.TacticSets[set], l.TacticSets[set]);
            }
        }

        Assert.Equal(["b", "a"], loaded.Lineup);
        Assert.Equal((120, 1, 77), (loaded.Gold, loaded.ActiveTacticSet, loaded.NextSeed));
        Assert.Equal(original.TacticSetNames, loaded.TacticSetNames);
        Assert.Equal(2, loaded.StashCount("silver_relic"));
        Assert.Equal(original.Gear, loaded.Gear);
        Assert.Equal(original.NextItemUid, loaded.NextItemUid);
    }

    [Fact]
    public void Serialized_save_is_readable_json_with_version()
    {
        var json = SaveGame.Serialize(SampleCompany());

        Assert.Contains("\"version\": 12", json);
        Assert.Contains("\"roster\"", json);
        Assert.DoesNotContain("\"party\"", json);
        Assert.Contains("\"name\": \"이졸데\"", json);
        Assert.Contains("\"itemId\": \"wooden_relic\"", json);
        Assert.DoesNotContain("\"abilityChoices\"", json);
        Assert.DoesNotContain("\"weapon\"", json);
        Assert.DoesNotContain("\"stash\"", json);
        Assert.Contains("\"gear\"", json);
        Assert.Contains("\"itemId\": \"silver_relic\"", json);
        Assert.Contains("\"nextItemUid\"", json);
        Assert.Contains("\"healing\": 2", json);
    }

    [Fact]
    public void Rejects_older_and_unknown_versions()
    {
        foreach (var version in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 99 })
        {
            var e = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize($$"""{ "version": {{version}}, "party": [] }""", Data));
            Assert.Contains($"unsupported version {version}", Assert.Single(e.Errors));
        }
    }

    [Fact]
    public void Converts_version_11_by_saving_each_piece_of_equipment()
    {
        var a = Member("""
            , "equipment": { "MainHand": "silver_relic", "Body": "cloth_robe" }, "masteryXp": { "relic": 100 }, "skillLevels": { "healing": 1 }
            """);
        var b = Member(""", "equipment": { "MainHand": "old_sword" }, "injury": 2""", id: "b");
        var v11 = $$"""
            { "version": 11, "gold": 10, "nextSeed": 3, "lineup": [ "a" ], "roster": [ {{a}}, {{b}} ],
              "stash": { "wooden_relic": 2, "iron_scrap": 5, "gold_relic": 1 }, "facilities": { "infirmary": 1 } }
            """;

        var loaded = SaveGame.Deserialize(v11, Data);

        // 로스터 순서, 부위 순서로 번호를 먼저 붙인다. 레벨은 티어.
        Assert.Equal(new ItemInstance(1, "silver_relic", 2), loaded.Roster[0].GearIn(EquipmentSlot.MainHand));
        Assert.Equal(new ItemInstance(2, "cloth_robe", 1), loaded.Roster[0].GearIn(EquipmentSlot.Body));
        Assert.Equal(new ItemInstance(3, "old_sword", 1), loaded.Roster[1].GearIn(EquipmentSlot.MainHand));

        // 창고의 장비는 아이템 ID 순서로 한 점씩, 재료는 개수로 남는다.
        Assert.Equal(
            [new ItemInstance(4, "gold_relic", 3), new ItemInstance(5, "wooden_relic", 1), new ItemInstance(6, "wooden_relic", 1)],
            loaded.Gear);
        Assert.Equal(new Dictionary<string, int> { ["iron_scrap"] = 5 }, loaded.Materials);
        Assert.Equal(7, loaded.NextItemUid);
        Assert.Equal((2, 1), (loaded.Roster[1].Injury, loaded.FacilityLevel(Facilities.Infirmary))); // 부상과 시설은 그대로

        var json = SaveGame.Serialize(loaded);
        Assert.Contains("\"version\": 12", json);
        Assert.DoesNotContain("\"stash\"", json);
    }

    [Fact]
    public void Converts_the_starting_company_saved_as_version_11()
    {
        var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        var company = StartingCompany.Create(data, seed: 11);
        var root = System.Text.Json.Nodes.JsonNode.Parse(SaveGame.Serialize(company))!.AsObject();
        root["version"] = 11;
        foreach (var member in root["roster"]!.AsArray().Select(m => m!.AsObject()))
        {
            var ids = new System.Text.Json.Nodes.JsonObject();
            foreach (var (slot, piece) in member["equipment"]!.AsObject())
            {
                ids[slot] = piece!["itemId"]!.GetValue<string>();
            }

            member["equipment"] = ids;
        }

        root.Remove("materials");
        root.Remove("gear");
        root.Remove("nextItemUid");
        root["stash"] = new System.Text.Json.Nodes.JsonObject { ["iron_scrap"] = 3 };

        var loaded = SaveGame.Deserialize(root.ToJsonString(), data);

        for (var i = 0; i < company.Roster.Count; i++)
        {
            Assert.Equal(company.Roster[i].Equipment, loaded.Roster[i].Equipment);
        }

        var worn = loaded.Roster.SelectMany(m => m.Gear.Values).ToList();
        Assert.Equal(Enumerable.Range(1, worn.Count), worn.Select(g => g.Uid).Order());
        Assert.All(worn, g => Assert.Equal(data.Items[g.ItemId].Tier, g.Level));
        Assert.Equal(3, loaded.StashCount("iron_scrap"));
        Assert.Equal(worn.Count + 1, loaded.NextItemUid);
    }

    [Fact]
    public void Encounter_records_round_trip()
    {
        var company = SampleCompany();
        company.RecordEncounter("camp", won: false);
        company.RecordEncounter("camp", won: true);

        var loaded = SaveGame.Deserialize(SaveGame.Serialize(company), Data);

        Assert.Equal(new EncounterRecord(2, 1), loaded.EncounterRecords["camp"]);
    }

    [Fact]
    public void Rejects_unknown_encounters_and_bad_encounter_records()
    {
        var e = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(
            Save(Member(""), extra: """, "encounters": { "nowhere": { "met": 1, "wins": 0 }, "camp": { "met": 1, "wins": 2 } }"""),
            Data));

        Assert.Contains(e.Errors, x => x.Contains("encounters has unknown encounter 'nowhere'"));
        Assert.Contains(e.Errors, x => x.Contains("encounter record of 'camp' needs met >= 1"));
    }

    [Fact]
    public void Injuries_and_facilities_round_trip()
    {
        var company = SampleCompany();
        company.Member("c").Injure(2);
        company.Gold = 1000;
        company.Upgrade(Facilities.Infirmary);

        var json = SaveGame.Serialize(company);
        var loaded = SaveGame.Deserialize(json, Data);

        Assert.Equal(2, loaded.Member("c").Injury);
        Assert.Equal(0, loaded.Member("a").Injury);
        Assert.Equal(1, loaded.FacilityLevel(Facilities.Infirmary));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"injury\"")); // 다치지 않은 멤버는 쓰지 않는다
    }

    [Fact]
    public void Rejects_bad_facilities_and_injuries()
    {
        var e = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(
            Save(Member(""", "injury": -1"""), extra: """, "facilities": { "tavern": 1, "infirmary": 9 }"""),
            Data));

        Assert.Contains(e.Errors, x => x.Contains("facilities has unknown facility 'tavern'"));
        Assert.Contains(e.Errors, x => x.Contains("facility 'infirmary' level must be 1-3, got 9"));
        Assert.Contains(e.Errors, x => x.Contains("injury must not be negative"));
    }

    [Fact]
    public void Clear_records_round_trip()
    {
        var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        var first = Depths.Ordered(data)[0];
        var company = StartingCompany.Create(data, seed: 11);
        company.RecordClear(first.Id, new ClearResult(Members: 3, GearTier: 1, Downed: 1));

        var loaded = SaveGame.Deserialize(SaveGame.Serialize(company), data);

        Assert.Equal(new ClearRecord(1, 3, 1, 1), loaded.ClearRecords[first.Id]);
        Assert.Null(Depths.WhyZoneLocked(loaded, data, Depths.Ordered(data)[1].Id));
    }

    [Fact]
    public void Rejects_unknown_zones_and_bad_clear_records()
    {
        var e = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(
            Save(Member(""), extra: """, "clears": { "elsewhere": { "clears": 0, "fewestMembers": 1, "lowestGearTier": 0, "fewestDowned": 0 } }"""),
            Data));

        Assert.Contains(e.Errors, x => x.Contains("clears has unknown zone 'elsewhere'"));
        Assert.Contains(e.Errors, x => x.Contains("clear record of 'elsewhere' needs clears >= 1"));
    }

    [Fact]
    public void Unmet_requirements_load_as_unwearable_and_block_fighting()
    {
        // 금 성구는 치유술 3을 요구하는데 1뿐이다 (게임 데이터가 바뀐 경우).
        var member = Member("""
            , "equipment": { "MainHand": { "uid": 1, "itemId": "gold_relic", "level": 3 } }, "masteryXp": { "relic": 300 }, "skillLevels": { "healing": 1 },
            "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "heal" } ] ]
            """);

        var company = SaveGame.Deserialize(
            Save(member, extra: """, "gear": [ { "uid": 2, "itemId": "wooden_relic", "level": 1 } ], "nextItemUid": 3"""), Data);

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
    public void Rejects_references_missing_from_game_data_and_wrong_slots()
    {
        var member = Member("""
            , "equipment": { "MainHand": { "uid": 1, "itemId": "cloth_robe", "level": 1 }, "Body": { "uid": 2, "itemId": "ghost_armor", "level": 0 } },
            "masteryXp": { "ghost": 100 },
            "skillLevels": { "ghost_skill": 1 },
            "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "removed_action" } ] ]
            """);
        const string stash = """
            , "materials": { "ghost_item": 1, "old_sword": 1, "iron_scrap": 0 },
            "gear": [ { "uid": 1, "itemId": "iron_scrap", "level": 1 }, { "uid": 0, "itemId": "ghost_gear", "level": 1 } ],
            "nextItemUid": 0
            """;

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Save(member, extra: stash), Data)).Errors;

        Assert.Contains("save member 'a': equipment: MainHand: 'cloth_robe' is Body", errors);
        Assert.Contains("save member 'a': equipment: Body: unknown item 'ghost_armor'", errors);
        Assert.Contains("save member 'a': unknown mastery 'ghost'", errors);
        Assert.Contains("save member 'a': unknown skill 'ghost_skill'", errors);
        Assert.Contains("save member 'a' set 1 tactic 1: unknown action 'removed_action'", errors);
        Assert.Contains("save member 'a': equipment Body: level must be at least 1, got 0", errors);
        Assert.DoesNotContain(errors, e => e.Contains("equipment Body: unknown item")); // 부위 검사가 한 번만 알린다
        Assert.Contains("save: materials has unknown item 'ghost_item'", errors);
        Assert.Contains("save: materials has equipment 'old_sword' (equipment is saved one piece at a time in gear)", errors);
        Assert.Contains("save: material count of 'iron_scrap' must be at least 1, got 0", errors);
        Assert.Contains("save gear 1: 'iron_scrap' is not equipment (materials are saved as counts)", errors);
        Assert.Contains("save gear 0: unknown item 'ghost_gear'", errors);
        Assert.Contains("save gear 0: uid must be at least 1, got 0", errors);
        Assert.Contains("save: item uid 1 is used by more than one piece", errors);
        Assert.Contains("save: nextItemUid must be at least 1, got 0", errors);
    }

    [Fact]
    public void Rejects_bad_lineups()
    {
        var two = Member("") + ", " + Member("", id: "b");
        var errors = Assert.Throws<SaveGameException>(() =>
            SaveGame.Deserialize(Save(two, lineup: "\"a\", \"a\", \"ghost\""), Data)).Errors;
        Assert.Contains("save: lineup lists 'a' more than once", errors);
        Assert.Contains("save: lineup member 'ghost' is not in the roster", errors);

        var six = string.Join(", ", Enumerable.Range(1, 6).Select(i => Member("", id: $"m{i}")));
        var lineup = string.Join(", ", Enumerable.Range(1, 6).Select(i => $"\"m{i}\""));
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Save(six, lineup), Data)).Errors;
        Assert.Contains("save: lineup has 6 members (at most 5)", errors);
    }

    [Fact]
    public void Empty_lineup_is_allowed_in_the_village()
    {
        var company = SaveGame.Deserialize(Save(Member(""), lineup: ""), Data);

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

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Save(member), Data)).Errors;

        Assert.Contains("save member 'a': 'holy' needs 'healing' level 3", errors);
        Assert.Contains("save member 'a': spent 4 points in 'relic' but mastery level is 1", errors);
    }

    [Fact]
    public void Loads_tactics_whose_action_became_locked_and_reports_them()
    {
        // 치료는 성구의 행동 칸에 있는데 검을 들었다 (장비를 바꾼 경우).
        var member = Member("""
            , "equipment": { "MainHand": { "uid": 1, "itemId": "old_sword", "level": 1 } }, "masteryXp": { "relic": 300 }, "skillLevels": { "healing": 1 },
            "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" },
                              { "priority": 2, "condition": "Always", "value": 0, "actionId": "heal" } ] ]
            """);

        var company = SaveGame.Deserialize(
            Save(member, extra: """, "gear": [ { "uid": 2, "itemId": "silver_relic", "level": 2 } ], "nextItemUid": 3"""), Data);

        Assert.Equal([1], company.Roster[0].LockedTacticIndexes(Data, 0));
        Assert.True(company.HasLockedTactics(Data));
        Assert.True(company.Equip("a", "silver_relic", Data));
        Assert.Empty(company.Roster[0].LockedTacticIndexes(Data, 0));
    }

    [Fact]
    public void Rejects_bad_tactic_set_data()
    {
        var tooMany = Member(""", "tacticSets": [ [], [], [] ]""");
        var named = Save(tooMany, extra: """, "tacticSetNames": [ "하나", "둘" ]""");
        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(named, Data)).Errors;
        Assert.Contains("save member 'a': has 3 tactic sets but the company has 2", errors);

        var badNames = Save(Member(""), extra: $$""", "tacticSetNames": [ " ", "{{new string('가', 21)}}" ]""");
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(badNames, Data)).Errors;
        Assert.Contains("save: tactic set name must not be empty", errors);
        Assert.Contains(errors, e => e.Contains("longer than 20 characters"));

        errors = Assert.Throws<SaveGameException>(() =>
            SaveGame.Deserialize(Save(Member(""), extra: """, "tacticSetNames": []"""), Data)).Errors;
        Assert.Contains("save: tacticSetNames needs at least one set", errors);

        var badActive = Save(Member(""), extra: """, "activeTacticSet": 5""");
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(badActive, Data)).Errors;
        Assert.Contains("save: activeTacticSet must be 0-1, got 5", errors); // 이름이 없는 옛 파일은 두 벌
    }

    [Fact]
    public void Files_without_set_names_load_as_set_1_and_2_and_more_sets_round_trip()
    {
        var member = Member(""", "tacticSets": [ [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "strike" } ] ]""");
        var company = SaveGame.Deserialize(Save(member, extra: """, "activeTacticSet": 1"""), Data);

        Assert.Equal(["세트 1", "세트 2"], company.TacticSetNames);
        Assert.Equal(1, company.ActiveTacticSet);
        Assert.Empty(company.Roster[0].TacticSets[1]);

        company.AddTacticSet("보스전");
        company.AddTacticSet();
        company.RenameTacticSet(0, "외곽");
        var loaded = SaveGame.Deserialize(SaveGame.Serialize(company), Data);

        Assert.Equal(["외곽", "세트 2", "보스전", "세트 4"], loaded.TacticSetNames);
        Assert.Equal(3, loaded.ActiveTacticSet);
        Assert.Equal(4, loaded.Roster[0].TacticSets.Count);
    }

    [Fact]
    public void Rejects_malformed_json_and_duplicate_members()
    {
        Assert.Throws<SaveGameException>(() => SaveGame.Deserialize("{ not json", Data));

        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(Save(Member("") + ", " + Member("")), Data)).Errors;
        Assert.Contains("save: duplicate member id 'a'", errors);
    }

    [Fact]
    public void Saves_and_restores_an_expedition_in_progress()
    {
        var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        var company = StartingCompany.Create(data, seed: 11);
        var zone = data.Zones.Values.OrderBy(z => z.Difficulty).First();
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

        // 다친 멤버는 원정에 나가 있을 수 없다.
        var company = StartingCompany.Create(data, seed: 1);
        ExpeditionRules.Start(company, data, zone.Id);
        company.Member(lineup[0]).Injure(1);
        errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(SaveGame.Serialize(company), data)).Errors;
        Assert.Contains($"save expedition: member '{lineup[0]}' is injured", errors);

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

        store.Save(company);
        var result = store.Load(Data, () => throw new InvalidOperationException("should not create new"));

        Assert.Equal(LoadStatus.Loaded, result.Status);
        Assert.Equal(Row.Back, result.Company.Roster[1].Row);
        Assert.Equal(new Tactic(1, Condition.EveryNthTurn, 3, "strike"), Assert.Single(result.Company.Roster[1].TacticSets[1]));
        Assert.Equal(0, result.Company.ActiveTacticSet);
        Assert.Equal("silver_relic", result.Company.Roster[0].ItemIn(EquipmentSlot.MainHand));
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
