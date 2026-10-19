using System.Text.Json;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Balance;

/// <summary>
/// 전술 측정(<see cref="TacticBench"/>)의 정의 파일(`tools/bench/bench.json`). 기준 파티, 성장 상태, 전술 세트, 맞대결 목록이다.
/// 게임 데이터가 아니라 설계 도구의 입력이다. 게임 데이터와 같은 JSON 설정(<see cref="GameDataJson.Options"/>)으로 읽는다.
/// </summary>
public sealed record BenchDefinition
{
    public string? Description { get; init; }

    /// <summary>기준 파티 (최대 5명). 성장 상태가 이 시작 상태에서 장비와 숙련을 올린다.</summary>
    public required IReadOnlyList<BenchMember> Party { get; init; }

    public required IReadOnlyList<BenchState> States { get; init; }

    /// <summary>전술 세트. 앞의 세트를 <see cref="BenchSet.Base"/>로 이어받을 수 있다.</summary>
    public required IReadOnlyList<BenchSet> Sets { get; init; }

    public required IReadOnlyList<BenchMatchup> Matchups { get; init; }

    /// <summary>
    /// 게임 데이터 밖의 추가 데이터 폴더 (이 파일 기준 상대 경로). 그 안의 encounters.json, effects.json 등은 게임 데이터 뒤에
    /// 붙는다(<see cref="GameDataLoader.LoadDirectory"/>). 어느 지역에도 넣기 전의 시제품 적과 그 적만 쓰는 타고난 효과를
    /// 게임에 싣지 않고 재려는 것이다. 지역에 넣기로 하면 data/로 옮긴다.
    /// </summary>
    public string? ExtraData { get; init; }

    public BenchState State(string id) => States.First(s => s.Id == id);

    /// <summary>세트의 멤버별 전술. 세트에 없는 멤버는 바탕 세트를 따른다.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Tactic>> TacticsOf(string setId)
    {
        var set = Sets.First(s => s.Id == setId);
        var tactics = set.Base is null ? new Dictionary<string, IReadOnlyList<Tactic>>() : TacticsOf(set.Base).ToDictionary();
        foreach (var (memberId, list) in set.Tactics)
        {
            tactics[memberId] = list;
        }

        return tactics;
    }

    /// <summary>세트의 멤버별 장비 교체 (부위 → 아이템 ID, 빈 문자열이면 그 부위를 비운다). 바탕 세트의 교체를 이어받는다.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<EquipmentSlot, string>> EquipmentOf(string setId)
    {
        var set = Sets.First(s => s.Id == setId);
        var equipment = set.Base is null
            ? new Dictionary<string, IReadOnlyDictionary<EquipmentSlot, string>>()
            : EquipmentOf(set.Base).ToDictionary();
        foreach (var (memberId, slots) in set.Equipment)
        {
            var merged = equipment.GetValueOrDefault(memberId)?.ToDictionary() ?? [];
            foreach (var (slot, itemId) in slots)
            {
                merged[slot] = itemId;
            }

            equipment[memberId] = merged;
        }

        return equipment;
    }

    /// <summary>
    /// 측정 정의와, 그 정의의 추가 데이터(<see cref="ExtraData"/>)를 더한 게임 데이터를 읽는다.
    /// 형식이나 참조가 틀리면 <see cref="GameDataException"/>을 던진다.
    /// </summary>
    public static (BenchDefinition Bench, GameData Data) Load(string path, string dataDirectory)
    {
        BenchDefinition? bench;
        try
        {
            bench = JsonSerializer.Deserialize<BenchDefinition>(File.ReadAllText(path), GameDataJson.Options);
        }
        catch (JsonException e)
        {
            throw new GameDataException([$"{Path.GetFileName(path)}: {e.Message}"]);
        }

        if (bench is null)
        {
            throw new GameDataException([$"{Path.GetFileName(path)}: empty"]);
        }

        var extra = bench.ExtraData is null ? null : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, bench.ExtraData);
        var data = GameDataLoader.LoadDirectory(dataDirectory, extra);
        var errors = bench.Validate(data);
        if (errors.Count > 0)
        {
            throw new GameDataException(errors.Select(e => $"{Path.GetFileName(path)}: {e}").ToList());
        }

        return (bench, data);
    }

    /// <summary>게임 데이터와 맞는지 본다: 없는 아이템·숙련·스킬·행동·적 조합, 겹친 ID, 세트 순환, 없는 상태·세트.</summary>
    public IReadOnlyList<string> Validate(GameData data)
    {
        var errors = new List<string>();
        if (Party.Count is 0 or > Runs.Run.MaxMembers)
        {
            errors.Add($"party: 1 to {Runs.Run.MaxMembers} members");
        }

        Duplicates(Party.Select(m => m.Id), "party", errors);
        foreach (var member in Party)
        {
            var at = $"party.{member.Id}";
            foreach (var (slot, itemId) in member.Equipment)
            {
                if (!data.Items.TryGetValue(itemId, out var item))
                {
                    errors.Add($"{at}: unknown item '{itemId}'");
                }
                else if (item.Slot != slot)
                {
                    errors.Add($"{at}: '{itemId}' is not a {slot} item");
                }
            }

            errors.AddRange(member.Masteries.Keys.Where(id => !data.Masteries.ContainsKey(id)).Select(id => $"{at}: unknown mastery '{id}'"));
            errors.AddRange(member.Skills.Keys.Where(id => !data.Skills.ContainsKey(id)).Select(id => $"{at}: unknown skill '{id}'"));
        }

        Duplicates(States.Select(s => s.Id), "states", errors);
        Duplicates(Sets.Select(s => s.Id), "sets", errors);
        var memberIds = Party.Select(m => m.Id).ToHashSet();
        for (var i = 0; i < Sets.Count; i++)
        {
            var set = Sets[i];
            if (set.Base is not null && Sets.Take(i).All(s => s.Id != set.Base))
            {
                errors.Add($"sets.{set.Id}: base '{set.Base}' must be an earlier set");
            }

            foreach (var (memberId, tactics) in set.Tactics)
            {
                if (!memberIds.Contains(memberId))
                {
                    errors.Add($"sets.{set.Id}: unknown member '{memberId}'");
                }

                foreach (var tactic in tactics)
                {
                    DataValidation.ValidateTactic(tactic, $"sets.{set.Id}.{memberId}", data.Actions, errors);
                }
            }

            foreach (var (memberId, slots) in set.Equipment)
            {
                if (!memberIds.Contains(memberId))
                {
                    errors.Add($"sets.{set.Id}: unknown member '{memberId}' in equipment");
                }

                foreach (var (slot, itemId) in slots.Where(p => p.Value != ""))
                {
                    if (!data.Items.TryGetValue(itemId, out var item))
                    {
                        errors.Add($"sets.{set.Id}.equipment.{memberId}: unknown item '{itemId}'");
                    }
                    else if (item.Slot != slot)
                    {
                        errors.Add($"sets.{set.Id}.equipment.{memberId}: '{itemId}' is not a {slot} item");
                    }
                }
            }
        }

        var stateIds = States.Select(s => s.Id).ToHashSet();
        var setIds = Sets.Select(s => s.Id).ToHashSet();
        foreach (var matchup in Matchups)
        {
            var at = $"matchups.{matchup.EncounterId}";
            if (!data.Encounters.ContainsKey(matchup.EncounterId))
            {
                errors.Add($"{at}: unknown encounter");
            }

            if (!stateIds.Contains(matchup.State))
            {
                errors.Add($"{at}: unknown state '{matchup.State}'");
            }

            errors.AddRange(matchup.Counters.Where(id => !setIds.Contains(id)).Select(id => $"{at}: unknown counter set '{id}'"));
        }

        return errors;
    }

    private static void Duplicates(IEnumerable<string> ids, string at, List<string> errors) =>
        errors.AddRange(ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => $"{at}: duplicate id '{g.Key}'"));
}

public sealed record BenchMember(string Id, string Name, Stats Stats, Row Row, IReadOnlyDictionary<EquipmentSlot, string> Equipment)
{
    /// <summary>숙련 ID별 시작 레벨.</summary>
    public IReadOnlyDictionary<string, int> Masteries { get; init; } = new Dictionary<string, int>();

    /// <summary>스킬 ID별 시작 레벨. 숙련 레벨이 주는 포인트 안이어야 한다.</summary>
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();
}

/// <summary>성장 상태. 티어가 0이면 시작 상태 그대로다(<see cref="Growth"/>를 쓰지 않는다).</summary>
public sealed record BenchState(string Id, string Name, int Tier, int MasteryLevel);

/// <param name="Tactics">멤버 ID별 전술.</param>
/// <param name="Base">이 세트가 이어받는 세트 ID. <paramref name="Tactics"/>에 적은 멤버의 전술만 바꾼다.</param>
public sealed record BenchSet(string Id, string Name, IReadOnlyDictionary<string, IReadOnlyList<Tactic>> Tactics, string? Base = null)
{
    public string? Description { get; init; }

    /// <summary>
    /// 멤버 ID별 장비 교체 (부위 → 아이템 ID, 빈 문자열이면 그 부위를 비운다). 출정 전에 장비를 바꾸는 대응을 재려는 것이다.
    /// 성장 상태는 바꾼 장비로 숙련과 패시브를 올린다.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<EquipmentSlot, string>> Equipment { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<EquipmentSlot, string>>();
}

public sealed record BenchMatchup(string EncounterId, string State)
{
    /// <summary>이 적에 맞춘 대응 세트 ID. 해법이 여럿이면 모두 적는다.</summary>
    public IReadOnlyList<string> Counters { get; init; } = [];
}
