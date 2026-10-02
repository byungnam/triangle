using Triangle.Core.Skills;

namespace Triangle.Desktop.Rendering;

/// <summary>스킬과 보너스의 화면 표시.</summary>
internal static class SkillText
{
    public static string StatLabel(Stat stat) => stat switch
    {
        Stat.Str => "근력",
        Stat.Dex => "민첩",
        Stat.Vital => "체력",
        Stat.Intel => "지능",
        _ => "신속",
    };

    public static string TagLabel(string? tag) => tag switch
    {
        null => "",
        "melee" => "근접 ",
        "bow" => "활 ",
        "magic" => "마법 ",
        "holy" => "신성 ",
        _ => tag + " ",
    };

    /// <summary>레벨당 보너스, 예: "활 위력 +5%".</summary>
    public static string BonusLabel(SkillBonus bonus)
    {
        var tag = TagLabel(bonus.Tag);
        var p = bonus.PercentPerLevel;
        return bonus.Kind switch
        {
            BonusKind.PowerPercent => $"{tag}위력 +{p}%",
            BonusKind.HealPercent => $"회복량 +{p}%",
            BonusKind.MpCostReductionPercent => $"{tag}MP 소모 -{p}%",
            BonusKind.DelayReductionPercent => tag.Length == 0 ? $"행동 대기 -{p}%" : $"{tag}행동 뒤 대기 -{p}%",
            BonusKind.MaxHpPercent => $"최대 HP +{p}%",
            BonusKind.MaxMpPercent => $"최대 MP +{p}%",
            BonusKind.DamageTakenReductionPercent => $"받는 피해 -{p}%",
            _ => bonus.Kind.ToString(),
        };
    }

    /// <summary>레벨 표시, 예: "■■■□□".</summary>
    public static string LevelPips(int level) =>
        new string('■', level) + new string('□', SkillDefinition.MaxLevel - level);

    public static string Sp(int sp) => sp.ToString("N0");
}
