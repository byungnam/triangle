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

    public static void ValidateTactic(
        Tactic tactic, string at, IReadOnlyDictionary<string, SkillDefinition> skills, List<string> errors)
    {
        if (!skills.ContainsKey(tactic.SkillId))
        {
            errors.Add($"{at}: unknown skill '{tactic.SkillId}'");
        }

        switch (tactic.Condition)
        {
            case Condition.Always:
                break;
            case >= Condition.SelfHpAtLeast and <= Condition.AllyAverageMpAtMost:
                if (tactic.Value is < 0 or > 100)
                {
                    errors.Add($"{at}: {tactic.Condition} value must be a percent 0-100, got {tactic.Value}");
                }

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
}
