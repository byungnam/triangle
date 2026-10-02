using Triangle.Core.Combat;
using Triangle.Core.Items;

namespace Triangle.Core.Masteries;

/// <summary>숙련 경험치 규칙 (임시 수치).</summary>
public sealed record MasteryRules
{
    public static MasteryRules Default { get; } = new();

    /// <summary>행동 1회당 무기 숙련.</summary>
    public int XpPerAction { get; init; } = 10;

    /// <summary>준 피해·회복 이만큼당 무기 숙련 1.</summary>
    public int AmountPerXp { get; init; } = 10;

    /// <summary>받은 피해 이만큼당 방어구 숙련 1 (부위로 나누기 전).</summary>
    public int DamageTakenPerXp { get; init; } = 10;

    /// <summary>전투 결과에 따라 무기·방어구 숙련에 각각 더한다.</summary>
    public int VictoryXp { get; init; } = 100;
    public int DrawXp { get; init; } = 50;
    public int DefeatXp { get; init; } = 30;

    public int ResultXp(CombatOutcome outcome) => outcome switch
    {
        CombatOutcome.Victory => VictoryXp,
        CombatOutcome.Defeat => DefeatXp,
        _ => DrawXp,
    };
}

/// <param name="Weapon">주무기 계열 (없으면 null).</param>
/// <param name="Armor">방어구 재질 계열별 경험치 (입은 부위가 없으면 비어 있다).</param>
public sealed record MasteryXp(string CombatantId, string? Weapon, int WeaponXp, IReadOnlyDictionary<string, int> Armor);

/// <summary>전투 기록에서 유닛별 숙련 경험치를 계산한다.</summary>
public static class MasteryGain
{
    /// <summary>
    /// 아군 유닛마다:
    /// 무기 = 행동 횟수 × XpPerAction + 준 피해·회복 / AmountPerXp + 결과,
    /// 방어구 = 받은 피해 / DamageTakenPerXp + 결과. 이것을 머리·몸통·신발 칸마다 1/3씩 그 칸 재질에 준다
    /// (판금 머리 + 천 몸통·신발이면 판금 1/3, 천 2/3). 빈 칸 몫은 버린다.
    /// 피해·회복은 바로 앞의 ActionUsed를 한 유닛이 준 것으로 본다. 지속 피해는 받은 피해로만 센다.
    /// </summary>
    public static IReadOnlyList<MasteryXp> ForAllies(CombatResult result, MasteryRules? rules = null)
    {
        rules ??= MasteryRules.Default;
        var actions = new Dictionary<string, int>();
        var dealt = new Dictionary<string, int>();
        var taken = new Dictionary<string, int>();
        string? actor = null;

        foreach (var e in result.Events)
        {
            switch (e)
            {
                case ActionUsed a:
                    actor = a.ActorId;
                    actions[a.ActorId] = actions.GetValueOrDefault(a.ActorId) + 1;
                    break;
                case Damaged d:
                    taken[d.TargetId] = taken.GetValueOrDefault(d.TargetId) + d.Amount;
                    if (actor is not null)
                    {
                        dealt[actor] = dealt.GetValueOrDefault(actor) + d.Amount;
                    }

                    break;
                case Healed h when actor is not null:
                    dealt[actor] = dealt.GetValueOrDefault(actor) + h.Amount;
                    break;
                case EffectTicked { HpChange: < 0 } t:
                    // 지속 피해는 받은 쪽 방어구 숙련에만 친다 (건 쪽을 따로 추적하지 않는다).
                    taken[t.TargetId] = taken.GetValueOrDefault(t.TargetId) - t.HpChange;
                    break;
                case TurnStarted or Waited:
                    actor = null;
                    break;
            }
        }

        var resultXp = rules.ResultXp(result.Outcome);
        return result.Combatants
            .Where(c => c.Side == CombatSide.Ally)
            .Select(c => new MasteryXp(
                c.Id,
                c.Weapon,
                c.Weapon is null ? 0 : actions.GetValueOrDefault(c.Id) * rules.XpPerAction + dealt.GetValueOrDefault(c.Id) / rules.AmountPerXp + resultXp,
                ArmorShares(c.ArmorMasteries, taken.GetValueOrDefault(c.Id) / rules.DamageTakenPerXp + resultXp)))
            .ToList();
    }

    /// <summary>방어구 경험치를 부위 수 비율로 재질마다 나눈다 (재질별로 한 번에 버림).</summary>
    private static IReadOnlyDictionary<string, int> ArmorShares(IReadOnlyList<string> pieces, int total) =>
        pieces
            .GroupBy(m => m)
            .ToDictionary(g => g.Key, g => total * g.Count() / EquipmentSlots.Armor.Count);
}
