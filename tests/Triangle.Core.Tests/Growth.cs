using Triangle.Core.Data;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Skills;

namespace Triangle.Core.Tests;

/// <summary>
/// 난이도 측정용 성장 상태: 낀 장비의 계열 숙련을 한 레벨까지 올리고, 착용 조건 패시브를 배운 뒤 남은 포인트도
/// 그 트리에 쓰고, 같은 계열·부위·종류의 한 티어 장비로 모두 바꿔 낀다(그 티어가 없는 장비는 그대로 둔다).
/// </summary>
internal static class Growth
{
    public static void Grow(Company company, GameData data, int tier, int masteryLevel)
    {
        foreach (var member in company.Roster)
        {
            var targets = member.WornItems(data)
                .Select(w => data.Items.Values.FirstOrDefault(i =>
                    i.Slot == w.Item.Slot && i.Mastery == w.Item.Mastery && i.Type == w.Item.Type && i.Tier == tier) ?? w.Item)
                .ToList();

            foreach (var mastery in targets.Select(i => i.Mastery).Distinct())
            {
                var xp = MasteryProgression.XpForLevel(masteryLevel) - member.MasteryXp.GetValueOrDefault(mastery);
                member.AddMasteryXp(mastery, xp);
            }

            foreach (var requirement in targets.SelectMany(i => i.Requirements))
            {
                LearnUpTo(member, data, requirement);
            }

            foreach (var mastery in targets.Select(i => i.Mastery).Distinct())
            {
                var tree = data.Skills.Values.Where(s => s.Mastery == mastery).OrderBy(s => s.Rank).ToList();
                while (tree.FirstOrDefault(s => member.WhyCannotLearn(s.Id, data) is null) is { } skill)
                {
                    member.Learn(skill.Id, data);
                }
            }

            foreach (var item in targets.Where(i => member.ItemIn(i.Slot) != i.Id))
            {
                company.AddToStash(item.Id);
                if (!company.Equip(member.Id, item.Id, data))
                {
                    throw new InvalidOperationException($"{member.Name} cannot wear {item.Id}: {company.WhyCannotEquip(member.Id, item.Id, data)}");
                }
            }
        }
    }

    /// <summary>모든 지역을 클리어한 것으로 한다 (깊은 지역을 바로 재려고).</summary>
    public static void UnlockAll(Company company, GameData data)
    {
        foreach (var zone in data.Zones.Values)
        {
            company.RecordClear(zone.Id, new ClearResult(1, 0, 0));
        }
    }

    private static void LearnUpTo(PartyMember member, GameData data, SkillRequirement requirement)
    {
        foreach (var prerequisite in data.Skills[requirement.SkillId].Prerequisites)
        {
            LearnUpTo(member, data, prerequisite);
        }

        while (member.SkillLevels.GetValueOrDefault(requirement.SkillId) < requirement.Level && member.Learn(requirement.SkillId, data))
        {
        }
    }
}
