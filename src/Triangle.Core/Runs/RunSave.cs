using System.Text.Json;
using System.Text.Json.Serialization;
using Triangle.Core.Data;
using Triangle.Core.Tactics;

namespace Triangle.Core.Runs;

/// <summary>진행 중인 판의 세이브 JSON. 판의 상태를 그대로 담는다(지도도 담아 데이터가 바뀌어도 같은 지도로 이어 간다).</summary>
public sealed record RunSaveFile
{
    public required int Version { get; init; }

    public required int Seed { get; init; }

    public required RunPhase Phase { get; init; }

    public int Act { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Position { get; init; }

    /// <summary>이 막에서 지나온 지점 ID.</summary>
    public IReadOnlyList<int> Path { get; init; } = [];

    public required IReadOnlyList<RunMap> Maps { get; init; }

    public required IReadOnlyList<string> Bosses { get; init; }

    public required IReadOnlyList<SavedRunMember> Members { get; init; }

    /// <summary>스킬 사본 (행동 ID → 장 수). 강화된 사본은 강화 ID(<c>fireball+power</c>)로 따로 담는다.</summary>
    public IReadOnlyDictionary<string, int> Copies { get; init; } = new Dictionary<string, int>();

    /// <summary>상점에서 산 줄 상한 수.</summary>
    public int LinesBought { get; init; }

    public IReadOnlyList<string> Bag { get; init; } = [];

    public int Gold { get; init; }

    public int Fights { get; init; }

    public int Step { get; init; }

    public IReadOnlyList<RunCard> Offer { get; init; } = [];

    public IReadOnlyList<RunStock> Stock { get; init; } = [];

    /// <summary>지금 이벤트의 ID (<see cref="RunPhase.Event"/>일 때).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Event { get; init; }

    /// <summary>막 보스를 이기고 보상을 고르는 중이다 (고르면 다음 막으로 간다).</summary>
    public bool BossCleared { get; init; }

    /// <summary>이 판에 나오지 않는 클래스·카드·아이템 (판을 시작할 때의 해금).</summary>
    public IReadOnlyList<string> Locked { get; init; } = [];

    /// <summary>연 해금이 이 판에 주는 편의 (판을 시작할 때의 해금). 없으면(예전 세이브) 편의가 없다.</summary>
    public RunBoons Boons { get; init; } = new();

    /// <summary>치른 전투.</summary>
    public IReadOnlyList<RunBattleRecord> History { get; init; } = [];
}

public sealed record SavedRunMember
{
    public required string Id { get; init; }

    public required string ClassId { get; init; }

    /// <summary>서는 줄. 없으면(줄을 고르기 전 세이브) 클래스의 처음 값.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Units.Row? Row { get; init; }

    public int Earned { get; init; }

    public int Sealed { get; init; }

    public bool Dead { get; init; }

    public IReadOnlyDictionary<string, int> Passives { get; init; } = new Dictionary<string, int>();

    public IReadOnlyList<Tactic> Tactics { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Weapon { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Armor { get; init; }

    public IReadOnlyList<string> Trinkets { get; init; } = [];
}

public sealed partial class Run
{
    /// <summary>1: 처음 (2026-10-19).</summary>
    public const int SaveVersion = 1;

    /// <summary>
    /// 막 수가 바뀐 게임 데이터로 예전 판을 이어 할 때의 오류 머리말 (막 3개 → 5개, 2026-10-20). 지도는 판을 시작할 때 막마다 만들어
    /// 세이브에 담으므로, 막 수가 다르면 마지막 막 다음에 갈 지도가 없거나 남는다. 화면은 이 머리말로 "게임이 바뀌어 새로 시작한다"를 알린다.
    /// </summary>
    public const string ActCountChanged = "run save: the game now has a different number of acts";

    public string Serialize() => JsonSerializer.Serialize(ToSave(), GameDataJson.Options);

    public RunSaveFile ToSave() => new()
    {
        Version = SaveVersion,
        Seed = Seed,
        Phase = Phase,
        Act = Act,
        Position = Position,
        Path = Path.ToList(),
        Maps = Maps,
        Bosses = Bosses,
        Members = Members.Select(m => new SavedRunMember
        {
            Id = m.Id,
            ClassId = m.ClassId,
            Row = m.Row,
            Earned = m.Earned,
            Sealed = m.Sealed,
            Dead = m.Dead,
            Passives = new SortedDictionary<string, int>(m.Passives, StringComparer.Ordinal),
            Tactics = m.Tactics.ToList(),
            Weapon = m.Weapon,
            Armor = m.Armor,
            Trinkets = m.Trinkets.ToList(),
        }).ToList(),
        Copies = new SortedDictionary<string, int>(Copies, StringComparer.Ordinal),
        LinesBought = LinesBought,
        Bag = Bag.ToList(),
        Gold = Gold,
        Fights = Fights,
        Step = Step,
        Offer = Offer,
        Stock = Stock.ToList(),
        Event = Event?.Id,
        BossCleared = _bossCleared,
        Locked = Locked.Order(StringComparer.Ordinal).ToList(),
        Boons = Boons,
        History = History.ToList(),
    };

    /// <summary>세이브 JSON을 읽는다. 형식이 틀리거나 게임 데이터와 맞지 않으면 <see cref="SaveGameException"/>.</summary>
    public static Run Deserialize(string json, GameData data)
    {
        RunSaveFile? file;
        try
        {
            file = JsonSerializer.Deserialize<RunSaveFile>(json, GameDataJson.Options);
        }
        catch (JsonException e)
        {
            throw new SaveGameException([$"run save: {e.Message}"]);
        }

        return Restore(file ?? throw new SaveGameException(["run save: empty"]), data);
    }

    /// <summary>세이브에서 판을 되살린다. 클래스·아이템·행동·패시브·적 조합·이벤트가 지금 데이터에 있는지와 진행 위치를 검사한다.</summary>
    public static Run Restore(RunSaveFile file, GameData data)
    {
        var errors = new List<string>();
        if (file.Version != SaveVersion)
        {
            throw new SaveGameException([$"run save: unsupported version {file.Version} (expected {SaveVersion})"]);
        }

        var rules = data.Run ?? throw new SaveGameException(["run save: the game data has no run.json"]);
        if (file.Maps.Count != file.Bosses.Count || file.Maps.Count == 0)
        {
            errors.Add("run save: maps and bosses must match and not be empty");
        }
        else if (file.Maps.Count != rules.Acts.Count)
        {
            errors.Add($"{ActCountChanged} (save {file.Maps.Count}, game {rules.Acts.Count})");
        }

        if (file.Act < 0 || file.Act >= file.Maps.Count || file.Act >= rules.Acts.Count)
        {
            errors.Add($"run save: act {file.Act} is out of range");
        }
        else if (file.Position is { } at && (at < 0 || at >= file.Maps[file.Act].Nodes.Count))
        {
            errors.Add($"run save: position {at} is not on the map");
        }
        else if (file.Path.Any(id => id < 0 || id >= file.Maps[file.Act].Nodes.Count))
        {
            errors.Add("run save: the path leaves the map");
        }

        errors.AddRange(file.Bosses.Concat(file.History.Select(h => h.EncounterId)).Distinct().Where(id => !data.Encounters.ContainsKey(id))
            .Select(id => $"run save: unknown encounter '{id}'"));
        var actions = Catalog(data).Actions;
        errors.AddRange(file.Copies.Keys.Where(id => !actions.ContainsKey(id)).Select(id => $"run save: unknown action '{id}' in copies"));
        errors.AddRange(file.Bag.Where(id => !data.Items.ContainsKey(id)).Select(id => $"run save: unknown item '{id}' in bag"));
        foreach (var m in file.Members)
        {
            var at = $"run save member '{m.Id}'";
            if (!data.Classes.ContainsKey(m.ClassId))
            {
                errors.Add($"{at}: unknown class '{m.ClassId}'");
            }

            errors.AddRange(new[] { m.Weapon, m.Armor }.Concat(m.Trinkets).OfType<string>().Where(id => !data.Items.ContainsKey(id))
                .Select(id => $"{at}: unknown item '{id}'"));
            errors.AddRange(m.Passives.Keys.Where(id => !data.Skills.ContainsKey(id)).Select(id => $"{at}: unknown passive '{id}'"));
            foreach (var tactic in m.Tactics)
            {
                DataValidation.ValidateTactic(tactic, at, actions, errors);
            }
        }

        foreach (var card in file.Offer)
        {
            var known = card.Kind switch
            {
                RunCardKind.Active => card.Id is not null && data.Actions.ContainsKey(card.Id),
                RunCardKind.Passive => card.Id is not null && data.Skills.ContainsKey(card.Id),
                _ => true,
            };
            if (!known || (card.ClassId is not null && !data.Classes.ContainsKey(card.ClassId)))
            {
                errors.Add($"run save: unknown card {card.Kind} '{card.Id}'");
            }
        }

        foreach (var stock in file.Stock)
        {
            var known = stock.Kind switch
            {
                RunStockKind.Item => data.Items.ContainsKey(stock.Id),
                RunStockKind.Skill => data.Actions.ContainsKey(stock.Id),
                RunStockKind.Upgrade => actions.ContainsKey(stock.Id) && stock.Upgrade is not null,
                RunStockKind.Line => true,
                _ => data.Classes.ContainsKey(stock.Id),
            };
            if (!known)
            {
                errors.Add($"run save: unknown {stock.Kind} '{stock.Id}' in the shop");
            }
        }

        var runEvent = file.Event is null ? null : rules.Events.FirstOrDefault(e => e.Id == file.Event);
        if (file.Event is not null && runEvent is null)
        {
            errors.Add($"run save: unknown event '{file.Event}'");
        }

        if (file.Phase == RunPhase.Event && runEvent is null)
        {
            errors.Add("run save: the event phase needs an event");
        }

        if (errors.Count > 0)
        {
            throw new SaveGameException(errors);
        }

        var run = new Run(file.Seed, file.Maps, file.Bosses, file.Gold, file.Locked.ToHashSet(), file.Boons ?? new RunBoons())
        {
            Phase = file.Phase,
            Act = file.Act,
            Position = file.Position,
            Fights = file.Fights,
            Step = file.Step,
            Offer = file.Offer,
            Event = runEvent,
            _bossCleared = file.BossCleared,
            LinesBought = file.LinesBought,
        };
        run._stock.AddRange(file.Stock);
        run._path.AddRange(file.Path);
        run._history.AddRange(file.History);
        run.Bag.AddRange(file.Bag);
        foreach (var (id, count) in file.Copies)
        {
            run.Copies[id] = count;
        }

        run.Members.AddRange(file.Members.Select(m => new RunMember(m.Id, m.ClassId, m.Earned)
        {
            Row = m.Row ?? data.Classes[m.ClassId].Row,
            Sealed = m.Sealed,
            Dead = m.Dead,
            Passives = m.Passives.ToDictionary(),
            Tactics = m.Tactics.ToList(),
            Weapon = m.Weapon,
            Armor = m.Armor,
            Trinkets = m.Trinkets.ToList(),
        }));
        return run;
    }
}

/// <param name="Run">불러온 판. 세이브가 없거나 깨졌으면 null.</param>
public sealed record RunLoadResult(Run? Run, LoadStatus Status, string? BrokenFilePath = null, IReadOnlyList<string>? Errors = null);

/// <summary>진행 중인 판의 세이브 파일 하나. 판이 끝나면 지운다.</summary>
public sealed class RunStore(string path)
{
    public string Path { get; } = path;

    /// <summary>OS별 사용자 설정 폴더 아래 Triangle/run.json.</summary>
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Triangle", "run.json");

    /// <summary>판을 불러온다. 없으면 null(새로 시작), 깨져 있으면 옆에 보관하고 null.</summary>
    public RunLoadResult Load(GameData data)
    {
        if (!File.Exists(Path))
        {
            return new RunLoadResult(null, LoadStatus.NewGame);
        }

        try
        {
            return new RunLoadResult(Run.Deserialize(File.ReadAllText(Path), data), LoadStatus.Loaded);
        }
        catch (SaveGameException e)
        {
            var broken = $"{Path}.broken-{DateTime.Now:yyyyMMdd-HHmmss}";
            for (var i = 2; File.Exists(broken); i++)
            {
                broken = $"{Path}.broken-{DateTime.Now:yyyyMMdd-HHmmss}-{i}";
            }

            File.Move(Path, broken);
            return new RunLoadResult(null, LoadStatus.Recovered, broken, e.Errors);
        }
    }

    /// <summary>임시 파일에 쓴 뒤 교체한다.</summary>
    public void Save(Run run)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = Path + ".tmp";
        File.WriteAllText(temp, run.Serialize());
        File.Move(temp, Path, overwrite: true);
    }

    public void Delete() => File.Delete(Path);
}
