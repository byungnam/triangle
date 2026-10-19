using System.Text.Json;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Progress;

namespace Triangle.Core.Runs;

/// <summary>
/// 판을 넘어 남는 것 (로그라이트 재편, 2026-10-19): 명성, 연 해금, 기록(판 수, 승리, 가장 멀리 간 막, 만난 적).
/// 판이 끝나면 <see cref="Record"/>로 명성과 기록을 받고, 명성을 내고 해금을 연다(<see cref="Unlock"/>).
/// </summary>
public sealed class Profile
{
    private readonly HashSet<string> _unlocked = [];
    private readonly Dictionary<string, EncounterRecord> _encounters = [];

    /// <summary>쓰지 않은 명성.</summary>
    public int Renown { get; private set; }

    /// <summary>지금까지 받은 명성 모두.</summary>
    public int TotalRenown { get; private set; }

    /// <summary>연 해금 ID.</summary>
    public IReadOnlySet<string> Unlocked => _unlocked;

    /// <summary>끝낸 판 수.</summary>
    public int Runs { get; private set; }

    /// <summary>이긴 판 수 (마지막 막의 보스를 이겼다).</summary>
    public int Victories { get; private set; }

    /// <summary>가장 많이 통과한 막 수 (판을 이기면 막 수 전부).</summary>
    public int BestActs { get; private set; }

    /// <summary>탐사 기록: 판에서 만난 적 조합 (인카운터 ID별).</summary>
    public IReadOnlyDictionary<string, EncounterRecord> Encounters => _encounters;

    /// <summary>다음 판을 시작할 때의 해금.</summary>
    public RunUnlocks UnlocksForRun(GameData data) => RunUnlocks.For(data, _unlocked);

    /// <summary>끝난 판이 주는 명성: 이긴 전투마다(보통·정예·보스), 판을 이기면 더.</summary>
    public static int RenownFor(Run run, GameData data)
    {
        var rules = data.Run!.Renown;
        var won = run.History.Where(b => b.Outcome == CombatOutcome.Victory).ToList();
        return won.Sum(b => b.Kind switch
            {
                RunNodeKind.Boss => rules.Boss,
                RunNodeKind.Elite => rules.Elite,
                _ => rules.Fight,
            })
            + (run.Phase == RunPhase.Victory ? rules.Victory : 0);
    }

    /// <summary>통과한 막 수: 이긴 판이면 막 수 전부, 아니면 지금 막.</summary>
    public static int ActsCleared(Run run, GameData data) => run.Phase == RunPhase.Victory ? data.Run!.Acts.Count : run.Act;

    /// <summary>끝난 판을 기록하고 명성을 받는다. 받은 명성을 돌려준다. 판은 한 번만 기록한다(부르는 쪽이 판을 치운다).</summary>
    public int Record(Run run, GameData data)
    {
        if (!run.Finished)
        {
            throw new InvalidOperationException("끝나지 않은 판이다");
        }

        var renown = RenownFor(run, data);
        Renown += renown;
        TotalRenown += renown;
        Runs++;
        Victories += run.Phase == RunPhase.Victory ? 1 : 0;
        BestActs = Math.Max(BestActs, ActsCleared(run, data));
        foreach (var battle in run.History)
        {
            _encounters[battle.EncounterId] = _encounters.GetValueOrDefault(battle.EncounterId, new EncounterRecord(0, 0))
                .Add(battle.Outcome == CombatOutcome.Victory);
        }

        return renown;
    }

    public string? WhyCannotUnlock(GameData data, string unlockId)
    {
        if (!data.Unlocks.TryGetValue(unlockId, out var unlock))
        {
            return "없는 해금이다";
        }

        if (_unlocked.Contains(unlockId))
        {
            return "이미 열었다";
        }

        return Renown < unlock.Cost ? $"명성이 모자란다 ({unlock.Cost})" : null;
    }

    /// <summary>명성을 내고 해금을 연다. 다음 판부터 나온다.</summary>
    public void Unlock(GameData data, string unlockId)
    {
        if (WhyCannotUnlock(data, unlockId) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        Renown -= data.Unlocks[unlockId].Cost;
        _unlocked.Add(unlockId);
    }

    // ── 저장 ──────────────────────────────────────────────

    /// <summary>1: 처음 (2026-10-19).</summary>
    public const int SaveVersion = 1;

    public string Serialize() => JsonSerializer.Serialize(new ProfileFile
    {
        Version = SaveVersion,
        Renown = Renown,
        TotalRenown = TotalRenown,
        Unlocked = _unlocked.Order(StringComparer.Ordinal).ToList(),
        Runs = Runs,
        Victories = Victories,
        BestActs = BestActs,
        Encounters = new SortedDictionary<string, EncounterRecord>(_encounters, StringComparer.Ordinal),
    }, GameDataJson.Options);

    /// <summary>
    /// 기록 JSON을 읽는다. 지금 데이터에 없는 해금과 적 조합은 버린다(데이터가 바뀌었을 수 있다). 형식이 틀리면 <see cref="SaveGameException"/>.
    /// </summary>
    public static Profile Deserialize(string json, GameData data)
    {
        ProfileFile? file;
        try
        {
            file = JsonSerializer.Deserialize<ProfileFile>(json, GameDataJson.Options);
        }
        catch (JsonException e)
        {
            throw new SaveGameException([$"profile: {e.Message}"]);
        }

        if (file is null || file.Version != SaveVersion)
        {
            throw new SaveGameException([$"profile: unsupported version {file?.Version} (expected {SaveVersion})"]);
        }

        if (file.Renown < 0 || file.Runs < 0 || file.Victories < 0 || file.BestActs < 0)
        {
            throw new SaveGameException(["profile: counts must not be negative"]);
        }

        var profile = new Profile
        {
            Renown = file.Renown,
            TotalRenown = file.TotalRenown,
            Runs = file.Runs,
            Victories = file.Victories,
            BestActs = file.BestActs,
        };
        profile._unlocked.UnionWith(file.Unlocked.Where(data.Unlocks.ContainsKey));
        foreach (var (id, record) in file.Encounters.Where(e => data.Encounters.ContainsKey(e.Key)))
        {
            profile._encounters[id] = record;
        }

        return profile;
    }
}

/// <summary>기록의 JSON 형태.</summary>
public sealed record ProfileFile
{
    public required int Version { get; init; }

    public int Renown { get; init; }

    public int TotalRenown { get; init; }

    public IReadOnlyList<string> Unlocked { get; init; } = [];

    public int Runs { get; init; }

    public int Victories { get; init; }

    public int BestActs { get; init; }

    public IReadOnlyDictionary<string, EncounterRecord> Encounters { get; init; } = new Dictionary<string, EncounterRecord>();
}

/// <param name="Profile">불러온 기록. 없거나 깨졌으면 새 기록.</param>
public sealed record ProfileLoadResult(Profile Profile, LoadStatus Status, string? BrokenFilePath = null, IReadOnlyList<string>? Errors = null);

/// <summary>판을 넘어 남는 기록 파일 하나. 깨져 있으면 옆에 보관하고 새로 시작한다.</summary>
public sealed class ProfileStore(string path)
{
    public string Path { get; } = path;

    /// <summary>OS별 사용자 설정 폴더 아래 Triangle/profile.json.</summary>
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Triangle", "profile.json");

    public ProfileLoadResult Load(GameData data)
    {
        if (!File.Exists(Path))
        {
            return new ProfileLoadResult(new Profile(), LoadStatus.NewGame);
        }

        try
        {
            return new ProfileLoadResult(Profile.Deserialize(File.ReadAllText(Path), data), LoadStatus.Loaded);
        }
        catch (SaveGameException e)
        {
            var broken = $"{Path}.broken-{DateTime.Now:yyyyMMdd-HHmmss}";
            for (var i = 2; File.Exists(broken); i++)
            {
                broken = $"{Path}.broken-{DateTime.Now:yyyyMMdd-HHmmss}-{i}";
            }

            File.Move(Path, broken);
            return new ProfileLoadResult(new Profile(), LoadStatus.Recovered, broken, e.Errors);
        }
    }

    /// <summary>임시 파일에 쓴 뒤 교체한다.</summary>
    public void Save(Profile profile)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = Path + ".tmp";
        File.WriteAllText(temp, profile.Serialize());
        File.Move(temp, Path, overwrite: true);
    }
}
