using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Tactics;

namespace Triangle.Core.Balance;

/// <summary>
/// 맞대결 하나를 전술만 바꿔 가며 잴 수 있게 준비한 것: 그 성장 상태의 파티 입력(전술은 비어 있다),
/// 멤버마다 쓸 수 있는 행동, 적 팀.
/// </summary>
public sealed record PreparedMatchup(
    BenchMatchup Matchup,
    BenchState State,
    IReadOnlyList<CombatantSetup> Party,
    IReadOnlyDictionary<string, IReadOnlySet<string>> UsableActions,
    IReadOnlyList<CombatantSetup> Enemies);

/// <summary>
/// 맞대결 하나를 시드 여러 개로 잰 결과. 무승부(행동 상한)는 진 것으로 센다.
/// </summary>
/// <param name="AllyHpOnWins">이긴 전투마다 남은 아군 HP 비율(0~1)의 합.</param>
/// <param name="EnemyHpOnLosses">이기지 못한 전투마다 남은 적 HP 비율(0~1)의 합.</param>
public sealed record BenchScore(int Battles, int Wins, int Draws, double AllyHpOnWins, double EnemyHpOnLosses)
{
    public double WinRate => Battles == 0 ? 0 : 100.0 * Wins / Battles;

    /// <summary>
    /// 탐색용 점수 (전투당 평균): 이기면 1 + 0.2 × 남은 아군 HP 비율, 지면 0.2 × 깎은 적 HP 비율.
    /// 이긴 전투는 언제나 진 전투보다 높고, 승률이 같으면 더 여유 있게 이기거나 더 많이 깎은 쪽이 높다.
    /// </summary>
    public double Fitness => Battles == 0 ? 0 : (Wins + 0.2 * AllyHpOnWins + 0.2 * (Battles - Wins - EnemyHpOnLosses)) / Battles;
}

/// <summary>
/// 전술 측정: 기준 파티(<see cref="BenchDefinition"/>)가 전술 세트를 바꿔 가며 적 조합과 HP·MP가 가득 찬 한 판을 싸운다.
/// 만능 세트 하나로 모든 적을 이기는지, 적마다 세트를 바꿔야 하는지를 숫자로 본다.
/// </summary>
public static class TacticBench
{
    /// <summary>
    /// 기준 파티를 그 성장 상태로 만든다(<see cref="BenchUnit.Grow"/>).
    /// <paramref name="equipment"/>는 세트의 장비 교체(<see cref="BenchDefinition.EquipmentOf"/>)로, 성장 전에 바꿔 낀다.
    /// </summary>
    public static IReadOnlyList<BenchUnit> Party(
        BenchDefinition bench,
        GameData data,
        BenchState state,
        IReadOnlyDictionary<string, IReadOnlyDictionary<EquipmentSlot, string>>? equipment = null)
    {
        var party = bench.Party.Select(m => new BenchUnit(data, m, Worn(m, equipment))).ToList();
        if (state.Tier > 0)
        {
            party.ForEach(u => u.Grow(state.Tier, state.MasteryLevel));
        }

        return party;
    }

    /// <summary>멤버의 시작 장비에 세트의 교체를 덮는다. 빈 문자열은 그 부위를 비운다.</summary>
    private static Dictionary<EquipmentSlot, string> Worn(BenchMember member, IReadOnlyDictionary<string, IReadOnlyDictionary<EquipmentSlot, string>>? equipment)
    {
        var worn = member.Equipment.ToDictionary();
        foreach (var (slot, itemId) in equipment?.GetValueOrDefault(member.Id) ?? new Dictionary<EquipmentSlot, string>())
        {
            if (itemId == "")
            {
                worn.Remove(slot);
            }
            else
            {
                worn[slot] = itemId;
            }
        }

        return worn;
    }

    /// <param name="setId">이 세트의 장비 교체로 파티를 만든다 (null이면 시작 장비 그대로).</param>
    public static PreparedMatchup Prepare(BenchDefinition bench, GameData data, BenchMatchup matchup, string? setId = null)
    {
        var state = bench.State(matchup.State);
        var party = Party(bench, data, state, setId is null ? null : bench.EquipmentOf(setId));
        var usable = party.ToDictionary(u => u.Id, u => u.UsableActions());
        return new PreparedMatchup(matchup, state, party.Select(u => u.Setup()).ToList(), usable, data.CreateEncounterTeam(matchup.EncounterId));
    }

    /// <summary>
    /// 전술을 넣은 파티 입력. 그 상태에서 쓸 수 없는 행동(장비나 패시브가 아직 없음)의 줄은 원정처럼 빼고 싸운다.
    /// 전술이 없는 멤버는 아무것도 하지 않는다.
    /// </summary>
    public static IReadOnlyList<CombatantSetup> Allies(PreparedMatchup prepared, IReadOnlyDictionary<string, IReadOnlyList<Tactic>> tactics) =>
        prepared.Party
            .Select(s => s with
            {
                Tactics = tactics.GetValueOrDefault(s.Id, []).Where(t => prepared.UsableActions[s.Id].Contains(t.ActionId)).ToList(),
            })
            .ToList();

    /// <summary>그 상태에서 쓸 수 없어 빠지는 줄 (멤버 ID, 전술).</summary>
    public static IReadOnlyList<(string MemberId, Tactic Tactic)> Dropped(PreparedMatchup prepared, IReadOnlyDictionary<string, IReadOnlyList<Tactic>> tactics) =>
        prepared.Party
            .SelectMany(s => tactics.GetValueOrDefault(s.Id, []).Where(t => !prepared.UsableActions[s.Id].Contains(t.ActionId)).Select(t => (s.Id, t)))
            .ToList();

    /// <summary>시드 <paramref name="firstSeed"/>부터 <paramref name="seeds"/>개로 잰다. 시드마다 따로 계산하므로 여러 스레드로 돌려도 결과가 같다.</summary>
    public static BenchScore Measure(
        PreparedMatchup prepared,
        IReadOnlyDictionary<string, IReadOnlyList<Tactic>> tactics,
        GameData data,
        int seeds,
        int firstSeed = 0)
    {
        var allies = Allies(prepared, tactics);
        var results = new (CombatOutcome Outcome, double AllyHp, double EnemyHp)[seeds];
        Parallel.For(0, seeds, i =>
        {
            var result = CombatSimulator.Run(allies, prepared.Enemies, data.Catalog, firstSeed + i);
            results[i] = (result.Outcome, HpFraction(result, CombatSide.Ally), HpFraction(result, CombatSide.Enemy));
        });

        var wins = results.Where(r => r.Outcome == CombatOutcome.Victory).ToList();
        var others = results.Where(r => r.Outcome != CombatOutcome.Victory).ToList();
        return new BenchScore(
            seeds,
            wins.Count,
            results.Count(r => r.Outcome == CombatOutcome.Draw),
            wins.Sum(r => r.AllyHp),
            others.Sum(r => r.EnemyHp));
    }

    /// <summary>소환수를 뺀 한쪽 편의 남은 HP 합 / 최대 HP 합.</summary>
    private static double HpFraction(CombatResult result, CombatSide side)
    {
        var units = result.Combatants.Where(c => c.Side == side && !c.IsSummon).ToList();
        return (double)units.Sum(c => Math.Max(0, c.Hp)) / units.Sum(c => c.MaxHp);
    }
}
