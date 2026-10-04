using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>
/// 새 게임의 시작 회사: 전위 탱커, 후위 궁수, 후위 마법사. 세이브가 없을 때 쓴다.
/// 모두 T1 주무기와 재질을 맞춘 머리·몸통·신발을 끼고, 탱커는 방패도 든다.
/// 숙련 경험치는 배운 패시브의 포인트를 딱 채우는 레벨로 준다.
/// </summary>
/// <remarks>
/// 세트 1은 다듬은 전술, 세트 2는 기본 전술(비교용)이다. 원정에서는 HP·MP가 전투 사이에 이어지므로
/// 세트 1은 MP를 관리한다: MP가 낮으면 MP가 들지 않는 행동으로 바꿔 "자원 부족"으로 턴을 잃지 않는다.
/// 상태 효과(독, 약화)는 처음과 주기적으로 다시 건다.
/// </remarks>
public static class StartingCompany
{
    private static int Xp(int level) => MasteryProgression.XpForLevel(level);

    private static Dictionary<EquipmentSlot, string> Gear(string mainHand, string material, string? offHand = null)
    {
        var (head, body, feet) = material switch
        {
            "plate" => ("plate_helm", "plate_armor", "plate_boots"),
            "leather" => ("leather_cap", "leather_armor", "leather_boots"),
            _ => ("cloth_hood", "cloth_robe", "cloth_shoes"),
        };
        var gear = new Dictionary<EquipmentSlot, string>
        {
            [EquipmentSlot.MainHand] = mainHand,
            [EquipmentSlot.Head] = head,
            [EquipmentSlot.Body] = body,
            [EquipmentSlot.Feet] = feet,
        };
        if (offHand is not null)
        {
            gear[EquipmentSlot.OffHand] = offHand;
        }

        return gear;
    }

    private static Tactic T(int priority, Condition condition, int value, string actionId) => new(priority, condition, value, actionId);

    /// <param name="seed">회사의 첫 시드 (원정과 모집이 이어서 쓴다).</param>
    public static Company Create(GameData data, int seed)
    {
        var members = Members();
        var company = new Company(members, members.Select(m => m.Id), Company.StartingGold, new Dictionary<string, int>(), activeTacticSet: 0, seed);
        company.RerollRecruits(data);
        return company;
    }

    private static List<PartyMember> Members() =>
    [
        // 전위 탱커: 받는 피해를 줄이고(방어 기술) 최대 HP를 늘린다(체력 단련). 공격은 기본 공격뿐이다.
        // HP가 35% 이하면 응급 처치로 버틴다.
        new PartyMember("ally_godric", "고드릭", new Stats(15, 12, 26, 20, 12), Row.Front, Gear("old_sword", "plate", offHand: "wooden_shield"), null,
            new Dictionary<string, int> { ["plate"] = Xp(4) },
            new Dictionary<string, int> { ["defense"] = 2, ["endurance"] = 1 },
            [
                [
                    T(1, Condition.SelfHpAtMost, 35, "first_aid"),
                    T(2, Condition.Always, 0, "basic_attack"),
                ],
                [T(1, Condition.Always, 0, "basic_attack")],
            ]),

        // 후위 궁수: 첫 턴과 4턴마다 독화살(없는 적 우선), 그 외 화살. HP가 30% 이하면 응급 처치,
        // MP가 15% 이하면 정신 집중으로 채운다.
        new PartyMember("ally_elsbeth", "엘스베트", new Stats(10, 14, 20, 24, 13), Row.Back, Gear("hunting_bow", "leather"), null,
            new Dictionary<string, int> { ["bow"] = Xp(2) },
            new Dictionary<string, int> { ["archery"] = 2 },
            [
                // 세트 1
                [
                    T(1, Condition.SelfHpAtMost, 30, "first_aid"),
                    T(2, Condition.SelfMpAtMost, 15, "focus"),
                    T(3, Condition.OnTurn, 1, "poison_arrow"),
                    T(4, Condition.EveryNthTurn, 4, "poison_arrow"),
                    T(5, Condition.Always, 0, "fire_arrow"),
                ],
                // 세트 2: 기본 전술
                [T(1, Condition.Always, 0, "fire_arrow")],
            ]),

        // 후위 마법사: 화염구(지속 피해)를 주로 쓰고, 싸움이 길어지면 4턴마다 약화를 건다.
        // HP가 30% 이하면 응급 처치, MP가 25% 이하면 정신 집중으로 채운다. 첫 턴 약화는 짧은 전투에서 MP와 턴만 써서 뺐다.
        new PartyMember("ally_morwen", "모르웬", new Stats(9, 11, 19, 27, 12), Row.Back, Gear("apprentice_staff", "cloth"),
            new Dictionary<EquipmentSlot, IReadOnlyList<string>> { [EquipmentSlot.MainHand] = ["fireball", "weaken"] },
            new Dictionary<string, int> { ["staff"] = Xp(3), ["cloth"] = Xp(2) },
            new Dictionary<string, int> { ["magic_control"] = 3, ["meditation"] = 2 },
            [
                // 세트 1
                [
                    T(1, Condition.SelfHpAtMost, 30, "first_aid"),
                    T(2, Condition.SelfMpAtMost, 25, "focus"),
                    T(3, Condition.EveryNthTurn, 4, "weaken"),
                    T(4, Condition.Always, 0, "fireball"),
                ],
                // 세트 2: 기본 전술 (행동 칸에서 화염구를 골랐으므로 마력탄 대신 화염구)
                [T(1, Condition.Always, 0, "fireball")],
            ]),
    ];
}
