using System.Text.Json;
using System.Text.Json.Serialization;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>세이브 파일의 JSON 형태. <see cref="Version"/>이 바뀌면 이전 형식을 변환하는 코드를 둔다.</summary>
public sealed record SaveFile
{
    public required int Version { get; init; }

    /// <summary>전투에 쓸 전술 세트 (0부터).</summary>
    public int ActiveTacticSet { get; init; }

    /// <summary>
    /// 전술 세트 이름 (세트 순서). 멤버의 tacticSets가 이 순서를 따른다.
    /// 없으면(2026-10-10 이전 파일) 멤버의 세트 수만큼 "세트 1", "세트 2"…가 된다.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? TacticSetNames { get; init; }

    /// <summary>보유한 캐릭터 모두.</summary>
    public IReadOnlyList<SavedMember> Roster { get; init; } = [];

    /// <summary>출전할 캐릭터 ID.</summary>
    public IReadOnlyList<string> Lineup { get; init; } = [];

    public int Gold { get; init; }

    /// <summary>창고: 아이템 ID별 개수.</summary>
    public IReadOnlyDictionary<string, int> Stash { get; init; } = new Dictionary<string, int>();

    public int NextSeed { get; init; }

    public int NextRecruitNumber { get; init; } = 1;

    public IReadOnlyList<RecruitOffer> RecruitOffers { get; init; } = [];

    /// <summary>진행 중인 원정. 마을에 있으면 없다.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SavedExpedition? Expedition { get; init; }
}

public sealed record SavedExpedition
{
    public required string ZoneId { get; init; }
    public required int Seed { get; init; }
    public required int BattleIndex { get; init; }
    public required IReadOnlyList<ExpeditionMember> Members { get; init; }
    public int CarriedGold { get; init; }
    public IReadOnlyDictionary<string, int> CarriedItems { get; init; } = new Dictionary<string, int>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleReport? LastBattle { get; init; }

    public IReadOnlyList<string> Deaths { get; init; } = [];
}

public sealed record SavedMember
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Stats Stats { get; init; }
    public required Row Row { get; init; }

    /// <summary>부위별 장착 아이템 ID (버전 6부터).</summary>
    public IReadOnlyDictionary<EquipmentSlot, string> Equipment { get; init; } = new Dictionary<EquipmentSlot, string>();

    /// <summary>버전 6의 행동 칸 선택. 버전 7부터 아이템 행동을 모두 쓰므로 읽을 때 버린다.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<EquipmentSlot, IReadOnlyList<string>>? AbilityChoices { get; init; }

    /// <summary>버전 5의 무기·방어구 아이템 ID. 읽을 때만 쓰고 주무기·몸통으로 옮긴다.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Weapon { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Armor { get; init; }

    /// <summary>숙련 ID별 누적 경험치.</summary>
    public IReadOnlyDictionary<string, int> MasteryXp { get; init; } = new Dictionary<string, int>();

    /// <summary>배운 스킬의 레벨.</summary>
    public IReadOnlyDictionary<string, int> SkillLevels { get; init; } = new Dictionary<string, int>();

    /// <summary>전술 세트들 (회사의 tacticSetNames 순서).</summary>
    public IReadOnlyList<IReadOnlyList<Tactic>> TacticSets { get; init; } = [];
}

/// <summary>세이브를 읽거나 검증하다 실패했다. 발견한 오류를 모두 담는다.</summary>
public sealed class SaveGameException(IReadOnlyList<string> errors)
    : Exception("Invalid save:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>
/// 회사와 세이브 JSON 사이의 변환. 읽을 때 게임 데이터와 맞는지 검증한다.
/// 요구 스킬을 못 채운 행동이 든 전술과 착용 조건을 못 채운 장비는 오류로 보지 않는다(게임 데이터가 바뀌었을 수 있다).
/// 편집 화면이 그것을 표시하고 고칠 때까지 전투를 막는다.
/// </summary>
public static class SaveGame
{
    /// <summary>
    /// 4: 전술 세트 두 벌과 사용할 세트 (2026-10-02).
    /// 5: 회사 (로스터, 출전 명단, 골드, 창고, 모집 후보, 진행 중인 원정), 장비는 아이템 ID (2026-10-02).
    /// 6: 부위 5개 장비(equipment)와 행동 칸 선택(abilityChoices) (2026-10-02).
    ///    버전 5는 무기를 주무기로, 방어구를 몸통으로 옮기고 행동 칸은 첫 옵션으로 고른다. 그 이전은 읽지 않는다.
    /// 7: 무기 세분화로 지팡이 계열이 원소 계열이 되고, 행동 칸 선택이 없어짐 (2026-10-06). 버전 6의 staff 경험치는 fire로,
    ///    magic_control은 pyromancy로 옮기고 mana_efficiency는 지운다(포인트는 돌려받는다). abilityChoices는 버린다.
    ///    2026-10-10: 전술 세트 수 제한(2벌)을 없애고 세트 이름(tacticSetNames)을 덧붙였다. 이전 파일은 그대로 읽히므로
    ///    버전은 올리지 않았다(이름이 없으면 "세트 1", "세트 2").
    /// </summary>
    public const int CurrentVersion = 7;

    private const int OldestReadableVersion = 5;

    public static string Serialize(Company company)
    {
        var file = new SaveFile
        {
            Version = CurrentVersion,
            ActiveTacticSet = company.ActiveTacticSet,
            TacticSetNames = company.TacticSetNames.ToList(),
            Gold = company.Gold,
            NextSeed = company.NextSeed,
            NextRecruitNumber = company.NextRecruitNumber,
            RecruitOffers = company.RecruitOffers.ToList(),
            Expedition = company.Expedition is { } e
                ? new SavedExpedition
                {
                    ZoneId = e.ZoneId,
                    Seed = e.Seed,
                    BattleIndex = e.BattleIndex,
                    Members = e.Members.ToList(),
                    CarriedGold = e.CarriedGold,
                    CarriedItems = new SortedDictionary<string, int>(e.CarriedItems.ToDictionary()),
                    LastBattle = e.LastBattle,
                    Deaths = e.Deaths.ToList(),
                }
                : null,
            Stash = new SortedDictionary<string, int>(company.Stash.ToDictionary()),
            Lineup = company.Lineup.ToList(),
            Roster = company.Roster.Select(m => new SavedMember
            {
                Id = m.Id,
                Name = m.Name,
                Stats = m.Stats,
                Row = m.Row,
                Equipment = new SortedDictionary<EquipmentSlot, string>(m.Equipment.ToDictionary()),
                MasteryXp = new SortedDictionary<string, int>(m.MasteryXp.ToDictionary()),
                SkillLevels = new SortedDictionary<string, int>(m.SkillLevels.ToDictionary()),
                TacticSets = m.TacticSets.Select(set => (IReadOnlyList<Tactic>)set.ToList()).ToList(),
            }).ToList(),
        };
        return JsonSerializer.Serialize(file, GameDataJson.Options);
    }

    public static Company Deserialize(string json, GameData data)
    {
        // 버전을 먼저 본다: 읽지 않는 버전의 필드를 형식 오류로 보고하지 않도록.
        var version = ReadVersion(json);
        if (version is < OldestReadableVersion or > CurrentVersion)
        {
            throw new SaveGameException([$"save: unsupported version {version} (expected {OldestReadableVersion}-{CurrentVersion})"]);
        }

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

        if (file.Version == 5)
        {
            file = ConvertFromVersion5(file);
        }

        if (file.Version == 6)
        {
            file = ConvertFromVersion6(file);
        }

        Validate(file, data);

        var saved = file.Expedition;
        return new Company(
            file.Roster.Select(m =>
                new PartyMember(m.Id, m.Name, m.Stats, m.Row, m.Equipment, m.MasteryXp, m.SkillLevels, m.TacticSets)),
            file.Lineup,
            file.Gold,
            file.Stash,
            file.ActiveTacticSet,
            file.NextSeed,
            file.RecruitOffers,
            file.NextRecruitNumber,
            saved is null
                ? null
                : new Expedition(saved.ZoneId, saved.Seed, saved.BattleIndex, saved.Members, saved.CarriedGold, saved.CarriedItems, saved.LastBattle, saved.Deaths),
            TacticSetNames(file));
    }

    private static int ReadVersion(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new SaveGameException(["save: expected an object"]);
            }

            return document.RootElement.TryGetProperty("version", out var v) && v.TryGetInt32(out var version)
                ? version
                : throw new SaveGameException(["save: 'version' is missing or not an integer"]);
        }
        catch (JsonException e)
        {
            var line = e.LineNumber is { } n ? $" line {n + 1}" : "";
            throw new SaveGameException([$"save{line}: {e.Message}"]);
        }
    }

    /// <summary>
    /// 버전 5 → 6: 무기 아이템은 주무기로, 방어구 아이템은 몸통으로 옮긴다. v5 기본 아이템 ID는 새 T1 아이템 ID와 같다.
    /// 행동 칸 선택은 비워 두면 첫 옵션이 된다. 이제 쓸 수 없는 행동이 든 전술은 잠긴다(편집 화면에서 고친다).
    /// </summary>
    private static SaveFile ConvertFromVersion5(SaveFile file)
    {
        static Dictionary<EquipmentSlot, string> ToEquipment(SavedMember m)
        {
            var equipment = new Dictionary<EquipmentSlot, string>();
            if (m.Weapon is not null)
            {
                equipment[EquipmentSlot.MainHand] = m.Weapon;
            }

            if (m.Armor is not null)
            {
                equipment[EquipmentSlot.Body] = m.Armor;
            }

            return equipment;
        }

        return file with
        {
            Version = 6,
            Roster = file.Roster.Select(m => m with { Equipment = ToEquipment(m), Weapon = null, Armor = null }).ToList(),
        };
    }

    /// <summary>버전 6 → 7: 사라진 숙련·스킬 ID를 새 ID로 옮기고 행동 칸 선택을 버린다.</summary>
    private static SaveFile ConvertFromVersion6(SaveFile file)
    {
        static Dictionary<string, int> Rename(IReadOnlyDictionary<string, int> values, IReadOnlyDictionary<string, string?> renames)
        {
            var result = new Dictionary<string, int>();
            foreach (var (id, value) in values)
            {
                var newId = renames.TryGetValue(id, out var renamed) ? renamed : id;
                if (newId is not null)
                {
                    result[newId] = result.GetValueOrDefault(newId) + value;
                }
            }

            return result;
        }

        var masteries = new Dictionary<string, string?> { ["staff"] = "fire" };
        var skills = new Dictionary<string, string?> { ["magic_control"] = "pyromancy", ["mana_efficiency"] = null };
        return file with
        {
            Version = CurrentVersion,
            Roster = file.Roster.Select(m => m with
            {
                MasteryXp = Rename(m.MasteryXp, masteries),
                SkillLevels = Rename(m.SkillLevels, skills),
                AbilityChoices = null,
            }).ToList(),
        };
    }

    /// <summary>
    /// 세트 이름. 이름이 없는 파일(2026-10-10 이전)은 세트가 늘 두 벌이었으므로 두 벌 이상(멤버가 가진 만큼)으로 본다.
    /// </summary>
    private static IReadOnlyList<string> TacticSetNames(SaveFile file) =>
        file.TacticSetNames
        ?? Enumerable.Range(1, Math.Max(2, file.Roster.Select(m => m.TacticSets.Count).DefaultIfEmpty(0).Max()))
            .Select(Company.DefaultTacticSetName)
            .ToList();

    private static void Validate(SaveFile file, GameData data)
    {
        var errors = new List<string>();

        var setCount = TacticSetNames(file).Count;
        if (file.TacticSetNames is { Count: 0 })
        {
            errors.Add("save: tacticSetNames needs at least one set");
        }

        foreach (var name in file.TacticSetNames ?? [])
        {
            DataValidation.RequireText(name, "save: tactic set name", errors);
            if (name.Length > Company.MaxTacticSetNameLength)
            {
                errors.Add($"save: tactic set name '{name}' is longer than {Company.MaxTacticSetNameLength} characters");
            }
        }

        if (setCount > 0 && (file.ActiveTacticSet < 0 || file.ActiveTacticSet >= setCount))
        {
            errors.Add($"save: activeTacticSet must be 0-{setCount - 1}, got {file.ActiveTacticSet}");
        }

        foreach (var member in file.Roster.Where(m => m.TacticSets.Count > setCount))
        {
            errors.Add($"save member '{member.Id}': has {member.TacticSets.Count} tactic sets but the company has {setCount}");
        }

        DataValidation.RequireNonNegative(file.Gold, "save: gold", errors);

        foreach (var (itemId, count) in file.Stash)
        {
            if (!data.Items.ContainsKey(itemId))
            {
                errors.Add($"save: stash has unknown item '{itemId}'");
            }

            if (count < 1)
            {
                errors.Add($"save: stash count of '{itemId}' must be at least 1, got {count}");
            }
        }

        foreach (var duplicate in file.Roster.GroupBy(m => m.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"save: duplicate member id '{duplicate.Key}'");
        }

        if (file.Lineup.Count > Company.MaxLineup)
        {
            errors.Add($"save: lineup has {file.Lineup.Count} members (at most {Company.MaxLineup})");
        }

        foreach (var duplicate in file.Lineup.GroupBy(id => id).Where(g => g.Count() > 1))
        {
            errors.Add($"save: lineup lists '{duplicate.Key}' more than once");
        }

        foreach (var id in file.Lineup.Where(id => file.Roster.All(m => m.Id != id)))
        {
            errors.Add($"save: lineup member '{id}' is not in the roster");
        }

        foreach (var member in file.Roster)
        {
            ValidateMember(member, data, errors);
        }

        if (file.NextRecruitNumber < 1)
        {
            errors.Add($"save: nextRecruitNumber must be at least 1, got {file.NextRecruitNumber}");
        }

        foreach (var offer in file.RecruitOffers)
        {
            if (!data.Recruits.ContainsKey(offer.TemplateId))
            {
                errors.Add($"save: recruit offer '{offer.Name}' has unknown template '{offer.TemplateId}'");
            }

            DataValidation.RequireText(offer.Name, "save: recruit offer name", errors);
            DataValidation.ValidateStats(offer.Stats, $"save recruit offer '{offer.Name}'", errors);
            DataValidation.RequireNonNegative(offer.Price, $"save recruit offer '{offer.Name}': price", errors);
        }

        if (file.Expedition is { } expedition)
        {
            ValidateExpedition(expedition, file, data, errors);
        }

        if (errors.Count > 0)
        {
            throw new SaveGameException(errors);
        }
    }

    /// <summary>원정 상태의 일관성: 지역, 전투 번호, 멤버(로스터와 출전 명단에 있는지), 전리품.</summary>
    private static void ValidateExpedition(SavedExpedition expedition, SaveFile file, GameData data, List<string> errors)
    {
        const string at = "save expedition";
        if (!data.Zones.TryGetValue(expedition.ZoneId, out var zone))
        {
            errors.Add($"{at}: unknown zone '{expedition.ZoneId}'");
        }
        else if (expedition.BattleIndex < 0 || expedition.BattleIndex >= zone.MaxBattles)
        {
            errors.Add($"{at}: battleIndex must be 0-{zone.MaxBattles - 1}, got {expedition.BattleIndex}");
        }

        if (expedition.Members.All(m => m.Down))
        {
            errors.Add($"{at}: needs at least one standing member");
        }

        foreach (var duplicate in expedition.Members.GroupBy(m => m.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"{at}: duplicate member '{duplicate.Key}'");
        }

        foreach (var member in expedition.Members)
        {
            if (!file.Lineup.Contains(member.Id))
            {
                errors.Add($"{at}: member '{member.Id}' is not in the lineup");
            }

            DataValidation.RequireNonNegative(member.Hp, $"{at} member '{member.Id}': hp", errors);
            DataValidation.RequireNonNegative(member.Mp, $"{at} member '{member.Id}': mp", errors);
        }

        foreach (var id in file.Lineup.Where(id => expedition.Members.All(m => m.Id != id)))
        {
            errors.Add($"{at}: lineup member '{id}' is not on the expedition");
        }

        DataValidation.RequireNonNegative(expedition.CarriedGold, $"{at}: carriedGold", errors);
        foreach (var (itemId, count) in expedition.CarriedItems)
        {
            if (!data.Items.ContainsKey(itemId))
            {
                errors.Add($"{at}: carries unknown item '{itemId}'");
            }

            if (count < 1)
            {
                errors.Add($"{at}: count of '{itemId}' must be at least 1, got {count}");
            }
        }
    }

    private static void ValidateMember(SavedMember member, GameData data, List<string> errors)
    {
        var at = $"save member '{member.Id}'";
        DataValidation.RequireText(member.Id, "save: member id", errors);
        DataValidation.RequireText(member.Name, $"{at}: name", errors);
        DataValidation.ValidateStats(member.Stats, at, errors);
        if (member.Weapon is not null || member.Armor is not null)
        {
            errors.Add($"{at}: 'weapon' and 'armor' are version 5 fields; use 'equipment'");
        }

        DataValidation.ValidateEquipped(member.Equipment, $"{at}: equipment", data.Items, errors);
        if (member.AbilityChoices is not null)
        {
            errors.Add($"{at}: 'abilityChoices' is a version 6 field; items now grant all their actions");
        }

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

        for (var set = 0; set < member.TacticSets.Count; set++)
        {
            foreach (var tactic in member.TacticSets[set])
            {
                DataValidation.ValidateTactic(tactic, $"{at} set {set + 1} tactic {tactic.Priority}", data.Actions, errors);
            }
        }
    }
}
