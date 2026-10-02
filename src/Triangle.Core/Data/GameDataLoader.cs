using System.Text.Json;
using Triangle.Core.Actions;
using Triangle.Core.Skills;

namespace Triangle.Core.Data;

/// <summary>
/// data/ 폴더의 JSON 파일을 읽고 검증한다. 오류는 멈추지 않고 모두 모아서
/// <see cref="GameDataException"/> 하나로 던진다.
/// </summary>
public static class GameDataLoader
{
    public const string SkillsFile = "skills.json";
    public const string ActionsFile = "actions.json";
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

        var skills = Read(SkillsFile);
        var actions = Read(ActionsFile);
        var encounters = Read(EncountersFile);
        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        return Parse(skills!, actions!, encounters!);
    }

    public static GameData Parse(string skillsJson, string actionsJson, string encountersJson)
    {
        var errors = new List<string>();

        var skills = Deserialize<SkillDefinition>(skillsJson, SkillsFile, errors);
        var actions = Deserialize<ActionDefinition>(actionsJson, ActionsFile, errors);
        var encounters = Deserialize<EncounterDefinition>(encountersJson, EncountersFile, errors);

        // 형식 오류가 있으면 참조 검증은 의미가 없다.
        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        var skillMap = ToMap(skills!, s => s.Id, SkillsFile, errors);
        var actionMap = ToMap(actions!, a => a.Id, ActionsFile, errors);
        var encounterMap = ToMap(encounters!, e => e.Id, EncountersFile, errors);

        foreach (var s in skills!)
        {
            ValidateSkill(s, skillMap, errors);
        }

        ValidateNoPrerequisiteCycles(skillMap, errors);

        foreach (var a in actions!)
        {
            ValidateAction(a, skillMap, errors);
        }

        foreach (var e in encounters!)
        {
            ValidateEncounter(e, skillMap, actionMap, errors);
        }

        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        return new GameData(skillMap, actionMap, encounterMap);
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

    private static void ValidateSkill(SkillDefinition s, IReadOnlyDictionary<string, SkillDefinition> skills, List<string> errors)
    {
        var at = $"{SkillsFile} '{s.Id}'";
        DataValidation.RequireText(s.Id, $"{SkillsFile}: id", errors);
        DataValidation.RequireText(s.Name, $"{at}: name", errors);
        if (s.Rank < 1)
        {
            errors.Add($"{at}: rank must be at least 1, got {s.Rank}");
        }

        DataValidation.ValidateRequirements(s.Prerequisites, $"{at} prerequisite", skills, errors);
    }

    /// <summary>선행 스킬이 돌고 돌아 자기 자신을 요구하면 영원히 배울 수 없다.</summary>
    private static void ValidateNoPrerequisiteCycles(IReadOnlyDictionary<string, SkillDefinition> skills, List<string> errors)
    {
        var done = new HashSet<string>();
        var visiting = new HashSet<string>();

        bool Visit(string id, List<string> path)
        {
            if (done.Contains(id) || !skills.TryGetValue(id, out var skill))
            {
                return true;
            }

            if (!visiting.Add(id))
            {
                var start = path.IndexOf(id);
                errors.Add($"{SkillsFile}: prerequisite cycle {string.Join(" -> ", path.Skip(start).Append(id))}");
                return false;
            }

            path.Add(id);
            var ok = skill.Prerequisites.All(p => Visit(p.SkillId, path));
            path.RemoveAt(path.Count - 1);
            visiting.Remove(id);
            done.Add(id);
            return ok;
        }

        foreach (var id in skills.Keys)
        {
            Visit(id, []);
        }
    }

    private static void ValidateAction(ActionDefinition a, IReadOnlyDictionary<string, SkillDefinition> skills, List<string> errors)
    {
        var at = $"{ActionsFile} '{a.Id}'";
        DataValidation.RequireText(a.Id, $"{ActionsFile}: id", errors);
        DataValidation.RequireText(a.Name, $"{at}: name", errors);
        DataValidation.RequireNonNegative(a.HpCost, $"{at}: hpCost", errors);
        DataValidation.RequireNonNegative(a.MpCost, $"{at}: mpCost", errors);
        DataValidation.RequireNonNegative(a.Power, $"{at}: power", errors);
        DataValidation.ValidateRequirements(a.Requirements, $"{at} requirement", skills, errors);
    }

    private static void ValidateEncounter(
        EncounterDefinition e,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, ActionDefinition> actions,
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
            DataValidation.ValidateStats(unit.Stats, unitAt, errors);
            DataValidation.ValidateSkillLevels(unit.Skills, unitAt, skills, errors);

            var set = new SkillSet(unit.Skills, skills);
            foreach (var tactic in unit.Tactics)
            {
                var tacticAt = $"{unitAt} tactic {tactic.Priority}";
                DataValidation.ValidateTactic(tactic, tacticAt, actions, errors);
                if (actions.TryGetValue(tactic.ActionId, out var action))
                {
                    foreach (var missing in set.Missing(action.Requirements))
                    {
                        errors.Add($"{tacticAt}: '{tactic.ActionId}' needs '{missing.SkillId}' level {missing.Level}");
                    }
                }
            }
        }
    }
}
