using Triangle.Core.Data;

namespace Triangle.Core.Progress;

public enum LoadStatus
{
    /// <summary>세이브를 불러왔다.</summary>
    Loaded,

    /// <summary>세이브가 없어 새로 시작했다.</summary>
    NewGame,

    /// <summary>세이브가 깨져 있어 따로 보관하고 새로 시작했다.</summary>
    Recovered,
}

/// <param name="BrokenFilePath">Recovered일 때 깨진 세이브를 옮겨 둔 경로.</param>
/// <param name="Errors">Recovered일 때 세이브를 읽지 못한 이유.</param>
public sealed record LoadResult(Party Party, LoadStatus Status, string? BrokenFilePath = null, IReadOnlyList<string>? Errors = null);

/// <summary>세이브 파일 하나를 읽고 쓴다.</summary>
public sealed class SaveStore(string path)
{
    public string Path { get; } = path;

    /// <summary>OS별 사용자 설정 폴더 아래 Triangle/save.json (Linux: ~/.config, Windows: %APPDATA%).</summary>
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Triangle", "save.json");

    /// <summary>
    /// 세이브를 불러온다. 없으면 <paramref name="createNew"/>로 새로 시작한다.
    /// 깨져 있으면 덮어쓰지 않도록 옆에 보관한 뒤 새로 시작한다.
    /// </summary>
    public LoadResult Load(GameData data, Func<Party> createNew)
    {
        if (!File.Exists(Path))
        {
            return new LoadResult(createNew(), LoadStatus.NewGame);
        }

        try
        {
            return new LoadResult(SaveGame.Deserialize(File.ReadAllText(Path), data), LoadStatus.Loaded);
        }
        catch (SaveGameException e)
        {
            var broken = BrokenFilePath();
            File.Move(Path, broken);
            return new LoadResult(createNew(), LoadStatus.Recovered, broken, e.Errors);
        }
    }

    /// <summary>임시 파일에 쓴 뒤 교체한다. 쓰는 도중에 꺼져도 기존 세이브는 남는다.</summary>
    public void Save(Party party)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = Path + ".tmp";
        File.WriteAllText(temp, SaveGame.Serialize(party));
        File.Move(temp, Path, overwrite: true);
    }

    private string BrokenFilePath()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var candidate = $"{Path}.broken-{stamp}";
        for (var i = 2; File.Exists(candidate); i++)
        {
            candidate = $"{Path}.broken-{stamp}-{i}";
        }

        return candidate;
    }
}
