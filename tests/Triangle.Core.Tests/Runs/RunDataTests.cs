using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Skills;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Runs;

/// <summary>
/// 로그라이트 재편의 데이터(2026-10-19): 클래스와 카드 풀, 적의 행동 목록, 장신구. 지금 게임은 아직 옛 구조로 돌므로
/// 새 데이터는 옛 경로(상점, 전리품, 편성 부위)에 섞이지 않아야 한다.
/// </summary>
public class RunDataTests
{
    private static readonly GameData Shipped = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private const string Masteries = """
        [
          { "id": "sword", "name": "검", "kind": "Weapon" },
          { "id": "bow", "name": "활", "kind": "Weapon" },
          { "id": "plate", "name": "판금", "kind": "Armor" }
        ]
        """;

    private const string Skills = """
        [ { "id": "swordsmanship", "name": "검술", "mastery": "sword", "bonuses": [ { "kind": "PowerPercent", "percentPerLevel": 5, "tag": "sword" } ] } ]
        """;

    private const string Actions = """
        [
          { "id": "strike", "name": "공격", "power": 10, "universal": true },
          { "id": "slash", "name": "베기", "power": 20 },
          { "id": "shot", "name": "화살", "power": 20 }
        ]
        """;

    private const string Items = """
        [
          { "id": "sword", "name": "검", "slot": "MainHand", "mastery": "sword", "actions": [ "slash" ] },
          { "id": "bow", "name": "활", "slot": "MainHand", "mastery": "bow", "actions": [ "shot" ] },
          { "id": "plate", "name": "판금 갑옷", "slot": "Body", "mastery": "plate" }
        ]
        """;

    private static string Class(string id = "knight", string weapon = "sword", string armor = "plate", string start = "slash", string skills = """[ "slash" ]""", string passives = """[ "swordsmanship" ]""") => $$"""
        { "id": "{{id}}", "name": "{{id}}", "row": "Front", "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 },
          "families": [ "sword", "plate" ], "weapon": "{{weapon}}", "armor": "{{armor}}", "startingSkills": [ "{{start}}" ],
          "skills": {{skills}}, "passives": {{passives}} }
        """;

    private static IReadOnlyList<string> ClassErrors(params string[] classes) =>
        Assert.Throws<GameDataException>(() => GameDataLoader.Parse(Masteries, Skills, Actions, "[]", itemsJson: Items, classesJson: $"[{string.Join(",", classes)}]")).Errors;

    [Fact]
    public void Shipped_classes_are_the_four_narrow_ones_and_own_their_material_actions()
    {
        // 처음 넷은 처음부터 열려 있고, 나머지는 해금으로 연다.
        Assert.Equal(["knight", "archer", "elementalist", "priest", "warrior", "gunner"], Shipped.Classes.Keys);
        Assert.Equal(["warrior", "gunner"], Shipped.Unlocks.Values.SelectMany(u => u.Classes));
        Assert.All(Shipped.Classes.Values, c =>
        {
            Assert.All(c.StartingSkills, id => Assert.Contains(id, c.Skills));
            Assert.InRange(c.StartingSkills.Count, 2, 3);
            Assert.True(c.CanWield(Shipped.Items[c.Weapon]));
            Assert.Equal(EquipmentSlot.Body, Shipped.Items[c.Armor].Slot);
        });

        Assert.Contains("provoke", Shipped.Classes["knight"].Skills);
        Assert.Contains("step_forward", Shipped.Classes["knight"].Skills);
        Assert.Contains("throwing_knife", Shipped.Classes["archer"].Skills);
        Assert.Contains("step_back", Shipped.Classes["elementalist"].Skills);
        Assert.Contains("heal", Shipped.Classes["priest"].Skills);
        Assert.Equal(Row.Front, Shipped.Classes["knight"].Row);
    }

    [Fact]
    public void Unlocks_check_what_they_lock()
    {
        IReadOnlyList<string> Errors(string unlocks) => Assert.Throws<GameDataException>(() =>
            GameDataLoader.Parse(Masteries, Skills, Actions, "[]", itemsJson: Items, classesJson: $"[{Class()}]", unlocksJson: unlocks)).Errors;

        Assert.Contains(Errors("""[ { "id": "a", "name": "A", "cards": [ "shot" ] } ]"""), e => e.Contains("card 'shot' is in no class's skills"));
        Assert.Contains(Errors("""[ { "id": "a", "name": "A", "cards": [ "slash" ] } ]"""), e => e.Contains("starting skill of 'knight'"));
        Assert.Contains(Errors("""[ { "id": "a", "name": "A", "classes": [ "knight" ] } ]"""), e => e.Contains("every class is locked"));
        Assert.Contains(Errors("""[ { "id": "a", "name": "A" } ]"""), e => e.Contains("opens nothing"));
        var unknown = Errors("""[ { "id": "a", "name": "A", "classes": [ "bard" ], "items": [ "nope" ], "cost": -1 } ]""");
        Assert.Contains(unknown, e => e.Contains("unknown class 'bard'"));
        Assert.Contains(unknown, e => e.Contains("unknown item 'nope'"));
        Assert.Contains(unknown, e => e.Contains("cost"));
        Assert.Contains(Errors("""[ { "id": "a", "name": "A", "items": [ "bow" ] }, { "id": "b", "name": "B", "items": [ "bow" ] } ]"""),
            e => e.Contains("'bow' is locked by more than one unlock (a, b)"));

        Assert.Equal(50, Shipped.Unlocks["war_chest"].StartGold);
        Assert.All(Shipped.Unlocks.Values, u => Assert.True(u.Cost > 0));
    }

    [Fact]
    public void Classes_reject_bad_references_and_shared_cards()
    {
        Assert.Contains(ClassErrors(Class(weapon: "bow")), e => e.Contains("weapon 'bow' is not a main-hand item of the class families"));
        Assert.Contains(ClassErrors(Class(armor: "sword")), e => e.Contains("armor 'sword' is not a body item"));
        Assert.Contains(ClassErrors(Class(start: "shot")), e => e.Contains("starting skill 'shot' is not in skills"));
        Assert.Contains(ClassErrors(Class(start: "slash\", \"slash")), e => e.Contains("startingSkills must not be empty or repeat a skill"));
        Assert.Contains(ClassErrors(Class(skills: """[ "slash", "strike" ]""")), e => e.Contains("'strike' cannot be a card"));
        Assert.Contains(ClassErrors(Class(passives: """[ "nope" ]""")), e => e.Contains("unknown passive 'nope'"));
        Assert.Contains(ClassErrors(Class("a"), Class("b")), e => e.Contains("'slash' belongs to more than one class"));
    }

    [Fact]
    public void Enemies_use_only_their_own_action_list()
    {
        // 장비(검)가 베기를 주지만 행동 목록이 화살뿐이면 베기는 쓸 수 없다.
        const string encounter = """
            [ { "id": "camp", "name": "야영지", "units": [ { "id": "e1", "name": "적", "row": "Front",
                "stats": { "str": 10, "dex": 10, "vital": 20, "intel": 10, "speed": 10 },
                "equipment": { "MainHand": "sword" }, "actions": [ "shot" ],
                "tactics": [ { "priority": 1, "condition": "Always", "value": 0, "actionId": "{{action}}" } ] } ] } ]
            """;
        var data = GameDataLoader.Parse(Masteries, Skills, Actions, encounter.Replace("{{action}}", "shot"), itemsJson: Items);
        Assert.Equal(["shot"], data.CreateEncounterTeam("camp")[0].GrantedActions!);

        var errors = Assert.Throws<GameDataException>(() => GameDataLoader.Parse(Masteries, Skills, Actions, encounter.Replace("{{action}}", "slash"), itemsJson: Items)).Errors;
        Assert.Contains(errors, e => e.Contains("'slash' is not in the unit's actions"));

        // 실은 데이터의 적은 모두 행동 목록을 직접 가진다.
        Assert.All(Shipped.Encounters.Values.SelectMany(e => e.Units), u => Assert.NotNull(u.Actions));
    }

    [Fact]
    public void Trinkets_give_bonuses_and_starting_effects_without_a_mastery()
    {
        var troll = Shipped.Items["troll_amulet"];
        var rosary = Shipped.Items["saint_rosary"];
        var setup = new Loadout([(EquipmentSlot.MainHand, Shipped.Items["old_sword"]), (EquipmentSlot.Trinket, troll), (EquipmentSlot.Trinket, rosary)])
            .Apply(new CombatantSetup("k", "기사", new Stats(10, 10, 20, 10, 10), Row.Front, null, null, new Dictionary<string, int>(), []), Shipped.Actions.Values, _ => 0);

        Assert.Equal(["regenerating"], setup.StartingEffects);
        Assert.Contains(setup.ItemBonuses, b => b.Kind == BonusKind.HealPercent && b.Percent == 25);
    }

    [Fact]
    public void Trinkets_carry_no_mastery_gear_may_carry_effects_and_only_weapons_change_the_basic_attack()
    {
        const string bad = """
            [
              { "id": "charm", "name": "부적", "slot": "Trinket", "mastery": "sword" },
              { "id": "helm", "name": "투구", "slot": "Body", "mastery": "plate", "effects": [ "regenerating" ] },
              { "id": "spiked", "name": "가시 갑옷", "slot": "Body", "mastery": "plate", "basicAttack": "slash" },
              { "id": "costly", "name": "비싼 칼", "slot": "MainHand", "mastery": "sword", "basicAttack": "pricey" },
              { "id": "nowhere", "name": "없는 칼", "slot": "MainHand", "mastery": "sword", "basicAttack": "nope" }
            ]
            """;
        const string effects = """[ { "id": "regenerating", "name": "재생", "kind": "Buff", "tickHpPercent": 5 } ]""";
        var actions = Actions.Replace("""{ "id": "shot", "name": "화살", "power": 20 }""",
            """{ "id": "shot", "name": "화살", "power": 20 }, { "id": "pricey", "name": "비싼 베기", "power": 20, "mpCost": 5 }""");
        var errors = Assert.Throws<GameDataException>(() => GameDataLoader.Parse(Masteries, Skills, actions, "[]", effects, bad)).Errors;

        Assert.Contains(errors, e => e.Contains("'charm': a trinket has no mastery"));
        Assert.DoesNotContain(errors, e => e.Contains("'helm'"));
        Assert.Contains(errors, e => e.Contains("'spiked': only a main-hand item can have powerMultiplierPercent, effectDurationBonus or basicAttack"));
        Assert.Contains(errors, e => e.Contains("'costly': basicAttack 'pricey' must cost nothing"));
        Assert.Contains(errors, e => e.Contains("'nowhere': unknown basicAttack 'nope'"));
    }
}
