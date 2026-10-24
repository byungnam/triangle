using System.Text.Json;
using System.Text.Json.Nodes;
using Triangle.Core.Actions;
using Triangle.Core.Effects;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Runs;
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
    public const string ClassesFile = "classes.json";
    public const string RunFile = "run.json";
    public const string UnlocksFile = "unlocks.json";

    /// <param name="extraDirectory">
    /// 게임 데이터 밖의 추가 데이터 폴더 (전술 측정의 시제품, <see cref="Balance.BenchDefinition.ExtraData"/>).
    /// 이 폴더에 같은 이름의 파일(encounters.json, effects.json 등)이 있으면 게임 데이터 배열 뒤에 붙여 같은 검증을 받는다.
    /// 오류 메시지는 게임 데이터 파일 이름으로 나온다.
    /// </param>
    public static GameData LoadDirectory(string directory, string? extraDirectory = null)
    {
        var errors = new List<string>();

        string? Read(string fileName)
        {
            var path = Path.Combine(directory, fileName);
            try
            {
                var json = File.ReadAllText(path);
                var extra = extraDirectory is null ? null : Path.Combine(extraDirectory, fileName);
                return extra is not null && File.Exists(extra) ? ConcatArrays(json, File.ReadAllText(extra), extra, errors) : json;
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
        var classes = Read(ClassesFile);
        var run = Read(RunFile);
        var unlocks = Read(UnlocksFile);
        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        return Parse(masteries!, skills!, actions!, encounters!, effects!, items!, zones!, classes!, run!, unlocks!);
    }

    /// <summary>두 JSON 배열을 이어 붙인다. 배열이 아니거나 읽을 수 없으면 오류를 남기고 원래 것을 돌려준다.</summary>
    private static string ConcatArrays(string json, string extraJson, string extraPath, List<string> errors)
    {
        try
        {
            var all = JsonNode.Parse(json)?.AsArray() ?? [];
            var extra = JsonNode.Parse(extraJson)?.AsArray() ?? [];
            foreach (var node in extra.ToList())
            {
                extra.Remove(node);
                all.Add(node);
            }

            return all.ToJsonString();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            errors.Add($"{extraPath}: {e.Message}");
            return json;
        }
    }

    public static GameData Parse(string masteriesJson, string skillsJson, string actionsJson, string encountersJson, string effectsJson = "[]",
        string itemsJson = "[]", string zonesJson = "[]", string classesJson = "[]",
        string? runJson = null, string unlocksJson = "[]")
    {
        var errors = new List<string>();

        var masteries = Deserialize<MasteryDefinition>(masteriesJson, MasteriesFile, errors);
        var skills = Deserialize<SkillDefinition>(skillsJson, SkillsFile, errors);
        var actions = Deserialize<ActionDefinition>(actionsJson, ActionsFile, errors);
        var effects = Deserialize<EffectDefinition>(effectsJson, EffectsFile, errors);
        var encounters = Deserialize<EncounterDefinition>(encountersJson, EncountersFile, errors);
        var items = Deserialize<ItemDefinition>(itemsJson, ItemsFile, errors);
        var zones = Deserialize<ZoneDefinition>(zonesJson, ZonesFile, errors);
        var classes = Deserialize<ClassDefinition>(classesJson, ClassesFile, errors);
        var run = runJson is null ? null : DeserializeObject<RunDefinition>(runJson, RunFile, errors);
        var unlocks = Deserialize<UnlockDefinition>(unlocksJson, UnlocksFile, errors);

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
        var classMap = ToMap(classes!, c => c.Id, ClassesFile, errors);
        var unlockMap = ToMap(unlocks!, u => u.Id, UnlocksFile, errors);
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
            ValidateAction(a, skillMap, errors);
            ValidateActionMechanics(a, masteryMap, skillMap, actionMap, effectMap, errors);
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
            ValidateEncounter(e, skillMap, actionMap, itemMap, effectMap, errors);
        }

        foreach (var item in items!)
        {
            ValidateItem(item, masteryMap, skillMap, actionMap, effectMap, errors);
        }

        foreach (var zone in zones!)
        {
            ValidateZone(zone, encounterMap, errors);
        }

        ValidateClasses(classes!, masteryMap, skillMap, actionMap, itemMap, errors);
        if (run is not null)
        {
            ValidateRun(run, zoneMap, encounterMap, classMap, effectMap, errors);
        }

        ValidateUnlocks(unlocks!, classMap, actionMap, itemMap, errors);

        if (errors.Count > 0)
        {
            throw new GameDataException(errors);
        }

        return new GameData(masteryMap, skillMap, actionMap, effectMap, encounterMap, itemMap, zoneMap, classMap) { Run = run, Unlocks = unlockMap };
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

    private static T? DeserializeObject<T>(string json, string fileName, List<string> errors)
        where T : class
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(json, GameDataJson.Options);
            if (value is null)
            {
                errors.Add($"{fileName}: expected an object, found null");
            }

            return value;
        }
        catch (JsonException e)
        {
            var line = e.LineNumber is { } n ? $" line {n + 1}" : "";
            errors.Add($"{fileName}{line}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 해금: 이름과 값, 여는 것의 참조(클래스, 그 클래스 풀의 카드, 무기·방어구·장신구). 한 가지는 한 해금만 잠근다.
    /// 시작 카드는 잠글 수 없고, 처음부터 고를 수 있는 클래스가 하나는 있어야 한다.
    /// </summary>
    private static void ValidateUnlocks(
        IReadOnlyList<UnlockDefinition> unlocks,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, ActionDefinition> actions,
        IReadOnlyDictionary<string, ItemDefinition> items,
        List<string> errors)
    {
        foreach (var u in unlocks)
        {
            var at = $"{UnlocksFile} '{u.Id}'";
            DataValidation.RequireText(u.Id, $"{UnlocksFile}: id", errors);
            DataValidation.RequireText(u.Name, $"{at}: name", errors);
            DataValidation.RequireNonNegative(u.Cost, $"{at}: cost", errors);
            DataValidation.RequireNonNegative(u.StartGold, $"{at}: startGold", errors);
            var b = u.Boons;
            if (b.RestUnseal < 0 || b.ShopDiscountPercent is < 0 or > 50)
            {
                errors.Add($"{at}: boons must not be negative and the shop discount is at most 50%");
            }

            if (!u.Locks.Any() && u.StartGold == 0 && b.IsEmpty)
            {
                errors.Add($"{at}: opens nothing");
            }

            if (u.Requires is { } required && (required == u.Id || unlocks.All(o => o.Id != required)))
            {
                errors.Add($"{at}: requires unknown unlock '{required}'");
            }

            errors.AddRange(u.Classes.Where(id => !classes.ContainsKey(id)).Select(id => $"{at}: unknown class '{id}'"));
            foreach (var card in u.Cards)
            {
                if (!actions.ContainsKey(card))
                {
                    errors.Add($"{at}: unknown action '{card}'");
                }
                else if (classes.Values.FirstOrDefault(c => c.Skills.Contains(card)) is not { } owner)
                {
                    errors.Add($"{at}: card '{card}' is in no class's skills");
                }
                else if (owner.StartingSkills.Contains(card))
                {
                    errors.Add($"{at}: card '{card}' is the starting skill of '{owner.Id}'");
                }
            }

            foreach (var id in u.Items)
            {
                if (!items.TryGetValue(id, out var item))
                {
                    errors.Add($"{at}: unknown item '{id}'");
                }
                else if (item.Slot is not (EquipmentSlot.MainHand or EquipmentSlot.Body or EquipmentSlot.Trinket))
                {
                    errors.Add($"{at}: item '{id}' is not a weapon, armor or trinket");
                }
            }
        }

        foreach (var twice in unlocks.SelectMany(u => u.Locks.Distinct().Select(id => (id, u.Id))).GroupBy(x => x.id).Where(g => g.Count() > 1))
        {
            errors.Add($"{UnlocksFile}: '{twice.Key}' is locked by more than one unlock ({string.Join(", ", twice.Select(x => x.Item2))})");
        }

        // 단계가 고리를 이루면 아무것도 열 수 없다.
        var byId = unlocks.GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var u in unlocks)
        {
            var seen = new HashSet<string> { u.Id };
            for (var at = u.Requires; at is not null && byId.TryGetValue(at, out var next); at = next.Requires)
            {
                if (!seen.Add(at))
                {
                    errors.Add($"{UnlocksFile} '{u.Id}': requires go round in a circle");
                    break;
                }
            }
        }

        var lockedClasses = unlocks.SelectMany(u => u.Classes).ToHashSet();
        if (classes.Count > 0 && classes.Keys.All(lockedClasses.Contains))
        {
            errors.Add($"{UnlocksFile}: every class is locked; at least one must be open from the start");
        }
    }

    /// <summary>
    /// 원정 지도: 수식어는 이름과 위험도(1 이상)가 있고 판을 어렵게만 하며, 보상은 쉽게만 한다. 효과는 있어야 하고 ID는 겹치지 않는다.
    /// 수식어가 붙은 지도는 늘 보상이 있도록 위험도 1에서도 무언가를 주는 보상이 하나는 있어야 한다.
    /// </summary>
    private static void ValidateCharts(RunChartRules charts, IReadOnlyDictionary<string, EffectDefinition> effects, List<string> errors)
    {
        if (charts.Offers < 0 || charts.MaxTier < 0 || charts.RenownPerRisk < 0)
        {
            errors.Add($"{RunFile}: charts offers, maxTier and renownPerRisk must not be negative");
        }

        foreach (var duplicate in charts.Modifiers.Select(m => m.Id).Concat(charts.Rewards.Select(r => r.Id)).GroupBy(id => id).Where(g => g.Count() > 1))
        {
            errors.Add($"{RunFile}: duplicate chart modifier or reward id '{duplicate.Key}'");
        }

        foreach (var m in charts.Modifiers)
        {
            if (string.IsNullOrWhiteSpace(m.Name) || m.Risk < 1)
            {
                errors.Add($"{RunFile}: modifier '{m.Id}' needs a name and a risk of at least 1");
            }

            if (m.Terms.IsEmpty)
            {
                errors.Add($"{RunFile}: modifier '{m.Id}' changes nothing");
            }
            else if (!m.Terms.OnlyHarder)
            {
                errors.Add($"{RunFile}: modifier '{m.Id}' must only make the run harder");
            }

            errors.AddRange(m.Terms.Effects.Where(id => !effects.ContainsKey(id)).Select(id => $"{RunFile}: modifier '{m.Id}' uses unknown effect '{id}'"));
        }

        foreach (var r in charts.Rewards)
        {
            if (string.IsNullOrWhiteSpace(r.Name) || r.Step < 1 || r.Max < 0)
            {
                errors.Add($"{RunFile}: chart reward '{r.Id}' needs a name, a step of at least 1 and a max not below 0");
            }

            if (r.Terms.IsEmpty || !r.Terms.OnlyEasier)
            {
                errors.Add($"{RunFile}: chart reward '{r.Id}' must make the run easier (and add no effects)");
            }
        }

        if (charts.Modifiers.Count > 0 && charts.Rewards.All(r => r.Step > 1))
        {
            errors.Add($"{RunFile}: charts need a reward with step 1 so that every chart has a reward");
        }
    }

    /// <summary>판의 규칙: 막의 지역·보스·정예, 인원과 줄, 가중치와 값, 이벤트 선택지의 합류 클래스.</summary>
    private static void ValidateRun(
        RunDefinition run,
        IReadOnlyDictionary<string, ZoneDefinition> zones,
        IReadOnlyDictionary<string, EncounterDefinition> encounters,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, EffectDefinition> effects,
        List<string> errors)
    {
        if (run.PartySize is < 1 or > Run.MaxMembers)
        {
            errors.Add($"{RunFile}: partySize must be 1-{Run.MaxMembers}, got {run.PartySize}");
        }

        if (run.AllyMpPercent is < -90 or > 200)
        {
            errors.Add($"{RunFile}: allyMpPercent must be -90 to 200, got {run.AllyMpPercent}");
        }

        if (run.StartLines < 0 || run.StartLines > run.MaxLines)
        {
            errors.Add($"{RunFile}: startLines must be 0-maxLines ({run.MaxLines}), got {run.StartLines}");
        }

        DataValidation.RequireNonNegative(run.MaxTrinkets, $"{RunFile}: maxTrinkets", errors);
        DataValidation.RequireNonNegative(run.StartGold, $"{RunFile}: startGold", errors);
        if (run.Acts.Count == 0)
        {
            errors.Add($"{RunFile}: needs at least one act");
        }

        for (var i = 0; i < run.Acts.Count; i++)
        {
            var act = run.Acts[i];
            var at = $"{RunFile} acts[{i}]";
            if (!zones.TryGetValue(act.ZoneId, out var zone))
            {
                errors.Add($"{at}: unknown zone '{act.ZoneId}'");
            }
            else if (act.Bosses.Count == 0 && zone.Boss is null)
            {
                errors.Add($"{at}: zone '{act.ZoneId}' has no boss and the act names none");
            }

            if (act.Floors < 2)
            {
                errors.Add($"{at}: floors must be at least 2, got {act.Floors}");
            }

            errors.AddRange(act.Bosses.Concat(act.Elites).Where(id => !encounters.ContainsKey(id)).Select(id => $"{at}: unknown encounter '{id}'"));
            if (act.ShopTier is < 1 or > ItemDefinition.MaxTier)
            {
                errors.Add($"{at}: shopTier must be 1-{ItemDefinition.MaxTier}, got {act.ShopTier}");
            }

            if (new[] { act.FightStrength, act.EliteStrength, act.BossStrength }.Any(st => st.Hp < 10 || st.Power < 10))
            {
                errors.Add($"{at}: strength hp and power are percents and must be at least 10");
            }
        }

        var map = run.Map;
        if (map.Width < 1 || map.Paths < 1)
        {
            errors.Add($"{RunFile}: map width and paths must be at least 1");
        }
        else if (map.Starts < 1 || map.Starts > Math.Min(map.Width, map.Paths))
        {
            errors.Add($"{RunFile}: map starts must be 1-{Math.Min(map.Width, map.Paths)} (at most width and paths), got {map.Starts}");
        }

        if (new[] { map.Fight, map.Elite, map.Shop, map.Rest, map.Event }.Any(w => w < 0) || map.Fight + map.Elite + map.Shop + map.Rest + map.Event == 0)
        {
            errors.Add($"{RunFile}: map weights must not be negative and must not all be 0");
        }

        var reward = run.Reward;
        if (reward.Cards < 1 || reward.FightCards < 0 || reward.EliteExtraCards < 0 || new[] { reward.Active, reward.Passive, reward.Line, reward.Upgrade }.Any(w => w < 0)
            || reward.Active + reward.Passive + reward.Line + reward.Upgrade == 0)
        {
            errors.Add($"{RunFile}: reward needs at least one card and one positive weight");
        }

        if (reward.GoldMin > reward.GoldMax || reward.EliteGoldMin > reward.EliteGoldMax || reward.GoldMin < 0 || reward.EliteGoldMin < 0 || reward.BossGold < 0)
        {
            errors.Add($"{RunFile}: reward gold ranges must be 0 <= min <= max");
        }

        var shop = run.Shop;
        if (new[] { shop.Items, shop.Skills, shop.SkillPrice, shop.Recruits, shop.RecruitPrice, shop.UnsealPrice, shop.Upgrades, shop.UpgradePrice, shop.UpgradeStep, shop.LinePrice, shop.LineStep }
            .Any(v => v < 0))
        {
            errors.Add($"{RunFile}: shop counts and prices must not be negative");
        }

        if (shop.Lines is < 0 or > 1)
        {
            errors.Add($"{RunFile}: shop lines must be 0 or 1");
        }

        if (new[] { run.Renown.Fight, run.Renown.Elite, run.Renown.Boss, run.Renown.Victory }.Any(v => v < 0))
        {
            errors.Add($"{RunFile}: renown must not be negative");
        }

        if (run.Rest.Unseal < 0 || run.Rest.TrainCards < 1)
        {
            errors.Add($"{RunFile}: rest unseal must not be negative and trainCards must be at least 1");
        }

        ValidateCharts(run.Charts, effects, errors);

        foreach (var duplicate in run.Events.GroupBy(e => e.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"{RunFile}: duplicate event id '{duplicate.Key}'");
        }

        foreach (var e in run.Events)
        {
            var at = $"{RunFile} event '{e.Id}'";
            DataValidation.RequireText(e.Id, $"{RunFile}: event id", errors);
            DataValidation.RequireText(e.Name, $"{at}: name", errors);
            if (e.Options.Count == 0)
            {
                errors.Add($"{at}: needs at least one option");
            }

            if (e.Weight < 1)
            {
                errors.Add($"{at}: weight must be at least 1, got {e.Weight}");
            }

            foreach (var option in e.Options)
            {
                DataValidation.RequireText(option.Text, $"{at}: option text", errors);
                if (option.Join is { } join && join != "any" && !classes.ContainsKey(join))
                {
                    errors.Add($"{at}: unknown class '{join}'");
                }

                if (option.Seal < 0 || option.Unseal < 0)
                {
                    errors.Add($"{at}: seal and unseal must not be negative");
                }
            }
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
        IReadOnlyDictionary<string, SkillDefinition> skills,
        List<string> errors)
    {
        var at = $"{ActionsFile} '{a.Id}'";
        DataValidation.RequireText(a.Id, $"{ActionsFile}: id", errors);
        DataValidation.RequireText(a.Name, $"{at}: name", errors);
        DataValidation.RequireNonNegative(a.HpCost, $"{at}: hpCost", errors);
        DataValidation.RequireNonNegative(a.MpCost, $"{at}: mpCost", errors);
        DataValidation.RequireNonNegative(a.Power, $"{at}: power", errors);
        if (a.Delay < 1)
        {
            errors.Add($"{at}: delay must be at least 1, got {a.Delay}");
        }

        DataValidation.ValidateRequirements(a.Requirements, $"{at} requirement", skills, errors);
    }

    /// <summary>행동의 특수 규칙(연타, 연쇄, 영창, 소환 등) 검사.</summary>
    private static void ValidateActionMechanics(
        ActionDefinition a,
        IReadOnlyDictionary<string, MasteryDefinition> masteries,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, ActionDefinition> actions,
        IReadOnlyDictionary<string, EffectDefinition> effects,
        List<string> errors)
    {
        var at = $"{ActionsFile} '{a.Id}'";
        if (a.Hits < 1)
        {
            errors.Add($"{at}: hits must be at least 1, got {a.Hits}");
        }

        if (a.Chant < 1)
        {
            errors.Add($"{at}: chant must be at least 1, got {a.Chant}");
        }

        DataValidation.RequireNonNegative(a.ExtraTargets, $"{at}: extraTargets", errors);
        DataValidation.RequireNonNegative(a.MissingHpBonusPercent, $"{at}: missingHpBonusPercent", errors);
        DataValidation.RequireNonNegative(a.PushBack, $"{at}: pushBack", errors);
        DataValidation.RequireNonNegative(a.MpDamage, $"{at}: mpDamage", errors);

        if (a.BonusAgainst is { } bonus && !effects.ContainsKey(bonus.EffectId))
        {
            errors.Add($"{at}: bonusAgainst unknown effect '{bonus.EffectId}'");
        }

        if (a.Weapon is { } weapon)
        {
            DataValidation.RequireKind(weapon, MasteryKind.Weapon, $"{at}: weapon", masteries, errors);
        }

        if (a.SummonOnly && (a.Universal || a.Weapon is not null))
        {
            errors.Add($"{at}: a summonOnly action cannot be universal or weapon-granted");
        }

        if ((a.Effect == ActionEffect.Summon) != (a.Summon is not null))
        {
            errors.Add($"{at}: effect Summon and summon must be set together");
        }

        if (a.Summon is { } summon)
        {
            DataValidation.RequireText(summon.Name, $"{at}: summon name", errors);
            DataValidation.ValidateStats(summon.Stats, $"{at}: summon", errors);
            if (summon.Duration is < 1)
            {
                errors.Add($"{at}: summon duration must be at least 1, got {summon.Duration}");
            }

            if (!actions.TryGetValue(summon.ActionId ?? "", out var summonAction))
            {
                errors.Add($"{at}: summon uses unknown action '{summon.ActionId}'");
            }
            else if (!summonAction.SummonOnly)
            {
                errors.Add($"{at}: summon action '{summon.ActionId}' must be summonOnly");
            }

            if (summon.StatSkill is { } statSkill && !skills.ContainsKey(statSkill))
            {
                errors.Add($"{at}: summon statSkill unknown skill '{statSkill}'");
            }
        }
    }

    private static void ValidateItem(
        ItemDefinition item,
        IReadOnlyDictionary<string, MasteryDefinition> masteries,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, ActionDefinition> actions,
        IReadOnlyDictionary<string, EffectDefinition> effects,
        List<string> errors)
    {
        var at = $"{ItemsFile} '{item.Id}'";
        DataValidation.RequireText(item.Id, $"{ItemsFile}: id", errors);
        DataValidation.RequireText(item.Name, $"{at}: name", errors);
        DataValidation.RequireNonNegative(item.Price, $"{at}: price", errors);
        if (item.Tier is < 1 or > ItemDefinition.MaxTier)
        {
            errors.Add($"{at}: tier must be 1-{ItemDefinition.MaxTier}, got {item.Tier}");
        }

        DataValidation.ValidateRequirements(item.Requirements, $"{at} requirement", skills, errors);

        errors.AddRange(item.Effects.Where(id => !effects.ContainsKey(id)).Select(id => $"{at}: unknown effect '{id}'"));
        if (item.Slot == EquipmentSlot.Trinket)
        {
            if (item.Mastery is not null || item.TwoHanded || item.Actions.Count > 0 || item.Requirements.Count > 0)
            {
                errors.Add($"{at}: a trinket has no mastery, twoHanded, requirements or actions");
            }
        }
        else
        {
            if (item.Mastery is null)
            {
                errors.Add($"{at}: equipment needs a mastery");
            }
            else
            {
                DataValidation.RequireKind(item.Mastery, item.Slot.IsWeapon() ? MasteryKind.Weapon : MasteryKind.Armor, $"{at}: mastery", masteries, errors);
            }
        }

        if (item.TwoHanded && item.Slot != EquipmentSlot.MainHand)
        {
            errors.Add($"{at}: only a main-hand item can be twoHanded");
        }

        if (item.Slot != EquipmentSlot.MainHand && (item.PowerMultiplierPercent != 100 || item.EffectDurationBonus != 0 || item.BasicAttack is not null))
        {
            errors.Add($"{at}: only a main-hand item can have powerMultiplierPercent, effectDurationBonus or basicAttack");
        }

        if (item.BasicAttack is { } basic)
        {
            if (!actions.TryGetValue(basic, out var attack))
            {
                errors.Add($"{at}: unknown basicAttack '{basic}'");
            }
            else if (attack.MpCost > 0 || attack.HpCost > 0 || attack.Universal || attack.SummonOnly || attack.Chant > 1 || attack.Effect == ActionEffect.Summon)
            {
                errors.Add($"{at}: basicAttack '{basic}' must cost nothing, take one turn and not be universal, summon-only or a summon");
            }
        }

        if (item.Shop && item.Slot is not (EquipmentSlot.MainHand or EquipmentSlot.Body or EquipmentSlot.Trinket))
        {
            errors.Add($"{at}: only a weapon, armor or trinket can be sold in a run (shop)");
        }

        if (item.PowerMultiplierPercent < 1)
        {
            errors.Add($"{at}: powerMultiplierPercent must be at least 1, got {item.PowerMultiplierPercent}");
        }

        DataValidation.RequireNonNegative(item.EffectDurationBonus, $"{at}: effectDurationBonus", errors);

        foreach (var actionId in item.Actions)
        {
            if (!actions.TryGetValue(actionId, out var action))
            {
                errors.Add($"{at}: unknown action '{actionId}'");
            }
            else if (action.Universal || action.SummonOnly)
            {
                errors.Add($"{at}: offers {(action.Universal ? "universal" : "summonOnly")} action '{actionId}'");
            }
        }

        foreach (var duplicate in item.Actions.GroupBy(a => a).Where(g => g.Count() > 1))
        {
            errors.Add($"{at}: lists action '{duplicate.Key}' more than once");
        }
    }

    private static void ValidateEncounter(
        EncounterDefinition e,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, ActionDefinition> actions,
        IReadOnlyDictionary<string, ItemDefinition> items,
        IReadOnlyDictionary<string, EffectDefinition> effects,
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
            DataValidation.ValidateEquipped(unit.Equipment, $"{unitAt}: equipment", items, errors);
            DataValidation.ValidateSkillLevels(unit.Skills, unitAt, skills, errors);
            errors.AddRange(unit.Effects.Where(id => !effects.ContainsKey(id)).Select(id => $"{unitAt}: unknown effect '{id}'"));
            errors.AddRange(unit.Effects.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => $"{unitAt}: duplicate effect '{g.Key}'"));

            // 적은 착용 불가 상태를 두지 않는다: 착용 조건과 두손·보조 충돌은 데이터 오류다.
            var set = new SkillSet(unit.Skills, skills);
            var worn = unit.Equipment
                .Where(p => items.TryGetValue(p.Value, out var item) && item.Slot == p.Key)
                .Select(p => (p.Key, items[p.Value]))
                .ToList();
            foreach (var (_, item) in worn)
            {
                foreach (var missing in set.Missing(item.Requirements))
                {
                    errors.Add($"{unitAt}: '{item.Id}' needs '{missing.SkillId}' level {missing.Level}");
                }
            }

            if (unit.Equipment.ContainsKey(EquipmentSlot.OffHand) && worn.Any(w => w.Item2.TwoHanded))
            {
                errors.Add($"{unitAt}: an off-hand item cannot be worn with a two-handed weapon");
            }

            if (unit.Actions is not null)
            {
                errors.AddRange(unit.Actions.Where(id => !actions.ContainsKey(id)).Select(id => $"{unitAt}: unknown action '{id}'"));
                errors.AddRange(unit.Actions.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => $"{unitAt}: duplicate action '{g.Key}'"));
            }

            var granted = unit.Actions?.ToHashSet() ?? new Loadout(worn).GrantedActions(actions.Values);
            foreach (var tactic in unit.Tactics)
            {
                var tacticAt = $"{unitAt} tactic {tactic.Priority}";
                DataValidation.ValidateTactic(tactic, tacticAt, actions, errors);
                if (!actions.TryGetValue(tactic.ActionId, out var action))
                {
                    continue;
                }

                if (!action.IsGranted(granted))
                {
                    errors.Add(unit.Actions is null
                        ? $"{tacticAt}: no equipped item gives '{tactic.ActionId}'"
                        : $"{tacticAt}: '{tactic.ActionId}' is not in the unit's actions");
                }

                foreach (var missing in set.Missing(action.Requirements))
                {
                    errors.Add($"{tacticAt}: '{tactic.ActionId}' needs '{missing.SkillId}' level {missing.Level}");
                }
            }
        }
    }

    /// <summary>
    /// 클래스: 계열·무기·방어구·카드 풀의 참조. 무기는 그 클래스 계열의 주무기, 방어구는 몸통. 시작 스킬은 카드 풀에 있어야 하고,
    /// 공용·소환물 전용 행동은 카드가 될 수 없다. 액티브와 패시브는 한 클래스에만 속한다.
    /// </summary>
    private static void ValidateClasses(
        IReadOnlyList<ClassDefinition> classes,
        IReadOnlyDictionary<string, MasteryDefinition> masteries,
        IReadOnlyDictionary<string, SkillDefinition> skills,
        IReadOnlyDictionary<string, ActionDefinition> actions,
        IReadOnlyDictionary<string, ItemDefinition> items,
        List<string> errors)
    {
        foreach (var c in classes)
        {
            var at = $"{ClassesFile} '{c.Id}'";
            DataValidation.RequireText(c.Id, $"{ClassesFile}: id", errors);
            DataValidation.RequireText(c.Name, $"{at}: name", errors);
            DataValidation.ValidateStats(c.Stats, at, errors);
            errors.AddRange(c.Families.Where(f => !masteries.ContainsKey(f)).Select(f => $"{at}: unknown mastery '{f}'"));
            if (!items.TryGetValue(c.Weapon, out var weapon))
            {
                errors.Add($"{at}: unknown weapon '{c.Weapon}'");
            }
            else if (!c.CanWield(weapon))
            {
                errors.Add($"{at}: weapon '{c.Weapon}' is not a main-hand item of the class families");
            }

            if (!items.TryGetValue(c.Armor, out var armor))
            {
                errors.Add($"{at}: unknown armor '{c.Armor}'");
            }
            else if (armor.Slot != EquipmentSlot.Body)
            {
                errors.Add($"{at}: armor '{c.Armor}' is not a body item");
            }

            if (c.StartingSkills.Count == 0 || c.StartingSkills.Distinct().Count() != c.StartingSkills.Count)
            {
                errors.Add($"{at}: startingSkills must not be empty or repeat a skill");
            }

            foreach (var id in c.StartingSkills.Where(id => !c.Skills.Contains(id)))
            {
                errors.Add($"{at}: starting skill '{id}' is not in skills");
            }

            foreach (var id in c.Skills)
            {
                if (!actions.TryGetValue(id, out var action))
                {
                    errors.Add($"{at}: unknown skill '{id}'");
                }
                else if (action.Universal || action.SummonOnly)
                {
                    errors.Add($"{at}: '{id}' cannot be a card (universal or summon-only action)");
                }
            }

            errors.AddRange(c.Passives.Where(id => !skills.ContainsKey(id)).Select(id => $"{at}: unknown passive '{id}'"));
            errors.AddRange(c.Skills.Concat(c.Passives).GroupBy(id => id).Where(g => g.Count() > 1).Select(g => $"{at}: duplicate card '{g.Key}'"));
        }

        foreach (var shared in classes.SelectMany(c => c.Skills.Concat(c.Passives).Distinct().Select(id => (id, c.Id))).GroupBy(x => x.id).Where(g => g.Count() > 1))
        {
            errors.Add($"{ClassesFile}: '{shared.Key}' belongs to more than one class ({string.Join(", ", shared.Select(x => x.Item2))})");
        }
    }

    private static void ValidateZone(
        ZoneDefinition z,
        IReadOnlyDictionary<string, EncounterDefinition> encounters,
        List<string> errors)
    {
        var at = $"{ZonesFile} '{z.Id}'";
        DataValidation.RequireText(z.Id, $"{ZonesFile}: id", errors);
        DataValidation.RequireText(z.Name, $"{at}: name", errors);
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

        if (z.Boss is { } boss)
        {
            if (!encounters.ContainsKey(boss.EncounterId))
            {
                errors.Add($"{at}: boss has unknown encounter '{boss.EncounterId}'");
            }
        }
    }

}
