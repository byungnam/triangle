using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

/// <summary>공용 자기 회복 행동: 응급 처치(자기 HP)와 정신 집중(자기 MP)은 장비·패시브 없이 누구나 쓴다.</summary>
public class SelfRecoveryTests
{
    private static readonly GameData Data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    [Theory]
    [InlineData("first_aid")]
    [InlineData("focus")]
    public void Self_recovery_actions_are_universal_and_need_no_skills(string actionId)
    {
        var action = Data.Actions[actionId];

        Assert.True(action.Universal);
        Assert.Empty(action.Requirements);
        Assert.Equal(Actions.TargetSide.Self, action.Side);
    }

    [Fact]
    public void A_member_without_skills_heals_and_restores_mp_on_their_own()
    {
        var member = new PartyMember("a", "A", new Stats(10, 10, 20, 15, 10), Row.Front, TestGear.Of(), null,
            new Dictionary<string, int>(), new Dictionary<string, int>(),
            [[
                new Tactic(1, Condition.SelfMpAtMost, 90, "focus"),
                new Tactic(2, Condition.SelfHpAtMost, 99, "first_aid"),
                new Tactic(3, Condition.Always, 0, "basic_attack"),
            ]]);
        Assert.Empty(member.LockedTacticIndexes(Data, 0));

        var result = CombatSimulator.Run([member.ToCombatantSetup(Data, 0)], Data.CreateEncounterTeam("wild_dogs"), Data.Catalog, seed: 1);

        Assert.Contains(result.Events, e => e is Healed h && h.TargetId == "a" && h.Amount > 0);
        Assert.Contains(result.Events, e => e is MpRestored m && m.TargetId == "a" && m.Amount > 0);
    }

    [Fact]
    public void Starting_members_and_recruit_templates_use_first_aid_by_default()
    {
        var company = StartingCompany.Create(Data, seed: 1);
        foreach (var member in company.Roster)
        {
            Assert.Contains(member.TacticSets[0], t => t.ActionId == "first_aid");
            Assert.Empty(member.LockedTacticIndexes(Data, 0));
        }

        foreach (var template in Data.Recruits.Values)
        {
            Assert.Contains(template.Tactics, t => t.ActionId == "first_aid");
        }
    }
}
