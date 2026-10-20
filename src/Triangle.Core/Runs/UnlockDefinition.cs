namespace Triangle.Core.Runs;

/// <summary>
/// 해금 하나 (`data/unlocks.json`, 로그라이트 재편 2026-10-19). 판을 끝내고 받은 명성을 내고 연다. 판을 넘어 남는 것은 해금과 기록뿐이다.
/// 어떤 해금에 적힌 클래스·카드·아이템은 그 해금을 열기 전까지 판에 나오지 않는다(파티 고르기, 보상, 상점, 이벤트).
/// </summary>
public sealed record UnlockDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>여는 데 드는 명성.</summary>
    public int Cost { get; init; }

    /// <summary>여는 클래스 ID.</summary>
    public IReadOnlyList<string> Classes { get; init; } = [];

    /// <summary>여는 카드 (행동 ID, 그 클래스의 카드 풀에 있는 것).</summary>
    public IReadOnlyList<string> Cards { get; init; } = [];

    /// <summary>여는 아이템 (상점과 이벤트에 나온다).</summary>
    public IReadOnlyList<string> Items { get; init; } = [];

    /// <summary>열면 판을 시작할 때 이만큼 골드를 더 받는다.</summary>
    public int StartGold { get; init; }

    /// <summary>이 해금이 잠가 두는 ID (클래스, 카드, 아이템).</summary>
    public IEnumerable<string> Locks => Classes.Concat(Cards).Concat(Items);
}
