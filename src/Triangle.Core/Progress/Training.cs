using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Skills;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>훈련 큐 항목: 스킬을 이 레벨까지 올린다.</summary>
public sealed record TrainingQueueEntry(string SkillId, int Level);

/// <summary>훈련 관련 상수 (임시 수치).</summary>
public sealed record TrainingRules
{
    public static TrainingRules Default { get; } = new();

    public int VictorySp { get; init; } = 1_000;
    public int DrawSp { get; init; } = 500;
    public int DefeatSp { get; init; } = 300;

    /// <summary>스탯 배율 기준값. 1차·2차 스탯이 모두 이 값이면 배율 100%.</summary>
    public int ReferenceStat { get; init; } = 15;

    public int RewardFor(CombatOutcome outcome) => outcome switch
    {
        CombatOutcome.Victory => VictorySp,
        CombatOutcome.Defeat => DefeatSp,
        _ => DrawSp,
    };
}

public sealed record LevelUp(string MemberId, string SkillId, int Level);

/// <param name="Applied">스킬에 들어간 SP (배율 적용 후).</param>
/// <param name="LevelUps">이번에 오른 레벨.</param>
public sealed record TrainingResult(int Granted, int Applied, IReadOnlyList<LevelUp> LevelUps);

/// <summary>
/// 스킬 훈련 (EVE Online 방식을 전투 보상 SP로 옮긴 것).
/// 받은 SP는 큐 맨 앞 스킬부터 쌓이고, 레벨이 차면 남은 SP가 다음 항목으로 넘어간다.
/// 큐가 비면 미배정 SP로 남는다. 스킬마다 1차·2차 스탯에 따른 배율이 붙는다.
/// </summary>
public static class Training
{
    /// <summary>이 멤버가 이 스킬을 훈련할 때 SP에 붙는 배율(%): (1차×2 + 2차) / (기준×3).</summary>
    public static int MultiplierPercent(PartyMember member, SkillDefinition skill, TrainingRules? rules = null)
    {
        var reference = (rules ?? TrainingRules.Default).ReferenceStat;
        var weighted = StatValue(member.Stats, skill.Primary) * 2 + StatValue(member.Stats, skill.Secondary);
        return Math.Max(1, weighted * 100 / (reference * 3));
    }

    /// <summary>현재 레벨에 큐 앞쪽 count개 항목을 더한 계획상 레벨.</summary>
    public static Dictionary<string, int> PlannedLevels(PartyMember member, GameData data, int count)
    {
        var planned = new Dictionary<string, int>(member.SkillLevels(data));
        foreach (var entry in member.TrainingQueue.Take(count))
        {
            planned[entry.SkillId] = Math.Max(planned.GetValueOrDefault(entry.SkillId), entry.Level);
        }

        return planned;
    }

    /// <summary>큐 끝에 넣을 다음 레벨. 이미 5레벨(계획 포함)이면 null.</summary>
    public static int? NextQueueLevel(PartyMember member, string skillId, GameData data)
    {
        var next = PlannedLevels(member, data, member.TrainingQueue.Count).GetValueOrDefault(skillId) + 1;
        return next <= SkillDefinition.MaxLevel ? next : null;
    }

    /// <summary>큐 끝에 넣을 때 못 채우는 선행 조건 (현재 레벨 + 큐 전체 기준).</summary>
    public static IReadOnlyList<SkillRequirement> MissingPrerequisites(PartyMember member, string skillId, GameData data)
    {
        var planned = PlannedLevels(member, data, member.TrainingQueue.Count);
        return data.Skills[skillId].Prerequisites.Where(p => planned.GetValueOrDefault(p.SkillId) < p.Level).ToList();
    }

    /// <summary>
    /// 스킬의 다음 레벨을 큐 끝에 넣는다. 최대 레벨이거나 선행 조건이 모자라면 false.
    /// 넣은 뒤 미배정 SP가 있으면 바로 투입한다.
    /// </summary>
    public static bool Enqueue(PartyMember member, string skillId, GameData data, out TrainingResult? applied, TrainingRules? rules = null)
    {
        applied = null;
        if (NextQueueLevel(member, skillId, data) is not { } level || MissingPrerequisites(member, skillId, data).Count > 0)
        {
            return false;
        }

        member.MutableTrainingQueue.Add(new TrainingQueueEntry(skillId, level));
        if (member.UnallocatedSp > 0)
        {
            var sp = member.UnallocatedSp;
            member.UnallocatedSp = 0;
            applied = Grant(member, sp, data, rules);
        }

        return true;
    }

    /// <summary>
    /// index 항목을 지운다. 그 때문에 순서가 맞지 않게 된 뒤쪽 항목(같은 스킬의 다음 레벨,
    /// 이 스킬을 선행으로 하는 스킬)도 함께 지운다. 지운 항목 수를 돌려준다.
    /// </summary>
    public static int RemoveFromQueue(PartyMember member, int index, GameData data)
    {
        var queue = member.MutableTrainingQueue;
        var before = queue.Count;
        queue.RemoveAt(index);

        var kept = new List<TrainingQueueEntry>();
        var planned = new Dictionary<string, int>(member.SkillLevels(data));
        foreach (var entry in queue)
        {
            if (IsValidNext(entry, planned, data))
            {
                kept.Add(entry);
                planned[entry.SkillId] = entry.Level;
            }
        }

        queue.Clear();
        queue.AddRange(kept);
        return before - kept.Count;
    }

    /// <summary>index 항목을 offset만큼 옮긴다. 옮긴 순서가 규칙에 맞지 않으면 그대로 두고 false.</summary>
    public static bool MoveInQueue(PartyMember member, int index, int offset, GameData data)
    {
        var queue = member.MutableTrainingQueue;
        var target = index + offset;
        if (index < 0 || index >= queue.Count || target < 0 || target >= queue.Count)
        {
            return false;
        }

        var moved = queue.ToList();
        (moved[index], moved[target]) = (moved[target], moved[index]);
        if (!IsValidQueue(moved, member.SkillLevels(data), data))
        {
            return false;
        }

        queue.Clear();
        queue.AddRange(moved);
        return true;
    }

    /// <summary>
    /// SP를 큐 맨 앞부터 투입한다. 각 스킬의 배율을 적용하고, 레벨이 차면 남은 SP를 다음 항목으로 넘긴다.
    /// 큐가 비면 남은 SP는 미배정 SP가 된다.
    /// </summary>
    public static TrainingResult Grant(PartyMember member, int sp, GameData data, TrainingRules? rules = null)
    {
        var levelUps = new List<LevelUp>();
        var remaining = sp;
        var applied = 0;
        var queue = member.MutableTrainingQueue;

        while (remaining > 0 && queue.Count > 0)
        {
            var entry = queue[0];
            var skill = data.Skills[entry.SkillId];
            var current = member.SkillPoints.GetValueOrDefault(entry.SkillId);
            var needed = SkillProgression.SpForLevel(skill.Rank, entry.Level) - current;
            if (needed <= 0)
            {
                queue.RemoveAt(0);
                continue;
            }

            var multiplier = MultiplierPercent(member, skill, rules);
            var effective = (int)((long)remaining * multiplier / 100);
            if (effective >= needed)
            {
                // 이 항목을 채우는 데 쓴 원래 SP만큼 빼고 나머지는 다음 항목으로.
                var used = (int)(((long)needed * 100 + multiplier - 1) / multiplier);
                member.AddSkillPoints(entry.SkillId, needed);
                applied += needed;
                remaining -= Math.Min(remaining, used);
                queue.RemoveAt(0);
                levelUps.Add(new LevelUp(member.Id, entry.SkillId, entry.Level));
            }
            else
            {
                member.AddSkillPoints(entry.SkillId, effective);
                applied += effective;
                remaining = 0;
            }
        }

        member.UnallocatedSp += remaining;
        return new TrainingResult(sp, applied, levelUps);
    }

    /// <summary>큐 전체가 규칙에 맞는지: 레벨은 하나씩 차례로, 선행은 앞에서 채워져 있어야 한다.</summary>
    public static bool IsValidQueue(IEnumerable<TrainingQueueEntry> queue, IReadOnlyDictionary<string, int> levels, GameData data)
    {
        var planned = new Dictionary<string, int>(levels);
        foreach (var entry in queue)
        {
            if (!IsValidNext(entry, planned, data))
            {
                return false;
            }

            planned[entry.SkillId] = entry.Level;
        }

        return true;
    }

    private static bool IsValidNext(TrainingQueueEntry entry, IReadOnlyDictionary<string, int> planned, GameData data) =>
        data.Skills.TryGetValue(entry.SkillId, out var skill)
        && entry.Level == planned.GetValueOrDefault(entry.SkillId) + 1
        && entry.Level <= SkillDefinition.MaxLevel
        && skill.Prerequisites.All(p => planned.GetValueOrDefault(p.SkillId) >= p.Level);

    private static int StatValue(Stats stats, Stat stat) => stat switch
    {
        Stat.Str => stats.Str,
        Stat.Dex => stats.Dex,
        Stat.Vital => stats.Vital,
        Stat.Intel => stats.Intel,
        _ => stats.Speed,
    };
}
