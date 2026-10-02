using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 시연용 시작 파티. 세이브가 없을 때 쓴다.
/// 숙련 경험치는 배운 스킬의 포인트를 딱 채우는 레벨로 준다.
/// </summary>
internal static class DemoParty
{
    private static int Xp(int level) => MasteryProgression.XpForLevel(level);

    public static Party Create() => new(
    [
        new PartyMember("ally_marcus", "마르쿠스", new Stats(15, 12, 25, 20, 13), Row.Front, "sword", "plate",
            new Dictionary<string, int> { ["sword"] = Xp(4), ["plate"] = Xp(3) },
            new Dictionary<string, int> { ["swordsmanship"] = 2, ["flurry"] = 1, ["defense"] = 1, ["endurance"] = 1 },
            [
                new Tactic(1, Condition.EveryNthTurn, 3, "heavy_strike"),
                new Tactic(2, Condition.Always, 0, "basic_attack"),
            ]),

        new PartyMember("ally_gaius", "가이우스", new Stats(14, 13, 24, 20, 12), Row.Front, "sword", "plate",
            new Dictionary<string, int> { ["sword"] = Xp(2), ["plate"] = Xp(1) },
            new Dictionary<string, int> { ["swordsmanship"] = 2, ["defense"] = 1 },
            [new Tactic(1, Condition.Always, 0, "basic_attack")]),

        new PartyMember("ally_claudia", "클라우디아", new Stats(10, 14, 20, 25, 13), Row.Back, "bow", "leather",
            new Dictionary<string, int> { ["bow"] = Xp(9), ["leather"] = Xp(1) },
            new Dictionary<string, int> { ["archery"] = 4, ["precision_shooting"] = 1, ["rapid_fire"] = 1, ["mobility"] = 1 },
            [
                new Tactic(1, Condition.OnTurn, 1, "poison_arrow"),
                new Tactic(2, Condition.EveryNthTurn, 2, "snipe"),
                new Tactic(3, Condition.Always, 0, "fire_arrow"),
            ]),

        new PartyMember("ally_julia", "율리아", new Stats(10, 11, 21, 24, 12), Row.Back, "relic", "cloth",
            new Dictionary<string, int> { ["relic"] = Xp(2), ["cloth"] = Xp(1) },
            new Dictionary<string, int> { ["healing"] = 2, ["meditation"] = 1 },
            [
                new Tactic(1, Condition.OnTurn, 1, "bless"),
                new Tactic(2, Condition.AnyAllyHpAtMost, 60, "heal"),
                new Tactic(3, Condition.Always, 0, "basic_attack"),
            ]),
    ]);
}
