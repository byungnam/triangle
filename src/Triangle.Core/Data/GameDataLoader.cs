using System.Text.Json;
using Triangle.Core.Actions;
using Triangle.Core.Effects;
using Triangle.Core.Expeditions;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Skills;

namespace Triangle.Core.Data;

/// <summary>
/// data/ 폴더의 JSON 파일을 읽고 검증한다. 오류는 멈추지 않고 모두 모아서
/// <see cref="GameDataException"/> 하나로 던진다.
/// </summary>
public static class GameDataLoader
{
    public const string MasteriesFile = "masteries.json";
    public const string SkillsFile = "skills.json";
    public const string ActionsFile = "actions.json";
    public const string EffectsFile = "effects.json";
    public const string EncountersFile = "encounters.json";
    public const string ItemsFile = "items.json";
    public const string ZonesFile = "zones.json";
    public const string RecruitsFile = "recruits.json";

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

        var masteries = Read(MasteriesFile);
        var skills = Read(SkillsFile);
        var actions = Read(ActionsFile);
        var effects = Read(EffectsFile);
        var encounters = Read(EncountersFile);
        var items = Read(ItemsFile);
        var zones = Read(ZonesFile);
        var recruits = Read(RecruitsFile);
        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        return Parse(masteries!, skills!, actions!, encounters!, effects!, items!, zones!, recruits!);
    }

    public static GameData Parse(string masteriesJson, string skillsJson, string actionsJson, string encountersJson, string effectsJson = "[]",
        string itemsJson = "[]", string zonesJson = "[]", string recruitsJson = "[]")
    {
        var errors = new List<string>();

        var masteries = Deserialize<MasteryDefinition>(masteriesJson, MasteriesFile, errors);
        var skills = Deserialize<SkillDefinition>(skillsJson, SkillsFile, errors);
        var actions = Deserialize<ActionDefinition>(actionsJson, ActionsFile, errors);
        var effects = Deserialize<EffectDefinition>(effectsJson, EffectsFile, errors);
        var encounters = Deserialize<EncounterDefinition>(encountersJson, EncountersFile, errors);
        var items = Deserialize<ItemDefinition>(itemsJson, ItemsFile, errors);
        var zones = Deserialize<ZoneDefinition>(zonesJson, ZonesFile, errors);
        var recruits = Deserialize<RecruitTemplate>(recruitsJson, RecruitsFile, errors);

        // 형식 오류가 있으면 참조 검증은 의미가 없다.
        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        var masteryMap = ToMap(masteries!, m => m.Id, MasteriesFile, errors);
        var skillMap = ToMap(skills!, s => s.Id, SkillsFile, errors);
        var actionMap = ToMap(actions!, a => a.Id, ActionsFile, errors);
        var effectMap = ToMap(effects!, e => e.Id, EffectsFile, errors);

        foreach (var effect in effects!)
        {
            DataValidation.RequireText(effect.Id, $"{EffectsFile}: id", errors);
            DataValidation.RequireText(effect.Name, $"{EffectsFile} '{effect.Id}': name", errors);
            if (effect.TickHpPercent is < -100 or > 100)
            {
                errors.Add($"{EffectsFile} '{effect.Id}': tickHpPercent must be -100..100, got {effect.TickHpPercent}");
            }
        }
        var encounterMap = ToMap(encounters!, e => e.Id, EncountersFile, errors);
        var itemMap = ToMap(items!, i => i.Id, ItemsFile, errors);
        var zoneMap = ToMap(zones!, z => z.Id, ZonesFile, errors);
        var recruitMap = ToMap(recruits!, r => r.Id, RecruitsFile, errors);

        foreach (var m in masteries!)
        {
            DataValidation.RequireText(m.Id, $"{MasteriesFile}: id", errors);
            DataValidation.RequireText(m.Name, $"{MasteriesFile} '{m.Id}': name", errors);
        }

        foreach (var s in skills!)
        {
            ValidateSkill(s, masteryMap, skillMap, errors);
        }

        ValidateNoPrerequisiteCycles(skillMap, errors);

        foreach (var a in actions!)
        {
            ValidateAction(a, masteryMap, skillMap, errors);
            foreach (var applied in a.Applies)
            {
                if (!effectMap.ContainsKey(applied.EffectId))
                {
                    errors.Add($"{ActionsFile} '{a.Id}': applies unknown effect '{applied.EffectId}'");
                }

                if (applied.Duration < 1)
                {
                    errors.Add($"{ActionsFile} '{a.Id}': duration of '{applied.EffectId}' must be at least 1, got {applied.Duration}");
                }
            }
        }

        foreach (var e in encounters!)
        {
            ValidateEncounter(e, masteryMap, skillMap, actionMap, errors);
        }

        foreach (var item in items!)
        {
            var at = $"{ItemsFile} '{item.Id}'";
            DataValidation.RequireText(item.Id, $"{ItemsFile}: id", errors);
            DataValidation.RequireText(item.Name, $"{at}: name", errors);
            DataValidation.RequireNonNegative(item.Price, $"{at}: price", errors);
            if (!masteryMap.ContainsKey(item.Mastery))
            {
                errors.Add($"{at}: unknown mastery '{item.Mastery}'");
            }
        }

        foreach (var zone in zones!)
        {
            ValidateZone(zone, encounterMap, itemMap, errors);
        }

        foreach (var recruit in recruits!)
        {
            ValidateRecruit(recruit, masteryMap, skillMap, actionMap, itemMap, errors);
        }

        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        return new GameData(masteryMap, skillMap, actionMap, effectMap, encounterMap, itemMap, zoneMap, recruitMap);
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

    private static void ValidateSkill(
        SkillDefinition s,
        IReadOnlyDictionary<string, MasteryDefinition> masteries,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        List<string> errors)
    {
        var at = $"{SkillsFile} '{s.Id}'";
        DataValidation.RequireText(s.Id, $"{SkillsFile}: id", errors);
        DataValidation.RequireText(s.Name, $"{at}: name", errors);
        if (!masteries.ContainsKey(s.Mastery))
        {
            errors.Add($"{at}: unknown mastery '{s.Mastery}'");
        }

        foreach (var p in s.Prerequisites)
        {
            if (skills.TryGetValue(p.SkillId, out var pre) && pre.Mastery != s.Mastery)
            {
                errors.Add($"{at}: prerequisite '{p.SkillId}' belongs to another mastery tree");
            }
        }

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

    private static void ValidateAction(
        ActionDefinition a,
        IReadOnlyDictionary<string, MasteryDefinition> masteries,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        List<string> errors)
    {
        var at = $"{ActionsFile} '{a.Id}'";
        DataValidation.RequireText(a.Id, $"{ActionsFile}: id", errors);
        DataValidation.RequireText(a.Name, $"{at}: name", errors);
        DataValidation.RequireNonNegative(a.HpCost, $"{at}: hpCost", errors);
        DataValidation.RequireNonNegative(a.MpCost, $"{at}: mpCost", errors);
        DataValidation.RequireNonNegative(a.Power, $"{at}: power", errors);
        DataValidation.ValidateRequirements(a.Requirements, $"{at} requirement", skills, errors);
        DataValidation.RequireSlot(a.Weapon, EquipmentSlot.Weapon, $"{at}: weapon", masteries, errors);
    }

    private static void ValidateEncounter(
        EncounterDefinition e,
        IReadOnlyDictionary<string, MasteryDefinition> masteries,
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
            DataValidation.ValidateEquipment(unit.Weapon, unit.Armor, unitAt, masteries, errors);
            DataValidation.ValidateSkillLevels(unit.Skills, unitAt, skills, errors);

            var set = new SkillSet(unit.Skills, skills);
            foreach (var tactic in unit.Tactics)
            {
                var tacticAt = $"{unitAt} tactic {tactic.Priority}";
                DataValidation.ValidateTactic(tactic, tacticAt, actions, errors);
                if (actions.TryGetValue(tactic.ActionId, out var action))
                {
                    if (action.Weapon is not null && action.Weapon != unit.Weapon)
                    {
                        errors.Add($"{tacticAt}: '{tactic.ActionId}' needs weapon '{action.Weapon}'");
                    }

                    foreach (var missing in set.Missing(action.Requirements))
                    {
                        errors.Add($"{tacticAt}: '{tactic.ActionId}' needs '{missing.SkillId}' level {missing.Level}");
                    }
                }
            }
        }
    }

    private static void ValidateZone(
        ZoneDefinition z,
        IReadOnlyDictionary<string, EncounterDefinition> encounters,
        IReadOnlyDictionary<string, ItemDefinition> items,
        List<string> errors)
    {
        var at = $"{ZonesFile} '{z.Id}'";
        DataValidation.RequireText(z.Id, $"{ZonesFile}: id", errors);
        DataValidation.RequireText(z.Name, $"{at}: name", errors);
        if (z.MaxBattles < 1)
        {
            errors.Add($"{at}: maxBattles must be at least 1, got {z.MaxBattles}");
        }

        DataValidation.RequirePercent(z.EquipmentDestroyChance, $"{at}: equipmentDestroyChance", errors);
        if (z.Encounters.Count == 0)
        {
            errors.Add($"{at}: needs at least one encounter");
        }

        foreach (var e in z.Encounters)
        {
            if (!encounters.ContainsKey(e.EncounterId))
            {
                errors.Add($"{at}: unknown encounter '{e.EncounterId}'");
            }

            if (e.Weight < 1)
            {
                errors.Add($"{at}: weight of '{e.EncounterId}' must be at least 1, got {e.Weight}");
            }
        }

        var r = z.Rewards;
        DataValidation.RequireNonNegative(r.GoldMin, $"{at}: goldMin", errors);
        DataValidation.RequireNonNegative(r.DepthBonusPercent, $"{at}: depthBonusPercent", errors);
        DataValidation.RequireNonNegative(r.ClearBonusGold, $"{at}: clearBonusGold", errors);
        if (r.GoldMax < r.GoldMin)
        {
            errors.Add($"{at}: goldMax {r.GoldMax} is less than goldMin {r.GoldMin}");
        }

        foreach (var drop in r.ItemDrops)
        {
            if (!items.ContainsKey(drop.ItemId))
            {
                errors.Add($"{at}: drops unknown item '{drop.ItemId}'");
            }

            DataValidation.RequirePercent(drop.Chance, $"{at}: chance of '{drop.ItemId}'", errors);
        }
    }

    private static void ValidateRecruit(
        RecruitTemplate r,
        IReadOnlyDictionary<string, MasteryDefinition> masteries,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, ActionDefinition> actions,
        IReadOnlyDictionary<string, ItemDefinition> items,
        List<string> errors)
    {
        var at = $"{RecruitsFile} '{r.Id}'";
        DataValidation.RequireText(r.Id, $"{RecruitsFile}: id", errors);
        DataValidation.RequireText(r.Name, $"{at}: name", errors);
        if (r.Names.Count == 0)
        {
            errors.Add($"{at}: needs at least one name");
        }

        foreach (var name in r.Names)
        {
            DataValidation.RequireText(name, $"{at}: names entry", errors);
        }

        DataValidation.ValidateStats(r.StatsMin, $"{at} statsMin", errors);
        var (min, max) = (r.StatsMin, r.StatsMax);
        if (max.Str < min.Str || max.Dex < min.Dex || max.Vital < min.Vital || max.Intel < min.Intel || max.Speed < min.Speed)
        {
            errors.Add($"{at}: statsMax must be at least statsMin for every stat");
        }

        DataValidation.RequireNonNegative(r.Price, $"{at}: price", errors);
        DataValidation.ValidateItem(r.Weapon, EquipmentSlot.Weapon, $"{at}: weapon", items, masteries, errors);
        DataValidation.ValidateItem(r.Armor, EquipmentSlot.Armor, $"{at}: armor", items, masteries, errors);

        // 신입은 패시브가 없으므로 요구 스킬이 없는 행동만, 무기 계열이 맞는 행동만 쓸 수 있다.
        var weapon = r.Weapon is not null && items.TryGetValue(r.Weapon, out var item) ? item.Mastery : null;
        foreach (var tactic in r.Tactics)
        {
            var tacticAt = $"{at} tactic {tactic.Priority}";
            DataValidation.ValidateTactic(tactic, tacticAt, actions, errors);
            if (actions.TryGetValue(tactic.ActionId, out var action) && !action.IsUsableBy(weapon, new SkillSet(SkillSet.NoSkills, skills)))
            {
                errors.Add($"{tacticAt}: a new recruit cannot use '{tactic.ActionId}'");
            }
        }
    }
}
