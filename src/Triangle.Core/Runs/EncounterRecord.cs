namespace Triangle.Core.Runs;

/// <summary>탐사 기록의 한 항목: 판에서 만난 적 조합. 적은 직접 싸워 봐야 기록에 남는다.</summary>
/// <param name="Met">싸운 횟수 (1 이상: 기록이 있으면 만난 것이다).</param>
/// <param name="Wins">이긴 횟수.</param>
public sealed record EncounterRecord(int Met, int Wins)
{
    /// <summary>한 번 더 싸운 기록.</summary>
    public EncounterRecord Add(bool won) => new(Met + 1, Wins + (won ? 1 : 0));
}
