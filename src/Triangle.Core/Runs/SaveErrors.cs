namespace Triangle.Core.Runs;

/// <summary>세이브 파일(판, 기록)을 불러온 결과.</summary>
public enum LoadStatus
{
    /// <summary>세이브를 불러왔다.</summary>
    Loaded,

    /// <summary>세이브가 없어 새로 시작했다.</summary>
    NewGame,

    /// <summary>세이브가 깨져 있어 따로 보관하고 새로 시작했다.</summary>
    Recovered,
}

/// <summary>세이브를 읽거나 검증하다 실패했다. 발견한 오류를 모두 담는다.</summary>
public sealed class SaveGameException(IReadOnlyList<string> errors)
    : Exception("Invalid save:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
