using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Items;
using Triangle.Core.Progress;
using Triangle.Core.Skills;

namespace Triangle.Core.Balance;

public enum DraftCardKind
{
    /// <summary>액티브 스킬 사본 한 장. 사본 한 장은 전술 한 줄에만 놓는다.</summary>
    Active,

    /// <summary>패시브 1레벨.</summary>
    Passive,

    /// <summary>줄 상한 +1.</summary>
    Line,
}

/// <summary>전투 보상 카드. 액티브와 패시브는 그 클래스의 것이고, 줄 카드는 누구에게나 준다.</summary>
/// <param name="Id">행동 ID(액티브)나 패시브 ID. 줄 카드는 null.</param>
public sealed record DraftCard(DraftCardKind Kind, string? ClassId, string? Id);

/// <summary>고른 카드와 받는 멤버(파티 안의 번호).</summary>
public sealed record DraftChoice(DraftCard Card, int Member);

public enum DraftPicker
{
    /// <summary>후보 중 아무거나, 받을 멤버도 아무나.</summary>
    Random,

    /// <summary>후보와 받을 멤버마다 지금 막의 적 조합과 보스를 미리 싸워 보고 가장 잘 싸우는 쪽을 고른다.</summary>
    Greedy,

    /// <summary>
    /// 외워 둔 순위표(<see cref="DraftOptions.TierList"/>)에서 점수가 가장 높은 카드. 파티의 지금 모습과 다음 적은 보지 않는다.
    /// 앞을 보는 봇만큼 잘하면 고르기의 답이 뻔하다는 뜻이다.
    /// </summary>
    TierList,
}

public sealed record DraftOptions
{
    public DraftPicker Picker { get; init; } = DraftPicker.Greedy;

    /// <summary>앞을 보는 봇이 후보마다 적 조합 하나를 미리 싸워 보는 시드 수.</summary>
    public int LookaheadSeeds { get; init; } = 4;

    /// <summary>시작 줄 상한을 정의 대신 이 값으로 (null이면 정의대로).</summary>
    public int? StartLines { get; init; }

    /// <summary>
    /// 앞을 보는 봇이 그 판의 막 보스를 알고 미리 싸워 보는가. 보스는 판을 시작할 때 정해지고 공개된다(<see cref="DraftAct.Bosses"/>).
    /// 거짓이면 그 막의 보통 적 조합만 미리 싸워 본다.
    /// </summary>
    public bool KnowsBoss { get; init; } = true;

    /// <summary>순위표 봇의 카드 점수. 없는 카드는 0점이다.</summary>
    public IReadOnlyDictionary<DraftCard, double> TierList { get; init; } = new Dictionary<DraftCard, double>();
}

/// <summary>한 판의 결과.</summary>
/// <param name="Outcome">마지막 전투의 결과. 클리어면 승리다.</param>
/// <param name="FightsWon">이긴 전투 수 (보스 포함).</param>
/// <param name="Act">끝난 막 (0부터). 클리어면 막 수.</param>
/// <param name="Downs">이긴 전투에서 쓰러진 횟수. 봉인이나 사망으로 이어진다.</param>
/// <param name="FirstSeal">처음 봉인된 전투 번호 (1부터). 봉인이 없었으면 null.</param>
/// <param name="Picks">고른 카드 차례대로.</param>
/// <param name="Encounter">마지막으로 싸운 적 조합 (졌다면 진 상대).</param>
/// <param name="Offers">보상마다 나온 후보 카드 (<paramref name="Picks"/>와 같은 차례).</param>
/// <param name="IdleSkills">판이 끝날 때 줄 상한이 모자라 놓지 못한 사본 수 (살아 있는 멤버).</param>
public sealed record DraftRunResult(
    CombatOutcome Outcome,
    int FightsWon,
    int Act,
    int Downs,
    int Deaths,
    int? FirstSeal,
    IReadOnlyList<DraftChoice> Picks,
    string Encounter,
    IReadOnlyList<IReadOnlyList<DraftCard>> Offers,
    int IdleSkills)
{
    public bool Cleared => Outcome == CombatOutcome.Victory;
}

/// <summary>
/// 드래프트 시뮬레이션: 로그라이트 재편(설계, 구현 전)의 한 판을 지금 데이터로 돌린다. 클래스 파티가 막마다 지역 적 조합을 차례로
/// 싸우고(마지막은 보스), 이길 때마다 보상 카드 중 하나를 고른다. 봇 둘(<see cref="DraftPicker"/>)의 차이로 "고르기가 결과를
/// 바꾸는가"를 잰다. 지도, 상점, 이벤트, 멤버 합류는 아직 없다(장비는 막마다 정한 티어로 바꿔 낀다).
/// <list type="bullet">
/// <item>전투마다 HP·MP가 가득 찬다. 전술은 <see cref="DraftTactics"/>가 사본으로 짠다.</item>
/// <item>아이템은 행동을 주지 않는다: 쓸 수 있는 행동은 기본 공격과 가진 사본뿐이다. 행동의 패시브 요구는 없앤다.</item>
/// <item>이긴 전투에서 쓰러진 멤버는 줄 상한이 1 준다(봉인). 줄 상한이 0인데 또 쓰러지면 죽고, 사본은 같은 클래스 멤버에게 간다.</item>
/// <item>지거나 비기면 판이 끝난다. 막의 보스를 이기면 봉인을 정의만큼 푼다.</item>
/// </list>
/// </summary>
public sealed class DraftSimulation
{
    private const int LookaheadSalt = 0x5EED;

    private readonly DraftDefinition _draft;
    private readonly GameData _data;
    private readonly DraftOptions _options;
    private readonly CombatCatalog _catalog;
    private readonly CombatRules _rules;
    private readonly Dictionary<(string ClassId, int Tier), CombatantSetup> _bases = [];
    private readonly Dictionary<string, IReadOnlyList<CombatantSetup>> _enemies = [];
    private readonly Dictionary<string, IReadOnlyList<string>> _actives;
    private readonly Dictionary<string, IReadOnlyList<string>> _passives;

    public DraftSimulation(DraftDefinition draft, GameData data, DraftOptions? options = null)
    {
        _draft = draft;
        _data = data;
        _options = options ?? new DraftOptions();
        _rules = draft.MaxActions is { } max ? CombatRules.Default with { MaxActions = max } : CombatRules.Default;
        _catalog = data.Catalog with { Actions = data.Catalog.Actions.ToDictionary(p => p.Key, p => p.Value with { Requirements = [] }) };
        foreach (var c in draft.Classes)
        {
            foreach (var tier in draft.Acts.Select(a => a.GearTier).Distinct())
            {
                _bases[(c.Id, tier)] = BaseSetup(c, tier);
            }
        }

        foreach (var zone in draft.Acts.Select(a => data.Zones[a.ZoneId]))
        {
            foreach (var id in zone.Encounters.Select(e => e.EncounterId).Append(zone.Boss!.EncounterId).Concat(draft.Acts.SelectMany(a => a.Bosses)))
            {
                _enemies[id] = data.CreateEncounterTeam(id);
            }
        }

        _actives = draft.Classes.ToDictionary(c => c.Id, c => (IReadOnlyList<string>)ActivePool(data, c).Order(StringComparer.Ordinal).ToList());
        _passives = draft.Classes.ToDictionary(c => c.Id, c => PassivePool(data, c));
    }

    /// <summary>클래스가 받는 액티브: 그 계열 아이템이 주는 행동과 그 계열 무기가 주는 행동. 공용·소환물 전용 행동은 뺀다.</summary>
    public static IReadOnlySet<string> ActivePool(GameData data, DraftClass c) =>
        data.Items.Values.Where(i => i.Mastery is not null && c.Families.Contains(i.Mastery)).SelectMany(i => i.Actions)
            .Concat(data.Actions.Values.Where(a => a.Weapon is not null && c.Families.Contains(a.Weapon)).Select(a => a.Id))
            .Where(id => data.Actions.TryGetValue(id, out var a) && !a.Universal && !a.SummonOnly)
            .ToHashSet();

    /// <summary>클래스가 받는 패시브: 그 계열 패시브.</summary>
    public static IReadOnlyList<string> PassivePool(GameData data, DraftClass c) =>
        data.Skills.Values.Where(s => c.Families.Contains(s.Mastery)).Select(s => s.Id).Order(StringComparer.Ordinal).ToList();

    /// <summary>같은 종류(부위, 무기 종류, 계열)의 그 티어 아이템. 없으면 null.</summary>
    public static ItemDefinition? AtTier(GameData data, ItemDefinition item, int tier) =>
        item.Tier == tier
            ? item
            : data.Items.Values.FirstOrDefault(i => i.Slot == item.Slot && i.Type == item.Type && i.Mastery == item.Mastery && i.Tier == tier);

    /// <summary>판 여러 개를 시드 <paramref name="firstSeed"/>부터 돌린다. 판마다 따로 계산하므로 여러 스레드로 돌려도 결과가 같다.</summary>
    public IReadOnlyList<DraftRunResult> RunMany(int runs, int firstSeed = 0)
    {
        var results = new DraftRunResult[runs];
        Parallel.For(0, runs, i => results[i] = Run(firstSeed + i));
        return results;
    }

    public DraftRunResult Run(int seed)
    {
        var random = new Random(seed);
        var lines = _options.StartLines ?? _draft.StartLines;
        var party = _draft.Party.Select(id => new Member(id, lines) { Skills = { _draft.Class(id).StartingSkill } }).ToList();
        var picks = new List<DraftChoice>();
        var offers = new List<IReadOnlyList<DraftCard>>();
        var (fight, won, downs, deaths) = (0, 0, 0, 0);
        int? firstSeal = null;
        var total = _draft.Acts.Sum(a => a.Fights + 1);

        // 막 보스는 판을 시작할 때 정해 공개한다 (Slay the Spire처럼). 후보가 없으면 그 지역의 보스다.
        var bosses = _draft.Acts.Select(a => a.Bosses.Count > 0 ? a.Bosses[random.Next(a.Bosses.Count)] : _data.Zones[a.ZoneId].Boss!.EncounterId).ToList();
        for (var act = 0; act < _draft.Acts.Count; act++)
        {
            var zone = _data.Zones[_draft.Acts[act].ZoneId];
            for (var k = 0; k <= _draft.Acts[act].Fights; k++)
            {
                var boss = k == _draft.Acts[act].Fights;
                var encounter = boss ? bosses[act] : Draw(zone.Encounters, random);
                fight++;
                var result = Fight(party, encounter, _draft.Acts[act].GearTier, Seeds.Derive(seed, fight));
                if (result.Outcome != CombatOutcome.Victory)
                {
                    return new DraftRunResult(result.Outcome, won, act, downs, deaths, firstSeal, picks, encounter, offers, Idle(party));
                }

                won++;
                foreach (var down in Downed(result))
                {
                    downs++;
                    var member = party[down];
                    if (member.Lines == 0)
                    {
                        member.Dead = true;
                        deaths++;
                        Inherit(party, member);
                    }
                    else
                    {
                        member.Sealed++;
                        firstSeal ??= fight;
                    }
                }

                if (party.All(m => m.Dead))
                {
                    return new DraftRunResult(CombatOutcome.Defeat, won, act, downs, deaths, firstSeal, picks, encounter, offers, Idle(party));
                }

                if (boss)
                {
                    foreach (var member in party.Where(m => !m.Dead))
                    {
                        member.Sealed = Math.Max(0, member.Sealed - _draft.UnsealAfterAct);
                    }
                }

                if (fight == total)
                {
                    break;
                }

                var next = boss ? act + 1 : act;
                var offer = Offer(party, random);
                var choices = offer.SelectMany(card => Choices(card, party)).ToList();
                if (choices.Count > 0)
                {
                    var choice = Pick(choices, party, next, bosses[next], random);
                    Apply(party, choice);
                    picks.Add(choice);
                    offers.Add(offer);
                }
            }
        }

        return new DraftRunResult(
            CombatOutcome.Victory, won, _draft.Acts.Count, downs, deaths, firstSeal, picks, bosses[^1], offers, Idle(party));
    }

    private DraftChoice Pick(List<DraftChoice> choices, List<Member> party, int act, string boss, Random random)
    {
        if (_options.Picker == DraftPicker.Random)
        {
            // 카드를 먼저 고르고 받을 멤버를 고른다 (받을 수 있는 멤버가 많은 카드가 더 자주 뽑히지 않게).
            var cards = choices.Select(c => c.Card).Distinct().ToList();
            var card = cards[random.Next(cards.Count)];
            var members = choices.Where(c => c.Card == card).ToList();
            return members[random.Next(members.Count)];
        }

        if (_options.Picker == DraftPicker.TierList)
        {
            // 순위표에서 가장 높은 카드를, 클래스 카드는 사본이 가장 적은 그 클래스 멤버에게, 줄 카드는 놓지 못한 사본이 가장 많은 멤버에게.
            var card = choices.Select(c => c.Card).Distinct().MaxBy(c => _options.TierList.GetValueOrDefault(c))!;
            return choices.Where(c => c.Card == card)
                .MinBy(c => card.Kind == DraftCardKind.Line ? party[c.Member].Lines - party[c.Member].Skills.Count : party[c.Member].Skills.Count)!;
        }

        return choices.MaxBy(choice =>
        {
            var copy = party.Select(m => m.Clone()).ToList();
            Apply(copy, choice);
            return Lookahead(copy, act, boss);
        })!;
    }

    /// <summary>그 막의 적 조합마다와 보스를 미리 싸운 평균 점수. 이기면 1 + 0.2 × 서 있는 비율, 지면 0.2 × 깎은 적 HP 비율.</summary>
    private double Lookahead(List<Member> party, int act, string boss)
    {
        var zone = _data.Zones[_draft.Acts[act].ZoneId];
        var pool = zone.Encounters.Select(e => e.EncounterId).Distinct();
        var encounters = (_options.KnowsBoss ? pool.Append(boss) : pool).ToList();
        var score = 0.0;
        foreach (var encounter in encounters)
        {
            for (var s = 0; s < _options.LookaheadSeeds; s++)
            {
                var result = Fight(party, encounter, _draft.Acts[act].GearTier, Seeds.Derive(LookaheadSalt, s));
                score += result.Outcome == CombatOutcome.Victory
                    ? 1 + 0.2 * Standing(result, CombatSide.Ally)
                    : 0.2 * (1 - HpFraction(result, CombatSide.Enemy));
            }
        }

        return score / (encounters.Count * _options.LookaheadSeeds);
    }

    private CombatResult Fight(List<Member> party, string encounter, int tier, int seed)
    {
        var allies = party.Select((m, i) => (m, i)).Where(x => !x.m.Dead).Select(x => Setup(x.m, x.i, tier)).ToList();
        return CombatSimulator.Run(allies, _enemies[encounter], _catalog, seed, _rules);
    }

    private CombatantSetup Setup(Member member, int index, int tier) =>
        _bases[(member.ClassId, tier)] with
        {
            Id = MemberId(index),
            Skills = member.Passives.ToDictionary(),
            GrantedActions = member.Skills.ToHashSet(),
            Tactics = DraftTactics.Build(member.Skills, member.Lines, _data),
        };

    private static string MemberId(int index) => $"draft_{index}";

    private static IEnumerable<int> Downed(CombatResult result) =>
        result.Combatants.Where(c => c.Side == CombatSide.Ally && !c.IsSummon && !c.IsAlive).Select(c => int.Parse(c.Id["draft_".Length..]));

    private static double Standing(CombatResult result, CombatSide side)
    {
        var units = result.Combatants.Where(c => c.Side == side && !c.IsSummon).ToList();
        return (double)units.Count(c => c.IsAlive) / units.Count;
    }

    private static double HpFraction(CombatResult result, CombatSide side)
    {
        var units = result.Combatants.Where(c => c.Side == side && !c.IsSummon).ToList();
        return (double)units.Sum(c => Math.Max(0, c.Hp)) / units.Sum(c => c.MaxHp);
    }

    private static string Draw(IReadOnlyList<ZoneEncounter> encounters, Random random)
    {
        var roll = random.Next(encounters.Sum(e => e.Weight));
        return encounters.First(e => (roll -= e.Weight) < 0).EncounterId;
    }

    /// <summary>후보 카드. 같은 카드는 한 번만 나온다. 클래스 카드는 살아 있는 멤버를 하나 뽑아 그 클래스에서 고른다.</summary>
    private List<DraftCard> Offer(List<Member> party, Random random)
    {
        var alive = party.Where(m => !m.Dead).ToList();
        var offer = _draft.Offer;
        var cards = new List<DraftCard>();
        for (var tries = 0; cards.Count < offer.Cards && tries < 50; tries++)
        {
            var roll = random.Next(offer.Active + offer.Passive + offer.Line);
            var classId = alive[random.Next(alive.Count)].ClassId;
            DraftCard? card = null;
            if (roll < offer.Active)
            {
                var pool = _actives[classId];
                card = new DraftCard(DraftCardKind.Active, classId, pool[random.Next(pool.Count)]);
            }
            else if (roll < offer.Active + offer.Passive)
            {
                var pool = _passives[classId].Where(id => alive.Any(m => m.ClassId == classId && m.Level(id) < SkillDefinition.MaxLevel)).ToList();
                card = pool.Count == 0 ? null : new DraftCard(DraftCardKind.Passive, classId, pool[random.Next(pool.Count)]);
            }
            else if (alive.Any(m => m.Earned < _draft.MaxLines))
            {
                card = new DraftCard(DraftCardKind.Line, null, null);
            }

            if (card is not null && !cards.Contains(card))
            {
                cards.Add(card);
            }
        }

        return cards;
    }

    private IEnumerable<DraftChoice> Choices(DraftCard card, List<Member> party) =>
        party.Select((m, i) => (m, i))
            .Where(x => !x.m.Dead && card.Kind switch
            {
                DraftCardKind.Active => x.m.ClassId == card.ClassId,
                DraftCardKind.Passive => x.m.ClassId == card.ClassId && x.m.Level(card.Id!) < SkillDefinition.MaxLevel,
                _ => x.m.Earned < _draft.MaxLines,
            })
            .Select(x => new DraftChoice(card, x.i));

    private static void Apply(List<Member> party, DraftChoice choice)
    {
        var member = party[choice.Member];
        switch (choice.Card.Kind)
        {
            case DraftCardKind.Active:
                member.Skills.Add(choice.Card.Id!);
                break;
            case DraftCardKind.Passive:
                member.Passives[choice.Card.Id!] = member.Level(choice.Card.Id!) + 1;
                break;
            default:
                member.Earned++;
                break;
        }
    }

    private static int Idle(List<Member> party) => party.Where(m => !m.Dead).Sum(m => Math.Max(0, m.Skills.Count - m.Lines));

    /// <summary>죽은 멤버의 사본은 살아 있는 같은 클래스 멤버 중 사본이 가장 적은 쪽으로 간다. 그런 멤버가 없으면 사라진다.</summary>
    private static void Inherit(List<Member> party, Member dead)
    {
        var heir = party.Where(m => !m.Dead && m.ClassId == dead.ClassId).MinBy(m => m.Skills.Count);
        heir?.Skills.AddRange(dead.Skills);
        dead.Skills.Clear();
    }

    /// <summary>클래스의 기본 장비를 그 티어로 바꿔 낀 전투 입력. 재편에서는 아이템의 패시브 요구가 없으므로 요구를 보지 않고 낀다.</summary>
    private CombatantSetup BaseSetup(DraftClass c, int tier)
    {
        var worn = c.Member.Equipment.Select(p => (p.Key, AtTier(_data, _data.Items[p.Value], tier)!));
        var setup = new CombatantSetup(c.Id, c.Name, c.Member.Stats, c.Member.Row, null, null, new Dictionary<string, int>(), []);
        return new Loadout(worn).Apply(setup, _data.Actions.Values, _ => 0);
    }

    private sealed class Member(string classId, int earned)
    {
        public string ClassId { get; } = classId;

        public List<string> Skills { get; private init; } = [];

        public Dictionary<string, int> Passives { get; private init; } = [];

        /// <summary>얻은 줄 상한 (시작 + 줄 카드).</summary>
        public int Earned { get; set; } = earned;

        public int Sealed { get; set; }

        public bool Dead { get; set; }

        public int Lines => Math.Max(0, Earned - Sealed);

        public int Level(string passiveId) => Passives.GetValueOrDefault(passiveId);

        public Member Clone() => new(ClassId, Earned) { Skills = [.. Skills], Passives = new(Passives), Sealed = Sealed, Dead = Dead };
    }
}
