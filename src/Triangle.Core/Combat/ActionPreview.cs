using Triangle.Core.Actions;
using Triangle.Core.Skills;

namespace Triangle.Core.Combat;

/// <summary>
/// 전투 밖에서 미리 본 행동 수치: 이 유닛의 스탯, 스킬, 장비로 계산한 것 중 대상과 상관없는 부분.
/// 전투 엔진과 같은 식(<see cref="CombatMath"/>)을 쓴다.
/// </summary>
/// <param name="Amount">
/// 피해(대상 방어로 경감하기 전), 회복량, 보호막량, MP 회복량. 그 밖의 행동은 0.
/// 효과 보정, 처형, 표식 추가 피해는 대상에 따라 달라서 넣지 않는다.
/// </param>
/// <param name="MpCost">MP 소모 감소를 반영한 실제 MP 비용.</param>
/// <param name="Delay">이 행동 뒤 대기 (시간 단위, 효과 보정 제외).</param>
/// <param name="WeaponBoosted">주무기 계열 행동이라 무기 위력 배율과 효과 지속 보너스를 받는다.</param>
/// <param name="EffectDurationBonus">거는 효과의 지속 턴 보너스 (무기 계열 행동일 때만).</param>
public sealed record ActionPreview(int Amount, int MpCost, long Delay, bool WeaponBoosted, int EffectDurationBonus)
{
    public static ActionPreview For(CombatantSetup unit, ActionDefinition action, CombatCatalog catalog, CombatRules? rules = null)
    {
        rules ??= CombatRules.Default;
        var skills = new SkillSet(unit.Skills, catalog.Skills, unit.ItemBonuses);
        var amount = action.Effect switch
        {
            ActionEffect.Damage => CombatMath.RawDamage(action, unit.Stats, skills, unit.Weapon, unit.PowerMultiplierPercent, 0, rules),
            ActionEffect.Heal or ActionEffect.Shield => CombatMath.Heal(action, unit.Stats, skills, unit.Weapon, unit.PowerMultiplierPercent, rules),
            ActionEffect.RestoreMp => CombatMath.RestoreMp(action, unit.Stats, rules),
            _ => 0,
        };
        var boosted = CombatMath.WeaponBoosted(unit.Weapon, action);
        return new ActionPreview(
            amount,
            CombatMath.MpCost(action, skills, unit.Weapon, rules),
            CombatMath.Delay(action, unit.Stats, skills, unit.Weapon, 0, rules),
            boosted,
            boosted ? unit.EffectDurationBonus : 0);
    }
}

/// <summary>
/// 전투 밖에서 본 유닛의 전투 능력치 (스탯, 스킬, 장비 보너스 반영, 효과 제외).
/// </summary>
/// <param name="BaseDelay">대기 수치가 기본(1000)인 행동 뒤 대기. 작을수록 자주 행동한다.</param>
/// <param name="PhysicalPowerPercent">물리 행동 위력 배율 (근력 보정, %).</param>
/// <param name="MagicPowerPercent">마법·회복 위력 배율 (지능 보정, %).</param>
/// <param name="Defense">물리 방어 (근력, 방어 보너스 반영).</param>
/// <param name="MagicDefense">마법 방어 (지능, 방어 보너스 반영).</param>
/// <param name="PhysicalReductionPercent">물리 방어로 줄이는 피해 (%).</param>
/// <param name="MagicReductionPercent">마법 방어로 줄이는 피해 (%).</param>
/// <param name="DamageTakenReductionPercent">방어 뒤에 더 줄이는 받는 피해 (%).</param>
public sealed record UnitPreview(
    int MaxHp,
    int MaxMp,
    long BaseDelay,
    int PhysicalPowerPercent,
    int MagicPowerPercent,
    int Defense,
    int MagicDefense,
    int PhysicalReductionPercent,
    int MagicReductionPercent,
    int DamageTakenReductionPercent)
{
    public static UnitPreview For(CombatantSetup unit, CombatCatalog catalog, CombatRules? rules = null)
    {
        rules ??= CombatRules.Default;
        var skills = new SkillSet(unit.Skills, catalog.Skills, unit.ItemBonuses);
        var defenseBonus = Math.Max(0, 100 + skills.Bonus(BonusKind.DefensePercent));
        var defense = Ratio.ApplyPercent(unit.Stats.Str, defenseBonus);
        var magicDefense = Ratio.ApplyPercent(unit.Stats.Intel, defenseBonus);
        int Reduction(int value) => 100 - Ratio.DivideRounded(100L * 100, 100 + (long)value * rules.DefenseReductionPercentPerPoint);

        return new UnitPreview(
            rules.MaxHp(unit.Stats, skills),
            rules.MaxMp(unit.Stats, skills),
            CombatMath.Delay(null, unit.Stats, skills, unit.Weapon, 0, rules),
            100 + unit.Stats.Str * rules.PowerScalingPercentPerPoint,
            100 + unit.Stats.Intel * rules.PowerScalingPercentPerPoint,
            defense,
            magicDefense,
            Reduction(defense),
            Reduction(magicDefense),
            CombatMath.SignedReduction(skills.Bonus(BonusKind.DamageTakenReductionPercent), rules));
    }
}
