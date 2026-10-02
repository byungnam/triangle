using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 첫 화면 시연용 아군 팀. 로스터와 세이브가 생기면 그쪽에서 가져온다.
/// </summary>
internal static class DemoParty
{
    public static IReadOnlyList<CombatantSetup> Create(GameData data) =>
    [
        new("ally_marcus", "마르쿠스", data.Classes["soldier"], new Stats(15, 12, 25, 20, 13), Row.Front,
            [new Tactic(1, Condition.Always, 0, "basic_attack")]),

        new("ally_gaius", "가이우스", data.Classes["soldier"], new Stats(14, 13, 24, 20, 12), Row.Front,
            [new Tactic(1, Condition.Always, 0, "basic_attack")]),

        new("ally_claudia", "클라우디아", data.Classes["mage"], new Stats(10, 12, 20, 25, 13), Row.Back,
            [
                new Tactic(1, Condition.EveryNthTurn, 2, "artillery"),
                new Tactic(2, Condition.Always, 0, "fire_arrow"),
            ]),

        new("ally_julia", "율리아", data.Classes["priest"], new Stats(10, 11, 21, 24, 12), Row.Back,
            [
                new Tactic(1, Condition.AnyAllyHpAtMost, 60, "heal"),
                new Tactic(2, Condition.Always, 0, "fire_arrow"),
            ]),
    ];
}
