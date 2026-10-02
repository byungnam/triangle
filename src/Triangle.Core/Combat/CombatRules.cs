using Triangle.Core.Skills;
using Triangle.Core.Units;

namespace Triangle.Core.Combat;

/// <summary>
/// 전투 공식 상수. 기본값은 레거시 프로토타입에서 가져왔다.
/// 정밀도 문제를 피하려고 모든 계수는 정수 백분율로 둔다.
/// </summary>
public sealed record CombatRules
{
    public static CombatRules Default { get; } = new();

    /// <summary>행동 간격 = TimeConstant / speed (버림) × (100 − 대기 감소%) / 100.</summary>
    public int TimeConstant { get; init; } = 1000;

    /// <summary>전투 전체에서 허용하는 행동 횟수(라운드가 아니라 개별 행동).</summary>
    public int MaxActions { get; init; } = 100;

    public CombatOutcome ActionLimitOutcome { get; init; } = CombatOutcome.Draw;

    public int HpPerVital { get; init; } = 28;
    public int MpPerIntel { get; init; } = 10;

    /// <summary>방어 1당 피해 경감(%): 피해 × 100 / (100 + def × 값).</summary>
    public int DefenseReductionPercentPerPoint { get; init; } = 2;

    /// <summary>스탯 1당 위력 증가(%): 위력 × (100 + stat × 값) / 100.</summary>
    public int PowerScalingPercentPerPoint { get; init; } = 5;

    /// <summary>감소 보너스(대기, MP 소모, 받는 피해)의 합계 상한(%).</summary>
    public int MaxReductionPercent { get; init; } = 90;

    public int MaxHp(Stats stats, SkillSet skills) =>
        Ratio.ApplyPercent(stats.Vital * HpPerVital, 100 + skills.Bonus(BonusKind.MaxHpPercent));

    public int MaxMp(Stats stats, SkillSet skills) =>
        Ratio.ApplyPercent(stats.Intel * MpPerIntel, 100 + skills.Bonus(BonusKind.MaxMpPercent));
}
