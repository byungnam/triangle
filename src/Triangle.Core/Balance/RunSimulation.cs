using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Runs;
using Triangle.Core.Tactics;
using Triangle.Core.Units;

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
/// <param name="Plan">보스와 싸우기 전에 똑똑한 봇이 고른 전술 방식과 줄 배치 (<see cref="RunSimulation.PlanName"/>). 보스가 아니면 null.</param>
public sealed record RunSimFight(
    int Act, int Floor, RunNodeKind Kind, string EncounterId, CombatOutcome Outcome, double AllyHp, int Downs, long Time, string? Plan = null);

/// <summary>보스 대비 전술 방식: 기본 배치(<see cref="DraftTactics"/>)에서 줄을 빼거나 순서를 바꾼다.</summary>
public enum RunTacticPlan
{
    Default,

    /// <summary>디버프를 거는 줄을 뺀다 (상처 입으면 폭주하는 적).</summary>
    NoDebuffs,

    /// <summary>마법 피해 줄을 위로 (물리 면역).</summary>
    Magic,

    /// <summary>광역 피해 줄을 위로, 적 수 조건 없이 (도발·엄호 뒤의 적).</summary>
    Area,

    /// <summary>지속 피해를 거는 줄을 위로 (가시처럼 맞받아치는 적).</summary>
    OverTime,

    /// <summary>기절·둔화·약화를 거는 줄을 위로 (영창하는 적, 큰 한 방).</summary>
    Disable,

    /// <summary>회복·보호막·버프 줄을 위로 (오래 버티기).</summary>
    Support,

    /// <summary>피해를 주지 않는 줄을 뺀다 (빨리 잡기).</summary>
    Damage,
}

/// <summary>보스 대비 줄 배치.</summary>
public enum RunRowPlan
{
    /// <summary>클래스의 처음 줄.</summary>
    Default,

    AllFront,

    /// <summary>가장 단단한(체력) 한 명만 전열, 나머지는 후열.</summary>
    OneFront,

    /// <summary>가장 단단한 두 명이 전열.</summary>
    TwoFront,
}

/// <summary>상점에 한 번 들른 기록.</summary>
/// <param name="GoldIn">들어갈 때 골드.</param>
/// <param name="GoldOut">나올 때 골드.</param>
/// <param name="Offered">나온 물건 (산 것도 포함, 처음 모습).</param>
/// <param name="Bought">산 물건 (산 차례대로).</param>
/// <param name="Unseals">골드로 푼 봉인 수.</param>
/// <param name="Sealed">들어갈 때 살아 있는 멤버의 봉인 합.</param>
/// <param name="Values">똑똑한 봇이 들어가자마자 잰 값어치: 살 수 있던 것마다 사 봤을 때 미리 싸운 점수가 나아진 정도.</param>
public sealed record RunSimShop(
    int Act, int GoldIn, int GoldOut, IReadOnlyList<RunStock> Offered, IReadOnlyList<RunStock> Bought, int Unseals, int Sealed, IReadOnlyList<RunSimValue> Values);

/// <summary>상점에서 살 수 있던 것 하나의 값어치. <paramref name="Stock"/>이 null이면 봉인 풀기다.</summary>
public sealed record RunSimValue(RunStock? Stock, int Price, double Gain);

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
/// <param name="Shops">들른 상점 (들른 차례대로).</param>
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
    int Maxed,
    IReadOnlyList<RunSimShop> Shops)
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
/// <item>무기·방어구는 살 때 낄 멤버를 정한다(똑똑한 봇은 멤버마다 미리 싸워 보고, 아무렇게나 봇은 아무나). 장신구는 칸이 빈 멤버가 낀다.</item>
/// <item>줄(전열·후열)은 클래스의 처음 값 그대로 둔다.</item>
/// </list>
/// </summary>
public sealed class RunSimulation
{
    private const int LookaheadSalt = 0x5EED;

    /// <summary>똑똑한 봇이 상점에서 사는 가장 작은 나아짐 (미리 싸운 점수, 이기면 1점대).</summary>
    private const double MinShopGain = 0.01;

    private const int BossSalt = 0xB055;

    /// <summary>보스 대비에서 방식마다 보스를 미리 싸워 보는 시드 수.</summary>
    private const int BossSeeds = 4;

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
                    SetRows(run, RunRowPlan.Default);
                    Arrange(run);
                    var node = _options.Bot == RunBot.Random ? run.Choices[random.Next(run.Choices.Count)] : Route(run);
                    var plan = node.Kind == RunNodeKind.Boss && _options.Bot == RunBot.Smart ? PrepareForBoss(run) : null;
                    if (run.Enter(_data, node.Id) is { } battle)
                    {
                        tally.Record(run, node, battle, plan);
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
            living.Count(m => m.Earned >= _rules.MaxLines),
            tally.Shops);
    }

    /// <summary>
    /// 전술을 다시 짠다: 사본을 그 클래스의 살아 있는 멤버에게 나눠 주고(켤 줄이 많이 남은 멤버부터) <see cref="DraftTactics"/>로 줄을 적는다.
    /// </summary>
    private void Arrange(Run run, RunTacticPlan plan = RunTacticPlan.Default)
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
            run.SetTactics(_data, member, Apply(plan, DraftTactics.Build(skills[member], _rules.MaxLines, _data).SkipLast(1)).ToList());
        }
    }

    /// <summary>방식대로 줄을 빼거나 위로 올린다 (나머지 순서는 그대로).</summary>
    private IEnumerable<Tactic> Apply(RunTacticPlan plan, IEnumerable<Tactic> lines)
    {
        ActionDefinition A(Tactic t) => _data.Actions[t.ActionId];
        bool Applies(Tactic t, Func<Effects.EffectDefinition, bool> match) => A(t).Applies.Any(x => match(_data.Effects[x.EffectId]));
        bool Debuffs(Tactic t) => Applies(t, e => e.Kind == Effects.EffectKind.Debuff);
        IEnumerable<Tactic> First(Func<Tactic, bool> first) => lines.OrderBy(t => first(t) ? 0 : 1);
        return plan switch
        {
            RunTacticPlan.NoDebuffs => lines.Where(t => !Debuffs(t)),
            RunTacticPlan.Magic => First(t => A(t) is { Effect: ActionEffect.Damage, DamageType: DamageType.Magical }),
            RunTacticPlan.Area => First(t => A(t) is { Effect: ActionEffect.Damage, Scope: TargetScope.All })
                .Select(t => A(t).Scope == TargetScope.All && t.Condition == Condition.EnemyAliveAtLeast ? t with { Condition = Condition.Always, Value = 0 } : t),
            RunTacticPlan.OverTime => First(t => Applies(t, e => e.TickHpPercent < 0)),
            RunTacticPlan.Disable => First(t => Applies(t, e => e.Stun || e.Modifiers.Any(m => m.Percent < 0))),
            RunTacticPlan.Support => First(t => A(t).Effect is ActionEffect.Heal or ActionEffect.Shield || A(t).Side != TargetSide.Enemy),
            RunTacticPlan.Damage => lines.Where(t => A(t).Effect == ActionEffect.Damage),
            _ => lines,
        };
    }

    /// <summary>줄 배치: 처음 값, 모두 전열, 가장 단단한(클래스 체력) 한 명이나 두 명만 전열.</summary>
    private void SetRows(Run run, RunRowPlan plan)
    {
        var sturdy = run.Living.OrderByDescending(m => _data.Classes[m.ClassId].Stats.Vital).ThenBy(m => _data.Classes[m.ClassId].Row).ToList();
        for (var i = 0; i < sturdy.Count; i++)
        {
            var member = sturdy[i];
            var row = plan switch
            {
                RunRowPlan.AllFront => Row.Front,
                RunRowPlan.OneFront => i < 1 ? Row.Front : Row.Back,
                RunRowPlan.TwoFront => i < 2 ? Row.Front : Row.Back,
                _ => _data.Classes[member.ClassId].Row,
            };
            if (member.Row != row)
            {
                run.SetRow(member, row);
            }
        }
    }

    /// <summary>
    /// 보스 대비: 공개된 막 보스와 싸우기 전에 전술 방식과 줄 배치의 조합마다 판을 복사해 보스를 미리 싸워 보고, 가장 나은 조합으로 들어간다
    /// (사람이 보스를 보고 전술을 고치는 것을 흉내 낸다). 같으면 기본에 가까운 쪽. 고른 조합의 이름을 돌려준다.
    /// </summary>
    private string PrepareForBoss(Run run)
    {
        var boss = _teams[(run.Act, RunNodeKind.Boss, run.Bosses[run.Act])];
        var best = (Score: double.MinValue, Plan: RunTacticPlan.Default, Rows: RunRowPlan.Default);
        foreach (var plan in Enum.GetValues<RunTacticPlan>())
        {
            foreach (var rows in Enum.GetValues<RunRowPlan>())
            {
                var copy = Run.Restore(run.ToSave(), _data);
                SetRows(copy, rows);
                Arrange(copy, plan);
                var allies = copy.Living.Select(m => copy.Setup(_data, m)).ToList();
                var score = Enumerable.Range(0, BossSeeds).Average(s =>
                {
                    var result = CombatSimulator.Run(allies, boss, Run.Catalog(_data), Seeds.Derive(BossSalt, s));
                    return result.Outcome == CombatOutcome.Victory ? 1 + 0.5 * HpFraction(result, CombatSide.Ally) : 0.5 * (1 - HpFraction(result, CombatSide.Enemy));
                });
                if (score > best.Score + 1e-9)
                {
                    best = (score, plan, rows);
                }
            }
        }

        SetRows(run, best.Rows);
        Arrange(run, best.Plan);
        return PlanName(best.Plan, best.Rows);
    }

    /// <summary>보스 대비 조합의 이름 (기록과 보고용): "디버프 빼기 · 한 명만 전열".</summary>
    public static string PlanName(RunTacticPlan plan, RunRowPlan rows)
    {
        var tactics = plan switch
        {
            RunTacticPlan.NoDebuffs => "디버프 빼기",
            RunTacticPlan.Magic => "마법 먼저",
            RunTacticPlan.Area => "광역 먼저",
            RunTacticPlan.OverTime => "지속 피해 먼저",
            RunTacticPlan.Disable => "기절·약화 먼저",
            RunTacticPlan.Support => "회복·버프 먼저",
            RunTacticPlan.Damage => "공격만",
            _ => "기본 전술",
        };
        var line = rows switch
        {
            RunRowPlan.AllFront => "모두 전열",
            RunRowPlan.OneFront => "한 명만 전열",
            RunRowPlan.TwoFront => "둘 전열",
            _ => "기본 줄",
        };
        return $"{tactics} · {line}";
    }

    /// <summary>가방의 장신구를 칸이 빈 멤버가 낀다. 무기·방어구는 살 때 받을 멤버를 골라 끼므로 여기서 바꾸지 않는다.</summary>
    private void Equip(Run run)
    {
        for (var i = 0; i < run.Bag.Count; i++)
        {
            if (_data.Items[run.Bag[i]].Slot == EquipmentSlot.Trinket
                && run.Living.Where(m => m.Trinkets.Count < _rules.MaxTrinkets).MinBy(m => m.Trinkets.Count) is { } target)
            {
                run.Equip(_data, target, i);
                i = -1;
            }
        }
    }

    /// <summary>무기·방어구를 낄 수 있는 멤버 (무기는 그 계열 클래스). 장신구 등은 비어 있다.</summary>
    private IEnumerable<RunMember> Wearers(Run run, ItemDefinition item) => item.Slot switch
    {
        EquipmentSlot.MainHand => run.Living.Where(m => _data.Classes[m.ClassId].CanWield(item)),
        EquipmentSlot.Body => run.Living,
        _ => [],
    };

    /// <summary>방금 산 무기·방어구(가방 맨 끝)를 그 멤버가 낀다.</summary>
    private void Wear(Run run, string memberId)
    {
        run.Equip(_data, run.Members.Single(m => m.Id == memberId), run.Bag.Count - 1);
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
        Equip(run);
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
    /// 상점. 아무렇게나 봇은 물건마다 반반의 확률로 사고 봉인도 반반으로 푼다. 똑똑한 봇은 살 수 있는 것(물건, 가장 많이 봉인된 멤버의 봉인
    /// 풀기)마다 사 본 판으로 그 막의 적을 미리 싸워 보고, 골드 1당 나아지는 정도가 가장 큰 것부터 산다. 나아지는 것이
    /// <see cref="MinShopGain"/>보다 작으면 그만 산다(골드를 다음 상점에 남기는 셈이다).
    /// </summary>
    private void Shop(Run run, Random random, Tally tally)
    {
        var (goldIn, offered, sealedLines) = (run.Gold, run.Stock.ToList(), run.Living.Sum(m => m.Sealed));
        var (bought, unseals, values) = (new List<RunStock>(), 0, new List<RunSimValue>());
        void Buy(int i, string? wearer = null)
        {
            var stock = run.Stock[i];
            run.Buy(_data, i);
            if (wearer is not null)
            {
                Wear(run, wearer);
            }

            bought.Add(stock);
            tally.Spend(stock.Kind switch { RunStockKind.Item => RunSpend.Item, RunStockKind.Skill => RunSpend.Skill, _ => RunSpend.Recruit }, stock.Price);
        }

        void Unseal(RunMember member)
        {
            run.Unseal(_data, member);
            unseals++;
            tally.Spend(RunSpend.Unseal, _rules.Shop.UnsealPrice);
        }

        if (_options.Bot == RunBot.Random)
        {
            foreach (var i in Enumerable.Range(0, run.Stock.Count).OrderBy(_ => random.Next()).ToList())
            {
                if (random.Next(2) == 0 && run.WhyCannotBuy(_data, i) is null)
                {
                    // 무기·방어구는 낄 수 있는 멤버 아무에게나 낀다.
                    var wearers = run.Stock[i].Kind == RunStockKind.Item ? Wearers(run, _data.Items[run.Stock[i].Id]).ToList() : [];
                    Buy(i, wearers.Count > 0 ? wearers[random.Next(wearers.Count)].Id : null);
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
        }
        else
        {
            while (true)
            {
                var baseline = Lookahead(Run.Restore(run.ToSave(), _data));
                // 무기·방어구는 낄 멤버마다 따로 재 본다.
                var options = Enumerable.Range(0, run.Stock.Count)
                    .Where(i => run.WhyCannotBuy(_data, i) is null)
                    .SelectMany(i =>
                    {
                        var wearers = run.Stock[i].Kind == RunStockKind.Item ? Wearers(run, _data.Items[run.Stock[i].Id]).Select(m => (string?)m.Id).ToList() : [];
                        return (wearers.Count > 0 ? wearers : [null]).Select(m => (Stock: (int?)i, Member: m, Price: run.Stock[i].Price));
                    })
                    .ToList();
                if (run.Living.Where(m => m.Sealed > 0).MaxBy(m => m.Sealed) is { } most && run.WhyCannotUnseal(_data, most) is null)
                {
                    options.Add((null, most.Id, _rules.Shop.UnsealPrice));
                }

                var gains = options
                    .Select(o =>
                    {
                        var copy = Run.Restore(run.ToSave(), _data);
                        if (o.Stock is { } i)
                        {
                            copy.Buy(_data, i);
                            if (o.Member is { } wearer)
                            {
                                Wear(copy, wearer);
                            }
                        }
                        else
                        {
                            copy.Unseal(_data, copy.Members.Single(m => m.Id == o.Member));
                        }

                        return (Option: o, Gain: Lookahead(copy) - baseline);
                    })
                    .ToList();
                if (bought.Count == 0 && unseals == 0)
                {
                    // 물건마다 가장 잘 맞는 멤버에게 낀 값어치.
                    values.AddRange(gains.GroupBy(g => g.Option.Stock ?? -1).Select(g => g.MaxBy(x => x.Gain))
                        .Select(g => new RunSimValue(g.Option.Stock is { } i ? run.Stock[i] : null, g.Option.Price, g.Gain)));
                }

                var best = gains
                    .Where(x => x.Gain >= MinShopGain)
                    .OrderByDescending(x => x.Gain / Math.Max(1, x.Option.Price))
                    .FirstOrDefault();
                if (best.Option.Price == 0)
                {
                    break;
                }

                if (best.Option.Stock is { } stock)
                {
                    Buy(stock, best.Option.Member);
                }
                else
                {
                    Unseal(run.Members.Single(m => m.Id == best.Option.Member));
                }

                Equip(run);
            }
        }

        tally.Shops.Add(new RunSimShop(run.Act, goldIn, run.Gold, offered, bought, unseals, sealedLines, values));
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

        public List<RunSimShop> Shops { get; } = [];

        public Dictionary<RunSpend, int> Spent { get; } = [];

        public int Deaths { get; private set; }

        public int Skipped { get; set; }

        public int Earned { get; set; }

        public void Spend(RunSpend where, int gold) => Spent[where] = Spent.GetValueOrDefault(where) + gold;

        public void Record(Run run, RunNode node, RunBattle battle, string? plan)
        {
            var allies = battle.Result.Combatants.Where(c => c.Side == CombatSide.Ally && !c.IsSummon).ToList();
            Fights.Add(new RunSimFight(run.Act, node.Floor, battle.Kind, battle.EncounterId, battle.Result.Outcome,
                HpFraction(battle.Result, CombatSide.Ally), allies.Count(c => !c.IsAlive), battle.Result.Events.OfType<TurnStarted>().LastOrDefault()?.Time ?? 0, plan));
            Deaths += battle.Died.Count;
            Earned += battle.Gold;
        }
    }
}
