namespace Triangle.Core.Combat;

/// <summary>
/// 전투 공식 상수. 기본값은 레거시 프로토타입에서 가져왔다.
/// 정밀도 문제를 피하려고 모든 계수는 정수 백분율로 둔다.
/// </summary>
public sealed record CombatRules
{
    public static CombatRules Default { get; } = new();

    /// <summary>행동 간격 = TimeConstant × 100 / (직업 SpeedPercent × speed), 버림.</summary>
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
}
