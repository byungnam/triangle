using System.Text.Json;
using Triangle.Core.Actions;
using Triangle.Core.Balance;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Runs;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;

namespace Triangle.Core.Tests.Runs;

/// <summary>
/// 한 판의 규칙 (로그라이트 재편, 2026-10-19): 갈림길 지도, 파티 시작, 전투와 보상, 봉인과 죽음, 상점, 휴식, 이벤트, 전술과 장비.
/// 수치는 임시라 보지 않고 규칙만 지킨다. 전투 결과가 필요한 테스트는 시드를 돌려 원하는 결과가 나온 판을 쓴다.
/// </summary>
public class RunTests
{
    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "data");

    private static readonly GameData Shipped = GameDataLoader.LoadDirectory(DataDirectory);

    /// <summary>
    /// 보통 전투도 카드를 주는 게임 데이터. 게임은 보통 전투에 골드만 주지만, 보상 규칙을 보는 테스트는 첫 전투 뒤의 보상을 쓴다.
    /// </summary>
    private static readonly GameData Drafting = With(r => r);

    private static readonly string[] Party = ["knight", "archer", "elementalist", "priest"];

    /// <summary>판의 규칙만 바꾼 게임 데이터 (로더 검증을 거친다). 보통 전투도 카드를 준다(<see cref="Drafting"/>).</summary>
    private static GameData With(Func<RunDefinition, RunDefinition> change)
    {
        string Read(string file) => File.ReadAllText(Path.Combine(DataDirectory, file));
        var drafting = Shipped.Run! with { Reward = Shipped.Run.Reward with { FightCards = Shipped.Run.Reward.Cards } };
        return GameDataLoader.Parse(
            Read(GameDataLoader.MasteriesFile), Read(GameDataLoader.SkillsFile), Read(GameDataLoader.ActionsFile), Read(GameDataLoader.EncountersFile),
            Read(GameDataLoader.EffectsFile), Read(GameDataLoader.ItemsFile), Read(GameDataLoader.ZonesFile), Read(GameDataLoader.ClassesFile), JsonSerializer.Serialize(change(drafting), GameDataJson.Options),
            Read(GameDataLoader.UnlocksFile));
    }

    /// <summary>약한 보통 전투 (작은 파티로도 첫 전투를 이기게).</summary>
    private static readonly RunStrength Weak = new() { Hp = 30, Power = 30 };

    /// <summary>첫 막의 보통 전투를 약하게 한 규칙.</summary>
    private static RunDefinition Easy(RunDefinition r) => r with { Acts = [r.Acts[0] with { FightStrength = Weak }, .. r.Acts.Skip(1)] };

    /// <summary>첫 층 다음 층이 모두 <paramref name="kind"/>인 막 하나 (층 3개: 전투, kind, 휴식). 보통 전투는 약하다.</summary>
    private static GameData SecondFloor(string kind, Func<RunDefinition, RunDefinition>? also = null) => With(r =>
    {
        var map = new RunMapRules { SpecialFrom = 1, Fight = 0, Elite = 0, Shop = 0, Rest = 0, Event = 0 };
        map = kind switch
        {
            "shop" => map with { Shop = 1 },
            "event" => map with { Event = 1 },
            _ => map with { Fight = 1 },
        };
        var run = r with { Acts = [r.Acts[0] with { Floors = 3, FightStrength = Weak }], Map = map };
        return also?.Invoke(run) ?? run;
    });

    /// <summary>첫 전투를 이긴 판 (보상 단계). 이기는 시드를 찾는다.</summary>
    private static Run WonFirstFight(GameData data, IReadOnlyList<string> party, int from = 0, RunUnlocks? unlocks = null)
    {
        for (var seed = from; seed < from + 50; seed++)
        {
            var run = Run.Start(data, party, seed, unlocks);
            run.Enter(data, run.Choices[0].Id);
            if (run.Phase == RunPhase.Reward)
            {
                return run;
            }
        }

        throw new InvalidOperationException("첫 전투를 이기는 시드가 없다");
    }

    /// <summary>첫 전투를 이기고 보상을 건너뛴 뒤 다음 지점에 들어간 판.</summary>
    private static Run AtSecondFloor(GameData data, IReadOnlyList<string>? party = null, int from = 0, RunUnlocks? unlocks = null)
    {
        var run = WonFirstFight(data, party ?? Party, from, unlocks);
        run.Skip();
        run.Enter(data, run.Choices[0].Id);
        return run;
    }

    private static Tactic Line(string action, Condition condition = Condition.Always, int value = 0, int priority = 1) =>
        Tactic.Create(priority, [new(condition, value)], action);

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Map_starts_must_fit_the_width_and_paths(int starts)
    {
        var errors = Assert.Throws<GameDataException>(() => With(r => r with { Map = r.Map with { Starts = starts } })).Errors;
        Assert.Contains(errors, e => e.Contains("map starts must be 1-6"));
    }

    [Fact]
    public void Maps_are_deterministic_connected_and_keep_the_floor_rules()
    {
        // 게임의 지도: 한 막 15층, 폭 6칸, 경로 6개, 첫 층 3갈래.
        var rules = Drafting.Run!.Map;
        var floors = Drafting.Run.Acts[0].Floors;
        Assert.Equal((15, 6, 6, 3, 5), (floors, rules.Width, rules.Paths, rules.Starts, rules.SpecialFrom));
        var kinds = new HashSet<RunNodeKind>();
        for (var seed = 0; seed < 200; seed++)
        {
            var map = RunMap.Generate(rules, floors, hasElites: true, seed);
            Assert.Equal(Describe(map), Describe(RunMap.Generate(rules, floors, hasElites: true, seed)));
            Assert.Equal((RunNodeKind.Boss, map.Nodes.Count - 1, floors), (map.Boss.Kind, map.Boss.Id, map.Boss.Floor));
            Assert.Equal(rules.Starts, map.Starts.Count());

            foreach (var node in map.Nodes.SkipLast(1))
            {
                Assert.Equal(node, map.Node(node.Id));
                Assert.NotEmpty(node.Next);
                Assert.All(node.Next.Select(map.Node), next => Assert.Equal(node.Floor + 1, next.Floor));
                if (node.Floor == floors - 1)
                {
                    Assert.Equal(RunNodeKind.Rest, node.Kind);
                    Assert.Equal([map.Boss.Id], node.Next);
                    continue;
                }

                Assert.All(node.Next.Select(map.Node), next => Assert.InRange(next.Lane - node.Lane, -1, 1));
                if (node.Floor == 0)
                {
                    Assert.Equal(RunNodeKind.Fight, node.Kind);
                }

                if (node.Floor < rules.SpecialFrom || node.Floor == floors - 2)
                {
                    Assert.NotEqual(RunNodeKind.Rest, node.Kind);
                }

                if (node.Floor < rules.SpecialFrom)
                {
                    Assert.NotEqual(RunNodeKind.Elite, node.Kind);
                }

                kinds.Add(node.Kind);
            }

            // 정예·상점·휴식은 한 길에서 연달아 나오지 않는다.
            Assert.DoesNotContain(map.Nodes, n => n.Kind is RunNodeKind.Elite or RunNodeKind.Shop or RunNodeKind.Rest
                && n.Next.Select(map.Node).Any(next => next.Kind == n.Kind));

            // 길은 엇갈리지 않는다.
            var edges = map.Nodes.SkipLast(2).SelectMany(n => n.Next.Where(id => id != map.Boss.Id).Select(id => (n.Floor, From: n.Lane, To: map.Node(id).Lane))).ToList();
            Assert.DoesNotContain(edges, a => edges.Any(b => a.Floor == b.Floor && Math.Sign(a.From - b.From) * Math.Sign(a.To - b.To) < 0));

            // 모든 지점은 첫 층에서 갈 수 있다.
            var reached = map.Starts.Select(n => n.Id).ToHashSet();
            foreach (var node in map.Nodes.OrderBy(n => n.Floor).Where(n => reached.Contains(n.Id)))
            {
                reached.UnionWith(node.Next);
            }

            Assert.Equal(map.Nodes.Count, reached.Count);
        }

        Assert.Equal([RunNodeKind.Fight, RunNodeKind.Elite, RunNodeKind.Shop, RunNodeKind.Rest, RunNodeKind.Event], kinds.Order());
        Assert.DoesNotContain(Enumerable.Range(0, 50).SelectMany(s => RunMap.Generate(rules, floors, hasElites: false, s).Nodes), n => n.Kind == RunNodeKind.Elite);
    }

    [Fact]
    public void Starting_gives_each_member_the_class_kit_and_one_line_and_reveals_the_bosses()
    {
        var run = Run.Start(Drafting, ["knight", "knight", "priest"], 1);

        Assert.Equal(["m1", "m2", "m3"], run.Members.Select(m => m.Id));
        Assert.Equal(["기사 1", "기사 2", "사제"], run.Members.Select(m => run.Name(Drafting, m)));
        Assert.Equal(new Dictionary<string, int> { ["shield_wall"] = 2, ["heal"] = 1 }, run.Copies);
        Assert.All(run.Members, m =>
        {
            var c = Drafting.Classes[m.ClassId];
            Assert.Equal((c.Weapon, c.Armor), (m.Weapon, m.Armor));
            Assert.Equal(Drafting.Run!.StartLines, m.Lines);
            Assert.Equal([c.StartingSkill], m.Tactics.Select(t => t.ActionId));
        });

        Assert.Equal(Drafting.Run!.Acts.Select(a => Drafting.Zones[a.ZoneId].Boss!.EncounterId), run.Bosses);
        Assert.Equal(Drafting.Run.Acts.Count, run.Maps.Count);
        Assert.Equal((RunPhase.Map, 0, (int?)null, Drafting.Run.StartGold), (run.Phase, run.Act, run.Position, run.Gold));
        Assert.Equal(run.Map.Starts, run.Choices);
        Assert.Equal(run.Maps.Select(Describe), Run.Start(Drafting, ["priest"], 1).Maps.Select(Describe));

        Assert.NotNull(Run.WhyCannotStart(Drafting, []));
        Assert.NotNull(Run.WhyCannotStart(Drafting, ["knight", "knight", "knight", "knight", "knight"]));
        Assert.Contains("bard", Run.WhyCannotStart(Drafting, ["bard"]));
        Assert.Throws<InvalidOperationException>(() => Run.Start(Drafting, ["bard"], 1));
    }

    [Fact]
    public void Act_bosses_are_drawn_from_the_candidates_at_the_start()
    {
        var data = With(r => r with { Acts = [r.Acts[0] with { Bosses = ["wild_dogs", "poachers"] }] });
        var bosses = Enumerable.Range(0, 30).Select(seed => Run.Start(data, ["knight"], seed).Bosses.Single()).ToHashSet();

        Assert.Equal(["poachers", "wild_dogs"], bosses.Order());
    }

    [Fact]
    public void Rows_are_chosen_per_member_and_not_fixed_by_the_class()
    {
        // 처음 값은 클래스가 정하고, 시작할 때 고르거나 판 중에 바꾼다.
        var run = Run.Start(Drafting, ["knight", "priest", "knight"], 1, rows: [Units.Row.Back, Units.Row.Front]);
        Assert.Equal([Units.Row.Back, Units.Row.Front, Units.Row.Front], run.Members.Select(m => m.Row));
        Assert.Equal(Units.Row.Back, run.Setup(Drafting, run.Members[0]).Row);

        run.SetRow(run.Members[0], Units.Row.Front);
        Assert.Equal(Units.Row.Front, run.Setup(Drafting, run.Members[0]).Row);
        run.SetRow(run.Members[1], Units.Row.Back);

        // 세이브에 남고, 줄이 없는 세이브는 클래스의 처음 값이 된다.
        Assert.Equal(run.Members.Select(m => m.Row), Run.Deserialize(run.Serialize(), Drafting).Members.Select(m => m.Row));
        var old = run.ToSave() with { Members = run.ToSave().Members.Select(m => m with { Row = null }).ToList() };
        Assert.Equal([Units.Row.Front, Units.Row.Back, Units.Row.Front], Run.Restore(old, Drafting).Members.Select(m => m.Row));

        run.Members[2].Dead = true;
        Assert.NotNull(run.WhyCannotSetRow(run.Members[2]));
        run.Abandon();
        Assert.Equal("판이 끝났다", run.WhyCannotSetRow(run.Members[0]));
    }

    [Fact]
    public void Battle_setup_uses_only_the_lines_under_the_cap_and_ends_with_the_basic_attack()
    {
        var run = Run.Start(Drafting, ["elementalist"], 1);
        var member = run.Members[0];
        run.Copies["ember"] = 1;
        run.SetTactics(Drafting, member, [Line("fireball"), Line("ember"), Line("focus")]);

        var setup = run.Setup(Drafting, member);
        Assert.Equal(["fireball", "ember", "basic_attack"], setup.Tactics.Select(t => t.ActionId));
        Assert.Equal([1, 2, 3], setup.Tactics.Select(t => t.Priority));
        Assert.Equal(["ember", "fireball"], setup.GrantedActions!.Order());
        Assert.Equal(Drafting.Classes["elementalist"].Row, setup.Row);

        // 봉인되면 맨 아래 줄부터 꺼진다. 줄은 남아 있어 봉인이 풀리면 다시 켜진다.
        member.Sealed = 1;
        Assert.Equal(["fireball", "basic_attack"], run.Setup(Drafting, member).Tactics.Select(t => t.ActionId));
        Assert.Equal(["fireball"], run.Setup(Drafting, member).GrantedActions!);
        Assert.Equal(3, member.Tactics.Count);
    }

    [Fact]
    public void Winning_a_fight_pays_gold_and_offers_cards_and_skipping_returns_to_the_map()
    {
        var run = WonFirstFight(Drafting, Party);
        var reward = Drafting.Run!.Reward;

        Assert.Equal(1, run.Fights);
        Assert.InRange(run.Gold, reward.GoldMin, reward.GoldMax);
        Assert.Equal(reward.Cards, run.Offer.Count);
        Assert.Equal(run.Offer.Count, run.Offer.Distinct().Count());
        Assert.All(run.Offer.Where(c => c.Kind is RunCardKind.Active or RunCardKind.Passive), c => Assert.Contains(c.ClassId, Party));
        Assert.Empty(run.Choices);
        Assert.Throws<InvalidOperationException>(() => run.Enter(Drafting, run.Map.Starts.First().Id));

        var at = run.Position!.Value;
        Assert.Equal([at], run.Path);
        run.Skip();
        Assert.Equal((RunPhase.Map, 0), (run.Phase, run.Offer.Count));
        Assert.Equal(run.Map.Node(at).Next, run.Choices.Select(n => n.Id));
        Assert.NotNull(run.WhyCannotTake(Drafting, 0, null));
        Assert.Throws<InvalidOperationException>(run.Skip);
    }

    [Fact]
    public void Cards_go_to_the_party_or_to_a_member_of_the_right_class()
    {
        static GameData Only(int active, int passive, int line) =>
            With(r => Easy(r) with { Reward = r.Reward with { Active = active, Passive = passive, Line = line, Upgrade = 0 } });

        // 액티브는 파티의 사본이 된다 (멤버를 묻지 않는다).
        var actives = Only(1, 0, 0);
        var run = WonFirstFight(actives, ["knight", "priest"]);
        var active = run.Offer[0];
        Assert.All(run.Offer, c => Assert.Equal(RunCardKind.Active, c.Kind));
        Assert.Contains(active.Id, actives.Classes[active.ClassId!].Skills);
        var before = run.Copies.GetValueOrDefault(active.Id!);
        run.Take(actives, 0);
        Assert.Equal(before + 1, run.Copies[active.Id!]);

        // 패시브는 그 클래스 멤버만 받는다.
        var passives = Only(0, 1, 0);
        run = WonFirstFight(passives, ["knight", "priest"]);
        var passive = run.Offer[0];
        Assert.Equal(RunCardKind.Passive, passive.Kind);
        var owner = run.Members.First(m => m.ClassId == passive.ClassId);
        var other = run.Members.First(m => m.ClassId != passive.ClassId);
        Assert.NotNull(run.WhyCannotTake(passives, 0, null));
        Assert.NotNull(run.WhyCannotTake(passives, 0, other));
        owner.Passives[passive.Id!] = SkillDefinition.MaxLevel;
        Assert.NotNull(run.WhyCannotTake(passives, 0, owner));
        owner.Passives[passive.Id!] = 2;
        run.Take(passives, 0, owner);
        Assert.Equal(3, owner.Level(passive.Id!));
        Assert.Equal(new Dictionary<string, int> { [passive.Id!] = 3 }, run.Setup(passives, owner).Skills);

        // 줄 상한 +1은 누구나 받지만 최대치까지다. 같은 카드는 한 번만 나온다.
        var lines = Only(0, 0, 1);
        run = WonFirstFight(lines, ["knight", "priest"]);
        Assert.Equal([new RunCard(RunCardKind.Line, null, null)], run.Offer);
        var member = run.Members[1];
        member.Earned = lines.Run!.MaxLines;
        Assert.NotNull(run.WhyCannotTake(lines, 0, member));
        member.Earned = 2;
        run.Take(lines, 0, member);
        Assert.Equal(3, member.Lines);
        Assert.Equal(RunPhase.Map, run.Phase);
    }

    [Fact]
    public void Going_down_in_a_won_fight_seals_a_line_and_with_no_line_left_it_kills()
    {
        // 재의 왕도와 버려진 요새의 첫 전투는 T1 장비로 이겨도 가끔 누군가 쓰러진다.
        var capital = With(r => r with { Acts = [r.Acts[2]] });
        var fort = With(r => r with { Acts = [r.Acts[1]] });

        var sealedRun = Battles(capital, run => { }).First(x => x.Battle.Sealed.Count > 0);
        Assert.Empty(sealedRun.Battle.Died);
        Assert.All(sealedRun.Battle.Sealed.Select(id => sealedRun.Run.Members.Single(m => m.Id == id)), m =>
        {
            Assert.Equal((1, 1, false), (m.Sealed, m.Lines, m.Dead));
            Assert.Single(m.ActiveTactics);
        });
        Assert.Equal(RunPhase.Reward, sealedRun.Run.Phase);

        // 줄 상한이 0이면 쓰러진 멤버는 죽는다. 놓았던 사본은 파티에 남고 장비는 가방으로 간다.
        var deadRun = Battles(fort, run => run.Members.ForEach(m => m.Earned = 0)).First(x => x.Battle.Died.Count > 0);
        var (run, battle) = deadRun;
        Assert.Empty(battle.Sealed);
        var dead = battle.Died.Select(id => run.Members.Single(m => m.Id == id)).ToList();
        Assert.All(dead, m =>
        {
            Assert.True(m.Dead);
            Assert.Empty(m.Tactics);
            Assert.Equal(((string?)null, (string?)null), (m.Weapon, m.Armor));
            Assert.Contains(Drafting.Classes[m.ClassId].Weapon, run.Bag);
            Assert.DoesNotContain(m, run.Living);
            Assert.Equal(1, run.Copies[Drafting.Classes[m.ClassId].StartingSkill]);
            Assert.NotNull(run.WhyCannotSetTactics(fort, m, []));
            Assert.NotNull(run.WhyCannotEquip(fort, m, 0));
        });
        Assert.Equal(dead.Count * 2, run.Bag.Count);
        Assert.Equal(RunPhase.Reward, run.Phase);
    }

    /// <summary>시드마다 판을 시작해 <paramref name="prepare"/>한 뒤 첫 전투를 치른다. 이긴 전투만 돌려준다.</summary>
    private static IEnumerable<(Run Run, RunBattle Battle)> Battles(GameData data, Action<Run> prepare)
    {
        for (var seed = 0; seed < 300; seed++)
        {
            var run = Run.Start(data, Party, seed);
            prepare(run);
            var battle = run.Enter(data, run.Choices[0].Id)!;
            if (battle.Result.Outcome == CombatOutcome.Victory)
            {
                yield return (run, battle);
            }
        }
    }

    [Fact]
    public void Losing_ends_the_run()
    {
        var data = With(r => r with { Acts = [r.Acts[2] with { ShopTier = 1 }] });
        var run = Run.Start(data, ["priest"], 1);
        run.Enter(data, run.Choices[0].Id);

        Assert.Equal(RunPhase.Defeat, run.Phase);
        Assert.True(run.Finished);
        Assert.Equal(0, run.Gold);
        Assert.Empty(run.Choices);
        Assert.Empty(run.Offer);
        run.Unequip(run.Members[0], EquipmentSlot.Body);
        Assert.Equal("판이 끝났다", run.WhyCannotEquip(data, run.Members[0], 0));
    }

    [Fact]
    public void Beating_an_act_boss_moves_to_the_next_act_and_beating_the_last_one_wins()
    {
        // 층 둘(전투, 휴식) 뒤 들개 보스인 막 둘.
        var data = With(r => r with { Acts = [new RunAct("outskirts_forest", 2) { Bosses = ["wild_dogs"] }, new RunAct("outskirts_forest", 2) { Bosses = ["wild_dogs"] }] });
        for (var seed = 0; seed < 30; seed++)
        {
            var run = Run.Start(data, Party, seed);
            var battles = new List<RunBattle>();
            var acts = new List<int>();
            while (!run.Finished)
            {
                switch (run.Phase)
                {
                    case RunPhase.Map:
                        if (run.Enter(data, run.Choices[0].Id) is { } battle)
                        {
                            battles.Add(battle);
                            acts.Add(run.Act);
                        }

                        break;
                    case RunPhase.Rest:
                        run.RestUnseal(data);
                        break;
                    default:
                        run.Skip();
                        if (run.Position is null)
                        {
                            Assert.Equal(run.Maps[run.Act].Starts, run.Choices);
                            Assert.Empty(run.Path);
                        }

                        break;
                }
            }

            if (run.Phase == RunPhase.Defeat)
            {
                continue;
            }

            Assert.Equal([RunNodeKind.Fight, RunNodeKind.Boss, RunNodeKind.Fight, RunNodeKind.Boss], battles.Select(b => b.Kind));
            Assert.Equal([0, 0, 1, 1], acts);
            Assert.Equal(1, run.Act);
            Assert.All(battles.Where(b => b.Kind == RunNodeKind.Boss), b => Assert.Equal(("wild_dogs", data.Run!.Reward.BossGold), (b.EncounterId, b.Gold)));
            Assert.Equal(battles.Sum(b => b.Gold), run.Gold);
            Assert.Empty(run.Offer);
            return;
        }

        Assert.Fail("두 막을 모두 이기는 시드가 없다");
    }

    [Fact]
    public void Normal_fights_give_only_gold_and_elites_give_cards()
    {
        // 게임 규칙: 보통 전투는 골드만 주고 바로 지도로 돌아간다.
        var reward = Shipped.Run!.Reward;
        Assert.Equal(0, reward.FightCards);
        var won = Enumerable.Range(0, 50).Select(seed => Run.Start(Shipped, Party, seed))
            .First(run => run.Enter(Shipped, run.Choices[0].Id)!.Result.Outcome == CombatOutcome.Victory);
        Assert.Equal((RunPhase.Map, 0), (won.Phase, won.Offer.Count));
        Assert.InRange(won.Gold, reward.GoldMin, reward.GoldMax);
        Assert.NotEmpty(won.Choices);

        // 정예는 후보 카드를 더 많이 준다.
        var data = With(r => r with
        {
            Reward = r.Reward with { FightCards = 0 },
            Acts = [r.Acts[0] with { Floors = 3, FightStrength = Weak, EliteStrength = Weak }],
            Map = new RunMapRules { SpecialFrom = 1, Fight = 0, Elite = 1, Shop = 0, Rest = 0, Event = 0 },
        });
        foreach (var seed in Enumerable.Range(0, 50))
        {
            var run = Run.Start(data, Party, seed);
            run.Enter(data, run.Choices[0].Id);
            if (run.Phase == RunPhase.Map && run.Enter(data, run.Choices[0].Id) is { Result.Outcome: CombatOutcome.Victory } elite)
            {
                Assert.Equal(RunNodeKind.Elite, elite.Kind);
                Assert.Equal((RunPhase.Reward, reward.Cards + reward.EliteExtraCards), (run.Phase, run.Offer.Count));
                return;
            }
        }

        Assert.Fail("첫 전투와 정예를 이기는 시드가 없다");
    }

    [Fact]
    public void Act_strength_makes_enemies_tougher_by_node_kind()
    {
        var data = With(r => r with
        {
            Acts = [r.Acts[0] with { FightStrength = new RunStrength { Hp = 200, Power = 150 }, EliteStrength = new RunStrength() }, .. r.Acts.Skip(1)],
        });
        var id = data.Zones[data.Run!.Acts[0].ZoneId].Encounters[0].EncounterId;
        var plain = data.CreateEncounterTeam(id);
        Assert.Equal(plain.Select(e => e.ItemBonuses.Count), Run.EnemyTeam(data, 0, RunNodeKind.Elite, id).Select(e => e.ItemBonuses.Count));
        var tough = Run.EnemyTeam(data, 0, RunNodeKind.Fight, id);
        Assert.All(tough, e => Assert.Contains(new ItemBonus(BonusKind.MaxHpPercent, 100), e.ItemBonuses));
        Assert.All(tough, e => Assert.Contains(new ItemBonus(BonusKind.PowerPercent, 50), e.ItemBonuses));

        // 같은 전투를 세게 만든 적과 싸우면 적의 최대 HP가 두 배다.
        var start = Run.Start(data, Party, 1);
        var allies = start.Living.Select(m => start.Setup(data, m)).ToList();
        int MaxHp(IReadOnlyList<CombatantSetup> team) =>
            CombatSimulator.Run(allies, team, Run.Catalog(data), 1).Combatants.Where(c => c.Side == CombatSide.Enemy).Sum(c => c.MaxHp);
        Assert.Equal(2 * MaxHp(plain), MaxHp(tough));

        var errors = Assert.Throws<GameDataException>(() => With(r => r with { Acts = [r.Acts[0] with { BossStrength = new RunStrength { Hp = 5 } }] })).Errors;
        Assert.Contains(errors, e => e.Contains("strength hp and power are percents"));
        errors = Assert.Throws<GameDataException>(() => With(r => r with { Reward = r.Reward with { FightCards = -1 } })).Errors;
        Assert.Contains(errors, e => e.Contains("reward needs"));
    }

    [Fact]
    public void The_shop_sells_items_of_the_act_tier_class_cards_and_a_recruit()
    {
        // 게임의 상점은 합류를 팔지 않지만(이벤트에서만, 2026-10-20) 규칙은 남아 있다. 강화와 줄 상한은 아래에서 따로 본다.
        var data = SecondFloor("shop", r => r with { StartGold = 1000, Shop = r.Shop with { Recruits = 1, Upgrades = 0, Lines = 0 } });
        var run = AtSecondFloor(data, ["knight", "priest"]);
        Assert.Equal(RunPhase.Shop, run.Phase);
        var shop = data.Run!.Shop;
        Assert.Equal([(RunStockKind.Item, shop.Items), (RunStockKind.Skill, shop.Skills), (RunStockKind.Recruit, shop.Recruits)],
            run.Stock.GroupBy(s => s.Kind).Select(g => (g.Key, g.Count())));
        var classes = run.Members.Select(m => data.Classes[m.ClassId]).ToList();
        Assert.All(run.Stock.Where(s => s.Kind == RunStockKind.Item).Select(s => data.Items[s.Id]), item =>
        {
            Assert.Contains(item.Slot, (EquipmentSlot[])[EquipmentSlot.MainHand, EquipmentSlot.Body, EquipmentSlot.Trinket]);
            Assert.True(item.Shop, $"{item.Id} is not sold in a run");
            Assert.Equal(1, item.Tier);
            Assert.True(item.Slot != EquipmentSlot.MainHand || classes.Any(c => c.CanWield(item)));
        });
        Assert.All(run.Stock.Where(s => s.Kind == RunStockKind.Skill), s => Assert.Contains(classes, c => c.Skills.Contains(s.Id)));

        var gold = run.Gold;
        for (var i = 0; i < run.Stock.Count; i++)
        {
            run.Buy(data, i);
        }

        Assert.Equal(gold - run.Stock.Sum(s => s.Price), run.Gold);
        Assert.All(run.Stock, s => Assert.True(s.Sold));
        Assert.Equal("이미 팔렸다", run.WhyCannotBuy(data, 0));
        Assert.Equal(run.Stock.Where(s => s.Kind == RunStockKind.Item).Select(s => s.Id), run.Bag);
        Assert.All(run.Stock.Where(s => s.Kind == RunStockKind.Skill), s => Assert.True(run.Copies[s.Id] >= 1));
        var recruit = run.Stock.Single(s => s.Kind == RunStockKind.Recruit).Id;
        Assert.Equal(("m3", recruit, 2), (run.Members[^1].Id, run.Members[^1].ClassId, run.Members[^1].Lines));

        // 봉인은 하나씩 골드로 푼다.
        var member = run.Members[0];
        Assert.NotNull(run.WhyCannotUnseal(data, member));
        member.Sealed = 2;
        gold = run.Gold;
        run.Unseal(data, member);
        Assert.Equal((1, gold - shop.UnsealPrice), (member.Sealed, run.Gold));

        run.LeaveShop();
        Assert.Equal(RunPhase.Map, run.Phase);
        Assert.Empty(run.Stock);
        Assert.NotNull(run.WhyCannotUnseal(data, member));
    }

    [Fact]
    public void A_run_is_five_long_acts_and_recruits_come_only_from_a_rare_event()
    {
        // 2026-10-20 사용자 결정: 막 5개, 동료 합류는 이벤트에서만 드물게. 막마다 20층이었다가 2026-10-23 A안으로 15층.
        var rules = Shipped.Run!;
        Assert.Equal(5, rules.Acts.Count);
        Assert.All(rules.Acts, a => Assert.Equal(15, a.Floors));
        Assert.Equal(0, rules.Shop.Recruits);
        var joins = rules.Events.Where(e => e.Options.Any(o => o.Join is not null)).ToList();
        Assert.NotEmpty(joins);
        Assert.All(joins, e => Assert.True(e.Weight < rules.Events.Except(joins).Min(x => x.Weight), $"{e.Id} should be rarer than other events"));
    }

    [Fact]
    public void Events_are_drawn_by_weight()
    {
        RunEvent Event(string id, int weight) => new() { Id = id, Name = id, Weight = weight, Options = [new RunEventOption { Text = "지나간다" }] };
        var data = SecondFloor("event", r => r with { Events = [Event("common", 99), Event("rare", 1)] });
        var seen = Enumerable.Range(0, 40).Select(seed => AtSecondFloor(data, from: seed * 3).Event!.Id).ToList();
        Assert.True(seen.Count(id => id == "rare") <= 3, string.Join(",", seen));

        var errors = Assert.Throws<GameDataException>(() => With(r => r with { Events = [Event("never", 0)] })).Errors;
        Assert.Contains(errors, e => e.Contains("event 'never': weight must be at least 1"));
    }

    [Fact]
    public void The_shop_needs_gold_and_room_in_the_party()
    {
        var data = SecondFloor("shop", r => r with { Shop = r.Shop with { Recruits = 1, RecruitPrice = 0 } });
        var run = AtSecondFloor(data, ["knight", "knight", "knight", "knight"]);
        var item = run.Stock.ToList().FindIndex(s => s.Kind == RunStockKind.Item);
        var recruit = run.Stock.ToList().FindIndex(s => s.Kind == RunStockKind.Recruit);

        Assert.StartsWith("골드가 모자란다", run.WhyCannotBuy(data, item));
        run.Buy(data, recruit);
        Assert.Equal(5, run.Living.Count());

        run = AtSecondFloor(data, ["knight", "knight", "knight", "knight"]);
        run.Members.Add(new RunMember("m5", "knight", 2));
        Assert.Equal("파티는 5명까지다", run.WhyCannotBuy(data, recruit));
    }

    [Fact]
    public void Upgrades_raise_numbers_or_change_how_a_skill_works()
    {
        var bolt = new ActionDefinition { Id = "bolt", Name = "볼트", Power = 100, MpCost = 20, Applies = [new("slowed", 2)] };
        ActionDefinition Apply(params RunUpgradeKind[] kinds) => RunUpgrades.Apply(bolt, kinds);
        Assert.Equal(130, Apply(RunUpgradeKind.Power).Power);
        Assert.Equal(800, Apply(RunUpgradeKind.Swift).Delay);
        Assert.Equal((1, 70), (Apply(RunUpgradeKind.Chain).ExtraTargets, Apply(RunUpgradeKind.Chain).Power));
        Assert.Equal(3, Apply(RunUpgradeKind.Lasting).Applies[0].Duration);

        // 강화가 둘이면 둘 다 걸리고, ID는 산 순서와 상관없이 같다. 이름에는 강화 수만큼 +가 붙는다.
        var both = Apply(RunUpgradeKind.Swift, RunUpgradeKind.Power);
        Assert.Equal(("bolt+power+swift", "볼트++", 130, 800), (both.Id, both.Name, both.Power, both.Delay));
        Assert.Equal("bolt", both.Family);
        Assert.Equal(2, bolt.Applies[0].Duration);
        var resolved = RunUpgrades.Resolve(Shipped, "fireball+power");
        Assert.Equal(("화염구+", Shipped.Actions["fireball"].Power * 13 / 10), (resolved?.Name, resolved?.Power));
        Assert.Null(RunUpgrades.Resolve(Shipped, "fireball"));
        Assert.Null(RunUpgrades.Resolve(Shipped, "fireball+nothing"));
        Assert.Null(RunUpgrades.Resolve(Shipped, "fireball+swift+power"));
        Assert.Equal("fireball", RunUpgrades.BaseId("fireball+power+swift"));
        Assert.Equal([RunUpgradeKind.Power, RunUpgradeKind.Swift], RunUpgrades.Kinds("fireball+power+swift"));
        Assert.Equal("fireball+power+swift", RunUpgrades.With("fireball+swift", RunUpgradeKind.Power));

        // 뜻이 없는 강화는 붙지 않는다: 위력이 없는 행동에 위력, 적 하나를 치지 않는 행동에 연쇄, 효과가 없는 행동에 지속.
        var wall = Shipped.Actions["shield_wall"];
        var arrow = Shipped.Actions["fire_arrow"];
        Assert.False(RunUpgrades.Fits(wall, RunUpgradeKind.Power));
        Assert.False(RunUpgrades.Fits(wall, RunUpgradeKind.Chain));
        Assert.True(RunUpgrades.Fits(wall, RunUpgradeKind.Lasting));
        Assert.False(RunUpgrades.Fits(arrow, RunUpgradeKind.Lasting));
        Assert.False(RunUpgrades.Fits(Shipped.Actions["heal"], RunUpgradeKind.Chain));
    }

    [Fact]
    public void The_shop_upgrades_a_chosen_copy_and_sells_pricey_line_caps()
    {
        var data = SecondFloor("shop", r => r with { StartGold = 1000, Shop = r.Shop with { Upgrades = 2, UpgradePrice = 80, UpgradeStep = 40, Lines = 1, LinePrice = 150, LineStep = 50 } });
        var run = AtSecondFloor(data, ["knight", "elementalist"]);

        // 강화는 파티의 사본과 종류를 골라 산다: 상점 한 번에 정해진 수만큼, 그 스킬에 맞는 강화만.
        Assert.Equal(["fireball", "shield_wall"], run.UpgradableCopies(data));
        Assert.Equal((2, 80), (run.UpgradesLeft, run.UpgradePrice(data, "fireball")));
        Assert.Equal("이 스킬에는 맞지 않는 강화다", run.WhyCannotUpgrade(data, "shield_wall", RunUpgradeKind.Power));
        Assert.Equal("강화할 사본이 없다", run.WhyCannotUpgrade(data, "heal", RunUpgradeKind.Power));

        // 화염구 사본이 둘이면 강화는 줄에 놓인 한 장에만 걸리고, 그 줄이 강화된 사본을 쓴다. 놀고 있는 한 장은 그대로다.
        run.Copies["fireball"] = 2;
        var upgraded = RunUpgrades.Id("fireball", [RunUpgradeKind.Power]);
        var mage = run.Members.Single(m => m.ClassId == "elementalist");
        Assert.Equal(mage, run.UpgradeHolder("fireball"));
        var gold = run.Gold;
        run.Upgrade(data, "fireball", RunUpgradeKind.Power);
        Assert.Equal((1, 1), (run.Copies["fireball"], run.Copies[upgraded]));
        Assert.Equal((gold - 80, 1), (run.Gold, run.UpgradesLeft));
        Assert.Equal(upgraded, mage.Tactics[0].ActionId);
        Assert.Equal("화염구+", RunUpgrades.Action(data, upgraded).Name);

        // 강화가 걸린 사본에 하나 더 걸면 더 비싸고, 같은 강화는 두 번 걸 수 없다. 상점의 강화 수를 다 쓰면 더 걸 수 없다.
        Assert.Equal(120, run.UpgradePrice(data, upgraded));
        Assert.Equal("이미 건 강화다", run.WhyCannotUpgrade(data, upgraded, RunUpgradeKind.Power));
        run.Upgrade(data, "fireball", RunUpgradeKind.Swift);
        Assert.Equal("이 상점에서는 더 강화할 수 없다", run.WhyCannotUpgrade(data, upgraded, RunUpgradeKind.Swift));
        Assert.Equal(1, run.Copies[RunUpgrades.Id("fireball", [RunUpgradeKind.Swift])]);
        Assert.False(run.Copies.ContainsKey("fireball"));

        // 전투에서 그 줄은 강화된 행동으로 싸우고, 같은 행동을 쓰는 적은 그대로다.
        Assert.Equal(upgraded, run.Setup(data, mage).Tactics[0].ActionId);
        Assert.Contains(upgraded, run.Setup(data, mage).GrantedActions!);
        Assert.Equal(RunUpgrades.Action(data, upgraded).Power, Run.Catalog(data).Actions[upgraded].Power);
        Assert.Equal(data.Actions["fireball"].Power, Run.Catalog(data).Actions["fireball"].Power);

        // 강화된 사본은 강화가 다른 사본과 따로 센다: 한 장뿐인 사본을 두 줄에 놓을 수는 없다.
        var swift = RunUpgrades.Id("fireball", [RunUpgradeKind.Swift]);
        Assert.Null(run.WhyCannotSetTactics(data, mage, [Line(upgraded), Line(swift, Condition.SelfHpAtMost, 50)]));
        Assert.StartsWith("사본이 모자란다", run.WhyCannotSetTactics(data, mage, [Line(upgraded), Line(upgraded, Condition.SelfHpAtMost, 50)]));
        Assert.StartsWith("기사의 카드가 아니다", run.WhyCannotSetTactics(data, run.Members[0], [Line(upgraded)]));

        // 줄 상한은 받을 멤버를 골라 산다.
        var line = run.Stock.ToList().FindIndex(s => s.Kind == RunStockKind.Line);
        Assert.Equal(150, run.Stock[line].Price);
        Assert.Equal("받을 멤버를 골라야 한다", run.WhyCannotBuy(data, line));
        run.Buy(data, line, mage);
        Assert.Equal((3, 1), (mage.Earned, run.LinesBought));

        // 세이브에 강화된 사본, 남은 강화 수, 산 줄 상한 수가 남는다.
        var copy = Run.Deserialize(run.Serialize(), data);
        Assert.Equal(run.Serialize(), copy.Serialize());
        Assert.Equal((1, 0, 1), (copy.Copies[upgraded], copy.UpgradesLeft, copy.LinesBought));

        // 다음 상점: 강화 수가 다시 차고, 줄 상한은 산 수만큼 비싸다. 강화가 다 찬 사본은 더 강화할 수 없다.
        var before = WonFirstFight(data, ["knight", "elementalist"]);
        before.Skip();
        var wall = RunUpgrades.Id("shield_wall", [RunUpgradeKind.Swift, RunUpgradeKind.Lasting]);
        var file = before.ToSave() with { LinesBought = 2, Copies = new Dictionary<string, int> { [swift] = 1, [wall] = 1 } };
        var later = Run.Restore(file, data);
        later.Enter(data, later.Choices[0].Id);
        Assert.Equal(250, later.Stock.Single(s => s.Kind == RunStockKind.Line).Price);
        Assert.Equal((2, 120), (later.UpgradesLeft, later.UpgradePrice(data, swift)));
        Assert.Equal([swift], later.UpgradableCopies(data));
        Assert.Equal("사본 하나에 강화는 2개까지다", later.WhyCannotUpgrade(data, wall, RunUpgradeKind.Power));

        // 모르는 강화나 순서가 다른 강화 ID의 사본은 세이브에서 거부한다.
        var errors = Assert.Throws<SaveGameException>(() => Run.Restore(file with { Copies = new Dictionary<string, int> { ["fireball+swift+power"] = 1, ["fireball+fly"] = 1 } }, data)).Errors;
        Assert.Contains(errors, e => e.Contains("unknown action 'fireball+swift+power' in copies"));
        Assert.Contains(errors, e => e.Contains("unknown action 'fireball+fly' in copies"));
    }

    [Fact]
    public void An_upgrade_card_upgrades_a_copy_the_player_picks()
    {
        var data = With(r => Easy(r) with { Reward = r.Reward with { Active = 0, Passive = 0, Line = 0, Upgrade = 1 } });
        var run = WonFirstFight(data, ["knight", "elementalist"]);

        // 강화 카드는 강화의 종류만 정해져 있고, 파티의 사본 중 그 강화가 맞는 사본이 하나라도 있는 종류만 나온다.
        Assert.All(run.Offer, c => Assert.Equal(RunCardKind.Upgrade, c.Kind));
        Assert.All(run.Offer, c => Assert.Contains(run.UpgradableCopies(data), id => run.WhyCannotUpgradeCopy(data, id, Enum.Parse<RunUpgradeKind>(c.Id!)) is null));
        var kind = Enum.Parse<RunUpgradeKind>(run.Offer[0].Id!);
        Assert.Equal("강화할 사본을 골라야 한다", run.WhyCannotTake(data, 0, null));
        Assert.Equal("강화할 사본이 없다", run.WhyCannotTake(data, 0, null, "heal"));

        // 고른 사본 한 장에 걸리고 골드는 들지 않는다. 그 사본을 쓰던 줄은 강화된 사본을 쓴다.
        var copy = run.UpgradableCopies(data).First(id => run.WhyCannotTake(data, 0, null, id) is null);
        var holder = run.UpgradeHolder(copy)!;
        var gold = run.Gold;
        run.Take(data, 0, copy: copy);
        var upgraded = RunUpgrades.Id(copy, [kind]);
        Assert.Equal((1, gold), (run.Copies[upgraded], run.Gold));
        Assert.Contains(holder.Tactics, t => t.ActionId == upgraded);
        Assert.Equal(RunPhase.Map, run.Phase);

        // 세이브의 강화 카드도 아는 강화인지 본다.
        var file = run.ToSave() with { Phase = RunPhase.Reward, Offer = [new RunCard(RunCardKind.Upgrade, null, "Fly")] };
        Assert.Contains("run save: unknown card Upgrade 'Fly'", Assert.Throws<SaveGameException>(() => Run.Restore(file, data)).Errors);
    }

    [Fact]
    public void A_line_cap_needs_a_member_with_room_and_shop_numbers_are_checked()
    {
        var data = SecondFloor("shop", r => r with { StartGold = 1000, Shop = r.Shop with { Lines = 1 } });
        var run = AtSecondFloor(data, ["knight", "priest"]);
        var line = run.Stock.ToList().FindIndex(s => s.Kind == RunStockKind.Line);
        var full = run.Members[0];
        full.Earned = data.Run!.MaxLines;
        Assert.Equal("줄 상한이 이미 최대다", run.WhyCannotBuy(data, line, full));
        Assert.Null(run.WhyCannotBuy(data, line, run.Members[1]));

        var errors = Assert.Throws<GameDataException>(() => With(r => r with { Shop = r.Shop with { Lines = 2, UpgradePrice = -1 } })).Errors;
        Assert.Contains(errors, e => e.Contains("shop lines must be 0 or 1"));
        Assert.Contains(errors, e => e.Contains("shop counts and prices must not be negative"));
    }

    [Fact]
    public void Resting_unseals_everyone_or_trains_a_passive()
    {
        var data = With(r => r with { Acts = [r.Acts[0] with { Floors = 2 }] });
        var run = AtSecondFloor(data);
        Assert.Equal(RunPhase.Rest, run.Phase);
        run.Members.ForEach(m => m.Sealed = 2);
        run.RestUnseal(data);
        Assert.All(run.Members, m => Assert.Equal(1, m.Sealed));
        Assert.Equal(RunPhase.Map, run.Phase);
        Assert.Equal([run.Map.Boss], run.Choices);

        run = AtSecondFloor(data);
        run.RestTrain(data);
        Assert.Equal(RunPhase.Reward, run.Phase);
        Assert.Equal(data.Run!.Rest.TrainCards, run.Offer.Count);
        Assert.All(run.Offer, c => Assert.Contains(c.Id, data.Classes[c.ClassId!].Passives));
        var card = run.Offer[0];
        var owner = run.Members.First(m => m.ClassId == card.ClassId);
        run.Take(data, 0, owner);
        Assert.Equal(1, owner.Level(card.Id!));
        Assert.Equal(RunPhase.Map, run.Phase);
    }

    [Fact]
    public void Event_options_change_gold_party_seals_bag_and_cards()
    {
        RunEvent test = new()
        {
            Id = "test",
            Name = "시험",
            Options =
            [
                new RunEventOption { Text = "비싼 것", Gold = -1000 },
                new RunEventOption { Text = "궁수 합류", Join = "archer" },
                new RunEventOption { Text = "덫", Seal = 5, Trinket = true },
                new RunEventOption { Text = "기도", Unseal = 9 },
                new RunEventOption { Text = "카드", Card = true },
            ],
        };
        var data = SecondFloor("event", r => r with { Events = [test] });
        Run AtEvent()
        {
            var run = AtSecondFloor(data, ["knight", "priest"]);
            Assert.Equal((RunPhase.Event, "test"), (run.Phase, run.Event?.Id));
            return run;
        }

        var run = AtEvent();
        Assert.StartsWith("골드가 모자란다", run.WhyCannotChoose(0));
        Assert.NotNull(run.WhyCannotChoose(5));

        run.Choose(data, 1);
        Assert.Equal(["knight", "priest", "archer"], run.Members.Select(m => m.ClassId));
        Assert.Equal((RunPhase.Map, (RunEvent?)null), (run.Phase, run.Event));

        run = AtEvent();
        run.Choose(data, 2);
        Assert.Equal([0, 2], run.Members.Select(m => m.Sealed).Order());
        var trinket = data.Items[Assert.Single(run.Bag)];
        Assert.Equal((EquipmentSlot.Trinket, 1), (trinket.Slot, trinket.Tier));

        run = AtEvent();
        run.Members.ForEach(m => m.Sealed = 2);
        run.Choose(data, 3);
        Assert.All(run.Members, m => Assert.Equal(0, m.Sealed));

        run = AtEvent();
        run.Choose(data, 4);
        Assert.Equal((RunPhase.Reward, data.Run!.Reward.Cards), (run.Phase, run.Offer.Count));

        run = AtEvent();
        run.Members.AddRange(Enumerable.Range(3, 3).Select(i => new RunMember($"m{i}", "knight", 2)));
        Assert.Equal("파티는 5명까지다", run.WhyCannotChoose(1));
    }

    [Fact]
    public void Tactic_lines_are_limited_by_party_copies_class_cards_and_the_line_maximum()
    {
        var run = Run.Start(Drafting, ["knight", "knight", "elementalist"], 1);
        var (first, second, mage) = (run.Members[0], run.Members[1], run.Members[2]);

        // 사본 2장을 두 기사가 한 줄씩 쓰고 있다.
        Assert.StartsWith("사본이 모자란다", run.WhyCannotSetTactics(Drafting, first, [Line("shield_wall"), Line("shield_wall")]));
        run.SetTactics(Drafting, second, [Line("focus")]);
        run.SetTactics(Drafting, first, [Line("shield_wall", Condition.SelfHpAtMost, 50, priority: 7), Line("shield_wall", priority: 3)]);
        Assert.Equal([1, 2], first.Tactics.Select(t => t.Priority));
        Assert.Equal((Condition.SelfHpAtMost, 50), (first.Tactics[0].Condition, first.Tactics[0].Value));

        // 공용 행동은 사본이 없어도 된다. 다른 클래스의 카드는 놓을 수 없다.
        Assert.Null(run.WhyCannotSetTactics(Drafting, second, [Line("basic_attack"), Line("focus")]));
        Assert.Contains("기사의 카드가 아니다", run.WhyCannotSetTactics(Drafting, second, [Line("fireball")]));
        Assert.Null(run.WhyCannotSetTactics(Drafting, mage, [Line("fireball")]));
        Assert.StartsWith("사본이 모자란다", run.WhyCannotSetTactics(Drafting, mage, [Line("fireball"), Line("ember")]));

        // 줄은 줄 상한의 최대치까지 적을 수 있고, 조건은 데이터 검증과 같다.
        Assert.Null(run.WhyCannotSetTactics(Drafting, second, Enumerable.Repeat(Line("focus"), Drafting.Run!.MaxLines).ToList()));
        Assert.NotNull(run.WhyCannotSetTactics(Drafting, second, Enumerable.Repeat(Line("focus"), Drafting.Run.MaxLines + 1).ToList()));
        Assert.Contains("unknown action", run.WhyCannotSetTactics(Drafting, second, [Line("nope")]));
        Assert.Contains("percent", run.WhyCannotSetTactics(Drafting, second, [Line("focus", Condition.SelfHpAtMost, 150)]));
        Assert.Throws<InvalidOperationException>(() => run.SetTactics(Drafting, second, [Line("nope")]));
    }

    [Fact]
    public void A_weapon_can_replace_the_basic_attack()
    {
        var run = Run.Start(Drafting, ["archer"], 1);
        var archer = run.Members[0];
        run.SetTactics(Drafting, archer, [Line(DraftTactics.Fallback, Condition.SelfHpAtMost, 50)]);
        Assert.Equal(DraftTactics.Fallback, Run.BasicAttack(Drafting, archer));
        run.Bag.Add("longbow");
        run.Equip(Drafting, archer, 0);
        Assert.Equal("longbow_shot", Run.BasicAttack(Drafting, archer));

        // 기본 공격을 놓은 줄과 맨 아래 줄이 모두 장궁의 공격이 되고, 전투에서 그 행동을 쓴다.
        var setup = run.Setup(Drafting, archer);
        Assert.Equal(["longbow_shot", "longbow_shot"], setup.Tactics.Select(t => t.ActionId));
        Assert.Contains("longbow_shot", setup.GrantedActions!);
        var result = CombatSimulator.Run([setup], Drafting.CreateEncounterTeam("wild_dogs"), Run.Catalog(Drafting), 1);
        Assert.Contains(result.Events.OfType<ActionUsed>(), a => a.ActorId == archer.Id && a.ActionId == "longbow_shot");
        Assert.DoesNotContain(result.Events.OfType<ActionUsed>(), a => a.ActorId == archer.Id && a.ActionId == DraftTactics.Fallback);
    }

    [Fact]
    public void Equipment_comes_from_the_bag_and_keeps_to_class_weapons_and_the_trinket_limit()
    {
        var run = Run.Start(Drafting, ["knight"], 1);
        var knight = run.Members[0];

        run.Unequip(knight, EquipmentSlot.MainHand);
        Assert.Null(knight.Weapon);
        Assert.Equal(["old_sword"], run.Bag);
        run.Equip(Drafting, knight, 0);
        Assert.Equal(("old_sword", 0), (knight.Weapon, run.Bag.Count));
        Assert.Throws<InvalidOperationException>(() => run.Unequip(knight, EquipmentSlot.Trinket));

        run.Bag.AddRange(["hunting_bow", "plate_helm", "leather_armor", "frost_ring", "mana_crystal", "banner_of_defiance"]);
        Assert.Contains("쓰지 않는다", run.WhyCannotEquip(Drafting, knight, 0));
        Assert.NotNull(run.WhyCannotEquip(Drafting, knight, 1));
        Assert.NotNull(run.WhyCannotEquip(Drafting, knight, 9));

        run.Equip(Drafting, knight, 2);
        Assert.Equal("leather_armor", knight.Armor);
        Assert.Equal("plate_armor", run.Bag[^1]);

        run.Equip(Drafting, knight, run.Bag.IndexOf("banner_of_defiance"));
        run.Equip(Drafting, knight, run.Bag.IndexOf("frost_ring"));
        Assert.Equal("장신구는 2개까지다", run.WhyCannotEquip(Drafting, knight, run.Bag.IndexOf("mana_crystal")));
        Assert.Contains("taunting", run.Setup(Drafting, knight).StartingEffects);

        run.Unequip(knight, EquipmentSlot.Trinket, 0);
        Assert.Equal(["frost_ring"], knight.Trinkets);
        Assert.Equal("banner_of_defiance", run.Bag[^1]);
    }

    [Fact]
    public void The_same_seed_and_the_same_choices_make_the_same_run()
    {
        static string Play(int seed)
        {
            var run = Run.Start(Drafting, Party, seed);
            var log = new List<string>();
            while (!run.Finished && run.Fights < 8)
            {
                switch (run.Phase)
                {
                    case RunPhase.Map:
                        var battle = run.Enter(Drafting, run.Choices[^1].Id);
                        log.Add($"{run.Position}:{battle?.EncounterId}:{battle?.Result.Outcome}:{battle?.Gold}");
                        break;
                    case RunPhase.Reward:
                        log.Add(string.Join(",", run.Offer));
                        var card = run.Offer[0];
                        var member = run.Living.FirstOrDefault(m => run.WhyCannotTake(Drafting, 0, m) is null);
                        if (card.Kind == RunCardKind.Active || member is not null)
                        {
                            run.Take(Drafting, 0, member);
                        }
                        else
                        {
                            run.Skip();
                        }

                        break;
                    case RunPhase.Shop:
                        log.Add(string.Join(",", run.Stock));
                        run.LeaveShop();
                        break;
                    case RunPhase.Rest:
                        run.RestTrain(Drafting);
                        break;
                    case RunPhase.Event:
                        log.Add(run.Event!.Id);
                        run.Choose(Drafting, run.Event.Options.Count - 1);
                        break;
                }
            }

            return $"{run.Phase} {run.Gold} {string.Join(";", log)} {string.Join(",", run.Copies.OrderBy(c => c.Key))}";
        }

        Assert.Equal(Play(3), Play(3));
        Assert.NotEqual(Play(3), Play(4));
    }

    [Fact]
    public void A_saved_run_continues_exactly_like_the_original()
    {
        // 지도, 보상, 상점, 이벤트 단계에서 저장했다가 되살려도 같은 선택이면 같은 판이 된다.
        foreach (var data in new[] { Drafting, SecondFloor("shop", r => r with { StartGold = 500 }), SecondFloor("event") })
        {
            var original = WonFirstFight(data, Party);
            for (var step = 0; step < 4 && !original.Finished; step++)
            {
                var copy = Run.Deserialize(original.Serialize(), data);
                Assert.Equal(original.Serialize(), copy.Serialize());
                Assert.Equal(Advance(data, original), Advance(data, copy));
                Assert.Equal(original.Serialize(), copy.Serialize());
            }
        }
    }

    /// <summary>판을 한 걸음 진행한다: 지점은 마지막 것, 카드는 첫 장(받을 수 있는 첫 멤버), 상점은 살 수 있는 첫 물건 뒤 나가기, 이벤트는 첫 선택지.</summary>
    private static string Advance(GameData data, Run run)
    {
        switch (run.Phase)
        {
            case RunPhase.Map:
                var battle = run.Enter(data, run.Choices[^1].Id);
                return $"{run.Phase} {battle?.EncounterId} {battle?.Result.Outcome} {battle?.Gold}";
            case RunPhase.Reward:
                var member = run.Living.FirstOrDefault(m => run.WhyCannotTake(data, 0, m) is null);
                run.Take(data, 0, run.Offer[0].Kind == RunCardKind.Active ? null : member ?? run.Living.First());
                return $"took {run.Phase}";
            case RunPhase.Shop:
                var stock = Enumerable.Range(0, run.Stock.Count).FirstOrDefault(i => run.WhyCannotBuy(data, i) is null, -1);
                if (stock >= 0)
                {
                    run.Buy(data, stock);
                }

                run.LeaveShop();
                return $"bought {stock}";
            case RunPhase.Event:
                var option = Enumerable.Range(0, run.Event!.Options.Count).First(i => run.WhyCannotChoose(i) is null);
                run.Choose(data, option);
                return $"chose {option} {run.Phase}";
            default:
                run.RestUnseal(data);
                return "rested";
        }
    }

    [Fact]
    public void Saves_that_do_not_match_the_game_data_are_rejected()
    {
        var run = Run.Start(Drafting, ["knight"], 1);
        var file = run.ToSave();

        Assert.Contains(Assert.Throws<SaveGameException>(() => Run.Restore(file with { Version = 99 }, Drafting)).Errors, e => e.Contains("version 99"));
        var errors = Assert.Throws<SaveGameException>(() => Run.Restore(file with
        {
            Members = [file.Members[0] with { ClassId = "bard", Weapon = "no_sword" }],
            Bag = ["no_item"],
            Phase = RunPhase.Event,
            Position = 999,
        }, Drafting)).Errors;
        Assert.Contains(errors, e => e.Contains("unknown class 'bard'"));
        Assert.Contains(errors, e => e.Contains("unknown item 'no_sword'"));
        Assert.Contains(errors, e => e.Contains("unknown item 'no_item' in bag"));
        Assert.Contains(errors, e => e.Contains("position 999"));
        Assert.Contains(errors, e => e.Contains("event phase needs an event"));
        Assert.Throws<SaveGameException>(() => Run.Deserialize("{ \"version\": 1 }", Drafting));
    }

    [Fact]
    public void A_run_saved_with_a_different_number_of_acts_is_set_aside()
    {
        // 막 3개일 때 저장한 판: 3막 보스 다음에 갈 지도가 없으므로 이어 하지 않는다.
        var threeActs = With(r => r with { Acts = r.Acts.Take(3).ToList() });
        var old = Run.Start(threeActs, Party, 5).Serialize();
        Assert.Equal(3, Run.Deserialize(old, threeActs).Maps.Count);
        var errors = Assert.Throws<SaveGameException>(() => Run.Deserialize(old, Drafting)).Errors;
        Assert.Contains(errors, e => e.StartsWith(Run.ActCountChanged) && e.Contains("(save 3, game 5)"));

        var dir = Path.Combine(Path.GetTempPath(), "triangle-run-" + Guid.NewGuid());
        try
        {
            var store = new RunStore(Path.Combine(dir, "run.json"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(store.Path, old);
            var loaded = store.Load(Drafting);
            Assert.Equal((null, LoadStatus.Recovered), (loaded.Run, loaded.Status));
            Assert.Contains(loaded.Errors!, e => e.StartsWith(Run.ActCountChanged));
            Assert.True(File.Exists(loaded.BrokenFilePath));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void The_run_store_keeps_one_run_and_sets_a_broken_one_aside()
    {
        var dir = Path.Combine(Path.GetTempPath(), "triangle-run-" + Guid.NewGuid());
        try
        {
            var store = new RunStore(Path.Combine(dir, "run.json"));
            Assert.Equal((null, LoadStatus.NewGame), (store.Load(Drafting).Run, store.Load(Drafting).Status));

            var run = Run.Start(Drafting, Party, 5);
            store.Save(run);
            var loaded = store.Load(Drafting);
            Assert.Equal(LoadStatus.Loaded, loaded.Status);
            Assert.Equal(run.Serialize(), loaded.Run!.Serialize());

            run.Abandon();
            Assert.Equal(RunPhase.Defeat, run.Phase);
            store.Delete();
            Assert.Equal(LoadStatus.NewGame, store.Load(Drafting).Status);

            File.WriteAllText(store.Path, "not json");
            var broken = store.Load(Drafting);
            Assert.Equal(LoadStatus.Recovered, broken.Status);
            Assert.True(File.Exists(broken.BrokenFilePath));
            Assert.False(File.Exists(store.Path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── 해금과 기록 ───────────────────────────────────────

    /// <summary>아무것도 열지 않은 처음 상태의 해금.</summary>
    private static readonly RunUnlocks Fresh = RunUnlocks.For(Drafting, new HashSet<string>());

    [Fact]
    public void Unopened_unlocks_lock_their_classes_cards_and_items_and_opened_ones_add_start_gold()
    {
        Assert.Contains("warrior", Fresh.Locked);
        Assert.Contains("gunner", Fresh.Locked);
        Assert.Contains("snipe", Fresh.Locked);
        Assert.Contains("troll_amulet", Fresh.Locked);
        Assert.DoesNotContain("knight", Fresh.Locked);
        Assert.Equal(0, Fresh.StartGold);

        var rich = RunUnlocks.For(Drafting, new HashSet<string> { "war_chest", "class_warrior" });
        Assert.Equal(50, rich.StartGold);
        Assert.DoesNotContain("warrior", rich.Locked);
        Assert.Equal(Drafting.Run!.StartGold + 50, Run.Start(Drafting, ["warrior"], 1, rich).Gold);

        Assert.Contains("전사", Run.WhyCannotStart(Drafting, ["knight", "warrior"], Fresh));
        Assert.Throws<InvalidOperationException>(() => Run.Start(Drafting, ["warrior"], 1, Fresh));
        Assert.Null(Run.WhyCannotStart(Drafting, ["warrior"]));
    }

    [Fact]
    public void Opened_unlocks_add_their_small_conveniences_to_the_run()
    {
        // A안(2026-10-23): 해금은 고를 거리와 작은 편의(시작 골드, 휴식, 상점 할인)만 준다. 숫자가 쌓이는 힘은 없다.
        var opened = new HashSet<string> { "war_chest", "war_chest_2", "field_medic", "regular" };
        var unlocks = RunUnlocks.For(Drafting, opened);
        Assert.Equal(100, unlocks.StartGold);
        var expected = new RunBoons
        {
            RestUnseal = Drafting.Unlocks["field_medic"].Boons.RestUnseal,
            ShopDiscountPercent = Drafting.Unlocks["regular"].Boons.ShopDiscountPercent,
        };
        Assert.Equal(expected, unlocks.Boons);
        Assert.True(Fresh.Boons.IsEmpty);

        var data = SecondFloor("shop", r => r with { StartGold = 1000, Shop = r.Shop with { Lines = 1 } });
        var plain = AtSecondFloor(data, Party);
        var boosted = AtSecondFloor(data, Party, unlocks: unlocks);
        Assert.Equal(expected, boosted.Boons);

        // 상점 값과 봉인 풀기 값이 싸지고, 전투 입력은 그대로다.
        Assert.Equal(plain.Stock.Select(s => s.Price * (100 - expected.ShopDiscountPercent) / 100), boosted.Stock.Select(s => s.Price));
        Assert.Equal(data.Run!.Shop.UnsealPrice * (100 - expected.ShopDiscountPercent) / 100, boosted.UnsealPrice(data));
        Assert.Equal(plain.Setup(data, plain.Members[0]).ItemBonuses, boosted.Setup(data, boosted.Members[0]).ItemBonuses);
        Assert.All(boosted.Members, m => Assert.Equal(data.Run.StartLines, m.Earned));

        // 휴식은 봉인을 더 푼다. 세이브에도 남는다.
        Assert.Equal(data.Run.Rest.Unseal + expected.RestUnseal, boosted.RestUnseals(data));
        Assert.Equal(expected, Run.Deserialize(boosted.Serialize(), data).Boons);
    }

    [Fact]
    public void Tiered_unlocks_open_in_order_and_are_checked()
    {
        var profile = Profile.Deserialize(JsonSerializer.Serialize(new ProfileFile { Version = Profile.SaveVersion, Renown = 1000 }, GameDataJson.Options), Drafting);
        Assert.Equal($"먼저 열어야 한다: {Drafting.Unlocks["war_chest"].Name}", profile.WhyCannotUnlock(Drafting, "war_chest_2"));
        profile.Unlock(Drafting, "war_chest");
        profile.Unlock(Drafting, "war_chest_2");
        Assert.Equal(100, profile.UnlocksForRun(Drafting).StartGold);

        UnlockDefinition Unlock(string id, string? requires = null, RunBoons? boons = null) => new() { Id = id, Name = id, Cost = 1, Requires = requires, Boons = boons ?? new() { RestUnseal = 1 } };
        var errors = Assert.Throws<GameDataException>(() => WithUnlocks([Unlock("a", "b"), Unlock("b", "a"), Unlock("c", "nope"), Unlock("d", boons: new()), Unlock("e", boons: new() { ShopDiscountPercent = 80 })])).Errors;
        Assert.Contains(errors, e => e.Contains("'a': requires go round in a circle"));
        Assert.Contains(errors, e => e.Contains("'c': requires unknown unlock 'nope'"));
        Assert.Contains(errors, e => e.Contains("'d': opens nothing"));
        Assert.Contains(errors, e => e.Contains("'e': boons must not be negative and the shop discount is at most 50%"));
    }

    /// <summary>해금만 바꾼 게임 데이터 (로더 검증을 거친다).</summary>
    private static GameData WithUnlocks(IReadOnlyList<UnlockDefinition> unlocks)
    {
        string Read(string file) => File.ReadAllText(Path.Combine(DataDirectory, file));
        return GameDataLoader.Parse(
            Read(GameDataLoader.MasteriesFile), Read(GameDataLoader.SkillsFile), Read(GameDataLoader.ActionsFile), Read(GameDataLoader.EncountersFile),
            Read(GameDataLoader.EffectsFile), Read(GameDataLoader.ItemsFile), Read(GameDataLoader.ZonesFile), Read(GameDataLoader.ClassesFile), Read(GameDataLoader.RunFile),
            JsonSerializer.Serialize(unlocks, GameDataJson.Options));
    }

    [Fact]
    public void Locked_cards_items_and_classes_never_show_up_in_a_run()
    {
        bool Locked(string? id) => id is not null && Fresh.Locked.Contains(id);

        // 보상: 원소술사와 궁수 카드에는 잠긴 카드가 많다.
        var actives = With(r => r with { Reward = r.Reward with { Active = 1, Passive = 0, Line = 0 }, Acts = [r.Acts[0] with { FightStrength = Weak }] });
        for (var from = 0; from < 40; from += 4)
        {
            var run = WonFirstFight(actives, ["elementalist", "archer"], from, Fresh);
            Assert.DoesNotContain(run.Offer, c => Locked(c.Id));
        }

        // 상점: 3막 상점은 잠긴 T3 장신구도 파는 티어다.
        var shop = SecondFloor("shop", r => r with { Acts = [r.Acts[0] with { Floors = 3, ShopTier = 3 }], Shop = r.Shop with { Items = 30, Skills = 40, Recruits = 6 } });
        var stock = AtSecondFloor(shop, ["elementalist", "archer"], unlocks: Fresh).Stock;
        Assert.Contains(stock, s => s.Kind == RunStockKind.Item && shop.Items[s.Id].Slot == EquipmentSlot.Trinket);
        Assert.DoesNotContain(stock, s => Locked(s.Id));
        Assert.Contains(AtSecondFloor(shop, ["elementalist", "archer"]).Stock, s => Locked(s.Id));

        // 이벤트: 아무 클래스 합류는 열린 클래스에서만, 잠긴 클래스를 데려오는 선택지는 고를 수 없다.
        RunEvent join = new()
        {
            Id = "join",
            Name = "합류",
            Options = [new RunEventOption { Text = "아무나", Join = "any" }, new RunEventOption { Text = "전사", Join = "warrior" }],
        };
        var events = SecondFloor("event", r => r with { Events = [join] });
        for (var from = 0; from < 30; from += 3)
        {
            var run = AtSecondFloor(events, ["knight", "archer", "priest"], from, Fresh);
            Assert.Equal("아직 열지 않은 클래스다", run.WhyCannotChoose(1));
            run.Choose(events, 0);
            Assert.DoesNotContain(run.Members, m => Locked(m.ClassId));
        }

        // 판을 저장해도 잠긴 것은 그대로다.
        var saved = WonFirstFight(Drafting, Party, unlocks: Fresh);
        Assert.Equal(Fresh.Locked.Order(), Run.Deserialize(saved.Serialize(), Drafting).Locked.Order());
    }

    [Fact]
    public void A_finished_run_gives_renown_and_records_and_renown_opens_unlocks()
    {
        var renown = Drafting.Run!.Renown;
        var lost = WonFirstFight(Drafting, Party);
        var first = Assert.Single(lost.History);
        Assert.Equal((0, RunNodeKind.Fight, CombatOutcome.Victory), (first.Act, first.Kind, first.Outcome));
        Assert.Throws<InvalidOperationException>(() => new Profile().Record(lost, Drafting));
        lost.Abandon();

        var profile = new Profile();
        Assert.Equal(renown.Fight, profile.Record(lost, Drafting));
        Assert.Equal((renown.Fight, 1, 0, 0), (profile.Renown, profile.Runs, profile.Victories, profile.BestActs));
        Assert.Equal(new EncounterRecord(1, 1), profile.Encounters[first.EncounterId]);

        // 두 막을 이긴 판: 보통 전투 둘, 보스 둘, 승리.
        var data = With(r => r with { Acts = [new RunAct("outskirts_forest", 2) { Bosses = ["wild_dogs"] }, new RunAct("outskirts_forest", 2) { Bosses = ["wild_dogs"] }] });
        var won = Enumerable.Range(0, 30).Select(seed => PlayThrough(data, seed)).First(r => r.Phase == RunPhase.Victory);
        Assert.Equal(renown.Fight * 2 + renown.Boss * 2 + renown.Victory, Profile.RenownFor(won, data));
        profile.Record(won, data);
        Assert.Equal((2, 1, 2), (profile.Runs, profile.Victories, profile.BestActs));
        Assert.Equal(2, profile.Encounters["wild_dogs"].Wins);

        // 명성을 내고 연다. 모자라면, 이미 열었으면, 없는 해금이면 열 수 없다.
        var cost = Drafting.Unlocks["class_warrior"].Cost;
        var before = profile.Renown;
        Assert.True(before < cost);
        Assert.StartsWith("명성이 모자란다", profile.WhyCannotUnlock(Drafting, "class_warrior"));
        Assert.NotNull(profile.WhyCannotUnlock(Drafting, "nope"));
        var cheap = Drafting.Unlocks.Values.Where(u => u.Cost <= before).MinBy(u => u.Cost)!;
        profile.Unlock(Drafting, cheap.Id);
        Assert.Equal(before - cheap.Cost, profile.Renown);
        Assert.Equal("이미 열었다", profile.WhyCannotUnlock(Drafting, cheap.Id));
        Assert.DoesNotContain(cheap.Locks.FirstOrDefault() ?? "", profile.UnlocksForRun(Drafting).Locked);
    }

    /// <summary>판을 끝까지 둔다: 첫 지점, 보상은 건너뛰기, 휴식은 봉인 풀기.</summary>
    private static Run PlayThrough(GameData data, int seed)
    {
        var run = Run.Start(data, Party, seed);
        while (!run.Finished)
        {
            switch (run.Phase)
            {
                case RunPhase.Map:
                    run.Enter(data, run.Choices[0].Id);
                    break;
                case RunPhase.Rest:
                    run.RestUnseal(data);
                    break;
                default:
                    run.Skip();
                    break;
            }
        }

        return run;
    }

    [Fact]
    public void The_profile_is_saved_and_a_broken_one_is_set_aside()
    {
        var run = WonFirstFight(Drafting, Party);
        run.Abandon();
        var profile = new Profile();
        profile.Record(run, Drafting);
        var json = profile.Serialize();
        Assert.Equal(json, Profile.Deserialize(json, Drafting).Serialize());

        // 지금 데이터에 없는 해금은 버린다.
        var stale = json.Replace("\"unlocked\": []", "\"unlocked\": [ \"gone\" ]");
        Assert.Empty(Profile.Deserialize(stale, Drafting).Unlocked);

        var dir = Path.Combine(Path.GetTempPath(), "triangle-profile-" + Guid.NewGuid());
        try
        {
            var store = new ProfileStore(Path.Combine(dir, "profile.json"));
            Assert.Equal(LoadStatus.NewGame, store.Load(Drafting).Status);
            store.Save(profile);
            Assert.Equal(json, store.Load(Drafting).Profile.Serialize());
            File.WriteAllText(store.Path, "{ \"version\": 7 }");
            var broken = store.Load(Drafting);
            Assert.Equal((LoadStatus.Recovered, 0), (broken.Status, broken.Profile.Runs));
            Assert.True(File.Exists(broken.BrokenFilePath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string Describe(RunMap map) => string.Join(";", map.Nodes.Select(n => $"{n.Id}:{n.Floor}/{n.Lane}{n.Kind}>{string.Join(",", n.Next)}"));
}
