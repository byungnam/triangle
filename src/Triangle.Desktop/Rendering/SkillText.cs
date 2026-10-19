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

    /// <summary>보너스 하나, 예: "방어 +10%". 음수는 거꾸로 적는다 (예: "방어 -15%", "받는 피해 +25%").</summary>
    public static string BonusLabel(BonusKind kind, int p, string? tagId, GameData data)
    {
        var tag = TagLabel(tagId, data);
        string Up(int v) => v < 0 ? $"-{-v}%" : $"+{v}%";
        string Down(int v) => v < 0 ? $"+{-v}%" : $"-{v}%";
        return kind switch
        {
            BonusKind.PowerPercent => $"{tag}위력 {Up(p)}",
            BonusKind.HealPercent => $"회복량 {Up(p)}",
            BonusKind.MpCostReductionPercent => $"{tag}MP 소모 {Down(p)}",
            BonusKind.DelayReductionPercent => tag.Length == 0 ? $"행동 대기 {Down(p)}" : $"{tag}행동 뒤 대기 {Down(p)}",
            BonusKind.MaxHpPercent => $"최대 HP {Up(p)}",
            BonusKind.MaxMpPercent => $"최대 MP {Up(p)}",
            BonusKind.DamageTakenReductionPercent => $"받는 피해 {Down(p)}",
            BonusKind.DefensePercent => $"방어 {Up(p)}",
            BonusKind.LifestealPercent => $"흡혈 {p}% (준 피해만큼 회복)",
            BonusKind.ThornsPercent => $"가시 {p}% (받은 피해를 때린 적에게)",
            _ => kind.ToString(),
        };
    }

    /// <summary>레벨 표시, 예: "■■■□□".</summary>
    public static string LevelPips(int level) =>
        new string('■', level) + new string('□', SkillDefinition.MaxLevel - level);
}
