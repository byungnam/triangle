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
    /// 다 아는 플레이어: 카드·상점은 고를 수 있는 것마다 그 막의 적을 미리 싸워 보고 가장 잘 싸우는 쪽을 고르고, 보스 앞에서 전술과 줄을
    /// 미리 싸워 보고 바꾼다. 사람이 할 수 없는 플레이라 난이도의 천장으로 쓴다. 지점, 휴식, 이벤트는 정해 둔 규칙으로 고른다.
    /// </summary>
    Smart,

    /// <summary>
    /// 보통 사람 (2026-10-23): 미리 싸워 보지 않고 눈에 보이는 것만으로 고른다. 카드는 정해 둔 우선순위(줄 상한, 놓인 사본의 강화, 없는 액티브,
    /// 패시브), 상점은 봉인 풀기 → 강화 → 방어구·무기 → 장신구 → 없는 스킬 → 줄 상한 순으로 살 수 있는 만큼, 보스 대비는 하지 않는다.
    /// 첫 판 난이도의 기준이다. 지점, 휴식, 이벤트는 똑똑한 봇과 같은 규칙이다.
    /// </summary>
    Novice,
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

    /// <summary>모든 판의 원정 지도 (기본: 기본 지도). <see cref="Tier"/>를 주면 쓰지 않는다.</summary>
    public RunChart Chart { get; init; } = RunChart.Base;

    /// <summary>주면 판마다 이 지도 등급의 지도를 하나 굴려 그 지도로 둔다 (판의 시드로 굴린다).</summary>
    public int? Tier { get; init; }
}

/// <summary>골드를 쓴 곳.</summary>
public enum RunSpend
{
    Item,
    Skill,
    Recruit,
    Unseal,
    Event,
    Upgrade,
    Line,
}

/// <summary>전투 하나의 기록.</summary>
/// <param name="Floor">지점의 층 (0부터, 보스는 막의 층 수).</param>
/// <param name="AllyHp">끝났을 때 아군(소환수 빼고)의 남은 HP 합 / 최대 HP 합.</param>
/// <param name="Downs">쓰러진 아군 수 (소환수 빼고).</param>
/// <param name="Time">전투가 끝난 시각 (마지막 행동의 전투 시계).</param>
/// <param name="Plan">보스와 싸우기 전에 똑똑한 봇이 고른 전술 방식과 줄 배치 (<see cref="RunSimulation.PlanName"/>). 보스가 아니면 null.</param>
/// <param name="DefaultWin">보스 대비에서 기본 전술·기본 줄로 미리 싸워 이긴 비율 (보스 대비를 하지 않았으면 null).</param>
/// <param name="PlanWin">보스 대비에서 고른 조합으로 미리 싸워 이긴 비율. 기본과의 차이가 "보스에 맞춰야 이긴다"의 크기다.</param>
public sealed record RunSimFight(
    int Act, int Floor, RunNodeKind Kind, string EncounterId, CombatOutcome Outcome, double AllyHp, int Downs, long Time, string? Plan = null,
    double? DefaultWin = null, double? PlanWin = null)
{
    /// <summary>끝났을 때 아군(소환수 빼고, MP가 있는 유닛)의 남은 MP 비율 평균.</summary>
    public double AllyMp { get; init; }

    /// <summary>진 전투를 다시 싸운 것이다 (2026-10-24, 목숨).</summary>
    public bool Retry { get; init; }

    /// <summary>MP가 20% 이하로 끝난 아군(소환수 빼고, MP가 있는 유닛) 수.</summary>
    public int LowMp { get; init; }

    /// <summary>아군이 MP를 채운 횟수 (정신 집중, 장전).</summary>
    public int Restores { get; init; }
}

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

    /// <summary>아군에게 버프를 거는 줄을 뺀다 (버프를 보면 태우는 적, 2026-10-23). 기존 값 뒤에 덧붙였다.</summary>
    NoBuffs,
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
/// <param name="Upgraded">골드로 건 강화와 낸 값 (건 차례대로).</param>
public sealed record RunSimShop(
    int Act, int GoldIn, int GoldOut, IReadOnlyList<RunStock> Offered, IReadOnlyList<RunStock> Bought, int Unseals, int Sealed, IReadOnlyList<RunSimValue> Values,
    IReadOnlyList<(RunUpgradeKind Kind, int Price)> Upgraded);

/// <summary>상점에서 살 수 있던 것 하나의 값어치. <paramref name="Stock"/>이 null이면 강화(<paramref name="Upgrade"/>)나 봉인 풀기다.</summary>
public sealed record RunSimValue(RunStock? Stock, int Price, double Gain, RunUpgradeKind? Upgrade = null);

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

    /// <summary>이 판의 원정 지도.</summary>
    public RunChart Chart { get; init; } = RunChart.Base;

    /// <summary>판이 끝나 받는 명성 (<see cref="Profile.RenownFor"/>, 지도의 명성 보너스 포함).</summary>
    public int Renown { get; init; }
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

    /// <summary>MP를 채우는 공용 행동 (MP를 쓰는 멤버에게 봇이 둔다).</summary>
    private const string Focus = "focus";

    private readonly GameData _data;
    private readonly RunDefinition _rules;
    private readonly RunSimOptions _options;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(RunChart Chart, int Act, RunNodeKind Kind, string EncounterId), IReadOnlyList<CombatantSetup>> _teams = new();
    private readonly Dictionary<string, string> _owners;

    public RunSimulation(GameData data, RunSimOptions? options = null)
    {
        _data = data;
        _rules = data.Run ?? throw new InvalidOperationException("run.json이 없다");
        _options = options ?? new RunSimOptions();
        _owners = data.Classes.Values.SelectMany(c => c.Skills.Select(s => (Skill: s, Class: c.Id))).ToDictionary(x => x.Skill, x => x.Class);
    }

    /// <summary>그 판의 지도로 센 적 조합 (지도와 막, 지점 종류, 조합마다 한 번 만들어 둔다).</summary>
    private IReadOnlyList<CombatantSetup> Team(Run run, RunNodeKind kind, string encounterId) =>
        _teams.GetOrAdd((run.Chart, run.Act, kind, encounterId), k => Run.EnemyTeam(_data, k.Act, k.Kind, k.EncounterId, run.Terms));

    /// <summary>판 여러 개를 시드 <paramref name="firstSeed"/>부터 돌린다. 판마다 따로 계산하므로 여러 스레드로 돌려도 결과가 같다.</summary>
    public IReadOnlyList<RunSimResult> RunMany(int runs, int firstSeed = 0)
    {
        var results = new RunSimResult[runs];
        Parallel.For(0, runs, i => results[i] = Play(firstSeed + i));
        return results;
    }

    public RunSimResult Play(int seed)
    {
        var chart = _options.Tier is { } tier ? RunChart.Roll(_data, tier, 1, new Random(Seeds.Derive(seed, 0xC4A)))[0] : _options.Chart;
        var run = Run.Start(_data, _options.Party, seed, _options.Unlocks, chart: chart);
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
                    var plan = node.Kind == RunNodeKind.Boss && _options.Bot == RunBot.Smart ? PrepareForBoss(run) : ((string, double, double)?)null;
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
                    if (run.WhyCannotRestUnseal(_data) is null && (_options.Bot == RunBot.Random ? random.Next(2) == 0 : sealedLines > 0))
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
                case RunPhase.Retry:
                    // 진 전투를 다시 싸우기 전에 고친다: 똑똑한 봇은 그 적과 미리 싸워 보고, 나머지는 다른 방식을 하나 골라 본다.
                    var lost = run.Retrying!;
                    if (_options.Bot == RunBot.Smart)
                    {
                        PrepareFor(run, Team(run, lost.Kind, lost.EncounterId));
                    }
                    else
                    {
                        Improvise(run, random);
                    }

                    tally.Record(run, run.Map.Node(run.Position!.Value), run.Retry(_data), null, retry: true);
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
            living.Sum(m => m.ActiveTactics.Count(t => !RunUpgrades.Action(_data, t.ActionId).Universal)),
            living.Sum(m => m.Earned),
            living.Count(m => m.Earned >= _rules.MaxLines),
            tally.Shops)
        {
            Chart = run.Chart,
            Renown = Profile.RenownFor(run, _data),
        };
    }

    /// <summary>
    /// 전술을 다시 짠다: 사본을 그 클래스의 살아 있는 멤버에게 나눠 주고(켤 줄이 많이 남은 멤버부터) <see cref="DraftTactics"/>로 줄을 적는다.
    /// MP를 쓰는 사본이 있는 멤버는 정신 집중(MP 30% 이하일 때) 줄도 둔다(켤 줄이 모자라면 다른 줄과 같이 남길 것을 고른다).
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
            var holders = living.Where(m => _owners.GetValueOrDefault(RunUpgrades.BaseId(id)) == m.ClassId).ToList();
            for (var k = 0; k < count && holders.Count > 0; k++)
            {
                skills[holders.MinBy(m => skills[m].Count - m.Lines)!].Add(id);
            }
        }

        foreach (var member in living)
        {
            if (skills[member].Any(id => RunUpgrades.Action(_data, id).MpCost > 0))
            {
                skills[member].Add(Focus);
            }

            run.SetTactics(_data, member, Apply(plan, DraftTactics.Build(skills[member], _rules.MaxLines, _data, active: member.Lines).SkipLast(1)).ToList());
        }
    }

    /// <summary>방식대로 줄을 빼거나 위로 올린다 (나머지 순서는 그대로).</summary>
    private IEnumerable<Tactic> Apply(RunTacticPlan plan, IEnumerable<Tactic> lines)
    {
        ActionDefinition A(Tactic t) => RunUpgrades.Action(_data, t.ActionId);
        bool Applies(Tactic t, Func<Effects.EffectDefinition, bool> match) => A(t).Applies.Any(x => match(_data.Effects[x.EffectId]));
        bool Debuffs(Tactic t) => Applies(t, e => e.Kind == Effects.EffectKind.Debuff);
        bool Buffs(Tactic t) => A(t).Side != TargetSide.Enemy && Applies(t, e => e.Kind == Effects.EffectKind.Buff);
        IEnumerable<Tactic> First(Func<Tactic, bool> first) => lines.OrderBy(t => first(t) ? 0 : 1);
        return plan switch
        {
            RunTacticPlan.NoDebuffs => lines.Where(t => !Debuffs(t)),
            RunTacticPlan.NoBuffs => lines.Where(t => !Buffs(t)),
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
    /// (사람이 보스를 보고 전술을 고치는 것을 흉내 낸다). 같으면 기본에 가까운 쪽. 고른 조합의 이름과, 기본 조합과 고른 조합이 미리 싸워 이긴
    /// 비율을 돌려준다.
    /// </summary>
    private (string Name, double DefaultWin, double PlanWin) PrepareForBoss(Run run) => PrepareFor(run, Team(run, RunNodeKind.Boss, run.Bosses[run.Act]));

    /// <summary>
    /// 이 적과 싸우기 전에 전술 방식과 줄 배치의 조합마다 판을 복사해 미리 싸워 보고, 가장 나은 조합으로 들어간다(보스 대비, 진 전투 다시 싸우기).
    /// </summary>
    private (string Name, double DefaultWin, double PlanWin) PrepareFor(Run run, IReadOnlyList<CombatantSetup> boss)
    {
        var best = (Score: double.MinValue, Plan: RunTacticPlan.Default, Rows: RunRowPlan.Default, Win: 0.0);
        var defaultWin = 0.0;
        foreach (var plan in Enum.GetValues<RunTacticPlan>())
        {
            foreach (var rows in Enum.GetValues<RunRowPlan>())
            {
                var copy = Run.Restore(run.ToSave(), _data);
                SetRows(copy, rows);
                Arrange(copy, plan);
                var allies = copy.Living.Select(m => copy.Setup(_data, m)).ToList();
                var results = Enumerable.Range(0, BossSeeds).Select(s => CombatSimulator.Run(allies, boss, Run.Catalog(_data), Seeds.Derive(BossSalt, s))).ToList();
                var score = results.Average(result =>
                    result.Outcome == CombatOutcome.Victory ? 1 + 0.5 * HpFraction(result, CombatSide.Ally) : 0.5 * (1 - HpFraction(result, CombatSide.Enemy)));
                var win = results.Average(result => result.Outcome == CombatOutcome.Victory ? 1.0 : 0.0);
                if (plan == RunTacticPlan.Default && rows == RunRowPlan.Default)
                {
                    defaultWin = win;
                }

                if (score > best.Score + 1e-9)
                {
                    best = (score, plan, rows, win);
                }
            }
        }

        SetRows(run, best.Rows);
        Arrange(run, best.Plan);
        return (PlanName(best.Plan, best.Rows), defaultWin, best.Win);
    }

    /// <summary>진 전투를 다시 싸우기 전에, 미리 싸워 보지 않고 전술 방식과 줄 배치를 아무거나 하나 골라 본다 (보통 사람·무작위 봇).</summary>
    private void Improvise(Run run, Random random)
    {
        var rows = Enum.GetValues<RunRowPlan>();
        var plans = Enum.GetValues<RunTacticPlan>();
        SetRows(run, rows[random.Next(rows.Length)]);
        Arrange(run, plans[random.Next(plans.Length)]);
    }

    /// <summary>보스 대비 조합의 이름 (기록과 보고용): "디버프 빼기 · 한 명만 전열".</summary>
    public static string PlanName(RunTacticPlan plan, RunRowPlan rows)
    {
        var tactics = plan switch
        {
            RunTacticPlan.NoDebuffs => "디버프 빼기",
            RunTacticPlan.NoBuffs => "버프 빼기",
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

    /// <summary>물건을 받을 멤버: 무기·방어구는 낄 수 있는 멤버, 줄 상한은 늘릴 수 있는 멤버. 받을 멤버가 없는 물건은 비어 있다.</summary>
    private IEnumerable<RunMember> Recipients(Run run, RunStock stock) => stock.Kind switch
    {
        RunStockKind.Item => Wearers(run, _data.Items[stock.Id]),
        RunStockKind.Line => run.Living.Where(m => m.Earned < _rules.MaxLines),
        _ => [],
    };

    /// <summary>산다. 무기·방어구는 받을 멤버가 바로 끼고, 줄 상한은 받을 멤버에게 간다.</summary>
    private void Buy(Run run, int stock, string? memberId)
    {
        var member = memberId is null ? null : run.Members.Single(m => m.Id == memberId);
        var kind = run.Stock[stock].Kind;
        run.Buy(_data, stock, kind == RunStockKind.Line ? member : null);
        if (kind == RunStockKind.Item && memberId is not null)
        {
            Wear(run, memberId);
        }
    }

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

    /// <summary>
    /// 보상 카드 하나를 고른다. 아무렇게나 봇은 카드를 먼저 고르고 받을 멤버(강화 카드는 사본)를 고른다. 똑똑한 봇은 고를 수 있는 것마다 미리
    /// 싸워 본다. 보통 사람 봇은 정해 둔 우선순위(<see cref="NoviceWorth"/>)로 고른다.
    /// </summary>
    private void TakeReward(Run run, Random random, Tally tally)
    {
        var choices = new List<(int Card, RunMember? Member, string? Copy)>();
        for (var i = 0; i < run.Offer.Count; i++)
        {
            switch (run.Offer[i].Kind)
            {
                case RunCardKind.Active:
                    choices.Add((i, null, null));
                    break;
                case RunCardKind.Upgrade:
                    choices.AddRange(run.UpgradableCopies(_data).Where(c => run.WhyCannotTake(_data, i, null, c) is null).Select(c => (i, (RunMember?)null, (string?)c)));
                    break;
                default:
                    choices.AddRange(run.Living.Where(m => run.WhyCannotTake(_data, i, m) is null).Select(m => (i, (RunMember?)m, (string?)null)));
                    break;
            }
        }

        if (choices.Count == 0)
        {
            run.Skip();
            tally.Skipped++;
            return;
        }

        (int Card, RunMember? Member, string? Copy) choice;
        if (_options.Bot == RunBot.Random)
        {
            // 카드를 먼저 고르고 받을 멤버나 사본을 고른다 (받을 수 있는 것이 많은 카드가 더 자주 뽑히지 않게).
            var cards = choices.Select(c => c.Card).Distinct().ToList();
            var card = cards[random.Next(cards.Count)];
            var options = choices.Where(c => c.Card == card).ToList();
            choice = options[random.Next(options.Count)];
        }
        else if (_options.Bot == RunBot.Novice)
        {
            choice = choices.MaxBy(c => NoviceWorth(run, run.Offer[c.Card], c.Member, c.Copy));
        }
        else
        {
            choice = choices.MaxBy(c =>
            {
                var copy = Run.Restore(run.ToSave(), _data);
                copy.Take(_data, c.Card, c.Member is null ? null : copy.Members.Single(m => m.Id == c.Member.Id), c.Copy);
                return Lookahead(copy);
            });
        }

        tally.Picks.Add(run.Offer[choice.Card]);
        run.Take(_data, choice.Card, choice.Member, choice.Copy);
    }

    /// <summary>
    /// 보통 사람 봇의 보상 값: 줄 상한 3(놀고 있는 사본이 있는 클래스 멤버에게는 3.5, 줄이 적을수록 조금 더), 켜진 줄에 놓인 사본의 강화 2.6(위력이
    /// 클수록 조금 더), 없는 액티브 2.4(그 클래스에 빈 줄이 없으면 2.2), 패시브 2(레벨이 낮을수록 조금 더), 있는 액티브 1(빈 줄이 없으면 0.5),
    /// 놀고 있는 사본의 강화 0.3. 빈 줄을 새 액티브로 먼저 채우게 하면 오히려 더 졌다(2026-10-23, 2막 탈락 52% → 76%).
    /// </summary>
    private double NoviceWorth(Run run, RunCard card, RunMember? member, string? copy) => card.Kind switch
    {
        RunCardKind.Line => (Room(run, member!.ClassId) < 0 ? 3.5 : 3) - 0.1 * member.Earned,
        RunCardKind.Upgrade when run.Living.Any(m => m.ActiveTactics.Any(t => t.ActionId == copy)) => 2.6 + RunUpgrades.Action(_data, copy!).Power / 1000.0,
        RunCardKind.Upgrade => 0.3,
        RunCardKind.Active => (Owned(run, card.Id!), Room(run, card.ClassId!) > 0) switch
        {
            (false, true) => 2.4,
            (false, false) => 2.2,
            (true, true) => 1,
            _ => 0.5,
        },
        RunCardKind.Passive => 2 - 0.1 * member!.Level(card.Id!),
        _ => 0,
    };

    /// <summary>클래스의 빈 줄: 살아 있는 그 클래스 멤버의 줄 상한 합에서 그 클래스 사본 수를 뺀 것. 음수면 놀고 있는 사본이 있다.</summary>
    private int Room(Run run, string classId) =>
        run.Living.Where(m => m.ClassId == classId).Sum(m => m.Lines)
        - run.Copies.Where(p => _owners.GetValueOrDefault(RunUpgrades.BaseId(p.Key)) == classId).Sum(p => p.Value);

    /// <summary>파티가 이 행동의 사본(강화된 것 포함)을 가졌는가.</summary>
    private static bool Owned(Run run, string actionId) => run.Copies.Keys.Any(id => RunUpgrades.BaseId(id) == actionId);

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
                var result = CombatSimulator.Run(allies, Team(run, kind, encounter), Run.Catalog(_data), Seeds.Derive(LookaheadSalt, s));
                score += result.Outcome == CombatOutcome.Victory
                    ? 1 + 0.5 * HpFraction(result, CombatSide.Ally)
                    : 0.5 * (1 - HpFraction(result, CombatSide.Enemy));
            }
        }

        return score / (encounters.Count * _options.LookaheadSeeds);
    }

    /// <summary>상점에서 할 수 있는 것 하나: 물건(받을 멤버), 사본 강화, 봉인 풀기.</summary>
    private sealed record ShopOption(int? Stock, string? Member, string? Copy, RunUpgradeKind? Upgrade, int Price);

    /// <summary>
    /// 상점. 아무렇게나 봇은 물건마다 반반의 확률로 사고, 남은 강화마다 반반으로 아무 사본에 아무 강화를 걸고, 봉인도 반반으로 푼다.
    /// 똑똑한 봇은 할 수 있는 것(물건, 받을 멤버마다의 줄 상한, 켜진 줄에 놓인 사본의 강화, 가장 많이 봉인된 멤버의 봉인 풀기)마다 해 본 판으로
    /// 그 막의 적을 미리 싸워 보고, 골드 1당 나아지는 정도가 가장 큰 것부터 한다. 나아지는 것이 <see cref="MinShopGain"/>보다 작으면 그만한다.
    /// 보통 사람 봇은 봉인 풀기 → 켜진 줄 사본의 강화 → 방어구·무기 → 장신구 → 줄 상한 순으로 살 수 있는 만큼 산다.
    /// </summary>
    private void Shop(Run run, Random random, Tally tally)
    {
        var (goldIn, offered, sealedLines) = (run.Gold, run.Stock.ToList(), run.Living.Sum(m => m.Sealed));
        var (bought, unseals, values, upgraded) = (new List<RunStock>(), 0, new List<RunSimValue>(), new List<(RunUpgradeKind Kind, int Price)>());
        void Do(ShopOption o)
        {
            if (o.Stock is { } i)
            {
                var stock = run.Stock[i];
                Buy(run, i, o.Member);
                bought.Add(stock);
                tally.Spend(stock.Kind switch
                {
                    RunStockKind.Item => RunSpend.Item,
                    RunStockKind.Skill => RunSpend.Skill,
                    RunStockKind.Line => RunSpend.Line,
                    _ => RunSpend.Recruit,
                }, stock.Price);
            }
            else if (o.Upgrade is { } kind)
            {
                var price = run.UpgradePrice(_data, o.Copy!);
                tally.Spend(RunSpend.Upgrade, price);
                run.Upgrade(_data, o.Copy!, kind);
                upgraded.Add((kind, price));
            }
            else
            {
                tally.Spend(RunSpend.Unseal, run.UnsealPrice(_data));
                run.Unseal(_data, run.Members.Single(m => m.Id == o.Member));
                unseals++;
            }
        }

        switch (_options.Bot)
        {
            case RunBot.Random:
                foreach (var i in Enumerable.Range(0, run.Stock.Count).OrderBy(_ => random.Next()).ToList())
                {
                    // 무기·방어구와 줄 상한은 받을 수 있는 멤버 아무에게나 준다.
                    var recipients = Recipients(run, run.Stock[i]).ToList();
                    var member = recipients.Count > 0 ? recipients[random.Next(recipients.Count)] : null;
                    if (random.Next(2) == 0 && run.WhyCannotBuy(_data, i, member) is null)
                    {
                        Do(new ShopOption(i, member?.Id, null, null, run.Stock[i].Price));
                    }
                }

                while (run.UpgradesLeft > 0 && random.Next(2) == 0)
                {
                    var possible = UpgradeOptions(run, run.UpgradableCopies(_data)).ToList();
                    if (possible.Count == 0)
                    {
                        break;
                    }

                    Do(possible[random.Next(possible.Count)]);
                }

                var sealedMembers = run.Living.Where(m => m.Sealed > 0).ToList();
                if (sealedMembers.Count > 0 && random.Next(2) == 0)
                {
                    var member = sealedMembers[random.Next(sealedMembers.Count)];
                    if (run.WhyCannotUnseal(_data, member) is null)
                    {
                        Do(new ShopOption(null, member.Id, null, null, run.UnsealPrice(_data)));
                    }
                }

                break;
            case RunBot.Novice:
                NoviceShop(run, Do);
                break;
            default:
                while (true)
                {
                    var baseline = Lookahead(Run.Restore(run.ToSave(), _data));
                    // 무기·방어구와 줄 상한은 받을 멤버마다, 강화는 켜진 줄에 놓인 사본과 종류마다 따로 재 본다.
                    var options = Enumerable.Range(0, run.Stock.Count)
                        .SelectMany(i =>
                        {
                            var recipients = Recipients(run, run.Stock[i]).Select(m => (RunMember?)m).ToList();
                            return (recipients.Count > 0 ? recipients : [null])
                                .Where(m => run.WhyCannotBuy(_data, i, m) is null)
                                .Select(m => new ShopOption(i, m?.Id, null, null, run.Stock[i].Price));
                        })
                        .Concat(UpgradeOptions(run, PlacedCopies(run)))
                        .ToList();
                    if (run.Living.Where(m => m.Sealed > 0).MaxBy(m => m.Sealed) is { } most && run.WhyCannotUnseal(_data, most) is null)
                    {
                        options.Add(new ShopOption(null, most.Id, null, null, run.UnsealPrice(_data)));
                    }

                    var gains = options
                        .Select(o =>
                        {
                            var copy = Run.Restore(run.ToSave(), _data);
                            if (o.Stock is { } i)
                            {
                                Buy(copy, i, o.Member);
                            }
                            else if (o.Upgrade is { } kind)
                            {
                                copy.Upgrade(_data, o.Copy!, kind);
                            }
                            else
                            {
                                copy.Unseal(_data, copy.Members.Single(m => m.Id == o.Member));
                            }

                            return (Option: o, Gain: Lookahead(copy) - baseline);
                        })
                        .ToList();
                    if (bought.Count == 0 && unseals == 0 && upgraded.Count == 0)
                    {
                        // 물건마다 가장 잘 맞는 멤버에게 낀 값어치, 강화 종류마다 가장 잘 맞는 사본에 건 값어치.
                        values.AddRange(gains.GroupBy(g => (g.Option.Stock ?? -1, g.Option.Upgrade)).Select(g => g.MaxBy(x => x.Gain))
                            .Select(g => new RunSimValue(g.Option.Stock is { } i ? run.Stock[i] : null, g.Option.Price, g.Gain, g.Option.Upgrade)));
                    }

                    var best = gains
                        .Where(x => x.Gain >= MinShopGain)
                        .OrderByDescending(x => x.Gain / Math.Max(1, x.Option.Price))
                        .FirstOrDefault();
                    if (best.Option is null)
                    {
                        break;
                    }

                    Do(best.Option);
                    Equip(run);
                }

                break;
        }

        tally.Shops.Add(new RunSimShop(run.Act, goldIn, run.Gold, offered, bought, unseals, sealedLines, values, upgraded));
    }

    /// <summary>켜진 줄에 놓인 사본 중 강화할 수 있는 것 (똑똑한 봇이 강화를 재 보는 후보).</summary>
    private IReadOnlyList<string> PlacedCopies(Run run)
    {
        var placed = run.Living.SelectMany(m => m.ActiveTactics).Select(t => t.ActionId).ToHashSet();
        return run.UpgradableCopies(_data).Where(placed.Contains).ToList();
    }

    /// <summary>사본마다 걸 수 있는 강화 (이 상점에서 살 수 있는 것).</summary>
    private IEnumerable<ShopOption> UpgradeOptions(Run run, IEnumerable<string> copies) =>
        copies.SelectMany(c => Enum.GetValues<RunUpgradeKind>().Where(k => run.WhyCannotUpgrade(_data, c, k) is null)
            .Select(k => new ShopOption(null, null, c, k, run.UpgradePrice(_data, c))));

    /// <summary>
    /// 보통 사람 봇의 상점: 가장 많이 봉인된 멤버의 봉인을 풀 수 있는 만큼 풀고, 켜진 줄에 놓인 사본 중 위력이 가장 큰 것에 강화(위력, 연쇄,
    /// 지속, 신속 순으로 처음 맞는 것)를 걸고, 방어구는 전열 멤버, 무기는 쓰는 멤버, 장신구는 칸이 빈 멤버에게 비싼 것부터 사고, 빈 줄이 있는
    /// 클래스의 없는 스킬 사본을 사고, 남으면 줄 상한을 놀고 있는 사본이 있는 클래스(없으면 줄이 가장 적은) 멤버에게 산다.
    /// </summary>
    private void NoviceShop(Run run, Action<ShopOption> act)
    {
        while (run.Living.Where(m => m.Sealed > 0).MaxBy(m => m.Sealed) is { } most && run.WhyCannotUnseal(_data, most) is null)
        {
            act(new ShopOption(null, most.Id, null, null, run.UnsealPrice(_data)));
        }

        RunUpgradeKind[] order = [RunUpgradeKind.Power, RunUpgradeKind.Chain, RunUpgradeKind.Lasting, RunUpgradeKind.Swift];
        while (run.UpgradesLeft > 0
            && PlacedCopies(run).OrderByDescending(c => RunUpgrades.Action(_data, c).Power).ThenBy(c => c, StringComparer.Ordinal)
                .SelectMany(c => order.Where(k => run.WhyCannotUpgrade(_data, c, k) is null).Select(k => (Copy: c, Kind: k)))
                .FirstOrDefault() is { Copy: not null } pick)
        {
            act(new ShopOption(null, null, pick.Copy, pick.Kind, run.UpgradePrice(_data, pick.Copy)));
        }

        var items = Enumerable.Range(0, run.Stock.Count).Where(i => run.Stock[i].Kind == RunStockKind.Item)
            .OrderBy(i => _data.Items[run.Stock[i].Id].Slot == EquipmentSlot.Trinket ? 1 : 0)
            .ThenByDescending(i => run.Stock[i].Price)
            .ToList();
        foreach (var i in items)
        {
            var item = _data.Items[run.Stock[i].Id];
            var wearer = item.Slot switch
            {
                EquipmentSlot.Body => run.Living.FirstOrDefault(m => m.Row == Row.Front) ?? run.Living.FirstOrDefault(),
                EquipmentSlot.MainHand => Wearers(run, item).FirstOrDefault(),
                _ => null,
            };
            if ((item.Slot == EquipmentSlot.Trinket || wearer is not null) && run.WhyCannotBuy(_data, i) is null)
            {
                act(new ShopOption(i, wearer?.Id, null, null, run.Stock[i].Price));
            }
        }

        var skills = Enumerable.Range(0, run.Stock.Count).Where(i => run.Stock[i].Kind == RunStockKind.Skill)
            .OrderBy(i => Owned(run, run.Stock[i].Id) ? 1 : 0)
            .ToList();
        foreach (var i in skills)
        {
            if (Room(run, _owners[run.Stock[i].Id]) > 0 && !Owned(run, run.Stock[i].Id) && run.WhyCannotBuy(_data, i) is null)
            {
                act(new ShopOption(i, null, null, null, run.Stock[i].Price));
            }
        }

        var line = Enumerable.Range(0, run.Stock.Count).FirstOrDefault(i => run.Stock[i].Kind == RunStockKind.Line, -1);
        if (line >= 0 && run.Living.Where(m => m.Earned < _rules.MaxLines).OrderBy(m => Room(run, m.ClassId) < 0 ? 0 : 1).ThenBy(m => m.Earned).FirstOrDefault() is { } to
            && run.WhyCannotBuy(_data, line, to) is null)
        {
            act(new ShopOption(line, to.Id, null, null, run.Stock[line].Price));
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

        public List<RunSimShop> Shops { get; } = [];

        public Dictionary<RunSpend, int> Spent { get; } = [];

        public int Deaths { get; private set; }

        public int Skipped { get; set; }

        public int Earned { get; set; }

        public void Spend(RunSpend where, int gold) => Spent[where] = Spent.GetValueOrDefault(where) + gold;

        public void Record(Run run, RunNode node, RunBattle battle, (string Name, double DefaultWin, double PlanWin)? plan, bool retry = false)
        {
            var allies = battle.Result.Combatants.Where(c => c.Side == CombatSide.Ally && !c.IsSummon).ToList();
            var casters = allies.Where(c => c.MaxMp > 0).ToList();
            var ids = allies.Select(c => c.Id).ToHashSet();
            Fights.Add(new RunSimFight(run.Act, node.Floor, battle.Kind, battle.EncounterId, battle.Result.Outcome,
                HpFraction(battle.Result, CombatSide.Ally), allies.Count(c => !c.IsAlive), battle.Result.Events.OfType<TurnStarted>().LastOrDefault()?.Time ?? 0,
                plan?.Name, plan?.DefaultWin, plan?.PlanWin)
            {
                AllyMp = casters.Count == 0 ? 1 : casters.Average(c => (double)c.Mp / c.MaxMp),
                LowMp = casters.Count(c => c.Mp * 5 <= c.MaxMp),
                Restores = battle.Result.Events.OfType<MpRestored>().Count(e => ids.Contains(e.TargetId)),
                Retry = retry,
            });
            Deaths += battle.Died.Count;
            Earned += battle.Gold;
        }
    }
}
