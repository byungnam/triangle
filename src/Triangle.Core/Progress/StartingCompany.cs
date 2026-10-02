using Triangle.Core.Masteries;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>
/// 새 게임의 시작 회사. 세이브가 없을 때 쓴다.
/// 숙련 경험치는 배운 패시브의 포인트를 딱 채우는 레벨로 준다.
/// </summary>
/// <remarks>
/// 세트 1은 측정해서 고른 전술이다 (2026-10-02, 시드 500개). 정예 부대에게 세트 2(처음의 단순한 전술)는
/// 승률 4%, 세트 1은 98%였다. 세트 1의 핵심은 세 가지다:
/// - MP를 관리한다: MP가 낮으면 MP가 들지 않는 행동으로 바꿔 "자원 부족"으로 턴을 잃지 않는다.
/// - 위급할 때와 여유 있을 때 회복을 나눈다.
/// - 상태 효과를 처음과 주기적으로 다시 건다.
/// </remarks>
public static class StartingCompany
{
    private static int Xp(int level) => MasteryProgression.XpForLevel(level);

    private static Tactic T(int priority, Condition condition, int value, string actionId) => new(priority, condition, value, actionId);

    /// <param name="seed">회사의 첫 시드 (원정과 모집이 이어서 쓴다).</param>
    public static Company Create(int seed)
    {
        var members = Members();
        return new Company(members, members.Select(m => m.Id), Company.StartingGold, new Dictionary<string, int>(), activeTacticSet: 0, seed);
    }

    private static List<PartyMember> Members() =>
    [
        // 전위 공격수: MP가 있으면 강타, MP가 20% 이하로 떨어지면 기본 공격으로 아낀다.
        new PartyMember("ally_marcus", "마르쿠스", new Stats(15, 12, 25, 20, 13), Row.Front, "old_sword", "plate_armor",
            new Dictionary<string, int> { ["sword"] = Xp(4), ["plate"] = Xp(3) },
            new Dictionary<string, int> { ["swordsmanship"] = 2, ["flurry"] = 1, ["defense"] = 1, ["endurance"] = 1 },
            [
                // 세트 1
                [
                    T(1, Condition.SelfMpAtMost, 20, "basic_attack"),
                    T(2, Condition.Always, 0, "heavy_strike"),
                ],
                // 세트 2: 처음의 단순한 전술 (비교용)
                [T(1, Condition.EveryNthTurn, 3, "heavy_strike"), T(2, Condition.Always, 0, "basic_attack")],
            ]),

        // 두 번째 전위: 같은 방식.
        new PartyMember("ally_gaius", "가이우스", new Stats(14, 13, 24, 20, 12), Row.Front, "old_sword", "plate_armor",
            new Dictionary<string, int> { ["sword"] = Xp(2), ["plate"] = Xp(1) },
            new Dictionary<string, int> { ["swordsmanship"] = 2, ["defense"] = 1 },
            [
                // 세트 1
                [
                    T(1, Condition.SelfMpAtMost, 20, "basic_attack"),
                    T(2, Condition.Always, 0, "heavy_strike"),
                ],
                // 세트 2: 처음의 단순한 전술 (비교용)
                [T(1, Condition.Always, 0, "basic_attack")],
            ]),

        // 후위 궁수: 첫 턴과 4턴마다 독(없는 적 우선), 2턴마다 저격(엄호 무시), 그 외 화살.
        // MP가 15% 이하면 기본 공격으로 아낀다.
        new PartyMember("ally_claudia", "클라우디아", new Stats(10, 14, 20, 25, 13), Row.Back, "hunting_bow", "leather_armor",
            new Dictionary<string, int> { ["bow"] = Xp(9), ["leather"] = Xp(1) },
            new Dictionary<string, int> { ["archery"] = 4, ["precision_shooting"] = 1, ["rapid_fire"] = 1, ["mobility"] = 1 },
            [
                // 세트 1
                [
                    T(1, Condition.OnTurn, 1, "poison_arrow"),
                    T(2, Condition.SelfMpAtMost, 15, "basic_attack"),
                    T(3, Condition.EveryNthTurn, 4, "poison_arrow"),
                    T(4, Condition.EveryNthTurn, 2, "snipe"),
                    T(5, Condition.Always, 0, "fire_arrow"),
                ],
                // 세트 2: 처음의 단순한 전술 (비교용)
                [T(1, Condition.OnTurn, 1, "poison_arrow"), T(2, Condition.EveryNthTurn, 2, "snipe"), T(3, Condition.Always, 0, "fire_arrow")],
            ]),

        // 후위 회복: 위급(40% 이하)하면 치료가 최우선. 첫 턴 축복, MP가 낮으면 정신 집중,
        // 평균 HP가 떨어지면 재생, 조금 다친 아군(65% 이하)은 치료, 4턴마다 축복을 다시 건다.
        new PartyMember("ally_julia", "율리아", new Stats(10, 11, 21, 24, 12), Row.Back, "wooden_relic", "cloth_robe",
            new Dictionary<string, int> { ["relic"] = Xp(2), ["cloth"] = Xp(2) },
            new Dictionary<string, int> { ["healing"] = 2, ["meditation"] = 2 },
            [
                // 세트 1
                [
                    T(1, Condition.AnyAllyHpAtMost, 40, "heal"),
                    T(2, Condition.OnTurn, 1, "bless"),
                    T(3, Condition.SelfMpAtMost, 30, "focus"),
                    T(4, Condition.AllyAverageHpAtMost, 70, "regen"),
                    T(5, Condition.AnyAllyHpAtMost, 65, "heal"),
                    T(6, Condition.EveryNthTurn, 4, "bless"),
                    T(7, Condition.Always, 0, "basic_attack"),
                ],
                // 세트 2: 처음의 단순한 전술 (비교용)
                [T(1, Condition.OnTurn, 1, "bless"), T(2, Condition.AnyAllyHpAtMost, 60, "heal"), T(3, Condition.Always, 0, "basic_attack")],
            ]),
    ];
}
