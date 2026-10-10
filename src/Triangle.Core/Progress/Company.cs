using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Items;

namespace Triangle.Core.Progress;

/// <summary>
/// 플레이어의 용병 회사. 세이브 데이터가 이 모델을 저장한다.
/// - 로스터: 보유한 캐릭터 모두. 출전 명단은 그중 최대 <see cref="MaxLineup"/>명이다.
/// - 골드와 창고(아이템 ID별 개수, 재료 포함). 장착과 해제는 창고와 오간다.
/// - 전술 세트: 이름 붙은 전술 계획. 수에 제한이 없고 플레이어가 만들고 지운다. 멤버마다 세트별 전술 목록을 갖고,
///   출정 전에 고른 세트(<see cref="ActiveTacticSet"/>)로 출전 멤버 전원이 싸운다.
/// - 모집 후보와 진행 중인 원정. 원정 중에는 명단, 장비, 모집을 바꿀 수 없다(전술과 전열만 바꾼다).
/// </summary>
public sealed class Company
{
    public const int MaxLineup = 5;

    /// <summary>새 게임에 주는 골드 (임시 수치).</summary>
    public const int StartingGold = 300;

    private readonly List<PartyMember> _roster;
    private readonly List<string> _lineup;
    private readonly Dictionary<string, int> _stash;
    private readonly List<RecruitOffer> _recruitOffers;
    private readonly List<string> _tacticSetNames;
    private int _activeTacticSet;

    /// <param name="tacticSetNames">전술 세트 이름. null이면 멤버가 가진 세트 수만큼 "세트 1", "세트 2"…로 짓는다.</param>
    public Company(
        IEnumerable<PartyMember> roster,
        IEnumerable<string> lineup,
        int gold,
        IReadOnlyDictionary<string, int> stash,
        int activeTacticSet,
        int nextSeed,
        IEnumerable<RecruitOffer>? recruitOffers = null,
        int nextRecruitNumber = 1,
        Expedition? expedition = null,
        IEnumerable<string>? tacticSetNames = null)
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

        Gold = gold;
        _stash = new Dictionary<string, int>(stash.Where(p => p.Value > 0));
        ActiveTacticSet = activeTacticSet;
        NextSeed = nextSeed;
        _recruitOffers = recruitOffers?.ToList() ?? [];
        NextRecruitNumber = nextRecruitNumber;
        Expedition = expedition;
    }

    /// <summary>보유한 캐릭터 모두.</summary>
    public IReadOnlyList<PartyMember> Roster => _roster;

    /// <summary>출전할 캐릭터 ID (순서대로, 최대 <see cref="MaxLineup"/>명).</summary>
    public IReadOnlyList<string> Lineup => _lineup;

    /// <summary>출전 명단의 캐릭터 (명단 순서).</summary>
    public IReadOnlyList<PartyMember> LineupMembers => _lineup.Select(Member).ToList();

    public int Gold { get; set; }

    /// <summary>창고: 아이템 ID별 개수 (0개인 항목은 없다).</summary>
    public IReadOnlyDictionary<string, int> Stash => _stash;

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

    public int StashCount(string itemId) => _stash.GetValueOrDefault(itemId);

    public void AddToStash(string itemId, int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > 0)
        {
            _stash[itemId] = StashCount(itemId) + count;
        }
    }

    /// <summary>창고에서 꺼낸다. 모자라면 false.</summary>
    public bool TakeFromStash(string itemId)
    {
        var count = StashCount(itemId);
        if (count == 0)
        {
            return false;
        }

        if (count == 1)
        {
            _stash.Remove(itemId);
        }
        else
        {
            _stash[itemId] = count - 1;
        }

        return true;
    }

    /// <summary>
    /// 창고의 아이템을 장착할 수 없는 이유. null이면 장착할 수 있다.
    /// 원정 중, 창고에 없음, 장비가 아님, 착용 조건(패시브 레벨) 미달, 두손 무기를 낀 채 보조.
    /// </summary>
    public string? WhyCannotEquip(string memberId, string itemId, GameData data)
    {
        if (OnExpedition)
        {
            return "원정 중에는 장비를 바꿀 수 없습니다";
        }

        if (!data.Items.TryGetValue(itemId, out var item) || !item.IsEquipment)
        {
            return "장비가 아닙니다";
        }

        var member = Member(memberId);
        if (member.ItemIn(item.Slot) == itemId)
        {
            return null;
        }

        if (StashCount(itemId) == 0)
        {
            return "창고에 없습니다";
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
    /// 창고의 아이템을 그 부위에 장착한다. 끼고 있던 아이템은 창고로 돌아가고, 행동 칸은 첫 옵션으로 고른다.
    /// 두손 무기를 끼면 보조도 창고로 돌아간다. 이미 낀 아이템이면 아무것도 하지 않고 true.
    /// 장착할 수 없으면(<see cref="WhyCannotEquip"/>) false.
    /// </summary>
    public bool Equip(string memberId, string itemId, GameData data)
    {
        if (WhyCannotEquip(memberId, itemId, data) is not null)
        {
            return false;
        }

        var member = Member(memberId);
        var item = data.Items[itemId];
        if (member.ItemIn(item.Slot) == itemId)
        {
            return true;
        }

        TakeFromStash(itemId);
        Unequip(memberId, item.Slot, data);
        if (item.TwoHanded)
        {
            Unequip(memberId, EquipmentSlot.OffHand, data);
        }

        member.SetItem(item.Slot, itemId, data);
        return true;
    }

    /// <summary>그 부위의 아이템을 빼서 창고에 넣는다. 원정 중이거나 비어 있으면 false.</summary>
    public bool Unequip(string memberId, EquipmentSlot slot, GameData data)
    {
        var member = Member(memberId);
        if (OnExpedition || member.ItemIn(slot) is not { } current)
        {
            return false;
        }

        member.SetItem(slot, null, data);
        AddToStash(current);
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

    /// <summary>정가에 사서 창고에 넣는다. 살 수 없으면(<see cref="WhyCannotBuy"/>) false.</summary>
    public bool Buy(string itemId, GameData data)
    {
        if (WhyCannotBuy(itemId, data) is not null)
        {
            return false;
        }

        Gold -= data.Items[itemId].Price;
        AddToStash(itemId);
        return true;
    }

    /// <summary>창고의 아이템 하나를 판다(<see cref="Shop.SellPrice"/>). 원정 중이거나 창고에 없으면 false.</summary>
    public bool Sell(string itemId, GameData data)
    {
        if (OnExpedition || !data.Items.TryGetValue(itemId, out var item) || !TakeFromStash(itemId))
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

    /// <summary>재료와 골드를 내고 장비를 만들어 창고에 넣는다. 만들 수 없으면(<see cref="WhyCannotCraft"/>) false.</summary>
    public bool Craft(string resultId, GameData data)
    {
        if (WhyCannotCraft(resultId, data) is not null)
        {
            return false;
        }

        var recipe = data.Recipes[resultId];
        foreach (var material in recipe.Materials)
        {
            for (var i = 0; i < material.Count; i++)
            {
                TakeFromStash(material.ItemId);
            }
        }

        Gold -= recipe.Gold;
        AddToStash(resultId);
        return true;
    }

    /// <summary>모집 후보를 새로 굴린다 (원정이 끝날 때, 새 게임).</summary>
    public void RerollRecruits(GameData data)
    {
        _recruitOffers.Clear();
        _recruitOffers.AddRange(Recruitment.Roll(data, TakeSeed()));
    }

    /// <summary>
    /// 후보의 고용 비용. 로스터가 비었고 가장 싼 후보도 못 살 만큼 골드가 없으면, 가장 싼 후보 한 명은 무료다
    /// (영구 사망 지역에서 전멸해도 게임이 막히지 않도록).
    /// </summary>
    public int HirePrice(int offerIndex)
    {
        var price = _recruitOffers[offerIndex].Price;
        if (_roster.Count > 0 || _recruitOffers.Count == 0)
        {
            return price;
        }

        var cheapest = _recruitOffers.Min(o => o.Price);
        return Gold < cheapest && offerIndex == _recruitOffers.FindIndex(o => o.Price == cheapest) ? 0 : price;
    }

    public bool CanHire(int offerIndex) =>
        !OnExpedition && offerIndex >= 0 && offerIndex < _recruitOffers.Count && Gold >= HirePrice(offerIndex);

    /// <summary>
    /// 후보를 고용한다: 골드를 내고 로스터에 넣는다. 출전 명단에 자리가 있으면 명단에도 넣는다.
    /// 시작 장비는 새로 생긴다. 숙련 0, 패시브 없음, 두 전술 세트 모두 템플릿의 기본 전술.
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

        Gold -= HirePrice(offerIndex);
        _recruitOffers.RemoveAt(offerIndex);
        var member = new PartyMember(
            id, offer.Name, offer.Stats, template.Row, template.Equipment,
            new Dictionary<string, int>(), new Dictionary<string, int>(),
            _tacticSetNames.Select(_ => template.Tactics).ToList()); // 모든 세트를 모집 템플릿의 전술로 시작한다
        _roster.Add(member);
        AddToLineup(id);
        return member;
    }

    /// <summary>영구 사망: 로스터와 출전 명단에서 뺀다.</summary>
    internal void RemoveMember(string memberId)
    {
        _roster.RemoveAll(m => m.Id == memberId);
        _lineup.Remove(memberId);
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
