using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Progress;

/// <summary>
/// 플레이어 파티의 유닛 한 명.
/// - 전열과 전술을 편집한다. 전술은 세트 두 벌(<see cref="TacticSetCount"/>)을 저장하고,
///   어느 세트로 싸울지는 회사가 정한다(<see cref="Company.ActiveTacticSet"/>).
/// - 부위 5개(주무기, 보조, 머리, 몸통, 신발)에 아이템을 하나씩 낀다. 장착과 해제는 회사 창고와 오간다
///   (<see cref="Company.Equip"/>). 낀 아이템이 주는 행동(<see cref="GrantedActions"/>)과 공용 행동만 전술에 쓸 수 있다.
/// - 착용 조건(패시브 레벨)을 못 채운 아이템이나 두손 무기와 같이 낀 보조는 "착용 불가"다.
///   보너스와 행동을 주지 않고, 출정을 막는다.
/// - 장착한 계열로 싸우면 그 숙련 경험치가 쌓이고, 숙련 레벨 1당 그 트리 포인트 1점이 생긴다.
///   포인트로 그 트리의 패시브 스킬을 배운다 (되돌릴 수 없다).
/// </summary>
public sealed class PartyMember
{
    public const int TacticSetCount = 2;

    private readonly TacticList[] _tacticSets;
    private readonly Dictionary<string, int> _masteryXp;
    private readonly Dictionary<string, int> _skillLevels;
    private readonly Dictionary<EquipmentSlot, string> _equipment;

    /// <param name="equipment">부위별 아이템 ID.</param>
    public PartyMember(
        string id,
        string name,
        Stats stats,
        Row row,
        IReadOnlyDictionary<EquipmentSlot, string> equipment,
        IReadOnlyDictionary<string, int> masteryXp,
        IReadOnlyDictionary<string, int> skillLevels,
        IReadOnlyList<IEnumerable<Tactic>> tacticSets)
    {
        Id = id;
        Name = name;
        Stats = stats;
        Row = row;
        _equipment = new Dictionary<EquipmentSlot, string>(equipment);
        _masteryXp = new Dictionary<string, int>(masteryXp);
        _skillLevels = new Dictionary<string, int>(skillLevels.Where(p => p.Value > 0));
        if (tacticSets.Count > TacticSetCount)
        {
            throw new ArgumentException($"At most {TacticSetCount} tactic sets.", nameof(tacticSets));
        }

        // 모자란 세트는 빈 목록으로 채운다.
        _tacticSets = Enumerable.Range(0, TacticSetCount)
            .Select(i => new TacticList(i < tacticSets.Count ? tacticSets[i] : []))
            .ToArray();
    }

    public string Id { get; }
    public string Name { get; }
    public Stats Stats { get; }
    public Row Row { get; set; }

    /// <summary>부위별 장착 아이템 ID.</summary>
    public IReadOnlyDictionary<EquipmentSlot, string> Equipment => _equipment;

    /// <summary>그 부위의 아이템 ID (비었으면 null).</summary>
    public string? ItemIn(EquipmentSlot slot) => _equipment.GetValueOrDefault(slot);

    /// <summary>그 부위 아이템이 주는 행동. 아이템이 없으면 빈 목록.</summary>
    public IReadOnlyList<string> ItemActions(EquipmentSlot slot, GameData data) =>
        ItemIn(slot) is { } itemId && data.Items.TryGetValue(itemId, out var item) ? item.Actions : [];

    /// <summary>
    /// 그 부위의 아이템을 착용할 수 없는 이유. null이면 착용 중이거나 비어 있다.
    /// 착용 조건(패시브 레벨)을 못 채웠거나, 두손 무기와 같이 낀 보조면 착용 불가다.
    /// </summary>
    public string? WhyCannotWear(EquipmentSlot slot, GameData data)
    {
        if (ItemIn(slot) is not { } itemId)
        {
            return null;
        }

        var item = data.Items[itemId];
        if (slot == EquipmentSlot.OffHand && ItemIn(EquipmentSlot.MainHand) is { } main && data.Items[main].TwoHanded)
        {
            return "두손 무기와 함께 낄 수 없음";
        }

        var missing = Skills(data).Missing(item.Requirements);
        return missing.Count > 0
            ? "요구: " + string.Join(", ", missing.Select(r => $"{data.Skills[r.SkillId].Name} Lv{r.Level}"))
            : null;
    }

    /// <summary>착용 불가인 부위들.</summary>
    public IReadOnlyList<EquipmentSlot> UnwearableSlots(GameData data) =>
        EquipmentSlots.All.Where(s => WhyCannotWear(s, data) is not null).ToList();

    /// <summary>실제로 착용 중인 아이템 (착용 불가 제외, 부위 순서).</summary>
    public IEnumerable<(EquipmentSlot Slot, ItemDefinition Item)> WornItems(GameData data) =>
        EquipmentSlots.All
            .Where(s => ItemIn(s) is not null && WhyCannotWear(s, data) is null)
            .Select(s => (s, data.Items[ItemIn(s)!]));

    /// <summary>실제로 착용 중인 장비 묶음.</summary>
    public Loadout Loadout(GameData data) => new(WornItems(data));

    /// <summary>착용 중인 아이템의 행동과, 든 주무기 계열이 주는 행동(예: 정령 소환).</summary>
    public IReadOnlySet<string> GrantedActions(GameData data) => Loadout(data).GrantedActions(data.Actions.Values);

    /// <summary>착용 중인 아이템의 보너스 (숙련 아이템 파워 반영).</summary>
    public IReadOnlyList<ItemBonus> ItemBonuses(GameData data) => Loadout(data).ItemBonuses(MasteryLevel);

    /// <summary>주무기의 계열 ID (맨손이거나 착용 불가면 null).</summary>
    public string? WeaponMastery(GameData data) => Loadout(data).WeaponMastery;

    /// <summary>몸통 방어구의 재질 계열 ID (표시용, 없으면 null).</summary>
    public string? ArmorMastery(GameData data) => Loadout(data).ArmorMastery;

    /// <summary>
    /// 그 부위에 아이템을 넣는다(창고와 오가는 것은 <see cref="Company"/>가 한다).
    /// null이면 비운다.
    /// </summary>
    internal void SetItem(EquipmentSlot slot, string? itemId, GameData data)
    {
        if (itemId is null)
        {
            _equipment.Remove(slot);
            return;
        }

        _equipment[slot] = itemId;
    }

    /// <summary>전술 세트들 (항상 <see cref="TacticSetCount"/>벌).</summary>
    public IReadOnlyList<TacticList> TacticSets => _tacticSets;

    /// <summary>숙련 ID별 누적 경험치.</summary>
    public IReadOnlyDictionary<string, int> MasteryXp => _masteryXp;

    /// <summary>배운 스킬의 레벨 (1–5).</summary>
    public IReadOnlyDictionary<string, int> SkillLevels => _skillLevels;

    /// <summary>스킬 레벨 (요구 조건 검사용, 보너스 합계에 장비 제외).</summary>
    public SkillSet Skills(GameData data) => new(_skillLevels, data.Skills);

    /// <summary>스킬 레벨 + 장비 보너스 (전투 수치용).</summary>
    public SkillSet CombatSkills(GameData data) => new(_skillLevels, data.Skills, ItemBonuses(data));

    public int MasteryLevel(string masteryId) => MasteryProgression.LevelFor(_masteryXp.GetValueOrDefault(masteryId));

    /// <summary>그 트리에 쓴 포인트 = Σ(랭크 × 레벨).</summary>
    public int PointsSpent(string masteryId, GameData data) =>
        _skillLevels
            .Where(p => data.Skills.TryGetValue(p.Key, out var s) && s.Mastery == masteryId)
            .Sum(p => data.Skills[p.Key].Rank * p.Value);

    public int PointsAvailable(string masteryId, GameData data) => MasteryLevel(masteryId) - PointsSpent(masteryId, data);

    /// <summary>
    /// 경험치를 더한다. 오른 숙련 레벨 수(= 새로 생긴 포인트)를 돌려준다.
    /// </summary>
    public int AddMasteryXp(string masteryId, int xp)
    {
        var before = MasteryLevel(masteryId);
        _masteryXp[masteryId] = _masteryXp.GetValueOrDefault(masteryId) + Math.Max(0, xp);
        return MasteryLevel(masteryId) - before;
    }

    /// <summary>스킬을 다음 레벨로 올릴 수 없는 이유. null이면 올릴 수 있다.</summary>
    public LearnBlocker? WhyCannotLearn(string skillId, GameData data)
    {
        var skill = data.Skills[skillId];
        var level = _skillLevels.GetValueOrDefault(skillId);
        if (level >= SkillDefinition.MaxLevel)
        {
            return new LearnBlocker(LearnBlockerKind.MaxLevel, []);
        }

        var missing = Skills(data).Missing(skill.Prerequisites);
        if (missing.Count > 0)
        {
            return new LearnBlocker(LearnBlockerKind.Prerequisites, missing);
        }

        return PointsAvailable(skill.Mastery, data) < skill.Rank ? new LearnBlocker(LearnBlockerKind.NotEnoughPoints, []) : null;
    }

    /// <summary>포인트를 써서 스킬을 한 레벨 올린다. 올릴 수 없으면 false. 되돌릴 수 없다.</summary>
    public bool Learn(string skillId, GameData data)
    {
        if (WhyCannotLearn(skillId, data) is not null)
        {
            return false;
        }

        _skillLevels[skillId] = _skillLevels.GetValueOrDefault(skillId) + 1;
        return true;
    }

    /// <summary>
    /// 지금 고른 행동과 스킬로 쓸 수 없는 행동이 든 전술의 위치.
    /// 장비나 행동 칸 선택을 바꾸었거나 게임 데이터가 바뀌었을 때 생긴다.
    /// </summary>
    public IReadOnlyList<int> LockedTacticIndexes(GameData data, int tacticSet)
    {
        var skills = Skills(data);
        var granted = GrantedActions(data);
        return _tacticSets[tacticSet]
            .Select((t, i) => (t, i))
            .Where(x => !data.Actions.TryGetValue(x.t.ActionId, out var action) || !action.IsUsableBy(granted, skills))
            .Select(x => x.i)
            .ToList();
    }

    public void ToggleRow() => Row = Row == Row.Front ? Row.Back : Row.Front;

    /// <summary>전투 입력으로 바꾼다. 착용 중인 아이템의 계열, 행동, 보너스를 넘긴다.</summary>
    public CombatantSetup ToCombatantSetup(GameData data, int tacticSet) =>
        Loadout(data).Apply(
            new(Id, Name, Stats, Row, null, null, new Dictionary<string, int>(_skillLevels), _tacticSets[tacticSet].ToList()),
            data.Actions.Values,
            MasteryLevel);
}
