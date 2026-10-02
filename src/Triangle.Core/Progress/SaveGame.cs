using System.Text.Json;
using System.Text.Json.Serialization;
using Triangle.Core.Data;
using Triangle.Core.Masteries;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>세이브 파일의 JSON 형태. <see cref="Version"/>이 바뀌면 이전 형식을 변환하는 코드를 둔다.</summary>
public sealed record SaveFile
{
    public required int Version { get; init; }

    /// <summary>전투에 쓸 전술 세트 (0부터). 버전 4부터.</summary>
    public int ActiveTacticSet { get; init; }

    public required IReadOnlyList<SavedMember> Party { get; init; }
}

public sealed record SavedMember
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Stats Stats { get; init; }
    public required Row Row { get; init; }
    public string? Weapon { get; init; }
    public string? Armor { get; init; }

    /// <summary>숙련 ID별 누적 경험치.</summary>
    public IReadOnlyDictionary<string, int> MasteryXp { get; init; } = new Dictionary<string, int>();

    /// <summary>배운 스킬의 레벨.</summary>
    public IReadOnlyDictionary<string, int> SkillLevels { get; init; } = new Dictionary<string, int>();

    /// <summary>전술 세트들 (버전 4부터).</summary>
    public IReadOnlyList<IReadOnlyList<Tactic>> TacticSets { get; init; } = [];

    /// <summary>버전 3의 전술 목록. 읽을 때만 쓰고 세트 1로 옮긴다.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Tactic>? Tactics { get; init; }
}

/// <summary>세이브를 읽거나 검증하다 실패했다. 발견한 오류를 모두 담는다.</summary>
public sealed class SaveGameException(IReadOnlyList<string> errors)
    : Exception("Invalid save:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>
/// 파티와 세이브 JSON 사이의 변환. 읽을 때 게임 데이터와 맞는지 검증한다.
/// 요구 스킬을 못 채운 행동이 든 전술은 오류로 보지 않는다(게임 데이터가 바뀌었을 수 있다).
/// 편집 화면이 그 전술을 표시하고 고칠 때까지 전투를 막는다.
/// </summary>
public static class SaveGame
{
    /// <summary>
    /// 3: 장비와 숙련 (Albion식), SP·훈련 큐 제거 (2026-10-02).
    /// 4: 전술 세트 두 벌과 사용할 세트 (2026-10-02). 버전 3은 전술을 세트 1로 옮겨 읽는다. 그 이전은 읽지 않는다.
    /// </summary>
    public const int CurrentVersion = 4;

    private const int OldestReadableVersion = 3;

    public static string Serialize(Party party)
    {
        var file = new SaveFile
        {
            Version = CurrentVersion,
            ActiveTacticSet = party.ActiveTacticSet,
            Party = party.Members.Select(m => new SavedMember
            {
                Id = m.Id,
                Name = m.Name,
                Stats = m.Stats,
                Row = m.Row,
                Weapon = m.Weapon,
                Armor = m.Armor,
                MasteryXp = new SortedDictionary<string, int>(m.MasteryXp.ToDictionary()),
                SkillLevels = new SortedDictionary<string, int>(m.SkillLevels.ToDictionary()),
                TacticSets = m.TacticSets.Select(set => (IReadOnlyList<Tactic>)set.ToList()).ToList(),
            }).ToList(),
        };
        return JsonSerializer.Serialize(file, GameDataJson.Options);
    }

    public static Party Deserialize(string json, GameData data)
    {
        SaveFile? file;
        try
        {
            file = JsonSerializer.Deserialize<SaveFile>(json, GameDataJson.Options);
        }
        catch (JsonException e)
        {
            var line = e.LineNumber is { } n ? $" line {n + 1}" : "";
            throw new SaveGameException([$"save{line}: {e.Message}"]);
        }

        if (file is null)
        {
            throw new SaveGameException(["save: expected an object, found null"]);
        }

        Validate(file, data);

        return new Party(file.Party.Select(m =>
            new PartyMember(m.Id, m.Name, m.Stats, m.Row, m.Weapon, m.Armor, m.MasteryXp, m.SkillLevels, TacticSetsOf(file, m))),
            file.ActiveTacticSet);
    }

    /// <summary>버전 3은 전술 목록 하나를 세트 1로, 버전 4는 세트들을 그대로.</summary>
    private static IReadOnlyList<IEnumerable<Tactic>> TacticSetsOf(SaveFile file, SavedMember member) =>
        file.Version == 3 ? [member.Tactics ?? []] : member.TacticSets;

    private static void Validate(SaveFile file, GameData data)
    {
        if (file.Version is < OldestReadableVersion or > CurrentVersion)
        {
            throw new SaveGameException([$"save: unsupported version {file.Version} (expected {OldestReadableVersion}-{CurrentVersion})"]);
        }

        var errors = new List<string>();
        if (file.ActiveTacticSet is < 0 or >= PartyMember.TacticSetCount)
        {
            errors.Add($"save: activeTacticSet must be 0-{PartyMember.TacticSetCount - 1}, got {file.ActiveTacticSet}");
        }

        if (file.Party.Count == 0)
        {
            errors.Add("save: party is empty");
        }

        foreach (var duplicate in file.Party.GroupBy(m => m.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"save: duplicate member id '{duplicate.Key}'");
        }

        foreach (var member in file.Party)
        {
            var at = $"save member '{member.Id}'";
            DataValidation.RequireText(member.Id, "save: member id", errors);
            DataValidation.RequireText(member.Name, $"{at}: name", errors);
            DataValidation.ValidateStats(member.Stats, at, errors);
            DataValidation.ValidateEquipment(member.Weapon, member.Armor, at, data.Masteries, errors);

            foreach (var (masteryId, xp) in member.MasteryXp)
            {
                if (!data.Masteries.ContainsKey(masteryId))
                {
                    errors.Add($"{at}: unknown mastery '{masteryId}'");
                }

                DataValidation.RequireNonNegative(xp, $"{at}: xp of '{masteryId}'", errors);
            }

            DataValidation.ValidateSkillLevels(member.SkillLevels, at, data.Skills, errors);

            // 쓴 포인트가 숙련 레벨이 준 포인트를 넘으면 안 된다.
            foreach (var group in member.SkillLevels
                         .Where(p => data.Skills.ContainsKey(p.Key))
                         .GroupBy(p => data.Skills[p.Key].Mastery))
            {
                var spent = group.Sum(p => data.Skills[p.Key].Rank * p.Value);
                var earned = MasteryProgression.LevelFor(member.MasteryXp.GetValueOrDefault(group.Key));
                if (spent > earned)
                {
                    errors.Add($"{at}: spent {spent} points in '{group.Key}' but mastery level is {earned}");
                }
            }

            if (file.Version >= 4 && member.Tactics is not null)
            {
                errors.Add($"{at}: 'tactics' is a version 3 field; use 'tacticSets'");
            }

            var sets = TacticSetsOf(file, member);
            if (sets.Count > PartyMember.TacticSetCount)
            {
                errors.Add($"{at}: at most {PartyMember.TacticSetCount} tactic sets, got {sets.Count}");
            }

            for (var set = 0; set < sets.Count; set++)
            {
                foreach (var tactic in sets[set])
                {
                    DataValidation.ValidateTactic(tactic, $"{at} set {set + 1} tactic {tactic.Priority}", data.Actions, errors);
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new SaveGameException(errors);
        }
    }
}
