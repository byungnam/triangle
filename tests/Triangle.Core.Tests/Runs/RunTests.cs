using System.Text.Json;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Progress;
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

    private static readonly string[] Party = ["knight", "archer", "elementalist", "priest"];

    /// <summary>판의 규칙만 바꾼 게임 데이터 (로더 검증을 거친다).</summary>
    private static GameData With(Func<RunDefinition, RunDefinition> change)
    {
        string Read(string file) => File.ReadAllText(Path.Combine(DataDirectory, file));
        return GameDataLoader.Parse(
            Read(GameDataLoader.MasteriesFile), Read(GameDataLoader.SkillsFile), Read(GameDataLoader.ActionsFile), Read(GameDataLoader.EncountersFile),
            Read(GameDataLoader.EffectsFile), Read(GameDataLoader.ItemsFile), Read(GameDataLoader.ZonesFile), Read(GameDataLoader.RecruitsFile),
            Read(GameDataLoader.RecipesFile), Read(GameDataLoader.ClassesFile), JsonSerializer.Serialize(change(Shipped.Run!), GameDataJson.Options),
            Read(GameDataLoader.UnlocksFile));
    }

    /// <summary>첫 층 다음 층이 모두 <paramref name="kind"/>인 막 하나 (층 3개: 전투, kind, 휴식).</summary>
    private static GameData SecondFloor(string kind, Func<RunDefinition, RunDefinition>? also = null) => With(r =>
    {
        var map = new RunMapRules { SpecialFrom = 1, Fight = 0, Elite = 0, Shop = 0, Rest = 0, Event = 0 };
        map = kind switch
        {
            "shop" => map with { Shop = 1 },
            "event" => map with { Event = 1 },
            _ => map with { Fight = 1 },
        };
        var run = r with { Acts = [r.Acts[0] with { Floors = 3 }], Map = map };
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

    [Fact]
    public void Maps_are_deterministic_connected_and_keep_the_floor_rules()
    {
        var rules = new RunMapRules();
        var kinds = new HashSet<RunNodeKind>();
        for (var seed = 0; seed < 200; seed++)
        {
            var map = RunMap.Generate(rules, 6, hasElites: true, seed);
            Assert.Equal(Describe(map), Describe(RunMap.Generate(rules, 6, hasElites: true, seed)));
            Assert.Equal((RunNodeKind.Boss, map.Nodes.Count - 1, 6), (map.Boss.Kind, map.Boss.Id, map.Boss.Floor));
            Assert.True(map.Starts.Count() >= 2, "갈림길이 생기도록 첫 층에 지점이 둘 이상 있다");

            foreach (var node in map.Nodes.SkipLast(1))
            {
                Assert.Equal(node, map.Node(node.Id));
                Assert.NotEmpty(node.Next);
                Assert.All(node.Next.Select(map.Node), next => Assert.Equal(node.Floor + 1, next.Floor));
                if (node.Floor == 5)
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

                if (node.Floor < rules.SpecialFrom || node.Floor == 4)
                {
                    Assert.NotEqual(RunNodeKind.Rest, node.Kind);
                }

                if (node.Floor < rules.SpecialFrom)
                {
                    Assert.NotEqual(RunNodeKind.Elite, node.Kind);
                }

                kinds.Add(node.Kind);
            }

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
        Assert.DoesNotContain(Enumerable.Range(0, 50).SelectMany(s => RunMap.Generate(rules, 6, hasElites: false, s).Nodes), n => n.Kind == RunNodeKind.Elite);
    }

    [Fact]
    public void Starting_gives_each_member_the_class_kit_and_one_line_and_reveals_the_bosses()
    {
        var run = Run.Start(Shipped, ["knight", "knight", "priest"], 1);

        Assert.Equal(["m1", "m2", "m3"], run.Members.Select(m => m.Id));
        Assert.Equal(["기사 1", "기사 2", "사제"], run.Members.Select(m => run.Name(Shipped, m)));
        Assert.Equal(new Dictionary<string, int> { ["shield_wall"] = 2, ["heal"] = 1 }, run.Copies);
        Assert.All(run.Members, m =>
        {
            var c = Shipped.Classes[m.ClassId];
            Assert.Equal((c.Weapon, c.Armor), (m.Weapon, m.Armor));
            Assert.Equal(Shipped.Run!.StartLines, m.Lines);
            Assert.Equal([c.StartingSkill], m.Tactics.Select(t => t.ActionId));
        });

        Assert.Equal(Shipped.Run!.Acts.Select(a => Shipped.Zones[a.ZoneId].Boss!.EncounterId), run.Bosses);
        Assert.Equal(Shipped.Run.Acts.Count, run.Maps.Count);
        Assert.Equal((RunPhase.Map, 0, (int?)null, Shipped.Run.StartGold), (run.Phase, run.Act, run.Position, run.Gold));
        Assert.Equal(run.Map.Starts, run.Choices);
        Assert.Equal(run.Maps.Select(Describe), Run.Start(Shipped, ["priest"], 1).Maps.Select(Describe));

        Assert.NotNull(Run.WhyCannotStart(Shipped, []));
        Assert.NotNull(Run.WhyCannotStart(Shipped, ["knight", "knight", "knight", "knight", "knight"]));
        Assert.Contains("bard", Run.WhyCannotStart(Shipped, ["bard"]));
        Assert.Throws<InvalidOperationException>(() => Run.Start(Shipped, ["bard"], 1));
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
        var run = Run.Start(Shipped, ["knight", "priest", "knight"], 1, rows: [Units.Row.Back, Units.Row.Front]);
        Assert.Equal([Units.Row.Back, Units.Row.Front, Units.Row.Front], run.Members.Select(m => m.Row));
        Assert.Equal(Units.Row.Back, run.Setup(Shipped, run.Members[0]).Row);

        run.SetRow(run.Members[0], Units.Row.Front);
        Assert.Equal(Units.Row.Front, run.Setup(Shipped, run.Members[0]).Row);
        run.SetRow(run.Members[1], Units.Row.Back);

        // 세이브에 남고, 줄이 없는 세이브는 클래스의 처음 값이 된다.
        Assert.Equal(run.Members.Select(m => m.Row), Run.Deserialize(run.Serialize(), Shipped).Members.Select(m => m.Row));
        var old = run.ToSave() with { Members = run.ToSave().Members.Select(m => m with { Row = null }).ToList() };
        Assert.Equal([Units.Row.Front, Units.Row.Back, Units.Row.Front], Run.Restore(old, Shipped).Members.Select(m => m.Row));

        run.Members[2].Dead = true;
        Assert.NotNull(run.WhyCannotSetRow(run.Members[2]));
        run.Abandon();
        Assert.Equal("판이 끝났다", run.WhyCannotSetRow(run.Members[0]));
    }

    [Fact]
    public void Battle_setup_uses_only_the_lines_under_the_cap_and_ends_with_the_basic_attack()
    {
        var run = Run.Start(Shipped, ["elementalist"], 1);
        var member = run.Members[0];
        run.Copies["ember"] = 1;
        run.SetTactics(Shipped, member, [Line("fireball"), Line("ember"), Line("focus")]);

        var setup = run.Setup(Shipped, member);
        Assert.Equal(["fireball", "ember", "basic_attack"], setup.Tactics.Select(t => t.ActionId));
        Assert.Equal([1, 2, 3], setup.Tactics.Select(t => t.Priority));
        Assert.Equal(["ember", "fireball"], setup.GrantedActions!.Order());
        Assert.Equal(Shipped.Classes["elementalist"].Row, setup.Row);

        // 봉인되면 맨 아래 줄부터 꺼진다. 줄은 남아 있어 봉인이 풀리면 다시 켜진다.
        member.Sealed = 1;
        Assert.Equal(["fireball", "basic_attack"], run.Setup(Shipped, member).Tactics.Select(t => t.ActionId));
        Assert.Equal(["fireball"], run.Setup(Shipped, member).GrantedActions!);
        Assert.Equal(3, member.Tactics.Count);
    }

    [Fact]
    public void Winning_a_fight_pays_gold_and_offers_cards_and_skipping_returns_to_the_map()
    {
        var run = WonFirstFight(Shipped, Party);
        var reward = Shipped.Run!.Reward;

        Assert.Equal(1, run.Fights);
        Assert.InRange(run.Gold, reward.GoldMin, reward.GoldMax);
        Assert.Equal(reward.Cards, run.Offer.Count);
        Assert.Equal(run.Offer.Count, run.Offer.Distinct().Count());
        Assert.All(run.Offer.Where(c => c.Kind != RunCardKind.Line), c => Assert.Contains(c.ClassId, Party));
        Assert.Empty(run.Choices);
        Assert.Throws<InvalidOperationException>(() => run.Enter(Shipped, run.Map.Starts.First().Id));

        var at = run.Position!.Value;
        Assert.Equal([at], run.Path);
        run.Skip();
        Assert.Equal((RunPhase.Map, 0), (run.Phase, run.Offer.Count));
        Assert.Equal(run.Map.Node(at).Next, run.Choices.Select(n => n.Id));
        Assert.NotNull(run.WhyCannotTake(Shipped, 0, null));
        Assert.Throws<InvalidOperationException>(run.Skip);
    }

    [Fact]
    public void Cards_go_to_the_party_or_to_a_member_of_the_right_class()
    {
        static GameData Only(int active, int passive, int line) =>
            With(r => r with { Reward = r.Reward with { Active = active, Passive = passive, Line = line } });

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
            Assert.Contains(Shipped.Classes[m.ClassId].Weapon, run.Bag);
            Assert.DoesNotContain(m, run.Living);
            Assert.Equal(1, run.Copies[Shipped.Classes[m.ClassId].StartingSkill]);
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
    public void The_shop_sells_items_of_the_act_tier_class_cards_and_a_recruit()
    {
        var data = SecondFloor("shop", r => r with { StartGold = 1000 });
        var run = AtSecondFloor(data, ["knight", "priest"]);
        Assert.Equal(RunPhase.Shop, run.Phase);
        var shop = data.Run!.Shop;
        Assert.Equal([(RunStockKind.Item, shop.Items), (RunStockKind.Skill, shop.Skills), (RunStockKind.Recruit, shop.Recruits)],
            run.Stock.GroupBy(s => s.Kind).Select(g => (g.Key, g.Count())));
        var classes = run.Members.Select(m => data.Classes[m.ClassId]).ToList();
        Assert.All(run.Stock.Where(s => s.Kind == RunStockKind.Item).Select(s => data.Items[s.Id]), item =>
        {
            Assert.Contains(item.Slot, (EquipmentSlot[])[EquipmentSlot.MainHand, EquipmentSlot.Body, EquipmentSlot.Trinket]);
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
    public void The_shop_needs_gold_and_room_in_the_party()
    {
        var data = SecondFloor("shop", r => r with { Shop = r.Shop with { RecruitPrice = 0 } });
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
        var run = Run.Start(Shipped, ["knight", "knight", "elementalist"], 1);
        var (first, second, mage) = (run.Members[0], run.Members[1], run.Members[2]);

        // 사본 2장을 두 기사가 한 줄씩 쓰고 있다.
        Assert.StartsWith("사본이 모자란다", run.WhyCannotSetTactics(Shipped, first, [Line("shield_wall"), Line("shield_wall")]));
        run.SetTactics(Shipped, second, [Line("focus")]);
        run.SetTactics(Shipped, first, [Line("shield_wall", Condition.SelfHpAtMost, 50, priority: 7), Line("shield_wall", priority: 3)]);
        Assert.Equal([1, 2], first.Tactics.Select(t => t.Priority));
        Assert.Equal((Condition.SelfHpAtMost, 50), (first.Tactics[0].Condition, first.Tactics[0].Value));

        // 공용 행동은 사본이 없어도 된다. 다른 클래스의 카드는 놓을 수 없다.
        Assert.Null(run.WhyCannotSetTactics(Shipped, second, [Line("basic_attack"), Line("focus")]));
        Assert.Contains("기사의 카드가 아니다", run.WhyCannotSetTactics(Shipped, second, [Line("fireball")]));
        Assert.Null(run.WhyCannotSetTactics(Shipped, mage, [Line("fireball")]));
        Assert.StartsWith("사본이 모자란다", run.WhyCannotSetTactics(Shipped, mage, [Line("fireball"), Line("ember")]));

        // 줄은 줄 상한의 최대치까지 적을 수 있고, 조건은 데이터 검증과 같다.
        Assert.Null(run.WhyCannotSetTactics(Shipped, second, Enumerable.Repeat(Line("focus"), Shipped.Run!.MaxLines).ToList()));
        Assert.NotNull(run.WhyCannotSetTactics(Shipped, second, Enumerable.Repeat(Line("focus"), Shipped.Run.MaxLines + 1).ToList()));
        Assert.Contains("unknown action", run.WhyCannotSetTactics(Shipped, second, [Line("nope")]));
        Assert.Contains("percent", run.WhyCannotSetTactics(Shipped, second, [Line("focus", Condition.SelfHpAtMost, 150)]));
        Assert.Throws<InvalidOperationException>(() => run.SetTactics(Shipped, second, [Line("nope")]));
    }

    [Fact]
    public void Equipment_comes_from_the_bag_and_keeps_to_class_weapons_and_the_trinket_limit()
    {
        var run = Run.Start(Shipped, ["knight"], 1);
        var knight = run.Members[0];

        run.Unequip(knight, EquipmentSlot.MainHand);
        Assert.Null(knight.Weapon);
        Assert.Equal(["old_sword"], run.Bag);
        run.Equip(Shipped, knight, 0);
        Assert.Equal(("old_sword", 0), (knight.Weapon, run.Bag.Count));
        Assert.Throws<InvalidOperationException>(() => run.Unequip(knight, EquipmentSlot.Trinket));

        run.Bag.AddRange(["hunting_bow", "plate_helm", "leather_armor", "frost_ring", "mana_crystal", "banner_of_defiance"]);
        Assert.Contains("쓰지 않는다", run.WhyCannotEquip(Shipped, knight, 0));
        Assert.NotNull(run.WhyCannotEquip(Shipped, knight, 1));
        Assert.NotNull(run.WhyCannotEquip(Shipped, knight, 9));

        run.Equip(Shipped, knight, 2);
        Assert.Equal("leather_armor", knight.Armor);
        Assert.Equal("plate_armor", run.Bag[^1]);

        run.Equip(Shipped, knight, run.Bag.IndexOf("banner_of_defiance"));
        run.Equip(Shipped, knight, run.Bag.IndexOf("frost_ring"));
        Assert.Equal("장신구는 2개까지다", run.WhyCannotEquip(Shipped, knight, run.Bag.IndexOf("mana_crystal")));
        Assert.Contains("taunting", run.Setup(Shipped, knight).StartingEffects);

        run.Unequip(knight, EquipmentSlot.Trinket, 0);
        Assert.Equal(["frost_ring"], knight.Trinkets);
        Assert.Equal("banner_of_defiance", run.Bag[^1]);
    }

    [Fact]
    public void The_same_seed_and_the_same_choices_make_the_same_run()
    {
        static string Play(int seed)
        {
            var run = Run.Start(Shipped, Party, seed);
            var log = new List<string>();
            while (!run.Finished && run.Fights < 8)
            {
                switch (run.Phase)
                {
                    case RunPhase.Map:
                        var battle = run.Enter(Shipped, run.Choices[^1].Id);
                        log.Add($"{run.Position}:{battle?.EncounterId}:{battle?.Result.Outcome}:{battle?.Gold}");
                        break;
                    case RunPhase.Reward:
                        log.Add(string.Join(",", run.Offer));
                        var card = run.Offer[0];
                        var member = run.Living.FirstOrDefault(m => run.WhyCannotTake(Shipped, 0, m) is null);
                        if (card.Kind == RunCardKind.Active || member is not null)
                        {
                            run.Take(Shipped, 0, member);
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
                        run.RestTrain(Shipped);
                        break;
                    case RunPhase.Event:
                        log.Add(run.Event!.Id);
                        run.Choose(Shipped, run.Event.Options.Count - 1);
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
        foreach (var data in new[] { Shipped, SecondFloor("shop", r => r with { StartGold = 500 }), SecondFloor("event") })
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
        var run = Run.Start(Shipped, ["knight"], 1);
        var file = run.ToSave();

        Assert.Contains(Assert.Throws<SaveGameException>(() => Run.Restore(file with { Version = 99 }, Shipped)).Errors, e => e.Contains("version 99"));
        var errors = Assert.Throws<SaveGameException>(() => Run.Restore(file with
        {
            Members = [file.Members[0] with { ClassId = "bard", Weapon = "no_sword" }],
            Bag = ["no_item"],
            Phase = RunPhase.Event,
            Position = 999,
        }, Shipped)).Errors;
        Assert.Contains(errors, e => e.Contains("unknown class 'bard'"));
        Assert.Contains(errors, e => e.Contains("unknown item 'no_sword'"));
        Assert.Contains(errors, e => e.Contains("unknown item 'no_item' in bag"));
        Assert.Contains(errors, e => e.Contains("position 999"));
        Assert.Contains(errors, e => e.Contains("event phase needs an event"));
        Assert.Throws<SaveGameException>(() => Run.Deserialize("{ \"version\": 1 }", Shipped));
    }

    [Fact]
    public void The_run_store_keeps_one_run_and_sets_a_broken_one_aside()
    {
        var dir = Path.Combine(Path.GetTempPath(), "triangle-run-" + Guid.NewGuid());
        try
        {
            var store = new RunStore(Path.Combine(dir, "run.json"));
            Assert.Equal((null, LoadStatus.NewGame), (store.Load(Shipped).Run, store.Load(Shipped).Status));

            var run = Run.Start(Shipped, Party, 5);
            store.Save(run);
            var loaded = store.Load(Shipped);
            Assert.Equal(LoadStatus.Loaded, loaded.Status);
            Assert.Equal(run.Serialize(), loaded.Run!.Serialize());

            run.Abandon();
            Assert.Equal(RunPhase.Defeat, run.Phase);
            store.Delete();
            Assert.Equal(LoadStatus.NewGame, store.Load(Shipped).Status);

            File.WriteAllText(store.Path, "not json");
            var broken = store.Load(Shipped);
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
    private static readonly RunUnlocks Fresh = RunUnlocks.For(Shipped, new HashSet<string>());

    [Fact]
    public void Unopened_unlocks_lock_their_classes_cards_and_items_and_opened_ones_add_start_gold()
    {
        Assert.Contains("warrior", Fresh.Locked);
        Assert.Contains("gunner", Fresh.Locked);
        Assert.Contains("snipe", Fresh.Locked);
        Assert.Contains("troll_amulet", Fresh.Locked);
        Assert.DoesNotContain("knight", Fresh.Locked);
        Assert.Equal(0, Fresh.StartGold);

        var rich = RunUnlocks.For(Shipped, new HashSet<string> { "war_chest", "class_warrior" });
        Assert.Equal(50, rich.StartGold);
        Assert.DoesNotContain("warrior", rich.Locked);
        Assert.Equal(Shipped.Run!.StartGold + 50, Run.Start(Shipped, ["warrior"], 1, rich).Gold);

        Assert.Contains("전사", Run.WhyCannotStart(Shipped, ["knight", "warrior"], Fresh));
        Assert.Throws<InvalidOperationException>(() => Run.Start(Shipped, ["warrior"], 1, Fresh));
        Assert.Null(Run.WhyCannotStart(Shipped, ["warrior"]));
    }

    [Fact]
    public void Locked_cards_items_and_classes_never_show_up_in_a_run()
    {
        bool Locked(string? id) => id is not null && Fresh.Locked.Contains(id);

        // 보상: 원소술사와 궁수 카드에는 잠긴 카드가 많다.
        var actives = With(r => r with { Reward = r.Reward with { Active = 1, Passive = 0, Line = 0 }, Acts = [r.Acts[0]] });
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
        var saved = WonFirstFight(Shipped, Party, unlocks: Fresh);
        Assert.Equal(Fresh.Locked.Order(), Run.Deserialize(saved.Serialize(), Shipped).Locked.Order());
    }

    [Fact]
    public void A_finished_run_gives_renown_and_records_and_renown_opens_unlocks()
    {
        var renown = Shipped.Run!.Renown;
        var lost = WonFirstFight(Shipped, Party);
        var first = Assert.Single(lost.History);
        Assert.Equal((0, RunNodeKind.Fight, CombatOutcome.Victory), (first.Act, first.Kind, first.Outcome));
        Assert.Throws<InvalidOperationException>(() => new Profile().Record(lost, Shipped));
        lost.Abandon();

        var profile = new Profile();
        Assert.Equal(renown.Fight, profile.Record(lost, Shipped));
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
        var cost = Shipped.Unlocks["class_warrior"].Cost;
        var before = profile.Renown;
        Assert.True(before < cost);
        Assert.StartsWith("명성이 모자란다", profile.WhyCannotUnlock(Shipped, "class_warrior"));
        Assert.NotNull(profile.WhyCannotUnlock(Shipped, "nope"));
        var cheap = Shipped.Unlocks.Values.Where(u => u.Cost <= before).MinBy(u => u.Cost)!;
        profile.Unlock(Shipped, cheap.Id);
        Assert.Equal(before - cheap.Cost, profile.Renown);
        Assert.Equal("이미 열었다", profile.WhyCannotUnlock(Shipped, cheap.Id));
        Assert.DoesNotContain(cheap.Locks.FirstOrDefault() ?? "", profile.UnlocksForRun(Shipped).Locked);
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
        var run = WonFirstFight(Shipped, Party);
        run.Abandon();
        var profile = new Profile();
        profile.Record(run, Shipped);
        var json = profile.Serialize();
        Assert.Equal(json, Profile.Deserialize(json, Shipped).Serialize());

        // 지금 데이터에 없는 해금은 버린다.
        var stale = json.Replace("\"unlocked\": []", "\"unlocked\": [ \"gone\" ]");
        Assert.Empty(Profile.Deserialize(stale, Shipped).Unlocked);

        var dir = Path.Combine(Path.GetTempPath(), "triangle-profile-" + Guid.NewGuid());
        try
        {
            var store = new ProfileStore(Path.Combine(dir, "profile.json"));
            Assert.Equal(LoadStatus.NewGame, store.Load(Shipped).Status);
            store.Save(profile);
            Assert.Equal(json, store.Load(Shipped).Profile.Serialize());
            File.WriteAllText(store.Path, "{ \"version\": 7 }");
            var broken = store.Load(Shipped);
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
