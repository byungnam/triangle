using Triangle.Core.Actions;
using Triangle.Core.Skills;
using Triangle.Core.Units;

namespace Triangle.Core.Combat;

/// <summary>
/// 전투 공식 중 상대(대상)와 상관없는 부분. 전투 엔진과 화면의 행동 미리보기(<see cref="ActionPreview"/>)가 같은 식을 쓴다.
/// </summary>
internal static class CombatMath
{
    /// <summary>위력 × (100 + 스탯 × 스탯당 %) / 100.</summary>
    public static int Scaled(int power, int stat, CombatRules rules) =>
        Ratio.ApplyPercent(power, 100 + stat * rules.PowerScalingPercentPerPoint);

    /// <summary>주무기 계열 태그가 붙은 행동인가 (무기 위력 배율과 효과 지속 보너스를 받는다).</summary>
    public static bool WeaponBoosted(string? weapon, ActionDefinition action) =>
        weapon is not null && action.Tags.Contains(weapon);

    public static int Multiplier(string? weapon, int powerMultiplierPercent, ActionDefinition action) =>
        WeaponBoosted(weapon, action) ? powerMultiplierPercent : 100;

    /// <summary>
    /// 방어 경감 전 피해: 위력 × 스탯 보정 × (100 + 위력 보너스) × 무기 위력 배율.
    /// <paramref name="extraBonus"/>는 효과·처형·추가 피해처럼 스킬 밖의 위력 보너스(%).
    /// </summary>
    public static int RawDamage(ActionDefinition action, Stats stats, SkillSet skills, string? weapon, int multiplierPercent, int extraBonus, CombatRules rules)
    {
        var attackStat = action.DamageType == DamageType.Physical ? stats.Str : stats.Intel;
        var powerBonus = skills.Bonus(BonusKind.PowerPercent, action.BonusTags(weapon)) + extraBonus;
        var raw = Ratio.ApplyPercent(Scaled(action.Power, attackStat, rules), Math.Max(0, 100 + powerBonus));
        return Ratio.ApplyPercent(raw, Multiplier(weapon, multiplierPercent, action));
    }

    /// <summary>회복·보호막 양: 위력 × 지능 보정 × (회복 + 위력 보너스) × 무기 위력 배율.</summary>
    public static int Heal(ActionDefinition action, Stats stats, SkillSet skills, string? weapon, int multiplierPercent, CombatRules rules)
    {
        var tags = action.BonusTags(weapon);
        var bonus = skills.Bonus(BonusKind.HealPercent, tags) + skills.Bonus(BonusKind.PowerPercent, tags);
        var heal = Ratio.ApplyPercent(Scaled(action.Power, stats.Intel, rules), 100 + bonus);
        return Ratio.ApplyPercent(heal, Multiplier(weapon, multiplierPercent, action));
    }

    /// <summary>MP 회복량: 위력 × 지능 보정.</summary>
    public static int RestoreMp(ActionDefinition action, Stats stats, CombatRules rules) => Scaled(action.Power, stats.Intel, rules);

    /// <summary>MP 소모 감소를 반영한 실제 MP 비용.</summary>
    public static int MpCost(ActionDefinition action, SkillSet skills, string? weapon, CombatRules rules) =>
        Ratio.ApplyPercent(action.MpCost, 100 - Reduction(skills.Bonus(BonusKind.MpCostReductionPercent, action.BonusTags(weapon)), rules));

    /// <summary>
    /// 행동 뒤 대기. 행동의 대기 수치(없으면 TimeConstant)를 속도로 나누고(버림), 대기 감소 보너스는
    /// 태그 없는 것(전체 속도)과 그 행동의 태그 것을 더한다. <paramref name="used"/>가 null이면 행동하지 못한 턴이다.
    /// </summary>
    public static long Delay(ActionDefinition? used, Stats stats, SkillSet skills, string? weapon, int effectModifier, CombatRules rules)
    {
        var speed = Math.Max(1, stats.Speed);
        var baseDelay = (used?.Delay ?? rules.TimeConstant) / speed;
        var reduction = SignedReduction(skills.Bonus(BonusKind.DelayReductionPercent, used?.BonusTags(weapon)) + effectModifier, rules);
        var delay = (long)baseDelay * (100 - reduction) / 100;
        return Math.Max(1, delay);
    }

    /// <summary>감소 보너스를 0 ~ 상한 사이로 자른다.</summary>
    public static int Reduction(int percent, CombatRules rules) => Math.Clamp(percent, 0, rules.MaxReductionPercent);

    /// <summary>감소 보너스를 -100 ~ 상한 사이로 자른다 (음수는 둔화·취약).</summary>
    public static int SignedReduction(int percent, CombatRules rules) => Math.Clamp(percent, -100, rules.MaxReductionPercent);
}
