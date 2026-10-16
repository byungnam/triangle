using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Items;

namespace Triangle.Core.Progress;

/// <summary>
/// 플레이어의 용병 회사. 세이브 데이터가 이 모델을 저장한다.
/// - 로스터: 보유한 캐릭터 모두. 출전 명단은 그중 최대 <see cref="MaxLineup"/>명이다.
/// - 골드와 창고. 재료는 아이템 ID별 개수, 장비는 한 점씩(<see cref="ItemInstance"/>) 둔다. 장착과 해제는 창고와 오간다.
///   새 장비(구매, 제작, 전리품, 모집 장비)에는 회사가 겹치지 않는 번호를 붙인다.
/// - 전술 세트: 이름 붙은 전술 계획. 수에 제한이 없고 플레이어가 만들고 지운다. 멤버마다 세트별 전술 목록을 갖고,
///   출정 전에 고른 세트(<see cref="ActiveTacticSet"/>)로 출전 멤버 전원이 싸운다.
/// - 모집 후보와 진행 중인 원정. 원정 중에는 명단, 장비, 모집을 바꿀 수 없다. 전술과 전열도 출정 전에만 짠다(원정 화면에서 편집기로 갈 수 없다).
/// </summary>
public sealed class Company
{
    public const int MaxLineup = 5;

    /// <summary>새 게임에 주는 골드 (임시 수치).</summary>
    public const int StartingGold = 300;

    private readonly List<PartyMember> _roster;
    private readonly List<string> _lineup;
    private readonly Dictionary<string, int> _materials;
    private readonly List<ItemInstance> _gear;
    private readonly List<RecruitOffer> _recruitOffers;
    private readonly List<string> _tacticSetNames;
    private readonly Dictionary<string, ClearRecord> _clearRecords;
    private readonly Dictionary<string, EncounterRecord> _encounterRecords;
    private int _activeTacticSet;

    /// <param name="materials">창고의 재료 (아이템 ID별 개수).</param>
    /// <param name="tacticSetNames">전술 세트 이름. null이면 멤버가 가진 세트 수만큼 "세트 1", "세트 2"…로 짓는다.</param>
    /// <param name="gear">창고의 장비. 번호는 멤버가 낀 장비와도 겹치지 않아야 한다.</param>
    /// <param name="nextItemUid">다음 장비 번호. 이미 쓴 번호보다 작으면 그 다음 번호로 올린다.</param>
    public Company(
        IEnumerable<PartyMember> roster,
        IEnumerable<string> lineup,
        int gold,
        IReadOnlyDictionary<string, int> materials,
        int activeTacticSet,
        int nextSeed,
        IEnumerable<RecruitOffer>? recruitOffers = null,
        int nextRecruitNumber = 1,
        Expedition? expedition = null,
        IEnumerable<string>? tacticSetNames = null,
        IReadOnlyDictionary<string, ClearRecord>? clearRecords = null,
        IReadOnlyDictionary<string, EncounterRecord>? encounterRecords = null,
        IEnumerable<ItemInstance>? gear = null,
        int nextItemUid = 1)
    {
        _roster = roster.ToList();
        _lineup = lineup.ToList();
        if (_lineup.Count > MaxLineup)
        {
            throw new ArgumentException($"At most {MaxLineup} members in the lineup.", nameof(lineup));
        }

        if (_lineup.FirstOrDefault(id => _roster.All(m => m.Id != id)) is { } unknown)
        {
            throw new ArgumentException($"Lineup member '{unknown}' is not in the roster.", nameof(lineup));
        }

        _tacticSetNames = tacticSetNames?.ToList()
            ?? Enumerable.Range(1, Math.Max(1, _roster.Select(m => m.TacticSets.Count).DefaultIfEmpty(1).Max()))
                .Select(DefaultTacticSetName)
                .ToList();
        if (_tacticSetNames.Count == 0)
        {
            throw new ArgumentException("A company needs at least one tactic set.", nameof(tacticSetNames));
        }

        if (_roster.FirstOrDefault(m => m.TacticSets.Count > _tacticSetNames.Count) is { } over)
        {
            throw new ArgumentException($"Member '{over.Id}' has more tactic sets than the company ({_tacticSetNames.Count}).", nameof(roster));
        }

        foreach (var member in _roster)
        {
            member.PadTacticSets(_tacticSetNames.Count);
        }

        _gear = gear?.ToList() ?? [];
        var uids = _roster.SelectMany(m => m.Gear.Values).Concat(_gear).Select(g => g.Uid).ToList();
        if (uids.GroupBy(u => u).FirstOrDefault(g => g.Count() > 1) is { } duplicate)
        {
            throw new ArgumentException($"Item uid {duplicate.Key} is used more than once.", nameof(gear));
        }

        NextItemUid = Math.Max(nextItemUid, uids.DefaultIfEmpty(0).Max() + 1);

        Gold = gold;
        _materials = new Dictionary<string, int>(materials.Where(p => p.Value > 0));
        ActiveTacticSet = activeTacticSet;
        NextSeed = nextSeed;
        _recruitOffers = recruitOffers?.ToList() ?? [];
        NextRecruitNumber = nextRecruitNumber;
        Expedition = expedition;
        _clearRecords = clearRecords?.ToDictionary() ?? [];
        _encounterRecords = encounterRecords?.ToDictionary() ?? [];
    }

    /// <summary>보유한 캐릭터 모두.</summary>
    public IReadOnlyList<PartyMember> Roster => _roster;

    /// <summary>출전할 캐릭터 ID (순서대로, 최대 <see cref="MaxLineup"/>명).</summary>
    public IReadOnlyList<string> Lineup => _lineup;

    /// <summary>출전 명단의 캐릭터 (명단 순서).</summary>
    public IReadOnlyList<PartyMember> LineupMembers => _lineup.Select(Member).ToList();

    public int Gold { get; set; }

    /// <summary>창고의 재료: 아이템 ID별 개수 (0개인 항목은 없다).</summary>
    public IReadOnlyDictionary<string, int> Materials => _materials;

    /// <summary>창고의 장비 (들어온 순서).</summary>
    public IReadOnlyList<ItemInstance> Gear => _gear;

    /// <summary>다음에 만들 장비의 번호.</summary>
    public int NextItemUid { get; private set; }

    /// <summary>창고에 든 아이템 수 (재료 개수 + 장비 점 수).</summary>
    public int StashTotal => _materials.Values.Sum() + _gear.Count;

    /// <summary>전술 세트 이름의 최대 길이.</summary>
    public const int MaxTacticSetNameLength = 20;

    /// <summary>전술 세트 이름 (세트 순서). 항상 하나 이상이다.</summary>
    public IReadOnlyList<string> TacticSetNames => _tacticSetNames;

    /// <summary>전투에 쓸 전술 세트 (0부터). 출전 멤버 전원이 같은 세트를 쓴다.</summary>
    public int ActiveTacticSet
    {
        get => _activeTacticSet;
        set => _activeTacticSet = value >= 0 && value < _tacticSetNames.Count
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Tactic set must be 0-{_tacticSetNames.Count - 1}.");
    }

    /// <summary>이름이 없을 때 쓰는 세트 이름 (number는 1부터).</summary>
    public static string DefaultTacticSetName(int number) => $"세트 {number}";

    /// <summary>
    /// 지금 고른 세트를 복사해 새 세트를 끝에 만들고 그 세트를 고른다. 새 세트의 번호를 돌려준다.
    /// 이름이 비어 있으면 "세트 N"으로 짓는다.
    /// </summary>
    public int AddTacticSet(string? name = null)
    {
        foreach (var member in _roster)
        {
            member.AddTacticSet(member.TacticSets[ActiveTacticSet]);
        }

        _tacticSetNames.Add(CleanName(name) ?? DefaultTacticSetName(_tacticSetNames.Count + 1));
        ActiveTacticSet = _tacticSetNames.Count - 1;
        return ActiveTacticSet;
    }

    /// <summary>세트를 지운다. 마지막 남은 세트는 지울 수 없다(false). 고른 세트는 같은 세트를 가리키도록 옮긴다.</summary>
    public bool RemoveTacticSet(int index)
    {
        if (_tacticSetNames.Count <= 1 || index < 0 || index >= _tacticSetNames.Count)
        {
            return false;
        }

        foreach (var member in _roster)
        {
            member.RemoveTacticSet(index);
        }

        _tacticSetNames.RemoveAt(index);
        if (_activeTacticSet > index || _activeTacticSet == _tacticSetNames.Count)
        {
            _activeTacticSet--;
        }

        return true;
    }

    /// <summary>세트 이름을 바꾼다. 앞뒤 공백을 지우고 최대 길이로 자른다. 비어 있으면 false.</summary>
    public bool RenameTacticSet(int index, string name)
    {
        if (index < 0 || index >= _tacticSetNames.Count || CleanName(name) is not { } clean)
        {
            return false;
        }

        _tacticSetNames[index] = clean;
        return true;
    }

    private static string? CleanName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim()[..Math.Min(name.Trim().Length, MaxTacticSetNameLength)];

    /// <summary>다음 무작위 시드. 원정과 모집이 <see cref="TakeSeed"/>로 꺼내 쓴다 (같은 세이브면 같은 결과).</summary>
    public int NextSeed { get; private set; }

    /// <summary>마을의 모집 후보. 원정이 끝날 때마다 새로 굴린다.</summary>
    public IReadOnlyList<RecruitOffer> RecruitOffers => _recruitOffers;

    /// <summary>다음 신입의 일련번호 (ID는 recruit_번호).</summary>
    public int NextRecruitNumber { get; private set; }

    /// <summary>진행 중인 원정. 마을에 있으면 null.</summary>
    public Expedition? Expedition { get; internal set; }

    public bool OnExpedition => Expedition is not null;

    /// <summary>클리어한 지역의 기록 (지역 ID별). 기록이 있으면 그 지역을 클리어한 것이다.</summary>
    public IReadOnlyDictionary<string, ClearRecord> ClearRecords => _clearRecords;

    public bool HasCleared(string zoneId) => _clearRecords.ContainsKey(zoneId);

    /// <summary>클리어한 원정을 기록한다. 첫 클리어인지와 좋아진 기록 항목을 돌려준다.</summary>
    internal (bool FirstClear, IReadOnlyList<ClearRecordKind> Improved) RecordClear(string zoneId, ClearResult result)
    {
        if (!_clearRecords.TryGetValue(zoneId, out var record))
        {
            _clearRecords[zoneId] = ClearRecord.First(result);
            return (true, []);
        }

        var (updated, improved) = record.Add(result);
        _clearRecords[zoneId] = updated;
        return (false, improved);
    }

    /// <summary>탐사 기록: 원정에서 만난 적 조합 (인카운터 ID별). 기록이 없는 조합은 아직 모른다.</summary>
    public IReadOnlyDictionary<string, EncounterRecord> EncounterRecords => _encounterRecords;

    public bool HasMet(string encounterId) => _encounterRecords.ContainsKey(encounterId);

    /// <summary>원정에서 싸운 적 조합을 기록한다. 처음 만났으면 true.</summary>
    internal bool RecordEncounter(string encounterId, bool won)
    {
        var first = !_encounterRecords.TryGetValue(encounterId, out var record);
        _encounterRecords[encounterId] = first ? new EncounterRecord(1, won ? 1 : 0) : record!.Add(won);
        return first;
    }

    public PartyMember Member(string id) => _roster.Single(m => m.Id == id);

    public bool IsInLineup(string memberId) => _lineup.Contains(memberId);

    /// <summary>시드를 하나 꺼내고 다음 시드로 넘어간다.</summary>
    public int TakeSeed()
    {
        var seed = NextSeed;
        NextSeed = Seeds.Next(seed);
        return seed;
    }

    /// <summary>출전 명단에 넣는다. 이미 있거나 꽉 찼거나 로스터에 없으면 false.</summary>
    public bool AddToLineup(string memberId)
    {
        if (OnExpedition || _lineup.Count >= MaxLineup || _lineup.Contains(memberId) || _roster.All(m => m.Id != memberId))
        {
            return false;
        }

        _lineup.Add(memberId);
        return true;
    }

    public bool RemoveFromLineup(string memberId) => !OnExpedition && _lineup.Remove(memberId);

    /// <summary>창고에 든 그 아이템의 수 (재료면 개수, 장비면 점 수).</summary>
    public int StashCount(string itemId) => _materials.GetValueOrDefault(itemId) + _gear.Count(g => g.ItemId == itemId);

    /// <summary>창고의 그 번호 장비. 없으면 null.</summary>
    public ItemInstance? StashGear(int uid) => _gear.FirstOrDefault(g => g.Uid == uid);

    /// <summary>창고에 있는 그 아이템의 장비 중 가장 좋은 한 점 (레벨이 높은 것, 같으면 먼저 들어온 것). 없으면 null.</summary>
    public ItemInstance? BestGear(string itemId) =>
        _gear.Where(g => g.ItemId == itemId).OrderByDescending(g => g.Level).FirstOrDefault();

    /// <summary>창고에 있는 그 아이템의 장비 중 가장 나쁜 한 점 (레벨이 낮은 것, 같으면 나중에 들어온 것). 없으면 null.</summary>
    public ItemInstance? WorstGear(string itemId) =>
        _gear.Where(g => g.ItemId == itemId).OrderBy(g => g.Level).ThenByDescending(g => g.Uid).FirstOrDefault();

    /// <summary>
    /// 창고에 넣는다. 재료는 개수를 더하고, 장비는 새 장비를 count점 만든다(레벨을 주지 않으면 그 아이템의 티어).
    /// 만든 장비를 돌려준다 (재료면 빈 목록).
    /// </summary>
    public IReadOnlyList<ItemInstance> AddToStash(string itemId, GameData data, int count = 1, int? level = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var item = data.Items[itemId];
        if (!item.IsEquipment)
        {
            if (count > 0)
            {
                _materials[itemId] = _materials.GetValueOrDefault(itemId) + count;
            }

            return [];
        }

        var made = Enumerable.Range(0, count).Select(_ => NewGear(itemId, level ?? item.Tier)).ToList();
        _gear.AddRange(made);
        return made;
    }

    private ItemInstance NewGear(string itemId, int level) => new(NextItemUid++, itemId, Math.Max(1, level));

    /// <summary>창고에서 재료를 꺼낸다. 모자라면 아무것도 꺼내지 않고 false.</summary>
    private bool TakeMaterial(string itemId, int count = 1)
    {
        var have = _materials.GetValueOrDefault(itemId);
        if (have < count)
        {
            return false;
        }

        if (have == count)
        {
            _materials.Remove(itemId);
        }
        else
        {
            _materials[itemId] = have - count;
        }

        return true;
    }

    /// <summary>
    /// 창고의 장비를 장착할 수 없는 이유. null이면 장착할 수 있다(이미 그 멤버가 낀 장비도 null).
    /// 원정 중, 창고에 없음, 장비가 아님, 착용 조건(패시브 레벨) 미달, 두손 무기를 낀 채 보조.
    /// </summary>
    public string? WhyCannotEquip(string memberId, int uid, GameData data)
    {
        if (OnExpedition)
        {
            return "원정 중에는 장비를 바꿀 수 없습니다";
        }

        var member = Member(memberId);
        if (member.Gear.Values.Any(g => g.Uid == uid))
        {
            return null;
        }

        if (StashGear(uid) is not { } gear)
        {
            return "창고에 없습니다";
        }

        if (!data.Items.TryGetValue(gear.ItemId, out var item) || !item.IsEquipment)
        {
            return "장비가 아닙니다";
        }

        var missing = member.Skills(data).Missing(item.Requirements);
        if (missing.Count > 0)
        {
            return "요구: " + string.Join(", ", missing.Select(r => $"{data.Skills[r.SkillId].Name} Lv{r.Level}"));
        }

        if (item.Slot == EquipmentSlot.OffHand && member.ItemIn(EquipmentSlot.MainHand) is { } main && data.Items[main].TwoHanded)
        {
            return "두손 무기를 들고 있습니다";
        }

        return null;
    }

    /// <summary>
    /// 창고의 장비를 그 부위에 장착한다. 끼고 있던 장비는 창고로 돌아간다.
    /// 두손 무기를 끼면 보조도 창고로 돌아간다. 이미 낀 장비면 아무것도 하지 않고 true.
    /// 장착할 수 없으면(<see cref="WhyCannotEquip"/>) false.
    /// </summary>
    public bool Equip(string memberId, int uid, GameData data)
    {
        if (WhyCannotEquip(memberId, uid, data) is not null)
        {
            return false;
        }

        var member = Member(memberId);
        if (member.Gear.Values.Any(g => g.Uid == uid))
        {
            return true;
        }

        var gear = StashGear(uid)!;
        var item = data.Items[gear.ItemId];
        _gear.Remove(gear);
        Unequip(memberId, item.Slot, data);
        if (item.TwoHanded)
        {
            Unequip(memberId, EquipmentSlot.OffHand, data);
        }

        member.SetGear(item.Slot, gear);
        return true;
    }

    /// <summary>그 부위의 장비를 빼서 창고에 넣는다. 원정 중이거나 비어 있으면 false.</summary>
    public bool Unequip(string memberId, EquipmentSlot slot, GameData data)
    {
        var member = Member(memberId);
        if (OnExpedition || member.GearIn(slot) is not { } current)
        {
            return false;
        }

        member.SetGear(slot, null);
        _gear.Add(current);
        return true;
    }

    /// <summary>상점에서 살 수 없는 이유. null이면 살 수 있다. 원정 중, 상점에 없는 아이템, 골드 부족.</summary>
    public string? WhyCannotBuy(string itemId, GameData data)
    {
        if (OnExpedition)
        {
            return "원정 중에는 상점을 쓸 수 없습니다";
        }

        if (!data.Items.TryGetValue(itemId, out var item) || !Shop.Sells(item))
        {
            return "상점에서 팔지 않습니다";
        }

        return Gold < item.Price ? "골드가 모자랍니다" : null;
    }

    /// <summary>정가에 사서 창고에 넣는다(아이템 레벨 = 티어). 살 수 없으면(<see cref="WhyCannotBuy"/>) false.</summary>
    public bool Buy(string itemId, GameData data)
    {
        if (WhyCannotBuy(itemId, data) is not null)
        {
            return false;
        }

        Gold -= data.Items[itemId].Price;
        AddToStash(itemId, data);
        return true;
    }

    /// <summary>창고의 장비 한 점을 판다(<see cref="Shop.SellPrice"/>). 원정 중이거나 창고에 없으면 false.</summary>
    public bool Sell(int uid, GameData data)
    {
        if (OnExpedition || StashGear(uid) is not { } gear || !data.Items.TryGetValue(gear.ItemId, out var item))
        {
            return false;
        }

        _gear.Remove(gear);
        Gold += Shop.SellPrice(item);
        return true;
    }

    /// <summary>창고의 재료 하나를 판다(<see cref="Shop.SellPrice"/>). 원정 중이거나 창고에 없으면 false.</summary>
    public bool SellMaterial(string itemId, GameData data)
    {
        if (OnExpedition || !data.Items.TryGetValue(itemId, out var item) || !TakeMaterial(itemId))
        {
            return false;
        }

        Gold += Shop.SellPrice(item);
        return true;
    }

    /// <summary>
    /// 그 장비를 제작할 수 없는 이유. null이면 만들 수 있다.
    /// 원정 중, 제작법 없음, 재료 부족("재료가 모자랍니다: 철 조각 2/6"), 골드 부족.
    /// </summary>
    public string? WhyCannotCraft(string resultId, GameData data)
    {
        if (OnExpedition)
        {
            return "원정 중에는 제작할 수 없습니다";
        }

        if (!data.Recipes.TryGetValue(resultId, out var recipe))
        {
            return "제작법이 없습니다";
        }

        var missing = recipe.Materials.Where(m => StashCount(m.ItemId) < m.Count).ToList();
        if (missing.Count > 0)
        {
            return "재료가 모자랍니다: " + string.Join(", ", missing.Select(m => $"{data.Items[m.ItemId].Name} {StashCount(m.ItemId)}/{m.Count}"));
        }

        return Gold < recipe.Gold ? "골드가 모자랍니다" : null;
    }

    /// <summary>
    /// 재료와 골드를 내고 장비를 만들어 창고에 넣는다(아이템 레벨 = 티어). 만들 수 없으면(<see cref="WhyCannotCraft"/>) false.
    /// </summary>
    public bool Craft(string resultId, GameData data)
    {
        if (WhyCannotCraft(resultId, data) is not null)
        {
            return false;
        }

        var recipe = data.Recipes[resultId];
        foreach (var material in recipe.Materials)
        {
            TakeMaterial(material.ItemId, material.Count);
        }

        Gold -= recipe.Gold;
        AddToStash(resultId, data);
        return true;
    }

    /// <summary>모집 후보를 새로 굴린다 (원정이 끝날 때, 새 게임).</summary>
    public void RerollRecruits(GameData data)
    {
        _recruitOffers.Clear();
        _recruitOffers.AddRange(Recruitment.Roll(data, TakeSeed()));
    }

    public bool CanHire(int offerIndex) =>
        !OnExpedition && offerIndex >= 0 && offerIndex < _recruitOffers.Count && Gold >= _recruitOffers[offerIndex].Price;

    /// <summary>
    /// 후보를 고용한다: 골드를 내고 로스터에 넣는다. 출전 명단에 자리가 있으면 명단에도 넣는다.
    /// 시작 장비는 새로 생긴다(아이템 레벨 = 티어). 숙련 0, 패시브 없음, 모든 전술 세트가 템플릿의 기본 전술.
    /// 골드가 모자라거나 원정 중이면 null.
    /// </summary>
    public PartyMember? Hire(int offerIndex, GameData data)
    {
        if (!CanHire(offerIndex))
        {
            return null;
        }

        var offer = _recruitOffers[offerIndex];
        var template = data.Recruits[offer.TemplateId];
        string id;
        do
        {
            id = $"recruit_{NextRecruitNumber++}";
        }
        while (_roster.Any(m => m.Id == id));

        Gold -= offer.Price;
        _recruitOffers.RemoveAt(offerIndex);
        var gear = template.Equipment.ToDictionary(p => p.Key, p => NewGear(p.Value, data.Items[p.Value].Tier));
        var member = new PartyMember(
            id, offer.Name, offer.Stats, template.Row, gear,
            new Dictionary<string, int>(), new Dictionary<string, int>(),
            _tacticSetNames.Select(_ => template.Tactics).ToList()); // 모든 세트를 모집 템플릿의 전술로 시작한다
        _roster.Add(member);
        AddToLineup(id);
        return member;
    }

    public IReadOnlyList<CombatantSetup> LineupSetups(GameData data) =>
        LineupMembers.Select(m => m.ToCombatantSetup(data, ActiveTacticSet)).ToList();

    /// <summary>출전 멤버 중 지금 세트에 잠긴 전술이 있는 멤버가 있는가 (있으면 전투를 시작할 수 없다).</summary>
    public bool HasLockedTactics(GameData data) => LineupMembers.Any(m => m.LockedTacticIndexes(data, ActiveTacticSet).Count > 0);

    /// <summary>출전 멤버 중 착용 불가 장비를 낀 멤버가 있는가 (있으면 전투를 시작할 수 없다).</summary>
    public bool HasUnwearableEquipment(GameData data) => LineupMembers.Any(m => m.UnwearableSlots(data).Count > 0);

    /// <summary>출전 명단이 싸울 수 없는 이유 (잠긴 전술, 착용 불가 장비). null이면 싸울 수 있다.</summary>
    public string? WhyLineupCannotFight(GameData data) =>
        HasUnwearableEquipment(data) ? "착용할 수 없는 장비를 바꿔야 합니다"
        : HasLockedTactics(data) ? "잠긴 전술을 고쳐야 합니다"
        : null;
}
