using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

public class PartyMemberTests
{
    private static PartyMember Member(params Tactic[] tactics) =>
        new("m", "멤버", new Stats(10, 10, 20, 10, 10), Row.Front, null, null, new Dictionary<string, int>(), new Dictionary<string, int>(), tactics);

    private static string[] Skills(PartyMember m) => m.Tactics.Select(t => t.ActionId).ToArray();

    private static void AssertPrioritiesAreSequential(PartyMember m) =>
        Assert.Equal(Enumerable.Range(1, m.Tactics.Count), m.Tactics.Select(t => t.Priority));

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

        m.AddTactic(Condition.SelfHpAtMost, 30, "b");

        Assert.Equal(new Tactic(2, Condition.SelfHpAtMost, 30, "b"), m.Tactics[1]);
    }

    [Fact]
    public void Replace_keeps_position_priority()
    {
        var m = Member(new Tactic(1, Condition.Always, 0, "a"), new Tactic(2, Condition.Always, 0, "b"));

        m.ReplaceTactic(1, Condition.EveryNthTurn, 2, "c");

        Assert.Equal(new Tactic(2, Condition.EveryNthTurn, 2, "c"), m.Tactics[1]);
    }

    [Fact]
    public void Remove_renumbers_remaining()
    {
        var m = Member(
            new Tactic(1, Condition.Always, 0, "a"),
            new Tactic(2, Condition.Always, 0, "b"),
            new Tactic(3, Condition.Always, 0, "c"));

        m.RemoveTactic(0);

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

        Assert.True(m.MoveTactic(2, -1));

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

        Assert.False(m.MoveTactic(index, offset));
        Assert.Equal(["a", "b"], Skills(m));
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
