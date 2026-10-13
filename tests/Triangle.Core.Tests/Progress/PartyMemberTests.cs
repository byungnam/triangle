using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

public class PartyMemberTests
{
    private static PartyMember Member(params Tactic[] tactics) =>
        new("m", "멤버", new Stats(10, 10, 20, 10, 10), Row.Front, TestGear.Of(), new Dictionary<string, int>(), new Dictionary<string, int>(), [tactics]);

    private static string[] Skills(PartyMember m) => m.TacticSets[0].Select(t => t.ActionId).ToArray();

    private static void AssertPrioritiesAreSequential(PartyMember m) =>
        Assert.Equal(Enumerable.Range(1, m.TacticSets[0].Count), m.TacticSets[0].Select(t => t.Priority));

    [Fact]
    public void Constructor_orders_by_priority_and_renumbers_from_one()
    {
        var m = Member(
            new Tactic(7, Condition.Always, 0, "c"),
            new Tactic(2, Condition.Always, 0, "a"),
            new Tactic(5, Condition.Always, 0, "b"));

        Assert.Equal(["a", "b", "c"], Skills(m));
        AssertPrioritiesAreSequential(m);
    }

    [Fact]
    public void Add_appends_with_next_priority()
    {
        var m = Member(new Tactic(1, Condition.Always, 0, "a"));

        m.TacticSets[0].Add(Condition.SelfHpAtMost, 30, "b");

        Assert.Equal(new Tactic(2, Condition.SelfHpAtMost, 30, "b"), m.TacticSets[0][1]);
    }

    [Fact]
    public void Replace_keeps_position_priority()
    {
        var m = Member(new Tactic(1, Condition.Always, 0, "a"), new Tactic(2, Condition.Always, 0, "b"));

        m.TacticSets[0].Replace(1, Condition.EveryNthTurn, 2, "c");

        Assert.Equal(new Tactic(2, Condition.EveryNthTurn, 2, "c"), m.TacticSets[0][1]);
    }

    [Fact]
    public void Remove_renumbers_remaining()
    {
        var m = Member(
            new Tactic(1, Condition.Always, 0, "a"),
            new Tactic(2, Condition.Always, 0, "b"),
            new Tactic(3, Condition.Always, 0, "c"));

        m.TacticSets[0].Remove(0);

        Assert.Equal(["b", "c"], Skills(m));
        AssertPrioritiesAreSequential(m);
    }

    [Fact]
    public void Move_swaps_and_renumbers()
    {
        var m = Member(
            new Tactic(1, Condition.Always, 0, "a"),
            new Tactic(2, Condition.Always, 0, "b"),
            new Tactic(3, Condition.Always, 0, "c"));

        Assert.True(m.TacticSets[0].Move(2, -1));

        Assert.Equal(["a", "c", "b"], Skills(m));
        AssertPrioritiesAreSequential(m);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(1, 1)]
    [InlineData(5, -1)]
    public void Move_outside_the_list_does_nothing(int index, int offset)
    {
        var m = Member(new Tactic(1, Condition.Always, 0, "a"), new Tactic(2, Condition.Always, 0, "b"));

        Assert.False(m.TacticSets[0].Move(index, offset));
        Assert.Equal(["a", "b"], Skills(m));
    }

    [Fact]
    public void Member_without_tactic_sets_starts_with_one_empty_set()
    {
        var m = new PartyMember("m", "멤버", new Stats(10, 10, 20, 10, 10), Row.Front, TestGear.Of(),
            new Dictionary<string, int>(), new Dictionary<string, int>(), []);

        Assert.Empty(Assert.Single(m.TacticSets));
    }

    private static Company CompanyWith(params PartyMember[] members) =>
        new(members, members.Select(m => m.Id), gold: 0, new Dictionary<string, int>(), activeTacticSet: 0, nextSeed: 0);

    [Fact]
    public void Company_pads_every_member_to_its_tactic_set_count()
    {
        var other = new PartyMember("o", "다른", new Stats(10, 10, 20, 10, 10), Row.Front, TestGear.Of(),
            new Dictionary<string, int>(), new Dictionary<string, int>(), [[], [], []]);
        var company = CompanyWith(Member(new Tactic(1, Condition.Always, 0, "a")), other);

        Assert.Equal(["세트 1", "세트 2", "세트 3"], company.TacticSetNames);
        Assert.Equal(3, company.Member("m").TacticSets.Count);
        Assert.Equal(["a"], company.Member("m").TacticSets[0].Select(t => t.ActionId));
        Assert.Empty(company.Member("m").TacticSets[2]);
    }

    [Fact]
    public void Company_rejects_unknown_tactic_set()
    {
        var company = CompanyWith(Member());

        Assert.Throws<ArgumentOutOfRangeException>(() => company.ActiveTacticSet = 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => company.ActiveTacticSet = -1);
    }

    [Fact]
    public void Tactic_sets_are_unlimited_and_new_sets_copy_the_active_one()
    {
        var company = CompanyWith(Member(new Tactic(1, Condition.Always, 0, "a")));
        var m = company.Member("m");

        for (var i = 0; i < 5; i++)
        {
            company.AddTacticSet();
        }

        Assert.Equal(6, company.TacticSetNames.Count);
        Assert.Equal("세트 6", company.TacticSetNames[5]);
        Assert.Equal(5, company.ActiveTacticSet);
        Assert.Equal(6, m.TacticSets.Count);
        Assert.Equal(["a"], m.TacticSets[5].Select(t => t.ActionId));

        // 복사본은 따로 편집된다.
        m.TacticSets[5].Add(Condition.Always, 0, "b");
        Assert.Equal(["a"], m.TacticSets[0].Select(t => t.ActionId));

        Assert.Equal(6, company.AddTacticSet("  보스전  "));
        Assert.Equal("보스전", company.TacticSetNames[6]);
    }

    [Fact]
    public void Removing_a_set_keeps_the_active_set_pointing_at_the_same_plan()
    {
        var company = CompanyWith(Member());
        company.AddTacticSet("둘");
        company.AddTacticSet("셋");
        company.ActiveTacticSet = 2;

        Assert.True(company.RemoveTacticSet(0));
        Assert.Equal(["둘", "셋"], company.TacticSetNames);
        Assert.Equal(1, company.ActiveTacticSet);
        Assert.Equal(2, company.Member("m").TacticSets.Count);

        // 고른 세트를 지우면 다음 세트(없으면 이전 세트)를 고른다.
        Assert.True(company.RemoveTacticSet(1));
        Assert.Equal(0, company.ActiveTacticSet);

        // 마지막 세트는 지울 수 없다.
        Assert.False(company.RemoveTacticSet(0));
        Assert.Single(company.TacticSetNames);
    }

    [Fact]
    public void Rename_trims_and_rejects_empty_names()
    {
        var company = CompanyWith(Member());

        Assert.False(company.RenameTacticSet(0, "   "));
        Assert.True(company.RenameTacticSet(0, " 후열 집중 "));
        Assert.Equal("후열 집중", company.TacticSetNames[0]);
        Assert.True(company.RenameTacticSet(0, new string('가', 30)));
        Assert.Equal(Company.MaxTacticSetNameLength, company.TacticSetNames[0].Length);
    }

    [Fact]
    public void ToggleRow_switches_between_front_and_back()
    {
        var m = Member();

        m.ToggleRow();
        Assert.Equal(Row.Back, m.Row);
        m.ToggleRow();
        Assert.Equal(Row.Front, m.Row);
    }
}
