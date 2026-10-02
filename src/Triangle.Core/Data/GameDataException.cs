namespace Triangle.Core.Data;

/// <summary>게임 데이터를 읽거나 검증하다 실패했다. 발견한 오류를 모두 담는다.</summary>
public sealed class GameDataException : Exception
{
    public GameDataException(IReadOnlyList<string> errors)
        : base("Invalid game data:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "  - " + e)))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
