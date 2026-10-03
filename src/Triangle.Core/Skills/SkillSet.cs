using Triangle.Core.Items;

namespace Triangle.Core.Skills;

/// <summary>
/// 한 유닛의 스킬 레벨. 행동 요구 조건과 보너스 합계를 계산한다.
/// 보너스 합계에는 장비 보너스(<paramref name="items"/>)도 더한다. 요구 조건은 스킬 레벨만 본다.
/// </summary>
public sealed class SkillSet(
    IReadOnlyDictionary<string, int> levels,
    IReadOnlyDictionary<string, SkillDefinition> definitions,
    IReadOnlyList<ItemBonus>? items = null)
{
    public static readonly IReadOnlyDictionary<string, int> NoSkills = new Dictionary<string, int>();

    public IReadOnlyDictionary<string, int> Levels { get; } = levels;

    /// <summary>보너스 합계에 더하는 장비 보너스.</summary>
    public IReadOnlyList<ItemBonus> Items { get; } = items ?? [];

    public int Level(string skillId) => Levels.GetValueOrDefault(skillId);

    public bool Meets(IEnumerable<SkillRequirement> requirements) => requirements.All(r => Level(r.SkillId) >= r.Level);

    public IReadOnlyList<SkillRequirement> Missing(IEnumerable<SkillRequirement> requirements) =>
        requirements.Where(r => Level(r.SkillId) < r.Level).ToList();

    /// <summary>
    /// 해당 종류 보너스의 합계(%) = 스킬 + 장비. 태그가 없는 보너스는 항상, 태그가 있는 보너스는
    /// <paramref name="tags"/>에 그 태그가 있을 때만 더한다.
    /// </summary>
    public int Bonus(BonusKind kind, IReadOnlyCollection<string>? tags = null)
    {
        bool Applies(BonusKind k, string? tag) => k == kind && (tag is null || (tags?.Contains(tag) ?? false));

        var total = 0;
        foreach (var (skillId, level) in Levels)
        {
            if (level <= 0 || !definitions.TryGetValue(skillId, out var skill))
            {
                continue;
            }

            foreach (var bonus in skill.Bonuses)
            {
                if (Applies(bonus.Kind, bonus.Tag))
                {
                    total += bonus.PercentPerLevel * level;
                }
            }
        }

        foreach (var bonus in Items)
        {
            if (Applies(bonus.Kind, bonus.Tag))
            {
                total += bonus.Percent;
            }
        }

        return total;
    }
}
