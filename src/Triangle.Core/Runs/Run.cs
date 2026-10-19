using System.Runtime.CompilerServices;
using Triangle.Core.Balance;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Items;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

namespace Triangle.Core.Runs;

public enum RunPhase
{
    /// <summary>지도에서 다음 지점을 고른다. 전술과 장비는 이때 고친다.</summary>
    Map,

    /// <summary>보상 카드 중 하나를 고르거나 건너뛴다 (전투, 단련, 이벤트).</summary>
    Reward,

    Shop,

    Rest,

    Event,

    /// <summary>마지막 막의 보스를 이겼다.</summary>
    Victory,

    /// <summary>지거나 비겼다(무승부는 패배다).</summary>
    Defeat,
}

public enum RunCardKind
{
    /// <summary>액티브 스킬 사본 한 장. 사본 한 장은 전술 한 줄에만 놓는다.</summary>
    Active,

    /// <summary>패시브 1레벨 (그 클래스 멤버 한 명에게).</summary>
    Passive,

    /// <summary>줄 상한 +1 (멤버 한 명에게).</summary>
    Line,
}

/// <summary>보상 카드. 액티브와 패시브는 그 클래스의 것이고, 줄 카드는 누구에게나 준다.</summary>
/// <param name="Id">행동 ID(액티브)나 패시브 ID. 줄 카드는 null.</param>
public sealed record RunCard(RunCardKind Kind, string? ClassId, string? Id);

public enum RunStockKind
{
    Item,
    Skill,
    Recruit,
}

/// <summary>상점의 물건 하나. <paramref name="Id"/>는 아이템 ID, 행동 ID, 클래스 ID 중 하나다.</summary>
public sealed record RunStock(RunStockKind Kind, string Id, int Price)
{
    public bool Sold { get; init; }
}

/// <summary>판의 전투 기록 한 줄 (판이 끝나면 명성과 탐사 기록에 쓴다).</summary>
public sealed record RunBattleRecord(int Act, string EncounterId, RunNodeKind Kind, CombatOutcome Outcome);

/// <summary>판을 시작할 때 해금이 정하는 것: 이 판에 나오지 않는 클래스·카드·아이템과 더 받는 시작 골드.</summary>
public sealed record RunUnlocks(IReadOnlySet<string> Locked, int StartGold)
{
    /// <summary>모두 열린 판 (시험·설계 도구용).</summary>
    public static RunUnlocks All { get; } = new(new HashSet<string>(), 0);

    /// <summary>연 해금 ID로 정한다: 열지 않은 해금이 적은 것은 잠기고, 연 해금의 시작 골드를 더한다.</summary>
    public static RunUnlocks For(GameData data, IReadOnlySet<string> unlocked) => new(
        data.Unlocks.Values.Where(u => !unlocked.Contains(u.Id)).SelectMany(u => u.Locks).ToHashSet(),
        data.Unlocks.Values.Where(u => unlocked.Contains(u.Id)).Sum(u => u.StartGold));
}

/// <summary>전투 하나의 결과. 봉인되거나 죽은 멤버 ID와 받은 골드를 함께 준다.</summary>
public sealed record RunBattle(string EncounterId, RunNodeKind Kind, CombatResult Result, IReadOnlyList<string> Sealed, IReadOnlyList<string> Died, int Gold);

/// <summary>
/// 한 판 (로그라이트 재편, 2026-10-19). 클래스로 파티를 꾸려 막마다 갈림길 지도를 따라 지점을 고르며 나아간다.
/// 규칙과 수치는 <see cref="RunDefinition"/>(`data/run.json`)에 있다. 같은 시드와 같은 선택이면 같은 판이 된다.
/// <list type="bullet">
/// <item>막 보스는 판을 시작할 때 정해 공개한다(<see cref="Bosses"/>). 보통 전투의 적은 들어가야 안다.</item>
/// <item>전투마다 HP·MP가 가득 찬다. 아이템은 행동을 주지 않는다: 쓸 수 있는 행동은 공용 행동과 전술에 놓은 사본뿐이다.
/// 행동과 아이템의 패시브 요구는 보지 않는다.</item>
/// <item>이긴 전투에서 쓰러진 멤버는 봉인된다(줄 상한 -1). 줄 상한이 0인데 또 쓰러지면 죽는다: 놓았던 사본은 파티로 돌아오고
/// 장비는 가방으로 간다. 패시브와 줄 상한은 사라진다.</item>
/// <item>지거나 비기면 판이 끝난다. 마지막 막의 보스를 이기면 승리다.</item>
/// </list>
/// </summary>
public sealed partial class Run
{
    private static readonly ConditionalWeakTable<GameData, CombatCatalog> Catalogs = new();

    private readonly List<RunStock> _stock = [];
    private readonly List<int> _path = [];
    private readonly List<RunBattleRecord> _history = [];
    private bool _bossCleared;

    private Run(int seed, IReadOnlyList<RunMap> maps, IReadOnlyList<string> bosses, int gold, IReadOnlySet<string> locked)
    {
        Seed = seed;
        Maps = maps;
        Bosses = bosses;
        Gold = gold;
        Locked = locked;
    }

    /// <summary>이 판에 나오지 않는 클래스·카드·아이템 (아직 열지 않은 해금, 판을 시작할 때 정한다).</summary>
    public IReadOnlySet<string> Locked { get; }

    /// <summary>치른 전투 (치른 순서).</summary>
    public IReadOnlyList<RunBattleRecord> History => _history;

    public int Seed { get; }

    public RunPhase Phase { get; private set; } = RunPhase.Map;

    /// <summary>지금 막 (0부터).</summary>
    public int Act { get; private set; }

    /// <summary>지금 서 있는 지점 ID. 막을 막 시작했으면 null.</summary>
    public int? Position { get; private set; }

    /// <summary>이 막에서 지나온 지점 ID (들어간 순서). 다음 막으로 가면 비운다.</summary>
    public IReadOnlyList<int> Path => _path;

    /// <summary>막마다 지도. 판을 시작할 때 모두 만든다.</summary>
    public IReadOnlyList<RunMap> Maps { get; }

    /// <summary>막마다 보스 (판을 시작할 때 공개된다).</summary>
    public IReadOnlyList<string> Bosses { get; }

    public RunMap Map => Maps[Act];

    /// <summary>합류한 순서의 멤버. 죽은 멤버도 남는다(<see cref="RunMember.Dead"/>).</summary>
    public List<RunMember> Members { get; } = [];

    public IEnumerable<RunMember> Living => Members.Where(m => !m.Dead);

    /// <summary>파티가 가진 스킬 사본 (행동 ID → 장 수). 전술에 놓은 줄 수가 이를 넘을 수 없다.</summary>
    public Dictionary<string, int> Copies { get; } = [];

    /// <summary>끼지 않은 아이템 ID.</summary>
    public List<string> Bag { get; } = [];

    public int Gold { get; private set; }

    /// <summary>싸운 전투 수. 전투 시드를 가른다.</summary>
    public int Fights { get; private set; }

    /// <summary>무작위를 쓰는 결정을 내린 횟수. 보상·상점·이벤트의 시드를 가른다.</summary>
    public int Step { get; private set; }

    /// <summary>고를 보상 카드 (<see cref="RunPhase.Reward"/>일 때).</summary>
    public IReadOnlyList<RunCard> Offer { get; private set; } = [];

    /// <summary>상점의 물건 (<see cref="RunPhase.Shop"/>일 때).</summary>
    public IReadOnlyList<RunStock> Stock => _stock;

    /// <summary>지금 이벤트 (<see cref="RunPhase.Event"/>일 때).</summary>
    public RunEvent? Event { get; private set; }

    public bool Finished => Phase is RunPhase.Victory or RunPhase.Defeat;

    /// <summary>판을 포기한다 (패배로 끝난다).</summary>
    public void Abandon()
    {
        if (!Finished)
        {
            Phase = RunPhase.Defeat;
        }
    }

    public static string? WhyCannotStart(GameData data, IReadOnlyList<string> classIds, RunUnlocks? unlocks = null)
    {
        if (unlocks is not null && classIds.FirstOrDefault(unlocks.Locked.Contains) is { } locked && data.Classes.TryGetValue(locked, out var lockedClass))
        {
            return $"아직 열지 않은 클래스: {lockedClass.Name}";
        }

        var rules = Rules(data);
        if (classIds.Count < 1 || classIds.Count > rules.PartySize)
        {
            return $"파티는 1명에서 {rules.PartySize}명이다";
        }

        return classIds.FirstOrDefault(id => !data.Classes.ContainsKey(id)) is { } unknown ? $"없는 클래스: {unknown}" : null;
    }

    /// <summary>판을 시작한다. 막마다 지도를 만들고 보스를 정해 공개한다. 멤버는 클래스의 기본 장비와 시작 스킬 한 줄로 시작한다.</summary>
    /// <param name="unlocks">해금이 정하는 잠긴 것과 시작 골드. null이면 모두 열린 판이다.</param>
    /// <param name="rows">멤버마다 서는 줄 (<paramref name="classIds"/>와 같은 순서). null이거나 모자라면 클래스의 처음 값이다.</param>
    public static Run Start(GameData data, IReadOnlyList<string> classIds, int seed, RunUnlocks? unlocks = null, IReadOnlyList<Row>? rows = null)
    {
        unlocks ??= RunUnlocks.All;
        if (WhyCannotStart(data, classIds, unlocks) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        var rules = Rules(data);
        var maps = rules.Acts.Select((a, i) => RunMap.Generate(rules.Map, a.Floors, a.Elites.Count > 0, Seeds.Derive(seed, 100 + i))).ToList();
        var random = new Random(Seeds.Derive(seed, 200));
        var bosses = rules.Acts.Select(a => a.Bosses.Count > 0 ? a.Bosses[random.Next(a.Bosses.Count)] : data.Zones[a.ZoneId].Boss!.EncounterId).ToList();
        var run = new Run(seed, maps, bosses, rules.StartGold + unlocks.StartGold, unlocks.Locked);
        for (var i = 0; i < classIds.Count; i++)
        {
            run.Join(data, classIds[i], rows is not null && i < rows.Count ? rows[i] : null);
        }

        return run;
    }

    /// <summary>지금 갈 수 있는 지점. 지도 단계가 아니면 없다.</summary>
    public IReadOnlyList<RunNode> Choices =>
        Phase != RunPhase.Map ? [] : Position is { } at ? Map.Node(at).Next.Select(Map.Node).ToList() : Map.Starts.ToList();

    /// <summary>
    /// 지점에 들어간다. 전투·정예·보스면 바로 싸우고 그 결과를 돌려준다(이기면 보상 단계, 지거나 비기면 패배).
    /// 상점·휴식·이벤트면 그 단계가 되고 null을 돌려준다.
    /// </summary>
    public RunBattle? Enter(GameData data, int nodeId)
    {
        if (Choices.All(n => n.Id != nodeId))
        {
            throw new InvalidOperationException("지금 갈 수 없는 지점이다");
        }

        var node = Map.Node(nodeId);
        var random = Stream(0);
        Position = nodeId;
        _path.Add(nodeId);
        Step++;
        switch (node.Kind)
        {
            case RunNodeKind.Shop:
                OpenShop(data, random);
                return null;
            case RunNodeKind.Rest:
                Phase = RunPhase.Rest;
                return null;
            case RunNodeKind.Event:
                var events = Rules(data).Events;
                if (events.Count == 0)
                {
                    return null;
                }

                Event = events[random.Next(events.Count)];
                Phase = RunPhase.Event;
                return null;
            default:
                return Fight(data, node.Kind, random);
        }
    }

    private RunBattle Fight(GameData data, RunNodeKind kind, Random random)
    {
        var rules = Rules(data);
        var act = rules.Acts[Act];
        var encounter = kind switch
        {
            RunNodeKind.Boss => Bosses[Act],
            RunNodeKind.Elite => act.Elites[random.Next(act.Elites.Count)],
            _ => Draw(data.Zones[act.ZoneId].Encounters, random),
        };
        Fights++;
        var at = Act;
        var allies = Living.Select(m => Setup(data, m)).ToList();
        var result = CombatSimulator.Run(allies, data.CreateEncounterTeam(encounter), Catalog(data), Seeds.Derive(Seed, Fights));
        _history.Add(new RunBattleRecord(at, encounter, kind, result.Outcome));
        if (result.Outcome != CombatOutcome.Victory)
        {
            Phase = RunPhase.Defeat;
            return new RunBattle(encounter, kind, result, [], [], 0);
        }

        var (sealedIds, died) = (new List<string>(), new List<string>());
        foreach (var down in result.Combatants.Where(c => c.Side == CombatSide.Ally && !c.IsSummon && !c.IsAlive))
        {
            var member = Members.First(m => m.Id == down.Id);
            if (member.Lines == 0)
            {
                Die(member);
                died.Add(member.Id);
            }
            else
            {
                member.Sealed++;
                sealedIds.Add(member.Id);
            }
        }

        if (!Living.Any())
        {
            Phase = RunPhase.Defeat;
            return new RunBattle(encounter, kind, result, sealedIds, died, 0);
        }

        var reward = rules.Reward;
        var gold = kind switch
        {
            RunNodeKind.Boss => reward.BossGold,
            RunNodeKind.Elite => random.Next(reward.EliteGoldMin, reward.EliteGoldMax + 1),
            _ => random.Next(reward.GoldMin, reward.GoldMax + 1),
        };
        Gold += gold;
        if (kind == RunNodeKind.Boss && Act == rules.Acts.Count - 1)
        {
            Phase = RunPhase.Victory;
        }
        else
        {
            _bossCleared = kind == RunNodeKind.Boss;
            Offer = MakeOffer(data, random, reward.Cards + (kind == RunNodeKind.Elite ? reward.EliteExtraCards : 0));
            Phase = RunPhase.Reward;
        }

        return new RunBattle(encounter, kind, result, sealedIds, died, gold);
    }

    /// <summary>
    /// 보상 카드 하나를 고른다. 액티브는 파티의 사본이 되고(멤버를 묻지 않는다), 패시브와 줄 상한은 <paramref name="member"/>가 받는다.
    /// </summary>
    public string? WhyCannotTake(GameData data, int card, RunMember? member)
    {
        if (Phase != RunPhase.Reward || card < 0 || card >= Offer.Count)
        {
            return "고를 카드가 없다";
        }

        var c = Offer[card];
        return c.Kind switch
        {
            RunCardKind.Active => null,
            _ when member is null || member.Dead || !Members.Contains(member) => "받을 멤버를 골라야 한다",
            RunCardKind.Passive when member.ClassId != c.ClassId => $"{data.Classes[c.ClassId!].Name}만 받는다",
            RunCardKind.Passive when member.Level(c.Id!) >= SkillDefinition.MaxLevel => "이미 최고 레벨이다",
            RunCardKind.Line when member.Earned >= Rules(data).MaxLines => "줄 상한이 이미 최대다",
            _ => null,
        };
    }

    public void Take(GameData data, int card, RunMember? member = null)
    {
        if (WhyCannotTake(data, card, member) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        var c = Offer[card];
        switch (c.Kind)
        {
            case RunCardKind.Active:
                Copies[c.Id!] = Copies.GetValueOrDefault(c.Id!) + 1;
                break;
            case RunCardKind.Passive:
                member!.Passives[c.Id!] = member.Level(c.Id!) + 1;
                break;
            default:
                member!.Earned++;
                break;
        }

        FinishReward();
    }

    /// <summary>보상을 건너뛴다.</summary>
    public void Skip()
    {
        if (Phase != RunPhase.Reward)
        {
            throw new InvalidOperationException("건너뛸 보상이 없다");
        }

        FinishReward();
    }

    private void FinishReward()
    {
        Offer = [];
        Phase = RunPhase.Map;
        if (_bossCleared)
        {
            _bossCleared = false;
            Act++;
            Position = null;
            _path.Clear();
        }
    }

    /// <summary>휴식: 모든 멤버의 봉인을 정한 수만큼 푼다.</summary>
    public void RestUnseal(GameData data)
    {
        RequirePhase(RunPhase.Rest);
        foreach (var member in Living)
        {
            member.Sealed = Math.Max(0, member.Sealed - Rules(data).Rest.Unseal);
        }

        Phase = RunPhase.Map;
    }

    /// <summary>휴식: 단련. 살아 있는 멤버의 클래스 패시브 후보 중 하나를 고른다(보상 단계가 된다). 올릴 패시브가 없으면 그냥 끝난다.</summary>
    public void RestTrain(GameData data)
    {
        RequirePhase(RunPhase.Rest);
        var random = Stream(1);
        Step++;
        var candidates = Living.SelectMany(m => data.Classes[m.ClassId].Passives.Where(p => m.Level(p) < SkillDefinition.MaxLevel).Select(p => new RunCard(RunCardKind.Passive, m.ClassId, p)))
            .Distinct()
            .ToList();
        Offer = Pick(candidates, Rules(data).Rest.TrainCards, random);
        Phase = Offer.Count > 0 ? RunPhase.Reward : RunPhase.Map;
    }

    public string? WhyCannotBuy(GameData data, int stock)
    {
        if (Phase != RunPhase.Shop || stock < 0 || stock >= _stock.Count)
        {
            return "살 물건이 없다";
        }

        var s = _stock[stock];
        if (s.Sold)
        {
            return "이미 팔렸다";
        }

        if (Gold < s.Price)
        {
            return $"골드가 모자란다 ({s.Price}G)";
        }

        return s.Kind == RunStockKind.Recruit && Living.Count() >= Company.MaxLineup ? $"파티는 {Company.MaxLineup}명까지다" : null;
    }

    /// <summary>상점에서 산다: 아이템은 가방으로, 스킬은 파티 사본으로, 합류는 새 멤버로.</summary>
    public void Buy(GameData data, int stock)
    {
        if (WhyCannotBuy(data, stock) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        var s = _stock[stock];
        Gold -= s.Price;
        _stock[stock] = s with { Sold = true };
        switch (s.Kind)
        {
            case RunStockKind.Item:
                Bag.Add(s.Id);
                break;
            case RunStockKind.Skill:
                Copies[s.Id] = Copies.GetValueOrDefault(s.Id) + 1;
                break;
            default:
                Join(data, s.Id);
                break;
        }
    }

    public string? WhyCannotUnseal(GameData data, RunMember member)
    {
        if (Phase != RunPhase.Shop)
        {
            return "상점에서만 푼다";
        }

        if (member.Dead || member.Sealed == 0)
        {
            return "풀 봉인이 없다";
        }

        return Gold < Rules(data).Shop.UnsealPrice ? $"골드가 모자란다 ({Rules(data).Shop.UnsealPrice}G)" : null;
    }

    /// <summary>상점에서 골드를 내고 멤버 한 명의 봉인 하나를 푼다.</summary>
    public void Unseal(GameData data, RunMember member)
    {
        if (WhyCannotUnseal(data, member) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        Gold -= Rules(data).Shop.UnsealPrice;
        member.Sealed--;
    }

    public void LeaveShop()
    {
        RequirePhase(RunPhase.Shop);
        _stock.Clear();
        Phase = RunPhase.Map;
    }

    public string? WhyCannotChoose(int option)
    {
        if (Phase != RunPhase.Event || Event is null || option < 0 || option >= Event.Options.Count)
        {
            return "고를 선택지가 없다";
        }

        var o = Event.Options[option];
        if (o.Gold < 0 && Gold < -o.Gold)
        {
            return $"골드가 모자란다 ({-o.Gold}G)";
        }

        if (o.Join is { } join && join != "any" && !Open(join))
        {
            return "아직 열지 않은 클래스다";
        }

        return o.Join is not null && Living.Count() >= Company.MaxLineup ? $"파티는 {Company.MaxLineup}명까지다" : null;
    }

    /// <summary>이벤트의 선택지를 고른다. 카드가 있으면 보상 단계가 된다.</summary>
    public void Choose(GameData data, int option)
    {
        if (WhyCannotChoose(option) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        var o = Event!.Options[option];
        var random = Stream(2);
        Step++;
        Gold += o.Gold;
        if (o.Join is { } join)
        {
            var classes = data.Classes.Keys.Where(Open).ToList();
            Join(data, join == "any" ? classes[random.Next(classes.Count)] : join);
        }

        if (o.Seal > 0)
        {
            var living = Living.ToList();
            var member = living[random.Next(living.Count)];
            member.Sealed = Math.Min(member.Earned, member.Sealed + o.Seal);
        }

        foreach (var member in Living)
        {
            member.Sealed = Math.Max(0, member.Sealed - o.Unseal);
        }

        if (o.Trinket)
        {
            var tier = Rules(data).Acts[Act].ShopTier;
            var trinkets = data.Items.Values.Where(i => i.Slot == EquipmentSlot.Trinket && i.Tier <= tier && Open(i.Id)).ToList();
            if (trinkets.Count > 0)
            {
                Bag.Add(trinkets[random.Next(trinkets.Count)].Id);
            }
        }

        Event = null;
        Offer = o.Card ? MakeOffer(data, random, Rules(data).Reward.Cards) : [];
        Phase = Offer.Count > 0 ? RunPhase.Reward : RunPhase.Map;
    }

    /// <summary>
    /// 멤버의 전술 줄을 바꿀 수 있는가. 행동은 공용이거나 그 클래스의 카드여야 하고, 같은 행동을 놓은 줄 수(살아 있는 모든 멤버)는
    /// 파티가 가진 사본 수를 넘을 수 없다. 줄은 줄 상한의 최대치까지 적을 수 있다(상한을 넘는 줄은 꺼져 있다).
    /// </summary>
    public string? WhyCannotSetTactics(GameData data, RunMember member, IReadOnlyList<Tactic> lines)
    {
        if (member.Dead || !Members.Contains(member))
        {
            return "살아 있는 멤버가 아니다";
        }

        if (lines.Count > Rules(data).MaxLines)
        {
            return $"줄은 {Rules(data).MaxLines}개까지다";
        }

        var skills = data.Classes[member.ClassId].Skills;
        foreach (var line in lines)
        {
            var errors = new List<string>();
            DataValidation.ValidateTactic(line, "tactic", data.Actions, errors);
            if (errors.Count > 0)
            {
                return errors[0];
            }

            var action = data.Actions[line.ActionId];
            if (!action.Universal && !skills.Contains(action.Id))
            {
                return $"{data.Classes[member.ClassId].Name}의 카드가 아니다: {action.Name}";
            }
        }

        var placed = Living.Where(m => m != member).SelectMany(m => m.Tactics).Concat(lines)
            .Where(t => !data.Actions[t.ActionId].Universal)
            .GroupBy(t => t.ActionId);
        foreach (var group in placed)
        {
            if (group.Count() > Copies.GetValueOrDefault(group.Key))
            {
                return $"사본이 모자란다: {data.Actions[group.Key].Name} ({Copies.GetValueOrDefault(group.Key)}장)";
            }
        }

        return null;
    }

    public void SetTactics(GameData data, RunMember member, IReadOnlyList<Tactic> lines)
    {
        if (WhyCannotSetTactics(data, member, lines) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        member.Tactics.Clear();
        member.Tactics.AddRange(lines.Select((t, i) => t with { Priority = i + 1 }));
    }

    /// <summary>멤버의 줄(전열·후열)을 바꾼다. 클래스와 상관없이 고른다.</summary>
    public string? WhyCannotSetRow(RunMember member)
    {
        if (Finished)
        {
            return "판이 끝났다";
        }

        return member.Dead || !Members.Contains(member) ? "살아 있는 멤버가 아니다" : null;
    }

    public void SetRow(RunMember member, Row row)
    {
        if (WhyCannotSetRow(member) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        member.Row = row;
    }

    /// <summary>가방의 아이템을 낀다. 무기는 그 클래스 계열만, 방어구는 몸통, 장신구는 정한 수까지. 끼고 있던 것은 가방으로 간다.</summary>
    public string? WhyCannotEquip(GameData data, RunMember member, int bagIndex)
    {
        if (Phase == RunPhase.Defeat || Phase == RunPhase.Victory)
        {
            return "판이 끝났다";
        }

        if (member.Dead || bagIndex < 0 || bagIndex >= Bag.Count)
        {
            return "낄 수 없다";
        }

        var item = data.Items[Bag[bagIndex]];
        return item.Slot switch
        {
            EquipmentSlot.MainHand when !data.Classes[member.ClassId].CanWield(item) => $"{data.Classes[member.ClassId].Name}는 이 무기를 쓰지 않는다",
            EquipmentSlot.MainHand or EquipmentSlot.Body => null,
            EquipmentSlot.Trinket when member.Trinkets.Count >= Rules(data).MaxTrinkets => $"장신구는 {Rules(data).MaxTrinkets}개까지다",
            EquipmentSlot.Trinket => null,
            _ => "원정에서 쓰지 않는 부위다",
        };
    }

    public void Equip(GameData data, RunMember member, int bagIndex)
    {
        if (WhyCannotEquip(data, member, bagIndex) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        var id = Bag[bagIndex];
        Bag.RemoveAt(bagIndex);
        switch (data.Items[id].Slot)
        {
            case EquipmentSlot.MainHand:
                Stash(member.Weapon);
                member.Weapon = id;
                break;
            case EquipmentSlot.Body:
                Stash(member.Armor);
                member.Armor = id;
                break;
            default:
                member.Trinkets.Add(id);
                break;
        }
    }

    /// <summary>무기, 방어구, 또는 장신구 하나(<paramref name="trinket"/>번째)를 벗어 가방에 넣는다.</summary>
    public void Unequip(RunMember member, EquipmentSlot slot, int trinket = 0)
    {
        switch (slot)
        {
            case EquipmentSlot.MainHand:
                Stash(member.Weapon);
                member.Weapon = null;
                break;
            case EquipmentSlot.Body:
                Stash(member.Armor);
                member.Armor = null;
                break;
            case EquipmentSlot.Trinket when trinket >= 0 && trinket < member.Trinkets.Count:
                Bag.Add(member.Trinkets[trinket]);
                member.Trinkets.RemoveAt(trinket);
                break;
            default:
                throw new InvalidOperationException("벗을 것이 없다");
        }
    }

    /// <summary>
    /// 멤버의 전투 입력: 클래스 스탯과 줄, 낀 장비의 보너스와 효과, 패시브, 켜진 전술 줄에 "항상 → 기본 공격"을 붙인 것.
    /// 쓸 수 있는 행동은 공용 행동과 켜진 줄의 행동뿐이다.
    /// </summary>
    public CombatantSetup Setup(GameData data, RunMember member)
    {
        var c = data.Classes[member.ClassId];
        var worn = new List<(EquipmentSlot, ItemDefinition)>();
        if (member.Weapon is { } weapon)
        {
            worn.Add((EquipmentSlot.MainHand, data.Items[weapon]));
        }

        if (member.Armor is { } armor)
        {
            worn.Add((EquipmentSlot.Body, data.Items[armor]));
        }

        worn.AddRange(member.Trinkets.Select(t => (EquipmentSlot.Trinket, data.Items[t])));
        var lines = member.ActiveTactics;
        List<Tactic> tactics = [.. lines.Select((t, i) => t with { Priority = i + 1 }), Tactic.Create(lines.Count + 1, [new(Condition.Always, 0)], DraftTactics.Fallback)];
        var setup = new CombatantSetup(member.Id, Name(data, member), c.Stats, member.Row, null, null, member.Passives.ToDictionary(), tactics);
        return new Loadout(worn).Apply(setup, data.Actions.Values, _ => 0) with
        {
            GrantedActions = lines.Select(t => t.ActionId).Where(id => !data.Actions[id].Universal).ToHashSet(),
        };
    }

    /// <summary>표시 이름: 클래스 이름. 같은 클래스가 여럿이면 합류 순서 번호를 붙인다.</summary>
    public string Name(GameData data, RunMember member)
    {
        var same = Members.Where(m => m.ClassId == member.ClassId).ToList();
        var name = data.Classes[member.ClassId].Name;
        return same.Count > 1 ? $"{name} {same.IndexOf(member) + 1}" : name;
    }

    /// <summary>요구 패시브를 뺀 행동 묶음 (재편에서는 행동이 패시브를 요구하지 않는다).</summary>
    public static CombatCatalog Catalog(GameData data) =>
        Catalogs.GetValue(data, d => d.Catalog with { Actions = d.Catalog.Actions.ToDictionary(p => p.Key, p => p.Value with { Requirements = [] }) });

    private static RunDefinition Rules(GameData data) => data.Run ?? throw new InvalidOperationException("run.json이 없다");

    /// <summary>새 멤버: 클래스의 기본 장비, 시작 스킬 사본 1장(파티 사본에 더한다), 그 사본으로 짠 한 줄.</summary>
    private void Join(GameData data, string classId, Row? row = null)
    {
        var c = data.Classes[classId];
        var rules = Rules(data);
        var member = new RunMember($"m{Members.Count + 1}", classId, rules.StartLines) { Row = row ?? c.Row, Weapon = c.Weapon, Armor = c.Armor };
        Copies[c.StartingSkill] = Copies.GetValueOrDefault(c.StartingSkill) + 1;
        if (rules.StartLines > 0)
        {
            member.Tactics.Add(DraftTactics.Build([c.StartingSkill], 1, data)[0]);
        }

        Members.Add(member);
    }

    /// <summary>죽는다: 줄을 비워 사본을 파티로 돌리고 장비는 가방에 넣는다. 패시브와 줄 상한은 함께 사라진다.</summary>
    private void Die(RunMember member)
    {
        member.Dead = true;
        member.Tactics.Clear();
        Stash(member.Weapon);
        Stash(member.Armor);
        Bag.AddRange(member.Trinkets);
        (member.Weapon, member.Armor) = (null, null);
        member.Trinkets.Clear();
    }

    private void Stash(string? itemId)
    {
        if (itemId is not null)
        {
            Bag.Add(itemId);
        }
    }

    /// <summary>
    /// 상점의 물건: 이 막 티어의 무기(살아 있는 멤버 클래스가 쓰는 것)와 방어구, 그 티어 이하의 장신구에서 아이템을, 살아 있는 멤버
    /// 클래스의 카드에서 스킬을, 모든 클래스에서 합류를 뽑는다.
    /// </summary>
    private void OpenShop(GameData data, Random random)
    {
        var rules = Rules(data);
        var tier = rules.Acts[Act].ShopTier;
        var classes = Living.Select(m => data.Classes[m.ClassId]).Distinct().ToList();
        var items = data.Items.Values
            .Where(i => Open(i.Id))
            .Where(i => i.Slot switch
            {
                EquipmentSlot.MainHand => i.Tier == tier && classes.Any(c => c.CanWield(i)),
                EquipmentSlot.Body => i.Tier == tier,
                EquipmentSlot.Trinket => i.Tier <= tier,
                _ => false,
            })
            .ToList();
        _stock.Clear();
        _stock.AddRange(Pick(items, rules.Shop.Items, random).Select(i => new RunStock(RunStockKind.Item, i.Id, i.Price)));
        _stock.AddRange(Pick(classes.SelectMany(c => c.Skills).Where(Open).Distinct().ToList(), rules.Shop.Skills, random)
            .Select(id => new RunStock(RunStockKind.Skill, id, rules.Shop.SkillPrice)));
        _stock.AddRange(Pick(data.Classes.Keys.Where(Open).ToList(), rules.Shop.Recruits, random)
            .Select(id => new RunStock(RunStockKind.Recruit, id, rules.Shop.RecruitPrice)));
        Phase = RunPhase.Shop;
    }

    /// <summary>
    /// 보상 후보: 종류를 가중치로 뽑고, 액티브·패시브는 살아 있는 멤버 하나를 뽑아 그 클래스에서 고른다. 같은 카드는 한 번만 나온다.
    /// 패시브는 올릴 수 있는 멤버가 있을 때만, 줄 상한은 늘릴 수 있는 멤버가 있을 때만 나온다.
    /// </summary>
    private List<RunCard> MakeOffer(GameData data, Random random, int count)
    {
        var rules = Rules(data);
        var reward = rules.Reward;
        var living = Living.ToList();
        var cards = new List<RunCard>();
        for (var tries = 0; cards.Count < count && tries < count * 20; tries++)
        {
            var roll = random.Next(reward.Active + reward.Passive + reward.Line);
            var c = data.Classes[living[random.Next(living.Count)].ClassId];
            RunCard? card = null;
            if (roll < reward.Active)
            {
                var pool = c.Skills.Where(Open).ToList();
                card = pool.Count == 0 ? null : new RunCard(RunCardKind.Active, c.Id, pool[random.Next(pool.Count)]);
            }
            else if (roll < reward.Active + reward.Passive)
            {
                var pool = c.Passives.Where(p => living.Any(m => m.ClassId == c.Id && m.Level(p) < SkillDefinition.MaxLevel)).ToList();
                card = pool.Count == 0 ? null : new RunCard(RunCardKind.Passive, c.Id, pool[random.Next(pool.Count)]);
            }
            else if (living.Any(m => m.Earned < rules.MaxLines))
            {
                card = new RunCard(RunCardKind.Line, null, null);
            }

            if (card is not null && !cards.Contains(card))
            {
                cards.Add(card);
            }
        }

        return cards;
    }

    /// <summary>후보에서 겹치지 않게 <paramref name="count"/>개를 뽑는다 (후보가 모자라면 있는 만큼).</summary>
    private static List<T> Pick<T>(IReadOnlyList<T> candidates, int count, Random random)
    {
        var left = candidates.ToList();
        var picked = new List<T>();
        while (picked.Count < count && left.Count > 0)
        {
            var i = random.Next(left.Count);
            picked.Add(left[i]);
            left.RemoveAt(i);
        }

        return picked;
    }

    private static string Draw(IReadOnlyList<ZoneEncounter> encounters, Random random)
    {
        var roll = random.Next(encounters.Sum(e => e.Weight));
        return encounters.First(e => (roll -= e.Weight) < 0).EncounterId;
    }

    /// <summary>이 판에 나오는 클래스·카드·아이템인가 (해금으로 잠기지 않았다).</summary>
    private bool Open(string id) => !Locked.Contains(id);

    /// <summary>이 결정의 무작위 줄기. 같은 시드, 같은 결정 순서면 같다.</summary>
    private Random Stream(int purpose) => new(Seeds.Derive(Seeds.Derive(Seed, 1000 + Step), purpose));

    private void RequirePhase(RunPhase phase)
    {
        if (Phase != phase)
        {
            throw new InvalidOperationException($"지금은 {phase} 단계가 아니다");
        }
    }
}
