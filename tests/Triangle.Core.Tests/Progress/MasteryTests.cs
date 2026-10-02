using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

public class MasteryTests
{
    private static readonly GameData Data = GameDataLoader.Parse(
        """
        [ { "id": "bow", "name": "활", "kind": "Weapon" },
          { "id": "sword", "name": "검", "kind": "Weapon" },
          { "id": "plate", "name": "판금", "kind": "Armor" } ]
        """,
        """
        [ { "id": "archery", "name": "활 숙련", "mastery": "bow" },
          { "id": "rapid", "name": "속사", "mastery": "bow", "rank": 2, "prerequisites": [ { "skillId": "archery", "level": 2 } ] },
          { "id": "swordsmanship", "name": "검술", "mastery": "sword" } ]
        """,
        """
        [ { "id": "strike", "name": "공격", "power": 10, "universal": true },
          { "id": "shot", "name": "화살", "power": 10 } ]
        """,
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "short_bow", "name": "짧은 활", "slot": "MainHand", "mastery": "bow", "abilities": [ { "options": [ "shot" ] } ] },
          { "id": "old_sword", "name": "낡은 검", "slot": "MainHand", "mastery": "sword" },
          { "id": "plate_mail", "name": "판금 갑옷", "slot": "Body", "mastery": "plate" } ]
        """);

    private static PartyMember Member(Dictionary<string, int>? xp = null, Dictionary<string, int>? skills = null) =>
        new("m", "멤버", new Stats(15, 15, 15, 15, 15), Row.Front, TestGear.Of("short_bow", "plate_mail"), null, xp ?? [], skills ?? [], []);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(99, 0)]
    [InlineData(100, 1)]
    [InlineData(299, 1)]
    [InlineData(300, 2)]
    [InlineData(1_500, 5)]
    [InlineData(10_000_000, MasteryProgression.MaxLevel)]
    public void Level_follows_50_l_l_plus_1_curve(int xp, int level)
    {
        Assert.Equal(level, MasteryProgression.LevelFor(xp));
    }

    [Fact]
    public void Adding_xp_reports_new_points_and_points_are_per_tree()
    {
        var m = Member();

        Assert.Equal(2, m.AddMasteryXp("bow", 300));
        Assert.Equal(0, m.AddMasteryXp("bow", 50));
        Assert.Equal(2, m.PointsAvailable("bow", Data));
        Assert.Equal(0, m.PointsAvailable("sword", Data));
    }

    [Fact]
    public void Learning_spends_rank_points_from_its_own_tree()
    {
        var m = Member(new Dictionary<string, int> { ["bow"] = MasteryProgression.XpForLevel(4) });

        Assert.True(m.Learn("archery", Data));
        Assert.True(m.Learn("archery", Data));
        Assert.True(m.Learn("rapid", Data)); // 랭크 2 → 2점
        Assert.Equal(4, m.PointsSpent("bow", Data));
        Assert.Equal(0, m.PointsAvailable("bow", Data));

        Assert.Equal(LearnBlockerKind.NotEnoughPoints, m.WhyCannotLearn("archery", Data)!.Kind);
        Assert.False(m.Learn("swordsmanship", Data)); // 검 트리 포인트는 없다
    }

    [Fact]
    public void Learning_requires_prerequisites_and_stops_at_level_5()
    {
        var m = Member(new Dictionary<string, int> { ["bow"] = MasteryProgression.XpForLevel(10) });

        var blocker = m.WhyCannotLearn("rapid", Data)!;
        Assert.Equal(LearnBlockerKind.Prerequisites, blocker.Kind);
        Assert.Equal(new SkillRequirement("archery", 2), Assert.Single(blocker.Missing));

        for (var i = 0; i < 5; i++)
        {
            Assert.True(m.Learn("archery", Data));
        }

        Assert.Equal(LearnBlockerKind.MaxLevel, m.WhyCannotLearn("archery", Data)!.Kind);
    }

    [Fact]
    public void Changing_weapon_locks_weapon_tactics()
    {
        var m = new PartyMember("m", "멤버", new Stats(15, 15, 15, 15, 15), Row.Front, TestGear.Of("short_bow"), null, new Dictionary<string, int>(),
            new Dictionary<string, int>(), [[new Tactic(1, Condition.Always, 0, "shot"), new Tactic(2, Condition.Always, 0, "strike")]]);

        Assert.Empty(m.LockedTacticIndexes(Data, 0));
        m.SetItem(EquipmentSlot.MainHand, "old_sword", Data);
        Assert.Equal([0], m.LockedTacticIndexes(Data, 0));
        Assert.Equal("sword", m.ToCombatantSetup(Data, 0).Weapon);
    }

    [Fact]
    public void Armor_xp_is_split_by_pieces_per_material()
    {
        CombatantSetup Setup(string id, string actionId, IReadOnlyList<string>? pieces) =>
            new CombatantSetup(id, id, new Stats(10, 10, 100, 10, 10), Row.Front, null, null, SkillSet.NoSkills, [new Tactic(1, Condition.Always, 0, actionId)])
            {
                ArmorPieces = pieces,
            };

        var result = CombatSimulator.Run([Setup("a", "strike", ["plate", "cloth", "cloth"])], [Setup("e", "strike", null)], Data.Catalog, seed: 1,
            new CombatRules { MaxActions = 10 });
        var taken = result.Events.OfType<Damaged>().Where(d => d.TargetId == "a").Sum(d => d.Amount);
        var total = taken / MasteryRules.Default.DamageTakenPerXp + MasteryRules.Default.ResultXp(result.Outcome);

        var gain = Assert.Single(MasteryGain.ForAllies(result));

        // 판금 머리 + 천 몸통·신발: 판금 1/3, 천 2/3. 무기가 없으면 무기 경험치도 없다.
        Assert.Equal(new Dictionary<string, int> { ["plate"] = total / 3, ["cloth"] = total * 2 / 3 }, gain.Armor);
        Assert.Equal(0, gain.WeaponXp);
    }

    [Fact]
    public void Combat_xp_mixes_actions_amount_dealt_damage_taken_and_result()
    {
        // 아군 a(활, 판금)가 매 턴 화살을 쏜다. 상대 e는 매 턴 공격한다.
        CombatantSetup Setup(string id, string? weapon, string actionId) =>
            new(id, id, new Stats(10, 10, 100, 10, 10), Row.Front, weapon, weapon is null ? null : "plate",
                SkillSet.NoSkills, [new Tactic(1, Condition.Always, 0, actionId)]);

        var result = CombatSimulator.Run([Setup("a", "bow", "shot")], [Setup("e", null, "strike")], Data.Catalog, seed: 1,
            new CombatRules { MaxActions = 10 });

        var actions = result.Events.OfType<ActionUsed>().Count(u => u.ActorId == "a");
        var dealt = result.Events.OfType<Damaged>().Where(d => d.TargetId == "e").Sum(d => d.Amount);
        var taken = result.Events.OfType<Damaged>().Where(d => d.TargetId == "a").Sum(d => d.Amount);
        var rules = MasteryRules.Default;
        var resultXp = rules.ResultXp(result.Outcome);

        var gain = Assert.Single(MasteryGain.ForAllies(result));

        Assert.Equal(("a", "bow"), (gain.CombatantId, gain.Weapon));
        Assert.Equal(actions * rules.XpPerAction + dealt / rules.AmountPerXp + resultXp, gain.WeaponXp);
        Assert.Equal(new Dictionary<string, int> { ["plate"] = taken / rules.DamageTakenPerXp + resultXp }, gain.Armor);
        Assert.True(actions > 0 && dealt > 0 && taken > 0);
    }
}
