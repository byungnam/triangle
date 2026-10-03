using Triangle.Core.Data;
using Triangle.Core.Skills;

namespace Triangle.Desktop.Rendering;

/// <summary>스킬과 보너스의 화면 표시.</summary>
internal static class SkillText
{
    /// <summary>보너스 태그(숙련 ID 등)를 이름으로, 예: "bow" → "활 ".</summary>
    private static string TagLabel(string? tag, GameData data) =>
        tag is null ? "" : (data.Masteries.TryGetValue(tag, out var m) ? m.Name : tag) + " ";

    /// <summary>레벨당 보너스, 예: "활 위력 +5%". 감소는 일반 하이픈을 쓴다(글꼴에 − 기호가 없다).</summary>
    public static string BonusLabel(SkillBonus bonus, GameData data) => BonusLabel(bonus.Kind, bonus.PercentPerLevel, bonus.Tag, data);

    /// <summary>보너스 하나, 예: "방어 +10%".</summary>
    public static string BonusLabel(BonusKind kind, int p, string? tagId, GameData data)
    {
        var tag = TagLabel(tagId, data);
        return kind switch
        {
            BonusKind.PowerPercent => $"{tag}위력 +{p}%",
            BonusKind.HealPercent => $"회복량 +{p}%",
            BonusKind.MpCostReductionPercent => $"{tag}MP 소모 -{p}%",
            BonusKind.DelayReductionPercent => tag.Length == 0 ? $"행동 대기 -{p}%" : $"{tag}행동 뒤 대기 -{p}%",
            BonusKind.MaxHpPercent => $"최대 HP +{p}%",
            BonusKind.MaxMpPercent => $"최대 MP +{p}%",
            BonusKind.DamageTakenReductionPercent => $"받는 피해 -{p}%",
            BonusKind.DefensePercent => $"방어 +{p}%",
            _ => kind.ToString(),
        };
    }

    /// <summary>레벨 표시, 예: "■■■□□".</summary>
    public static string LevelPips(int level) =>
        new string('■', level) + new string('□', SkillDefinition.MaxLevel - level);

    public static string Number(int value) => value.ToString("N0");
}
