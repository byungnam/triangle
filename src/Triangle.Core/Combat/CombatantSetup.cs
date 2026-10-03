using Triangle.Core.Items;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Combat;

/// <summary>전투에 들어가는 유닛 한 명의 입력.</summary>
/// <param name="Weapon">주무기 계열 ID (없으면 null). 무기 숙련과 보너스 태그에 쓴다.</param>
/// <param name="Armor">대표 방어구 계열 ID (없으면 null). <see cref="ArmorPieces"/>가 없으면 머리·몸통·신발 모두 이 재질로 본다.</param>
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
    int? StartMp = null)
{
    /// <summary>아이템 행동 칸에서 고른 행동 ID. null이면 아이템 제한이 없다 (적 유닛).</summary>
    public IReadOnlySet<string>? GrantedActions { get; init; }

    /// <summary>장비 보너스 (숙련 아이템 파워 반영). 스킬 보너스에 더한다.</summary>
    public IReadOnlyList<ItemBonus> ItemBonuses { get; init; } = [];

    /// <summary>입은 방어구 부위마다 그 재질 계열 (머리·몸통·신발, 최대 3개). 방어구 숙련 경험치를 나눈다.</summary>
    public IReadOnlyList<string>? ArmorPieces { get; init; }

    /// <summary>방어구 숙련을 받을 부위별 재질. <see cref="ArmorPieces"/>가 없으면 <see cref="Armor"/> 한 벌.</summary>
    public IReadOnlyList<string> ArmorMasteries =>
        ArmorPieces ?? (Armor is null ? [] : Enumerable.Repeat(Armor, EquipmentSlots.Armor.Count).ToList());
}
