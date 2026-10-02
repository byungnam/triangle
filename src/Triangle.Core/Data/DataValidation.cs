using Triangle.Core.Actions;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Data;

/// <summary>게임 데이터와 세이브가 함께 쓰는 검증 규칙. 오류는 errors에 쌓는다.</summary>
internal static class DataValidation
{
    public static void ValidateStats(Stats stats, string at, List<string> errors)
    {
        RequireNonNegative(stats.Str, $"{at}: str", errors);
        RequireNonNegative(stats.Dex, $"{at}: dex", errors);
        RequireNonNegative(stats.Vital, $"{at}: vital", errors);
        RequireNonNegative(stats.Intel, $"{at}: intel", errors);
        RequireNonNegative(stats.Speed, $"{at}: speed", errors);
    }

    /// <summary>적 장비: 무기 칸에는 무기 계열, 방어구 칸에는 방어구 계열이어야 한다. null은 맨손·맨몸.</summary>
    public static void ValidateEquipment(
        string? weapon, string? armor, string at, IReadOnlyDictionary<string, MasteryDefinition> masteries, List<string> errors)
    {
        RequireKind(weapon, MasteryKind.Weapon, $"{at}: weapon", masteries, errors);
        RequireKind(armor, MasteryKind.Armor, $"{at}: armor", masteries, errors);
    }

    public static void RequireKind(
        string? id, MasteryKind kind, string what, IReadOnlyDictionary<string, MasteryDefinition> masteries, List<string> errors)
    {
        if (id is null)
        {
            return;
        }

        if (!masteries.TryGetValue(id, out var mastery))
        {
            errors.Add($"{what}: unknown mastery '{id}'");
        }
        else if (mastery.Kind != kind)
        {
            errors.Add($"{what}: '{id}' is {mastery.Kind}, not {kind}");
        }
    }

    /// <summary>
    /// 부위별 장착 아이템: 있는 아이템이고, 아이템의 부위가 그 칸과 같은지. 착용 조건과 두손·보조 충돌은
    /// 오류로 보지 않는다(게임 데이터가 바뀌었을 수 있다, "착용 불가"로 처리한다).
    /// </summary>
    public static void ValidateEquipped(
        IReadOnlyDictionary<EquipmentSlot, string> equipment,
        string at,
        IReadOnlyDictionary<string, ItemDefinition> items,
        List<string> errors)
    {
        foreach (var (slot, itemId) in equipment)
        {
            if (!slot.IsEquipment())
            {
                errors.Add($"{at}: {slot} is not an equipment slot");
            }
            else if (!items.TryGetValue(itemId, out var item))
            {
                errors.Add($"{at}: {slot}: unknown item '{itemId}'");
            }
            else if (item.Slot != slot)
            {
                errors.Add($"{at}: {slot}: '{itemId}' is {item.Slot}");
            }
        }
    }

    public static void RequirePercent(int value, string what, List<string> errors)
    {
        if (value is < 0 or > 100)
        {
            errors.Add($"{what} must be 0-100, got {value}");
        }
    }

    /// <summary>요구/선행 조건: 있는 스킬을 가리키고 레벨이 1–5인지.</summary>
    public static void ValidateRequirements(
        IEnumerable<SkillRequirement> requirements, string at, IReadOnlyDictionary<string, SkillDefinition> skills, List<string> errors)
    {
        foreach (var r in requirements)
        {
            if (!skills.ContainsKey(r.SkillId))
            {
                errors.Add($"{at}: requires unknown skill '{r.SkillId}'");
            }

            RequireLevel(r.Level, $"{at}: required level of '{r.SkillId}'", errors);
        }
    }

    /// <summary>유닛의 스킬 레벨: 있는 스킬, 레벨 1–5, 선행 스킬 레벨을 채웠는지.</summary>
    public static void ValidateSkillLevels(
        IReadOnlyDictionary<string, int> levels, string at, IReadOnlyDictionary<string, SkillDefinition> skills, List<string> errors)
    {
        var set = new SkillSet(levels, skills);
        foreach (var (skillId, level) in levels)
        {
            if (!skills.TryGetValue(skillId, out var skill))
            {
                errors.Add($"{at}: unknown skill '{skillId}'");
                continue;
            }

            RequireLevel(level, $"{at}: level of '{skillId}'", errors);
            foreach (var missing in set.Missing(skill.Prerequisites))
            {
                errors.Add($"{at}: '{skillId}' needs '{missing.SkillId}' level {missing.Level}");
            }
        }
    }

    /// <summary>전술: 있는 행동인지, 조건 값이 범위 안인지. 요구 조건 충족 여부는 따로 본다.</summary>
    public static void ValidateTactic(
        Tactic tactic, string at, IReadOnlyDictionary<string, ActionDefinition> actions, List<string> errors)
    {
        if (!actions.ContainsKey(tactic.ActionId))
        {
            errors.Add($"{at}: unknown action '{tactic.ActionId}'");
        }

        switch (tactic.Condition)
        {
            case Condition.Always:
                break;
            case var c when c.IsPercent():
                if (tactic.Value is < 0 or > 100)
                {
                    errors.Add($"{at}: {tactic.Condition} value must be a percent 0-100, got {tactic.Value}");
                }

                break;
            case var c when c.IsAmount():
                // 실제 HP·MP 수치: 범위 제한이 없다.
                break;
            case Condition.EveryNthTurn:
                if (tactic.Value <= 0)
                {
                    errors.Add($"{at}: {tactic.Condition} value must be positive, got {tactic.Value}");
                }

                break;
            default:
                RequireNonNegative(tactic.Value, $"{at}: {tactic.Condition} value", errors);
                break;
        }
    }

    public static void RequireText(string value, string what, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{what} must not be empty");
        }
    }

    public static void RequireNonNegative(int value, string what, List<string> errors)
    {
        if (value < 0)
        {
            errors.Add($"{what} must not be negative, got {value}");
        }
    }

    public static void RequireLevel(int level, string what, List<string> errors)
    {
        if (level is < 1 or > SkillDefinition.MaxLevel)
        {
            errors.Add($"{what} must be 1-{SkillDefinition.MaxLevel}, got {level}");
        }
    }
}
