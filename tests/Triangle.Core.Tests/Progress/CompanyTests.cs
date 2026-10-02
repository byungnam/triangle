using Triangle.Core.Data;
using Triangle.Core.Progress;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

public class CompanyTests
{
    private static readonly GameData Data = GameDataLoader.Parse(
        """
        [ { "id": "sword", "name": "검", "slot": "Weapon" },
          { "id": "bow", "name": "활", "slot": "Weapon" },
          { "id": "plate", "name": "판금", "slot": "Armor" } ]
        """,
        "[]",
        """[ { "id": "strike", "name": "공격", "power": 10 } ]""",
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""",
        itemsJson: """
        [ { "id": "old_sword", "name": "낡은 검", "mastery": "sword" },
          { "id": "hunting_bow", "name": "사냥용 활", "mastery": "bow" },
          { "id": "plate_armor", "name": "판금 갑옷", "mastery": "plate" } ]
        """);

    private static PartyMember Member(string id, string? weapon = "old_sword", string? armor = null) =>
        new(id, id, new Stats(10, 10, 10, 10, 10), Row.Front, weapon, armor, new Dictionary<string, int>(), new Dictionary<string, int>(), []);

    private static Company Company(params PartyMember[] roster) =>
        new(roster, roster.Take(1).Select(m => m.Id), gold: 0, new Dictionary<string, int>(), activeTacticSet: 0, nextSeed: 5);

    [Fact]
    public void Equip_takes_from_stash_and_returns_the_old_item()
    {
        var company = Company(Member("a"));
        company.AddToStash("hunting_bow");
        company.AddToStash("plate_armor", 2);

        Assert.True(company.Equip("a", "hunting_bow", Data));
        Assert.True(company.Equip("a", "plate_armor", Data));

        var member = company.Member("a");
        Assert.Equal(("hunting_bow", "plate_armor"), (member.Weapon, member.Armor));
        Assert.Equal(1, company.StashCount("old_sword"));
        Assert.Equal(0, company.StashCount("hunting_bow"));
        Assert.Equal(1, company.StashCount("plate_armor"));
        Assert.False(company.Stash.ContainsKey("hunting_bow"));
        Assert.Equal("bow", member.ToCombatantSetup(Data, 0).Weapon);
    }

    [Fact]
    public void Equip_fails_when_the_item_is_not_in_the_stash()
    {
        var company = Company(Member("a"));

        Assert.False(company.Equip("a", "hunting_bow", Data));
        Assert.True(company.Equip("a", "old_sword", Data)); // 이미 낀 아이템
        Assert.Equal("old_sword", company.Member("a").Weapon);
        Assert.Empty(company.Stash);
    }

    [Fact]
    public void Lineup_holds_at_most_five_roster_members_once_each()
    {
        var company = Company(Enumerable.Range(1, 7).Select(i => Member($"m{i}")).ToArray());

        Assert.False(company.AddToLineup("m1"));
        Assert.False(company.AddToLineup("ghost"));
        for (var i = 2; i <= 5; i++)
        {
            Assert.True(company.AddToLineup($"m{i}"));
        }

        Assert.False(company.AddToLineup("m6"));
        Assert.True(company.RemoveFromLineup("m3"));
        Assert.True(company.AddToLineup("m6"));
        Assert.Equal(["m1", "m2", "m4", "m5", "m6"], company.Lineup);
        Assert.Equal(5, company.LineupSetups(Data).Count);
    }

    [Fact]
    public void Seeds_are_deterministic_and_advance()
    {
        var a = Company(Member("a"));
        var b = Company(Member("a"));

        var first = a.TakeSeed();
        var second = a.TakeSeed();

        Assert.Equal(5, first);
        Assert.NotEqual(first, second);
        Assert.Equal((first, second), (b.TakeSeed(), b.TakeSeed()));
    }
}
