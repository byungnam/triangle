using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Masteries;

namespace Triangle.Core.Progress;

/// <summary>
/// 플레이어의 용병 회사. 세이브 데이터가 이 모델을 저장한다.
/// - 로스터: 보유한 캐릭터 모두. 출전 명단은 그중 최대 <see cref="MaxLineup"/>명이다.
/// - 골드와 창고(아이템 ID별 개수). 장착과 해제는 창고와 오간다.
/// - 모집 후보와 진행 중인 원정. 원정 중에는 명단, 장비, 모집을 바꿀 수 없다(전술과 전열만 바꾼다).
/// </summary>
public sealed class Company
{
    public const int MaxLineup = 5;

    /// <summary>새 게임과 v4 세이브 변환 때 주는 골드 (임시 수치).</summary>
    public const int StartingGold = 300;

    private readonly List<PartyMember> _roster;
    private readonly List<string> _lineup;
    private readonly Dictionary<string, int> _stash;
    private readonly List<RecruitOffer> _recruitOffers;
    private int _activeTacticSet;

    public Company(
        IEnumerable<PartyMember> roster,
        IEnumerable<string> lineup,
        int gold,
        IReadOnlyDictionary<string, int> stash,
        int activeTacticSet,
        int nextSeed,
        IEnumerable<RecruitOffer>? recruitOffers = null,
        int nextRecruitNumber = 1,
        Expedition? expedition = null)
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

    /// <summary>전투에 쓸 전술 세트 (0부터). 출전 멤버 전원이 같은 번호의 세트를 쓴다.</summary>
    public int ActiveTacticSet
    {
        get => _activeTacticSet;
        set => _activeTacticSet = value is >= 0 and < PartyMember.TacticSetCount
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Tactic set must be 0-{PartyMember.TacticSetCount - 1}.");
    }

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
    /// 창고의 아이템을 장착한다. 슬롯은 아이템의 계열이 정하고, 끼고 있던 아이템은 창고로 돌아간다.
    /// 이미 낀 아이템이면 아무것도 하지 않고 true. 창고에 없거나 원정 중이면 false.
    /// </summary>
    public bool Equip(string memberId, string itemId, GameData data)
    {
        if (OnExpedition)
        {
            return false;
        }

        var member = Member(memberId);
        var slot = data.Masteries[data.Items[itemId].Mastery].Slot;
        var current = slot == EquipmentSlot.Weapon ? member.Weapon : member.Armor;
        if (current == itemId)
        {
            return true;
        }

        if (!TakeFromStash(itemId))
        {
            return false;
        }

        if (current is not null)
        {
            AddToStash(current);
        }

        if (slot == EquipmentSlot.Weapon)
        {
            member.Weapon = itemId;
        }
        else
        {
            member.Armor = itemId;
        }

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
            id, offer.Name, offer.Stats, template.Row, template.Weapon, template.Armor,
            new Dictionary<string, int>(), new Dictionary<string, int>(), [template.Tactics, template.Tactics]);
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
}
