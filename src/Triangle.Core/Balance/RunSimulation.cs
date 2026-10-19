using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Runs;
using Triangle.Core.Tactics;

namespace Triangle.Core.Balance;

public enum RunBot
{
    /// <summary>모든 결정을 아무렇게나: 갈 지점, 카드와 받을 멤버, 상점, 휴식, 이벤트.</summary>
    Random,

    /// <summary>
    /// 보통 사람처럼: 카드는 고를 수 있는 것(카드와 받을 멤버)마다 그 막의 적을 미리 싸워 보고 가장 잘 싸우는 쪽을 고른다.
    /// 지점, 상점, 휴식, 이벤트는 정해 둔 규칙으로 고른다.
    /// </summary>
    Smart,
}

public sealed record RunSimOptions
{
    public RunBot Bot { get; init; } = RunBot.Smart;

    /// <summary>시작 파티 (클래스 ID).</summary>
    public IReadOnlyList<string> Party { get; init; } = ["knight", "archer", "elementalist", "priest"];

    /// <summary>이 판의 해금 (잠긴 것과 더 받는 시작 골드).</summary>
    public RunUnlocks Unlocks { get; init; } = RunUnlocks.All;

    /// <summary>똑똑한 봇이 카드 후보마다 적 조합 하나를 미리 싸워 보는 시드 수.</summary>
    public int LookaheadSeeds { get; init; } = 3;
}

/// <summary>골드를 쓴 곳.</summary>
public enum RunSpend
{
    Item,
    Skill,
    Recruit,
    Unseal,
    Event,
}

/// <summary>전투 하나의 기록.</summary>
/// <param name="Floor">지점의 층 (0부터, 보스는 막의 층 수).</param>
/// <param name="AllyHp">끝났을 때 아군(소환수 빼고)의 남은 HP 합 / 최대 HP 합.</param>
/// <param name="Downs">쓰러진 아군 수 (소환수 빼고).</param>
/// <param name="Time">전투가 끝난 시각 (마지막 행동의 전투 시계).</param>
public sealed record RunSimFight(int Act, int Floor, RunNodeKind Kind, string EncounterId, CombatOutcome Outcome, double AllyHp, int Downs, long Time);

/// <summary>한 판의 결과.</summary>
/// <param name="Act">끝난 막 (0부터). 클리어면 막 수.</param>
/// <param name="Floor">끝난 지점의 층 (0부터). 클리어면 마지막 막의 보스 층.</param>
/// <param name="Picks">고른 보상 카드 차례대로 (전투, 단련, 이벤트).</param>
/// <param name="Skipped">고를 수 있는 카드가 없어 건너뛴 보상 수.</param>
/// <param name="GoldEarned">전투와 이벤트로 번 골드 (시작 골드 빼고).</param>
/// <param name="Members">끝날 때 살아 있는 멤버 수.</param>
/// <param name="Joined">판 중에 합류한 멤버 수 (상점, 이벤트).</param>
/// <param name="Copies">끝날 때 파티가 가진 스킬 사본 수.</param>
/// <param name="Placed">그중 켜진 전술 줄에 놓인 사본 수 (나머지는 놀고 있다).</param>
/// <param name="Lines">끝날 때 살아 있는 멤버의 얻은 줄 상한 합.</param>
/// <param name="Maxed">끝날 때 줄 상한이 최대에 닿은 멤버 수.</param>
public sealed record RunSimResult(
    bool Cleared,
    int Act,
    int Floor,
    IReadOnlyList<RunSimFight> Fights,
    int Deaths,
    IReadOnlyList<RunCard> Picks,
    int Skipped,
    int GoldEarned,
    IReadOnlyDictionary<RunSpend, int> Spent,
    int GoldLeft,
    int Members,
    int Joined,
    int Copies,
    int Placed,
    int Lines,
    int Maxed)
{
    /// <summary>마지막 전투 (판을 끝낸 상대). 싸우지 않고 끝났으면 null.</summary>
    public RunSimFight? Last => Fights.Count > 0 ? Fights[^1] : null;
}

/// <summary>
/// 판 시뮬레이션: 게임의 판 규칙(<see cref="Run"/>, `data/run.json`)을 그대로 쓰고 결정만 봇(<see cref="RunBot"/>)이 내린다.
/// 지도, 보상, 상점, 휴식, 이벤트, 봉인과 죽음이 모두 게임과 같다. 숫자 맞추기의 측정용이다.
/// <list type="bullet">
/// <item>전술은 두 봇 모두 <see cref="DraftTactics"/>로 짠다: 사본을 그 클래스 멤버에게 나눠 주고(남는 줄이 많은 멤버부터), 멤버마다
/// 줄을 최대치까지 적는다. 켜지는 것은 위에서부터 줄 상한만큼이다.</item>
/// <item>장비도 두 봇 모두 같은 규칙으로 낀다: 가방의 무기·방어구가 지금 것보다 티어가 높으면(방어구는 같은 재질) 바꿔 끼고,
/// 장신구는 칸이 빈 멤버가 낀다.</item>
/// <item>줄(전열·후열)은 클래스의 처음 값 그대로 둔다.</item>
/// </list>
/// </summary>
public sealed class RunSimulation
{
    private const int LookaheadSalt = 0x5EED;

    private readonly GameData _data;
    private readonly RunDefinition _rules;
    private readonly RunSimOptions _options;
    private readonly Dictionary<(int Act, RunNodeKind Kind, string EncounterId), IReadOnlyList<CombatantSetup>> _teams = [];
    private readonly Dictionary<string, string> _owners;

    public RunSimulation(GameData data, RunSimOptions? options = null)
    {
        _data = data;
        _rules = data.Run ?? throw new InvalidOperationException("run.json이 없다");
        _options = options ?? new RunSimOptions();
        for (var i = 0; i < _rules.Acts.Count; i++)
        {
            var act = _rules.Acts[i];
            var zone = data.Zones[act.ZoneId];
            var bosses = act.Bosses.Count > 0 ? act.Bosses : [zone.Boss!.EncounterId];
            var fights = zone.Encounters.Select(e => (Kind: RunNodeKind.Fight, Id: e.EncounterId))
                .Concat(act.Elites.Select(id => (Kind: RunNodeKind.Elite, Id: id)))
                .Concat(bosses.Select(id => (Kind: RunNodeKind.Boss, Id: id)));
            foreach (var (kind, id) in fights.Distinct())
            {
                _teams[(i, kind, id)] = Run.EnemyTeam(data, i, kind, id);
            }
        }

        _owners = data.Classes.Values.SelectMany(c => c.Skills.Select(s => (Skill: s, Class: c.Id))).ToDictionary(x => x.Skill, x => x.Class);
    }

    /// <summary>판 여러 개를 시드 <paramref name="firstSeed"/>부터 돌린다. 판마다 따로 계산하므로 여러 스레드로 돌려도 결과가 같다.</summary>
    public IReadOnlyList<RunSimResult> RunMany(int runs, int firstSeed = 0)
    {
        var results = new RunSimResult[runs];
        Parallel.For(0, runs, i => results[i] = Play(firstSeed + i));
        return results;
    }

    public RunSimResult Play(int seed)
    {
        var run = Run.Start(_data, _options.Party, seed, _options.Unlocks);
        var random = new Random(Seeds.Derive(seed, 0xB07));
        var tally = new Tally(run.Members.Count);
        while (!run.Finished)
        {
            switch (run.Phase)
            {
                case RunPhase.Map:
                    Equip(run);
                    Arrange(run);
                    var node = _options.Bot == RunBot.Random ? run.Choices[random.Next(run.Choices.Count)] : Route(run);
                    if (run.Enter(_data, node.Id) is { } battle)
                    {
                        tally.Record(run, node, battle);
                    }

                    break;
                case RunPhase.Reward:
                    TakeReward(run, random, tally);
                    break;
                case RunPhase.Shop:
                    Shop(run, random, tally);
                    run.LeaveShop();
                    break;
                case RunPhase.Rest:
                    var sealedLines = run.Living.Sum(m => m.Sealed);
                    if (_options.Bot == RunBot.Random ? random.Next(2) == 0 : sealedLines > 0)
                    {
                        run.RestUnseal(_data);
                    }
                    else
                    {
                        run.RestTrain(_data);
                    }

                    break;
                case RunPhase.Event:
                    Event(run, random, tally);
                    break;
            }
        }

        Arrange(run);
        var living = run.Living.ToList();
        var cleared = run.Phase == RunPhase.Victory;
        return new RunSimResult(
            cleared,
            cleared ? _rules.Acts.Count : run.Act,
            run.Position is { } at ? run.Map.Node(at).Floor : 0,
            tally.Fights,
            tally.Deaths,
            tally.Picks,
            tally.Skipped,
            tally.Earned,
            tally.Spent,
            run.Gold,
            living.Count,
            run.Members.Count - tally.StartMembers,
            run.Copies.Values.Sum(),
            living.Sum(m => m.ActiveTactics.Count(t => !_data.Actions[t.ActionId].Universal)),
            living.Sum(m => m.Earned),
            living.Count(m => m.Earned >= _rules.MaxLines));
    }

    /// <summary>
    /// 전술을 다시 짠다: 사본을 그 클래스의 살아 있는 멤버에게 나눠 주고(켤 줄이 많이 남은 멤버부터) <see cref="DraftTactics"/>로 줄을 적는다.
    /// </summary>
    private void Arrange(Run run)
    {
        var living = run.Living.ToList();
        foreach (var member in living)
        {
            run.SetTactics(_data, member, []);
        }

        var skills = living.ToDictionary(m => m, _ => new List<string>());
        foreach (var (id, count) in run.Copies.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var holders = living.Where(m => _owners.GetValueOrDefault(id) == m.ClassId).ToList();
            for (var k = 0; k < count && holders.Count > 0; k++)
            {
                skills[holders.MinBy(m => skills[m].Count - m.Lines)!].Add(id);
            }
        }

        foreach (var member in living)
        {
            run.SetTactics(_data, member, DraftTactics.Build(skills[member], _rules.MaxLines, _data).SkipLast(1).ToList());
        }
    }

    /// <summary>가방의 무기·방어구가 지금 것보다 티어가 높으면 바꿔 끼고(방어구는 같은 재질), 장신구는 칸이 빈 멤버가 낀다.</summary>
    private void Equip(Run run)
    {
        int Tier(string? id) => id is null ? 0 : _data.Items[id].Tier;
        for (var i = 0; i < run.Bag.Count; i++)
        {
            var item = _data.Items[run.Bag[i]];
            var target = item.Slot switch
            {
                EquipmentSlot.MainHand => run.Living.Where(m => _data.Classes[m.ClassId].CanWield(item) && Tier(m.Weapon) < item.Tier).MinBy(m => Tier(m.Weapon)),
                EquipmentSlot.Body => run.Living.Where(m => Tier(m.Armor) < item.Tier && (m.Armor is null || _data.Items[m.Armor].Mastery == item.Mastery))
                    .MinBy(m => Tier(m.Armor)),
                EquipmentSlot.Trinket => run.Living.Where(m => m.Trinkets.Count < _rules.MaxTrinkets).MinBy(m => m.Trinkets.Count),
                _ => null,
            };
            if (target is not null && run.WhyCannotEquip(_data, target, i) is null)
            {
                run.Equip(_data, target, i);
                i = -1;
            }
        }
    }

    /// <summary>
    /// 갈 지점: 지점 종류마다 지금 형편에 맞는 값을 매기고(봉인이 있으면 휴식, 골드가 있으면 상점, 정예는 카드를 주므로 더), 남은 길의
    /// 값을 멀수록 덜 쳐서 더한 것이 가장 큰 길로 간다.
    /// </summary>
    private RunNode Route(Run run)
    {
        var sealedLines = run.Living.Sum(m => m.Sealed);
        double Worth(RunNodeKind kind) => kind switch
        {
            RunNodeKind.Rest => sealedLines > 0 ? 2.5 : 0.3,
            RunNodeKind.Shop => run.Gold >= _rules.Shop.SkillPrice ? 1.8 : 0.3,
            RunNodeKind.Elite => 2.0,
            RunNodeKind.Event => 1.2,
            RunNodeKind.Fight => 1.0,
            _ => 0,
        };

        var memo = new Dictionary<int, double>();
        double Value(RunNode node)
        {
            if (!memo.TryGetValue(node.Id, out var value))
            {
                value = Worth(node.Kind) + 0.7 * node.Next.Select(id => Value(run.Map.Node(id))).DefaultIfEmpty(0).Max();
                memo[node.Id] = value;
            }

            return value;
        }

        return run.Choices.MaxBy(Value)!;
    }

    private void TakeReward(Run run, Random random, Tally tally)
    {
        var choices = new List<(int Card, RunMember? Member)>();
        for (var i = 0; i < run.Offer.Count; i++)
        {
            if (run.Offer[i].Kind == RunCardKind.Active)
            {
                choices.Add((i, null));
                continue;
            }

            choices.AddRange(run.Living.Where(m => run.WhyCannotTake(_data, i, m) is null).Select(m => (i, (RunMember?)m)));
        }

        if (choices.Count == 0)
        {
            run.Skip();
            tally.Skipped++;
            return;
        }

        (int Card, RunMember? Member) choice;
        if (_options.Bot == RunBot.Random)
        {
            // 카드를 먼저 고르고 받을 멤버를 고른다 (받을 수 있는 멤버가 많은 카드가 더 자주 뽑히지 않게).
            var cards = choices.Select(c => c.Card).Distinct().ToList();
            var card = cards[random.Next(cards.Count)];
            var members = choices.Where(c => c.Card == card).ToList();
            choice = members[random.Next(members.Count)];
        }
        else
        {
            choice = choices.MaxBy(c =>
            {
                var copy = Run.Restore(run.ToSave(), _data);
                copy.Take(_data, c.Card, c.Member is null ? null : copy.Members.Single(m => m.Id == c.Member.Id));
                return Lookahead(copy);
            });
        }

        tally.Picks.Add(run.Offer[choice.Card]);
        run.Take(_data, choice.Card, choice.Member);
    }

    /// <summary>그 막의 보통 적 조합, 정예, 보스를 미리 싸운 평균 점수. 이기면 1 + 0.5 × 남은 HP 비율, 지면 0.5 × 깎은 적 HP 비율.</summary>
    private double Lookahead(Run run)
    {
        Arrange(run);
        var act = _rules.Acts[run.Act];
        var encounters = _data.Zones[act.ZoneId].Encounters.Select(e => (Kind: RunNodeKind.Fight, Id: e.EncounterId))
            .Concat(act.Elites.Select(id => (Kind: RunNodeKind.Elite, Id: id)))
            .Append((Kind: RunNodeKind.Boss, Id: run.Bosses[run.Act]))
            .Distinct()
            .ToList();
        var allies = run.Living.Select(m => run.Setup(_data, m)).ToList();
        var score = 0.0;
        foreach (var (kind, encounter) in encounters)
        {
            for (var s = 0; s < _options.LookaheadSeeds; s++)
            {
                var result = CombatSimulator.Run(allies, _teams[(run.Act, kind, encounter)], Run.Catalog(_data), Seeds.Derive(LookaheadSalt, s));
                score += result.Outcome == CombatOutcome.Victory
                    ? 1 + 0.5 * HpFraction(result, CombatSide.Ally)
                    : 0.5 * (1 - HpFraction(result, CombatSide.Enemy));
            }
        }

        return score / (encounters.Count * _options.LookaheadSeeds);
    }

    /// <summary>
    /// 상점. 아무렇게나 봇은 물건마다 반반의 확률로 사고 봉인도 반반으로 푼다. 똑똑한 봇은 봉인부터 풀고, 합류 → 쓸 사람이 있는 장비 →
    /// 켤 줄이 남는 클래스의 스킬 순으로 산다(값이 싼 것부터).
    /// </summary>
    private void Shop(Run run, Random random, Tally tally)
    {
        void Buy(int i)
        {
            var stock = run.Stock[i];
            run.Buy(_data, i);
            tally.Spend(stock.Kind switch { RunStockKind.Item => RunSpend.Item, RunStockKind.Skill => RunSpend.Skill, _ => RunSpend.Recruit }, stock.Price);
        }

        void Unseal(RunMember member)
        {
            run.Unseal(_data, member);
            tally.Spend(RunSpend.Unseal, _rules.Shop.UnsealPrice);
        }

        if (_options.Bot == RunBot.Random)
        {
            foreach (var i in Enumerable.Range(0, run.Stock.Count).OrderBy(_ => random.Next()).ToList())
            {
                if (random.Next(2) == 0 && run.WhyCannotBuy(_data, i) is null)
                {
                    Buy(i);
                }
            }

            var sealedMembers = run.Living.Where(m => m.Sealed > 0).ToList();
            if (sealedMembers.Count > 0 && random.Next(2) == 0)
            {
                var member = sealedMembers[random.Next(sealedMembers.Count)];
                if (run.WhyCannotUnseal(_data, member) is null)
                {
                    Unseal(member);
                }
            }

            return;
        }

        while (run.Living.Where(m => m.Sealed > 0).MaxBy(m => m.Sealed) is { } member && run.WhyCannotUnseal(_data, member) is null)
        {
            Unseal(member);
        }

        foreach (var i in Enumerable.Range(0, run.Stock.Count).OrderByDescending(i => Want(run, run.Stock[i])).ThenBy(i => run.Stock[i].Price).ToList())
        {
            if (Want(run, run.Stock[i]) > 0 && run.WhyCannotBuy(_data, i) is null)
            {
                Buy(i);
            }
        }
    }

    /// <summary>똑똑한 봇이 상점 물건을 원하는 정도. 0이면 사지 않는다.</summary>
    private int Want(Run run, RunStock stock)
    {
        if (stock.Sold)
        {
            return 0;
        }

        int Tier(string? id) => id is null ? 0 : _data.Items[id].Tier;
        switch (stock.Kind)
        {
            case RunStockKind.Recruit:
                return run.Living.Count() < Run.MaxMembers ? 3 : 0;
            case RunStockKind.Item:
                var item = _data.Items[stock.Id];
                var useful = item.Slot switch
                {
                    EquipmentSlot.MainHand => run.Living.Any(m => _data.Classes[m.ClassId].CanWield(item) && Tier(m.Weapon) < item.Tier),
                    EquipmentSlot.Body => run.Living.Any(m => Tier(m.Armor) < item.Tier && (m.Armor is null || _data.Items[m.Armor].Mastery == item.Mastery)),
                    EquipmentSlot.Trinket => run.Living.Sum(m => _rules.MaxTrinkets - m.Trinkets.Count) > run.Bag.Count(id => _data.Items[id].Slot == EquipmentSlot.Trinket),
                    _ => false,
                };
                return useful ? 2 : 0;
            default:
                // 그 클래스 멤버의 켤 줄 수가 이미 가진 그 클래스 사본보다 많을 때만 산다 (놀 사본은 사지 않는다).
                var owner = _owners[stock.Id];
                var lines = run.Living.Where(m => m.ClassId == owner).Sum(m => m.Lines);
                var copies = run.Copies.Where(p => _owners.GetValueOrDefault(p.Key) == owner).Sum(p => p.Value);
                return lines > copies ? 1 : 0;
        }
    }

    /// <summary>
    /// 이벤트. 아무렇게나 봇은 고를 수 있는 선택지 중 아무거나. 똑똑한 봇은 선택지의 효과에 값을 매겨(합류 3, 카드 1.5, 장신구 1.2,
    /// 봉인 풀기는 봉인된 멤버마다 1, 받는 봉인 하나에 -1, 골드 40에 1) 가장 큰 것을 고른다.
    /// </summary>
    private void Event(Run run, Random random, Tally tally)
    {
        var options = Enumerable.Range(0, run.Event!.Options.Count).Where(i => run.WhyCannotChoose(i) is null).ToList();
        var sealedMembers = run.Living.Count(m => m.Sealed > 0);
        double Worth(RunEventOption o) =>
            (o.Join is not null ? 3 : 0) + (o.Card ? 1.5 : 0) + (o.Trinket ? 1.2 : 0)
            + Math.Min(o.Unseal, 1) * sealedMembers - o.Seal + o.Gold / 40.0;
        var option = _options.Bot == RunBot.Random ? options[random.Next(options.Count)] : options.MaxBy(i => Worth(run.Event.Options[i]));
        var gold = run.Event.Options[option].Gold;
        if (gold < 0)
        {
            tally.Spend(RunSpend.Event, -gold);
        }
        else
        {
            tally.Earned += gold;
        }

        run.Choose(_data, option);
    }

    private static double HpFraction(CombatResult result, CombatSide side)
    {
        var units = result.Combatants.Where(c => c.Side == side && !c.IsSummon).ToList();
        return (double)units.Sum(c => Math.Max(0, c.Hp)) / Math.Max(1, units.Sum(c => c.MaxHp));
    }

    private sealed class Tally(int startMembers)
    {
        public int StartMembers { get; } = startMembers;

        public List<RunSimFight> Fights { get; } = [];

        public List<RunCard> Picks { get; } = [];

        public Dictionary<RunSpend, int> Spent { get; } = [];

        public int Deaths { get; private set; }

        public int Skipped { get; set; }

        public int Earned { get; set; }

        public void Spend(RunSpend where, int gold) => Spent[where] = Spent.GetValueOrDefault(where) + gold;

        public void Record(Run run, RunNode node, RunBattle battle)
        {
            var allies = battle.Result.Combatants.Where(c => c.Side == CombatSide.Ally && !c.IsSummon).ToList();
            Fights.Add(new RunSimFight(run.Act, node.Floor, battle.Kind, battle.EncounterId, battle.Result.Outcome,
                HpFraction(battle.Result, CombatSide.Ally), allies.Count(c => !c.IsAlive), battle.Result.Events.OfType<TurnStarted>().LastOrDefault()?.Time ?? 0));
            Deaths += battle.Died.Count;
            Earned += battle.Gold;
        }
    }
}
