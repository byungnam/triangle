using Triangle.Core.Combat;
using Triangle.Core.Data;

namespace Triangle.Core.Progress;

/// <summary>플레이어 파티. 세이브 데이터가 이 모델을 저장한다.</summary>
public sealed class Party
{
    private int _activeTacticSet;

    public Party(IEnumerable<PartyMember> members, int activeTacticSet = 0)
    {
        Members = members.ToList();
        ActiveTacticSet = activeTacticSet;
    }

    public IReadOnlyList<PartyMember> Members { get; }

    /// <summary>전투에 쓸 전술 세트 (0부터). 파티 전원이 같은 번호의 세트를 쓴다.</summary>
    public int ActiveTacticSet
    {
        get => _activeTacticSet;
        set => _activeTacticSet = value is >= 0 and < PartyMember.TacticSetCount
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Tactic set must be 0-{PartyMember.TacticSetCount - 1}.");
    }

    public IReadOnlyList<CombatantSetup> ToCombatantSetups(GameData data) =>
        Members.Select(m => m.ToCombatantSetup(data, ActiveTacticSet)).ToList();

    /// <summary>지금 세트에 잠긴 전술이 있는 멤버가 있는가 (있으면 전투를 시작할 수 없다).</summary>
    public bool HasLockedTactics(GameData data) => Members.Any(m => m.LockedTacticIndexes(data, ActiveTacticSet).Count > 0);
}
