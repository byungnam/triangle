using Triangle.Core.Combat;
using Triangle.Core.Data;

namespace Triangle.Core.Progress;

/// <summary>플레이어 파티. 나중에 세이브 데이터가 이 모델을 저장한다.</summary>
public sealed class Party(IEnumerable<PartyMember> members)
{
    public IReadOnlyList<PartyMember> Members { get; } = members.ToList();

    public IReadOnlyList<CombatantSetup> ToCombatantSetups(GameData data) =>
        Members.Select(m => m.ToCombatantSetup(data)).ToList();
}
