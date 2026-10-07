using Triangle.Core.Actions;
using Triangle.Core.Combat;

namespace Triangle.Core.Items;

/// <summary>
/// 실제로 착용한 아이템 묶음 (착용 불가는 이미 뺐다). 파티 멤버와 적 유닛이 같은 규칙으로
/// 계열, 행동, 보너스, 위력 배율을 전투 입력에 옮긴다.
/// </summary>
public sealed class Loadout
{
    public Loadout(IEnumerable<(EquipmentSlot Slot, ItemDefinition Item)> worn)
    {
        Worn = worn.OrderBy(w => w.Slot).ToList();
    }

    /// <summary>부위 순서로 착용한 아이템.</summary>
    public IReadOnlyList<(EquipmentSlot Slot, ItemDefinition Item)> Worn { get; }

    public ItemDefinition? MainHand => In(EquipmentSlot.MainHand);

    /// <summary>주무기의 계열 ID (맨손이면 null).</summary>
    public string? WeaponMastery => MainHand?.Mastery;

    /// <summary>보조 아이템의 계열 ID (없으면 null).</summary>
    public string? OffHandMastery => In(EquipmentSlot.OffHand)?.Mastery;

    /// <summary>몸통 방어구의 재질 계열 ID (표시용, 없으면 null).</summary>
    public string? ArmorMastery => In(EquipmentSlot.Body)?.Mastery;

    /// <summary>입은 방어구 부위별 재질 (방어구 숙련 경험치를 나눈다).</summary>
    public IReadOnlyList<string> ArmorPieces => Worn.Where(w => w.Slot.IsArmor()).Select(w => w.Item.Mastery!).ToList();

    /// <summary>착용 중인 아이템의 행동과, 든 주무기 계열이 주는 행동(예: 정령 소환).</summary>
    public IReadOnlySet<string> GrantedActions(IEnumerable<ActionDefinition> actions)
    {
        var granted = Worn.SelectMany(w => w.Item.Actions).ToHashSet();
        if (WeaponMastery is { } weapon)
        {
            granted.UnionWith(actions.Where(a => a.Weapon == weapon).Select(a => a.Id));
        }

        return granted;
    }

    /// <summary>착용 중인 아이템의 보너스. masteryLevel은 계열 ID별 숙련 레벨 (숙련 아이템 파워).</summary>
    public IReadOnlyList<ItemBonus> ItemBonuses(Func<string, int> masteryLevel) =>
        Worn.SelectMany(w => w.Item.BonusesAt(masteryLevel(w.Item.Mastery!))).ToList();

    /// <summary>전투 입력에 장비가 정하는 값(계열, 행동, 보너스, 위력 배율)을 넣는다.</summary>
    public CombatantSetup Apply(CombatantSetup setup, IEnumerable<ActionDefinition> actions, Func<string, int> masteryLevel) =>
        setup with
        {
            Weapon = WeaponMastery,
            Armor = ArmorMastery,
            GrantedActions = GrantedActions(actions),
            ItemBonuses = ItemBonuses(masteryLevel),
            ArmorPieces = ArmorPieces,
            OffHand = OffHandMastery,
            PowerMultiplierPercent = MainHand?.PowerMultiplierPercent ?? 100,
            EffectDurationBonus = MainHand?.EffectDurationBonus ?? 0,
        };

    private ItemDefinition? In(EquipmentSlot slot) => Worn.Where(w => w.Slot == slot).Select(w => w.Item).FirstOrDefault();
}
