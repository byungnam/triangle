using System.Text.Json;
using Triangle.Core.Data;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>세이브 파일의 JSON 형태. <see cref="Version"/>이 바뀌면 이전 형식을 변환하는 코드를 둔다.</summary>
public sealed record SaveFile
{
    public required int Version { get; init; }
    public required IReadOnlyList<SavedMember> Party { get; init; }
}

public sealed record SavedMember
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ClassId { get; init; }
    public required Stats Stats { get; init; }
    public required Row Row { get; init; }
    public IReadOnlyList<Tactic> Tactics { get; init; } = [];
}

/// <summary>세이브를 읽거나 검증하다 실패했다. 발견한 오류를 모두 담는다.</summary>
public sealed class SaveGameException(IReadOnlyList<string> errors)
    : Exception("Invalid save:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>파티와 세이브 JSON 사이의 변환. 읽을 때 게임 데이터와 맞는지 검증한다.</summary>
public static class SaveGame
{
    public const int CurrentVersion = 1;

    public static string Serialize(Party party)
    {
        var file = new SaveFile
        {
            Version = CurrentVersion,
            Party = party.Members.Select(m => new SavedMember
            {
                Id = m.Id,
                Name = m.Name,
                ClassId = m.ClassId,
                Stats = m.Stats,
                Row = m.Row,
                Tactics = m.Tactics.ToList(),
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

        return new Party(file.Party.Select(m => new PartyMember(m.Id, m.Name, m.ClassId, m.Stats, m.Row, m.Tactics)));
    }

    private static void Validate(SaveFile file, GameData data)
    {
        if (file.Version != CurrentVersion)
        {
            throw new SaveGameException([$"save: unsupported version {file.Version} (expected {CurrentVersion})"]);
        }

        var errors = new List<string>();
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
            if (!data.Classes.ContainsKey(member.ClassId))
            {
                errors.Add($"{at}: unknown class '{member.ClassId}'");
            }

            DataValidation.ValidateStats(member.Stats, at, errors);
            foreach (var tactic in member.Tactics)
            {
                DataValidation.ValidateTactic(tactic, $"{at} tactic {tactic.Priority}", data.Skills, errors);
            }
        }

        if (errors.Count > 0)
        {
            throw new SaveGameException(errors);
        }
    }
}
