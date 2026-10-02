using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 시연용 시작 파티. 세이브가 없을 때 쓴다. 스킬은 누적 SP로 준다.
/// </summary>
internal static class DemoParty
{
    // 랭크 1 기준 레벨별 누적 SP (EVE 표). 랭크 r 스킬은 r배.
    private const int Lv1 = 250, Lv2 = 1_415, Lv3 = 8_000, Lv4 = 45_255;

    public static Party Create() => new(
    [
        new PartyMember("ally_marcus", "마르쿠스", new Stats(15, 12, 25, 20, 13), Row.Front,
            new Dictionary<string, int> { ["melee_weapons"] = Lv2, ["endurance"] = Lv2, ["defense"] = Lv1 * 2 },
            [new Tactic(1, Condition.Always, 0, "basic_attack")]),

        new PartyMember("ally_gaius", "가이우스", new Stats(14, 13, 24, 20, 12), Row.Front,
            new Dictionary<string, int> { ["melee_weapons"] = Lv2, ["endurance"] = Lv1 },
            [new Tactic(1, Condition.Always, 0, "basic_attack")]),

        new PartyMember("ally_claudia", "클라우디아", new Stats(10, 14, 20, 25, 13), Row.Back,
            new Dictionary<string, int> { ["archery"] = Lv4, ["precision_shooting"] = Lv1 * 3, ["rapid_fire"] = Lv1 * 2 },
            [
                new Tactic(1, Condition.EveryNthTurn, 2, "snipe"),
                new Tactic(2, Condition.Always, 0, "fire_arrow"),
            ]),

        new PartyMember("ally_julia", "율리아", new Stats(10, 11, 21, 24, 12), Row.Back,
            new Dictionary<string, int> { ["healing"] = Lv2, ["archery"] = Lv1, ["meditation"] = Lv1 },
            [
                new Tactic(1, Condition.AnyAllyHpAtMost, 60, "heal"),
                new Tactic(2, Condition.Always, 0, "fire_arrow"),
            ]),
    ]);
}
