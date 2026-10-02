using System.Text.Json;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Data;

/// <summary>
/// data/ 폴더의 JSON 파일을 읽고 검증한다. 오류는 멈추지 않고 모두 모아서
/// <see cref="GameDataException"/> 하나로 던진다.
/// </summary>
public static class GameDataLoader
{
    public const string ClassesFile = "classes.json";
    public const string SkillsFile = "skills.json";
    public const string EncountersFile = "encounters.json";

    public static GameData LoadDirectory(string directory)
    {
        var errors = new List<string>();

        string? Read(string fileName)
        {
            var path = Path.Combine(directory, fileName);
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{fileName}: cannot read '{path}' ({e.Message})");
                return null;
            }
        }

        var classes = Read(ClassesFile);
        var skills = Read(SkillsFile);
        var encounters = Read(EncountersFile);
        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        return Parse(classes!, skills!, encounters!);
    }

    public static GameData Parse(string classesJson, string skillsJson, string encountersJson)
    {
        var errors = new List<string>();

        var classes = Deserialize<ClassDefinition>(classesJson, ClassesFile, errors);
        var skills = Deserialize<SkillDefinition>(skillsJson, SkillsFile, errors);
        var encounters = Deserialize<EncounterDefinition>(encountersJson, EncountersFile, errors);

        // 형식 오류가 있으면 참조 검증은 의미가 없다.
        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        var classMap = ToMap(classes!, c => c.Id, ClassesFile, errors);
        var skillMap = ToMap(skills!, s => s.Id, SkillsFile, errors);
        var encounterMap = ToMap(encounters!, e => e.Id, EncountersFile, errors);

        foreach (var c in classes!)
        {
            ValidateClass(c, errors);
        }

        foreach (var s in skills!)
        {
            ValidateSkill(s, errors);
        }

        foreach (var e in encounters!)
        {
            ValidateEncounter(e, classMap, skillMap, errors);
        }

        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        return new GameData(classMap, skillMap, encounterMap);
    }

    private static List<T>? Deserialize<T>(string json, string fileName, List<string> errors)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<T>>(json, GameDataJson.Options);
            if (items is null)
            {
                errors.Add($"{fileName}: expected an array, found null");
            }

            return items;
        }
        catch (JsonException e)
        {
            var line = e.LineNumber is { } n ? $" line {n + 1}" : "";
            errors.Add($"{fileName}{line}: {e.Message}");
            return null;
        }
    }

    private static Dictionary<string, T> ToMap<T>(
        IEnumerable<T> items, Func<T, string> id, string fileName, List<string> errors)
    {
        var map = new Dictionary<string, T>();
        foreach (var item in items)
        {
            if (!map.TryAdd(id(item), item))
            {
                errors.Add($"{fileName}: duplicate id '{id(item)}'");
            }
        }

        return map;
    }

    private static void ValidateClass(ClassDefinition c, List<string> errors)
    {
        var at = $"{ClassesFile} '{c.Id}'";
        DataValidation.RequireText(c.Id, $"{ClassesFile}: id", errors);
        DataValidation.RequireText(c.Name, $"{at}: name", errors);
        if (c.SpeedPercent <= 0)
        {
            errors.Add($"{at}: speedPercent must be positive, got {c.SpeedPercent}");
        }
    }

    private static void ValidateSkill(SkillDefinition s, List<string> errors)
    {
        var at = $"{SkillsFile} '{s.Id}'";
        DataValidation.RequireText(s.Id, $"{SkillsFile}: id", errors);
        DataValidation.RequireText(s.Name, $"{at}: name", errors);
        DataValidation.RequireNonNegative(s.HpCost, $"{at}: hpCost", errors);
        DataValidation.RequireNonNegative(s.MpCost, $"{at}: mpCost", errors);
        DataValidation.RequireNonNegative(s.Power, $"{at}: power", errors);
    }

    private static void ValidateEncounter(
        EncounterDefinition e,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        List<string> errors)
    {
        var at = $"{EncountersFile} '{e.Id}'";
        DataValidation.RequireText(e.Id, $"{EncountersFile}: id", errors);
        DataValidation.RequireText(e.Name, $"{at}: name", errors);
        if (e.Units.Count == 0)
        {
            errors.Add($"{at}: needs at least one unit");
        }

        foreach (var duplicate in e.Units.GroupBy(u => u.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"{at}: duplicate unit id '{duplicate.Key}'");
        }

        foreach (var unit in e.Units)
        {
            var unitAt = $"{at} unit '{unit.Id}'";
            DataValidation.RequireText(unit.Id, $"{at}: unit id", errors);
            DataValidation.RequireText(unit.Name, $"{unitAt}: name", errors);
            if (!classes.ContainsKey(unit.ClassId))
            {
                errors.Add($"{unitAt}: unknown class '{unit.ClassId}'");
            }

            DataValidation.ValidateStats(unit.Stats, unitAt, errors);

            foreach (var tactic in unit.Tactics)
            {
                DataValidation.ValidateTactic(tactic, $"{unitAt} tactic {tactic.Priority}", skills, errors);
            }
        }
    }
}
