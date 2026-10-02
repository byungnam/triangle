using Triangle.Core.Combat;

namespace Triangle.Core.Masteries;

/// <summary>숙련 경험치 규칙 (임시 수치).</summary>
public sealed record MasteryRules
{
    public static MasteryRules Default { get; } = new();

    /// <summary>행동 1회당 무기 숙련.</summary>
    public int XpPerAction { get; init; } = 10;

    /// <summary>준 피해·회복 이만큼당 무기 숙련 1.</summary>
    public int AmountPerXp { get; init; } = 10;

    /// <summary>받은 피해 이만큼당 방어구 숙련 1.</summary>
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

/// <param name="Weapon">장착한 무기 계열 (없으면 null).</param>
public sealed record MasteryXp(string CombatantId, string? Weapon, int WeaponXp, string? Armor, int ArmorXp);

/// <summary>전투 기록에서 유닛별 숙련 경험치를 계산한다.</summary>
public static class MasteryGain
{
    /// <summary>
    /// 아군 유닛마다:
    /// 무기 = 행동 횟수 × XpPerAction + 준 피해·회복 / AmountPerXp + 결과,
    /// 방어구 = 받은 피해 / DamageTakenPerXp + 결과.
    /// 피해·회복은 바로 앞의 ActionUsed를 한 유닛이 준 것으로 본다.
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
                c.Armor,
                c.Armor is null ? 0 : taken.GetValueOrDefault(c.Id) / rules.DamageTakenPerXp + resultXp))
            .ToList();
    }
}
