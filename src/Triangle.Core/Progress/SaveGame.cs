using System.Text.Json;
using System.Text.Json.Serialization;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
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

    /// <summary>버전 4의 파티. 읽을 때만 쓰고 로스터와 출전 명단으로 옮긴다.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SavedMember>? Party { get; init; }

    /// <summary>보유한 캐릭터 모두 (버전 5부터).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SavedMember>? Roster { get; init; }

    /// <summary>출전할 캐릭터 ID (버전 5부터).</summary>
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

    /// <summary>장착한 아이템 ID. 버전 4에서는 계열 ID다.</summary>
    public string? Weapon { get; init; }
    public string? Armor { get; init; }

    /// <summary>숙련 ID별 누적 경험치.</summary>
    public IReadOnlyDictionary<string, int> MasteryXp { get; init; } = new Dictionary<string, int>();

    /// <summary>배운 스킬의 레벨.</summary>
    public IReadOnlyDictionary<string, int> SkillLevels { get; init; } = new Dictionary<string, int>();

    /// <summary>전술 세트들.</summary>
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
/// 요구 스킬을 못 채운 행동이 든 전술은 오류로 보지 않는다(게임 데이터가 바뀌었을 수 있다).
/// 편집 화면이 그 전술을 표시하고 고칠 때까지 전투를 막는다.
/// </summary>
public static class SaveGame
{
    /// <summary>
    /// 4: 전술 세트 두 벌과 사용할 세트 (2026-10-02).
    /// 5: 회사 (로스터, 출전 명단, 골드, 창고, 모집 후보, 진행 중인 원정), 장비는 아이템 ID (2026-10-02).
    ///    버전 4는 파티를 로스터와 출전 명단으로, 장비 계열을 그 계열의 기본 아이템으로 바꾸고 시작 골드를 준다.
    ///    그 이전은 읽지 않는다.
    /// </summary>
    public const int CurrentVersion = 5;

    private const int OldestReadableVersion = 4;

    /// <summary>버전 4를 변환할 때 쓰는 시드 (그 세이브에는 시드가 없다).</summary>
    private const int ConvertedSeed = 1;

    public static string Serialize(Company company)
    {
        var file = new SaveFile
        {
            Version = CurrentVersion,
            ActiveTacticSet = company.ActiveTacticSet,
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
                Weapon = m.Weapon,
                Armor = m.Armor,
                MasteryXp = new SortedDictionary<string, int>(m.MasteryXp.ToDictionary()),
                SkillLevels = new SortedDictionary<string, int>(m.SkillLevels.ToDictionary()),
                TacticSets = m.TacticSets.Select(set => (IReadOnlyList<Tactic>)set.ToList()).ToList(),
            }).ToList(),
        };
        return JsonSerializer.Serialize(file, GameDataJson.Options);
    }

    public static Company Deserialize(string json, GameData data)
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

        if (file.Version is < OldestReadableVersion or > CurrentVersion)
        {
            throw new SaveGameException([$"save: unsupported version {file.Version} (expected {OldestReadableVersion}-{CurrentVersion})"]);
        }

        var converted = file.Version == 4;
        if (converted)
        {
            file = ConvertFromVersion4(file, data);
        }

        Validate(file, data);

        var saved = file.Expedition;
        var company = new Company(
            file.Roster!.Select(m =>
                new PartyMember(m.Id, m.Name, m.Stats, m.Row, m.Weapon, m.Armor, m.MasteryXp, m.SkillLevels, m.TacticSets)),
            file.Lineup,
            file.Gold,
            file.Stash,
            file.ActiveTacticSet,
            file.NextSeed,
            file.RecruitOffers,
            file.NextRecruitNumber,
            saved is null
                ? null
                : new Expedition(saved.ZoneId, saved.Seed, saved.BattleIndex, saved.Members, saved.CarriedGold, saved.CarriedItems, saved.LastBattle, saved.Deaths));
        if (converted)
        {
            company.RerollRecruits(data);
        }

        return company;
    }

    /// <summary>
    /// 버전 4 → 5: 파티 멤버는 로스터로, 앞의 <see cref="Company.MaxLineup"/>명은 출전 명단으로.
    /// 장비 계열은 그 계열의 기본 아이템으로 바꾼다. 골드는 시작값을 준다.
    /// </summary>
    private static SaveFile ConvertFromVersion4(SaveFile file, GameData data)
    {
        var errors = new List<string>();
        if (file.Roster is not null)
        {
            errors.Add("save: 'roster' is a version 5 field; version 4 uses 'party'");
        }

        var party = file.Party ?? [];

        string? ToItem(string? masteryId, EquipmentSlot slot, string at)
        {
            if (masteryId is null)
            {
                return null;
            }

            DataValidation.RequireSlot(masteryId, slot, at, data.Masteries, errors);
            if (!data.Masteries.ContainsKey(masteryId))
            {
                return null;
            }

            var item = data.BasicItemFor(masteryId);
            if (item is null)
            {
                errors.Add($"{at}: no item of mastery '{masteryId}' to convert to");
            }

            return item?.Id;
        }

        var roster = party.Select(m => m with
        {
            Weapon = ToItem(m.Weapon, EquipmentSlot.Weapon, $"save member '{m.Id}': weapon"),
            Armor = ToItem(m.Armor, EquipmentSlot.Armor, $"save member '{m.Id}': armor"),
        }).ToList();

        if (party.Count == 0)
        {
            errors.Add("save: party is empty");
        }

        if (errors.Count > 0)
        {
            throw new SaveGameException(errors);
        }

        return new SaveFile
        {
            Version = CurrentVersion,
            ActiveTacticSet = file.ActiveTacticSet,
            Roster = roster,
            Lineup = roster.Select(m => m.Id).Distinct().Take(Company.MaxLineup).ToList(),
            Gold = Company.StartingGold,
            NextSeed = ConvertedSeed,
        };
    }

    private static void Validate(SaveFile file, GameData data)
    {
        var errors = new List<string>();
        if (file.Party is not null)
        {
            errors.Add("save: 'party' is a version 4 field; use 'roster' and 'lineup'");
        }

        if (file.Roster is null)
        {
            errors.Add("save: 'roster' is missing");
            throw new SaveGameException(errors);
        }

        if (file.ActiveTacticSet is < 0 or >= PartyMember.TacticSetCount)
        {
            errors.Add($"save: activeTacticSet must be 0-{PartyMember.TacticSetCount - 1}, got {file.ActiveTacticSet}");
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
        DataValidation.ValidateItem(member.Weapon, EquipmentSlot.Weapon, $"{at}: weapon", data.Items, data.Masteries, errors);
        DataValidation.ValidateItem(member.Armor, EquipmentSlot.Armor, $"{at}: armor", data.Items, data.Masteries, errors);

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

        if (member.TacticSets.Count > PartyMember.TacticSetCount)
        {
            errors.Add($"{at}: at most {PartyMember.TacticSetCount} tactic sets, got {member.TacticSets.Count}");
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
