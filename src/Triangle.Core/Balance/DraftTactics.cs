using Triangle.Core.Actions;
using Triangle.Core.Data;
using Triangle.Core.Effects;
using Triangle.Core.Tactics;

namespace Triangle.Core.Balance;

/// <summary>
/// 드래프트 시뮬레이션의 전술 배치. 스킬 사본마다 행동의 성격에 맞는 조건 틀로 한 줄을 만들고, 줄 상한만큼 남길 줄을 고른 뒤 정해진
/// 등급 순서로 쌓는다. 맨 아래에는 늘 "항상 → 기본 공격"을 둔다(줄 상한에 세지 않는다).
/// 사람이 짜는 전술보다 서툴지만, 봇이 모두 같은 배치를 쓰므로 봇 사이의 차이는 카드 고르기에서만 나온다.
/// </summary>
public static class DraftTactics
{
    public const string Fallback = "basic_attack";

    /// <summary>쓸모없는 줄(같은 행동을 같은 조건으로 또 놓은 사본)의 등급. 줄 상한이 남을 때만 들어간다.</summary>
    private const int Redundant = 99;

    /// <param name="skills">가진 사본 (같은 행동이 여럿일 수 있다). 앞에 있을수록 먼저 얻은 것이다.</param>
    /// <param name="lines">적을 줄의 수 (줄 상한, 또는 꺼진 줄까지 적을 때는 최대 줄 수).</param>
    /// <param name="active">켜질 줄 수 (줄 상한). 주면 켜질 줄을 먼저 고르고 나머지를 그 아래(꺼진 줄)에 적는다. 없으면 <paramref name="lines"/>.</param>
    public static IReadOnlyList<Tactic> Build(IReadOnlyList<string> skills, int lines, GameData data, int? active = null)
    {
        // 켤 줄을 먼저 고른다(2026-10-24): 회복, 가장 센 공격 하나, 나머지는 등급 순서. 고른 줄과 나머지를 각각 등급 순서로 쌓는다.
        // 예전에는 등급 순서로 바로 잘라서, 버프·도발·디버프를 거는 줄이 주 공격을 꺼진 줄로 밀어냈다.
        var items = skills
            .Select((id, i) => (Action: Runs.RunUpgrades.Action(data, id), Line: Line(Runs.RunUpgrades.Action(data, id), skills.Take(i).Count(s => s == id), data), Order: i))
            .ToList();
        var main = items.Where(x => x.Action.Effect == ActionEffect.Damage && x.Line.Rank != Redundant)
            .OrderByDescending(x => x.Line.Power).ThenBy(x => x.Order).Select(x => x.Order).DefaultIfEmpty(-1).First();
        int Keep((ActionDefinition Action, Template Line, int Order) x) =>
            x.Line.Rank == Redundant ? 3 : x.Action.Effect == ActionEffect.Heal ? 0 : x.Order == main ? 1 : 2;
        var chosen = items.OrderBy(Keep).ThenBy(x => x.Line.Rank).ThenByDescending(x => x.Line.Power).ThenBy(x => x.Order).ToList();
        var on = Math.Max(0, Math.Min(active ?? lines, lines));
        IEnumerable<Template> ByRank(IEnumerable<(ActionDefinition Action, Template Line, int Order)> part) =>
            part.OrderBy(x => x.Line.Rank).ThenByDescending(x => x.Line.Power).ThenBy(x => x.Order).Select(x => x.Line);

        // 공격 줄(등급 7 이상)은 켜진 회복·보호막·버프 줄을 쓸 MP를 남긴다: 그 줄 비용 중 가장 큰 것만큼 더 있을 때만 쓴다.
        var reserve = chosen.Take(on).Where(x => x.Line.Rank <= 6).Select(x => x.Line.MpCost).DefaultIfEmpty(0).Max();
        Template Reserved(Template l) => reserve > 0 && l.Rank is >= 7 and not Redundant && l.MpCost > 0
            ? l with { Conditions = l.Conditions.Select(c => c.Condition == Condition.SelfMpAmountAtLeast ? c with { Value = l.MpCost + reserve } : c).ToList() }
            : l;
        var kept = ByRank(chosen.Take(on)).Concat(ByRank(chosen.Skip(on))).Take(Math.Max(0, lines)).Select(Reserved).ToList();
        return
        [
            .. kept.Select((l, i) => Tactic.Create(i + 1, l.Conditions, l.ActionId)),
            Tactic.Create(kept.Count + 1, [new(Condition.Always, 0)], Fallback),
        ];
    }

    private sealed record Template(int Rank, int Power, IReadOnlyList<TacticCondition> Conditions, string ActionId, int MpCost);

    /// <param name="copy">같은 행동의 몇 번째 사본인가 (0부터). 회복·보호막·광역은 두 번째 사본을 느슨한 조건으로 쓴다.</param>
    private static Template Line(ActionDefinition a, int copy, GameData data)
    {
        var first = a.Applies.Count > 0 ? data.Effects[a.Applies[0].EffectId] : null;
        var (rank, condition, value, loosened) = a switch
        {
            { Effect: ActionEffect.Heal, Side: TargetSide.Self } => (1, Condition.SelfHpAtMost, copy == 0 ? 40 : 70, true),
            { Effect: ActionEffect.Heal, Scope: TargetScope.All } => (5, Condition.AllyAverageHpAtMost, copy == 0 ? 70 : 85, true),
            { Effect: ActionEffect.Heal } => (1, Condition.AnyAllyHpAtMost, copy == 0 ? 50 : 75, true),
            { Effect: ActionEffect.Shield, Side: TargetSide.Self } => (2, Condition.SelfHpAtMost, copy == 0 ? 70 : 90, true),
            { Effect: ActionEffect.Shield } => (2, Condition.AnyAllyHpAtMost, copy == 0 ? 70 : 90, true),
            { RemovesDebuffs: true } => (2, Condition.AnyAllyHasDebuff, 0, false),
            { MoveTo: not null } => (3, Condition.SelfDisplaced, 0, false),
            { Effect: ActionEffect.Summon } => (4, Condition.MaxUses, 1, false),
            { Effect: ActionEffect.None, Side: TargetSide.Self } when first is not null => (4, Condition.SelfLacksActionEffect, 0, false),
            { Effect: ActionEffect.None, Side: TargetSide.Ally } when first is not null => (4, Condition.AnyAllyLacksActionEffect, 0, false),
            { Effect: ActionEffect.RestoreMp, Side: TargetSide.Self } => (6, Condition.SelfMpAtMost, 30, false),
            { Effect: ActionEffect.RestoreMp } => (6, Condition.AnyAllyMpAtMost, 30, false),
            { RemovesBuffs: true } => (7, Condition.AnyEnemyHasBuff, 0, false),
            _ when first?.Kind == EffectKind.Debuff => (7, Condition.AnyEnemyLacksActionEffect, 0, false),
            { Scope: TargetScope.All } => (8, Condition.EnemyAliveAtLeast, copy == 0 ? 2 : 1, true),
            _ => (9, Condition.Always, 0, false),
        };

        if (copy >= (loosened ? 2 : 1))
        {
            rank = Redundant;
        }

        var conditions = new List<TacticCondition>();
        if (condition != Condition.Always)
        {
            conditions.Add(new(condition, value));
        }

        // 비용을 낼 수 없으면 차례를 잃으므로, 비용이 있는 줄은 낼 수 있을 때만 쓴다 (못 내면 아래 줄로 넘어간다).
        if (a.MpCost > 0)
        {
            conditions.Add(new(Condition.SelfMpAmountAtLeast, a.MpCost));
        }

        if (a.HpCost > 0)
        {
            conditions.Add(new(Condition.SelfHpAmountAtLeast, a.HpCost + 1));
        }

        return new Template(rank, a.Power * a.Hits, conditions.Count == 0 ? [new(Condition.Always, 0)] : conditions, a.Id, a.MpCost);
    }
}
