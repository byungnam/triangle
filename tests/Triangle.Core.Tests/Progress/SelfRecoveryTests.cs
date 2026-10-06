using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

/// <summary>자기 회복: 정신 집중(자기 MP)은 누구나 쓰고, 응급 처치(자기 HP)는 방패의 행동이다.</summary>
public class SelfRecoveryTests
{
    private static readonly GameData Data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    private static PartyMember Member(Dictionary<EquipmentSlot, string> gear) =>
        new("a", "A", new Stats(10, 10, 20, 15, 10), Row.Front, gear,
            new Dictionary<string, int>(), new Dictionary<string, int>(),
            [[
                new Tactic(1, Condition.SelfMpAtMost, 90, "focus"),
                new Tactic(2, Condition.SelfHpAtMost, 99, "first_aid"),
                new Tactic(3, Condition.Always, 0, "basic_attack"),
            ]]);

    [Fact]
    public void Focus_is_universal_and_needs_no_skills()
    {
        var focus = Data.Actions["focus"];

        Assert.True(focus.Universal);
        Assert.Empty(focus.Requirements);
        Assert.Equal(TargetSide.Self, focus.Side);
    }

    [Fact]
    public void Every_shield_offers_first_aid_and_nothing_else_does()
    {
        var firstAid = Data.Actions["first_aid"];
        Assert.False(firstAid.Universal);
        Assert.Empty(firstAid.Requirements);
        Assert.Equal(TargetSide.Self, firstAid.Side);

        var offering = Data.Items.Values.Where(i => i.Actions.Contains("first_aid")).Select(i => i.Id).ToHashSet();
        Assert.Equal(new HashSet<string> { "wooden_shield", "iron_shield", "knight_shield", "bulwark_shield" }, offering);
    }

    [Fact]
    public void Without_a_shield_first_aid_is_locked()
    {
        Assert.Equal([1], Member(TestGear.Of("old_sword")).LockedTacticIndexes(Data, 0));
    }

    [Fact]
    public void A_shield_bearer_without_skills_heals_and_restores_mp_on_their_own()
    {
        var member = Member(TestGear.Of("old_sword", offHand: "wooden_shield"));
        Assert.Empty(member.LockedTacticIndexes(Data, 0));

        var result = CombatSimulator.Run([member.ToCombatantSetup(Data, 0)], Data.CreateEncounterTeam("wild_dogs"), Data.Catalog, seed: 1);

        Assert.Contains(result.Events, e => e is Healed h && h.TargetId == "a" && h.Amount > 0);
        Assert.Contains(result.Events, e => e is MpRestored m && m.TargetId == "a" && m.Amount > 0);
    }

    [Fact]
    public void Default_tactics_use_first_aid_only_with_a_shield()
    {
        foreach (var member in StartingCompany.Create(Data, seed: 1).Roster)
        {
            Assert.Empty(member.LockedTacticIndexes(Data, 0));
        }

        foreach (var template in Data.Recruits.Values)
        {
            var hasShield = template.Equipment.TryGetValue(EquipmentSlot.OffHand, out var offHand) && offHand.EndsWith("_shield");
            Assert.Equal(hasShield, template.Tactics.Any(t => t.ActionId == "first_aid"));
        }
    }
}
