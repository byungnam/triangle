using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Combat;

/// <summary>전투에 들어가는 유닛 한 명의 입력.</summary>
/// <param name="Weapon">장착한 무기 계열 ID (없으면 null).</param>
/// <param name="Armor">장착한 방어구 계열 ID (없으면 null).</param>
/// <param name="Skills">스킬 ID별 레벨 (1–5). 없는 스킬은 0레벨.</param>
/// <param name="StartHp">시작 HP (원정에서 이어진 값). null이면 가득. 1 이상 최대치 이하로 자른다.</param>
/// <param name="StartMp">시작 MP. null이면 가득. 0 이상 최대치 이하로 자른다.</param>
public sealed record CombatantSetup(
    string Id,
    string Name,
    Stats Stats,
    Row Row,
    string? Weapon,
    string? Armor,
    IReadOnlyDictionary<string, int> Skills,
    IReadOnlyList<Tactic> Tactics,
    int? StartHp = null,
    int? StartMp = null);
