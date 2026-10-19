using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI.Styles;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Triangle.Core.Runs;
using Triangle.Core.Tactics;
using Triangle.Desktop.Rendering;
using Triangle.Desktop.Scenes;

namespace Triangle.Desktop;

/// <param name="ScreenshotPath">지정하면 첫 화면을 PNG에 저장하고 종료한다.</param>
/// <param name="ScreenshotLines">전투 화면 스크린샷 전에 진행할 로그 줄 수. null이면 전투 끝까지.</param>
/// <param name="StartInCombat">마을 대신 전투 화면으로 시작한다 (출전 명단과 훈련 부대의 시험 전투, 상태는 바꾸지 않는다).</param>
/// <param name="SavePath">세이브 파일 경로. null이면 OS별 기본 위치.</param>
/// <param name="StartInExploration">마을 대신 탐사 기록 화면으로 시작한다.</param>
/// <param name="StartScene">
/// 판 대신 이 화면으로 시작한다. village(옛 마을), 옛 화면 확인용 editor, editor:equipment, editor:stats, editor:picker, editor:preview,
/// training, shop, infirmary, 판 화면 확인용 run:map, run:reward, run:shop, run:rest, run:event, run:editor, run:equipment, run:picker,
/// run:end, run:party, run:unlocks (임시 폴더의 시험 판과 기록으로, 실제 세이브는 건드리지 않는다).
/// </param>
/// <param name="StartInSummary">전투 화면으로 시작할 때 요약 탭을 연다.</param>
public sealed record LaunchOptions(
    string? ScreenshotPath = null, int? ScreenshotLines = null, bool StartInCombat = false, string? SavePath = null,
    bool StartInExploration = false, string? StartScene = null, bool StartInSummary = false);

/// <summary>
/// 화면 흐름 (로그라이트 재편, 2026-10-19): 파티 고르기 → 지도 ↔ (편성, 전투 기록 → 보상, 상점, 휴식, 이벤트) → 결과 → 파티 고르기.
/// 판은 바뀔 때마다 자동 저장한다(<see cref="RunSession"/>).
/// 옛 흐름(마을 ↔ 편성·모집·상점…, 마을 → 원정 → 귀환)은 <c>--scene village</c>로만 연다. 새 판 화면이 자리 잡으면 지운다.
/// 옛 원정 중에는 전투가 끝날 때마다, 출정과 귀환 때 자동 저장하고, 종료하면 항상 저장한다.
/// 옛 마을에서는 수동 저장이고, 저장하지 않고 종료하면 확인 창을 띄운다.
/// </summary>
public class TriangleGame : Game
{
    /// <summary>기본 창 크기 (QHD). 모니터에 들어가지 않으면 16:9를 지키며 줄인다.</summary>
    private const int WindowWidth = 2560;
    private const int WindowHeight = 1440;
    private const string TestEncounter = "training";

    private readonly GraphicsDeviceManager _graphics;
    private readonly Input _input = new();
    private readonly LaunchOptions _options;
    private readonly Screen _screen = new();
    private Point _windowedSize;

    private SpriteBatch _spriteBatch = null!;
    private Ui _ui = null!;
    private IScene _scene = null!;
    private GameSession? _session;
    private RunSession? _runs;
    private VillageScene _village = null!;
    private ExpeditionScene _expedition = null!;
    private TacticEditorScene _editor = null!;
    private int _framesDrawn;
    private bool _quitConfirmed;

    public TriangleGame(LaunchOptions options)
    {
        _options = options;
        _windowedSize = InitialWindowSize();
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _windowedSize.X,
            PreferredBackBufferHeight = _windowedSize.Y,
            HardwareModeSwitch = false,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Triangle";
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
    }

    private static Rectangle Bounds => Screen.Bounds;

    private static DisplayMode Display => GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

    /// <summary>QHD 창이 모니터에 들어가면 QHD, 아니면 모니터의 90% 안에 들어가는 가장 큰 16:9 창.</summary>
    private static Point InitialWindowSize()
    {
        var display = Display;
        var scale = Math.Min(1f, Math.Min(display.Width * 0.9f / WindowWidth, display.Height * 0.9f / WindowHeight));
        return new Point((int)(WindowWidth * scale), (int)(WindowHeight * scale));
    }

    /// <summary>사용자가 창 크기를 바꿨다. 백버퍼를 창에 맞춘다 (화면 배율은 Update에서 맞춘다).</summary>
    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        var size = Window.ClientBounds.Size;
        if (_graphics.IsFullScreen || size.X <= 0 || size.Y <= 0
            || (size.X == _graphics.PreferredBackBufferWidth && size.Y == _graphics.PreferredBackBufferHeight))
        {
            return;
        }

        _windowedSize = size;
        _graphics.PreferredBackBufferWidth = size.X;
        _graphics.PreferredBackBufferHeight = size.Y;
        _graphics.ApplyChanges();
    }

    /// <summary>F11: 창 모드 ↔ 전체 화면 (모니터 해상도 그대로, 해상도 전환 없이).</summary>
    private void ToggleFullScreen()
    {
        var size = _graphics.IsFullScreen ? _windowedSize : new Point(Display.Width, Display.Height);
        _graphics.PreferredBackBufferWidth = size.X;
        _graphics.PreferredBackBufferHeight = size.Y;
        _graphics.IsFullScreen = !_graphics.IsFullScreen;
        _graphics.ApplyChanges();
    }

    private GameSession Session => _session!;
    private GameData Data => Session.Data;
    private Company Company => Session.Company;

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        // 글자는 화면 배율 이상의 해상도로 새겨 두어야 키워도 선명하다. 전체 화면까지 고려해 모니터 기준으로 정한다.
        var fontResolution = Math.Max(2f, MathF.Ceiling(Math.Min(Display.Width / (float)Screen.Width, Display.Height / (float)Screen.Height)));
        _screen.Resize(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        _ui = new Ui(GraphicsDevice, _screen, Path.Combine(AppContext.BaseDirectory, "Fonts"), fontResolution);
        SetUpMyra();
        _scene = CreateFirstScene();
    }

    /// <summary>Myra 기본 스타일의 글꼴을 한글 글꼴로 바꾼다 (직접 지정하지 않은 글자에도 한글이 나오도록).</summary>
    private void SetUpMyra()
    {
        MyraEnvironment.Game = this;
        var stylesheet = Stylesheet.Current;
        var font = _ui.Font(17);
        stylesheet.LabelStyle.Font = font;
        stylesheet.TooltipStyle.Font = font;
        stylesheet.TextBoxStyle.Font = font;
        stylesheet.TextBoxStyle.MessageFont = font;
        stylesheet.ComboBoxStyle.LabelStyle.Font = font;
    }

    private IScene CreateFirstScene()
    {
        GameData data;
        try
        {
            data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        }
        catch (GameDataException e)
        {
            return new ErrorScene(_ui, "게임 데이터를 읽지 못했습니다", e.Errors);
        }

        _runs = CreateRunSession(data);
        var store = new SaveStore(_options.SavePath ?? SaveStore.DefaultPath);
        var loaded = store.Load(data, () => StartingCompany.Create(data, Random.Shared.Next()));
        _session = new GameSession(data, loaded.Company, store) { Notice = LoadNotice(loaded) };

        _village = new VillageScene(_ui, Session, Bounds, OpenEditor, OpenRecruit, OpenShop, OpenCraft, OpenExploration, OpenInfirmary, Depart, ConfirmedExit);
        _expedition = new ExpeditionScene(_ui, Session, Bounds, NextBattle);
        _editor = new TacticEditorScene(_ui, Session, Bounds, OpenTraining, ShowHome);

        switch (_options.StartScene?.Split(':'))
        {
            case ["village"]:
                return Home;
            case ["run", var view]:
                return DemoRunScene(view);
            case ["editor", .. var rest]:
                _editor.Show(rest.FirstOrDefault());
                return _editor;
            case ["shop"]:
                return new ShopScene(_ui, Session, Bounds, back: ShowHome);
            case ["infirmary"]:
                return new InfirmaryScene(_ui, Session, Bounds, back: ShowHome);
            case ["training"]:
                return new MasteryScene(_ui, Session, Company.Roster[0], Bounds, back: OpenEditor);
        }

        if (_options.StartInExploration)
        {
            return new ExplorationScene(_ui, Session, Bounds, back: ShowHome);
        }

        if (!_options.StartInCombat || Company.Lineup.Count == 0)
        {
            return RunSceneFor(null);
        }

        var result = CombatSimulator.Run(
            Company.Expedition is null ? Company.LineupSetups(Data) : ExpeditionRules.AllySetups(Company, Data),
            Data.CreateEncounterTeam(TestEncounter), Data.Catalog, seed: 1);
        var combat = new CombatLogScene(_ui, Data, result, TestEncounter, "시험 전투", Bounds, ShowHome, "돌아가기");
        if (_options.ScreenshotPath is not null)
        {
            combat.RevealLines(_options.ScreenshotLines ?? int.MaxValue);
        }

        if (_options.StartInSummary)
        {
            combat.ShowSummary();
        }

        return combat;
    }

    /// <summary>판 세이브: 회사 세이브와 같은 폴더의 run.json. 판 화면 확인용 시작이면 임시 폴더를 쓴다.</summary>
    private RunSession CreateRunSession(GameData data)
    {
        var directory = _options.StartScene?.StartsWith("run:") == true ? Path.Combine(Path.GetTempPath(), $"triangle-demo-{Guid.NewGuid():N}")
            : _options.SavePath is { } save ? Path.GetDirectoryName(Path.GetFullPath(save))!
            : null;
        var store = new RunStore(directory is null ? RunStore.DefaultPath : Path.Combine(directory, "run.json"));
        var profiles = new ProfileStore(directory is null ? ProfileStore.DefaultPath : Path.Combine(directory, "profile.json"));
        var loaded = store.Load(data);
        var profile = profiles.Load(data);
        var session = new RunSession(data, store, profiles, profile.Profile);
        session.Resume(loaded.Run);
        if (loaded.Status == LoadStatus.Recovered)
        {
            session.Notice = ($"원정 세이브를 읽지 못해 새로 시작합니다. 원래 파일: {Path.GetFileName(loaded.BrokenFilePath)}", Theme.Enemy);
        }
        else if (profile.Status == LoadStatus.Recovered)
        {
            session.Notice = ($"기록을 읽지 못해 새로 시작합니다. 원래 파일: {Path.GetFileName(profile.BrokenFilePath)}", Theme.Enemy);
        }

        return session;
    }

    private RunSession Runs => _runs!;

    private void ShowRun(RunBattle? battle = null) => _scene = RunSceneFor(battle);

    /// <summary>판의 단계에 맞는 화면. 판이 없으면 파티 고르기, 끝났으면 결과.</summary>
    private IScene RunSceneFor(RunBattle? battle)
    {
        void Changed()
        {
            Runs.Save();
            ShowRun();
        }

        return Runs.Run switch
        {
            null => new RunPartyScene(_ui, Runs, Bounds, StartRun, OpenUnlocks),
            { Finished: true } => new RunEndScene(_ui, Runs, Bounds, NewRun),
            { Phase: RunPhase.Reward } => new RunRewardScene(_ui, Runs, Bounds, Changed, battle),
            { Phase: RunPhase.Shop } => new RunShopScene(_ui, Runs, Bounds, Changed),
            { Phase: RunPhase.Rest } => new RunRestScene(_ui, Runs, Bounds, Changed),
            { Phase: RunPhase.Event } => new RunEventScene(_ui, Runs, Bounds, Changed),
            _ => new RunMapScene(_ui, Runs, Bounds, EnterNode, OpenRunEditor, AbandonRun),
        };
    }

    private void StartRun(IReadOnlyList<string> classIds)
    {
        Runs.Start(classIds);
        ShowRun();
    }

    /// <summary>끝난 판의 명성과 기록을 받고 파티 고르기로 간다.</summary>
    private void NewRun()
    {
        Runs.Finish();
        ShowRun();
    }

    private void OpenUnlocks() => _scene = new RunUnlockScene(_ui, Runs, Bounds, back: () => ShowRun());

    private void AbandonRun()
    {
        Runs.Run!.Abandon();
        Runs.Save();
        ShowRun();
    }

    private void OpenRunEditor() => _scene = new RunEditorScene(_ui, Runs, Bounds, back: () => ShowRun());

    /// <summary>지점에 들어간다. 전투면 결과를 저장한 뒤 전투 기록을 보여주고, 다 보면 보상(또는 결과)으로 간다.</summary>
    private void EnterNode(int nodeId)
    {
        var run = Runs.Run!;
        var title = RunText.Act(run, Runs.Data);
        var battle = run.Enter(Runs.Data, nodeId);
        Runs.Save();
        if (battle is null)
        {
            ShowRun();
            return;
        }

        var combat = new CombatLogScene(_ui, Runs.Data, battle.Result, battle.EncounterId, $"{title} · {RunText.Node(battle.Kind).Name}", Bounds,
            () => ShowRun(battle), run.Finished ? "결과로" : "보상으로");
        combat.SetRewardLines(RunText.Battle(battle, run, Runs.Data).Skip(1).ToList());
        _scene = combat;
    }

    /// <summary>판 화면 확인용: 시험 판을 그 단계까지 자동으로 진행한다 (지점은 원하는 종류를, 카드는 첫 장을 고른다).</summary>
    private IScene DemoRunScene(string view)
    {
        var data = Runs.Data;
        var run = Triangle.Core.Runs.Run.Start(data, ["knight", "archer", "elementalist", "priest"], 3);
        Runs.Resume(run);
        var (phase, kind) = view switch
        {
            "reward" => (RunPhase.Reward, RunNodeKind.Fight),
            "shop" => (RunPhase.Shop, RunNodeKind.Shop),
            "rest" => (RunPhase.Rest, RunNodeKind.Rest),
            "event" => (RunPhase.Event, RunNodeKind.Event),
            _ => (RunPhase.Map, RunNodeKind.Fight),
        };
        RunBattle? battle = null;
        for (var step = 0; step < 80 && !run.Finished && !(run.Phase == phase && run.Fights > 0); step++)
        {
            switch (run.Phase)
            {
                case RunPhase.Map:
                    var choice = run.Choices.FirstOrDefault(n => n.Kind == kind) ?? run.Choices[0];
                    battle = run.Enter(data, choice.Id);
                    break;
                case RunPhase.Reward:
                    var member = run.Living.FirstOrDefault(m => run.WhyCannotTake(data, 0, m) is null);
                    if (run.Offer[0].Kind == RunCardKind.Active || member is not null)
                    {
                        run.Take(data, 0, member);
                    }
                    else
                    {
                        run.Skip();
                    }

                    break;
                case RunPhase.Shop:
                    run.LeaveShop();
                    break;
                case RunPhase.Rest:
                    run.RestUnseal(data);
                    break;
                default:
                    run.Choose(data, run.Event!.Options.Count - 1);
                    break;
            }
        }

        switch (view)
        {
            case "unlocks" or "party":
                run.Abandon();
                Runs.Finish();
                return view == "party" ? RunSceneFor(null) : new RunUnlockScene(_ui, Runs, Bounds, back: () => ShowRun());
            case "end":
                run.Abandon();
                break;
            case "editor" or "equipment" or "picker":
                run.Bag.AddRange(["frost_ring", "banner_of_defiance", "leather_armor"]);
                // 줄 상한(2)보다 한 줄 더 적어 꺼진 줄도 보이게 한다.
                var knight = run.Members[0];
                run.Copies["heavy_strike"] = run.Copies.GetValueOrDefault("heavy_strike") + 1;
                run.SetTactics(data, knight, [.. knight.Tactics, Tactic.Create(9, [new(Condition.SelfHpAtLeast, 50)], "heavy_strike"),
                    Tactic.Create(9, [new(Condition.Always, 0)], "focus")]);
                var editor = new RunEditorScene(_ui, Runs, Bounds, back: () => ShowRun());
                editor.Show(view);
                return editor;
        }

        return RunSceneFor(phase == RunPhase.Reward ? battle : null);
    }

    private static (string, Color)? LoadNotice(LoadResult result) => result.Status switch
    {
        LoadStatus.Loaded => ("세이브를 불러왔습니다", Theme.TextDim),
        LoadStatus.Recovered => ($"세이브를 읽지 못해 새로 시작합니다. 원래 파일: {Path.GetFileName(result.BrokenFilePath)}", Theme.Enemy),
        _ => null,
    };

    /// <summary>원정 중이면 원정 화면, 아니면 마을.</summary>
    private IScene Home
    {
        get
        {
            if (Company.OnExpedition)
            {
                _expedition.Refresh();
                return _expedition;
            }

            _village.Refresh();
            return _village;
        }
    }

    private void ShowHome() => _scene = Home;

    private void OpenEditor()
    {
        _editor.Refresh();
        _scene = _editor;
    }

    private void OpenTraining(PartyMember member) =>
        _scene = new MasteryScene(_ui, Session, member, Bounds, back: OpenEditor);

    private void OpenRecruit() => _scene = new RecruitScene(_ui, Session, Bounds, back: ShowHome);

    private void OpenShop() => _scene = new ShopScene(_ui, Session, Bounds, back: ShowHome);

    private void OpenExploration() => _scene = new ExplorationScene(_ui, Session, Bounds, back: ShowHome);

    private void OpenCraft() => _scene = new CraftScene(_ui, Session, Bounds, back: ShowHome);

    private void OpenInfirmary() => _scene = new InfirmaryScene(_ui, Session, Bounds, back: ShowHome);

    /// <summary>출정한다. 원정 상태를 바로 저장한다.</summary>
    private void Depart(string zoneId)
    {
        ExpeditionRules.Start(Company, Data, zoneId);
        Session.AutoSave();
        ShowHome();
    }

    /// <summary>
    /// 다음 전투를 치른다. 결과를 원정에 반영하고 저장한 뒤 전투 기록을 보여준다
    /// (기록을 보는 도중에 꺼도 결과는 확정되어 있다).
    /// </summary>
    private void NextBattle()
    {
        var expedition = Company.Expedition!;
        var zone = Data.Zones[expedition.ZoneId];
        var encounterId = ExpeditionRules.NextEncounter(expedition, Data);
        var title = ExpeditionRules.IsBossBattle(expedition, Data)
            ? $"전투 {expedition.BattleIndex + 1}/{zone.MaxBattles} · 보스"
            : $"전투 {expedition.BattleIndex + 1}/{zone.MaxBattles}";

        var result = ExpeditionRules.Fight(Company, Data);
        var summary = ExpeditionRules.ApplyResult(Company, Data, result);
        var report = expedition.LastBattle!;
        Session.AutoSave();

        var lines = new List<LogLine> { new("전투 결과", Theme.Cover) };
        lines.AddRange(ExpeditionText.Battle(Data, report));
        if (summary is not null)
        {
            var (text, color) = ExpeditionText.Summary(Data, summary);
            lines.Add(new LogLine(text, color));
            lines.AddRange(ExpeditionText.Injuries(summary));
            lines.AddRange(ExpeditionText.Clear(Data, summary));
            Session.Notice = (text, color);
        }

        var combat = new CombatLogScene(_ui, Data, result, encounterId, title, Bounds, ShowHome, summary is null ? "원정으로" : "마을로");
        combat.SetRewardLines(lines);
        _scene = combat;
    }

    /// <summary>마을이 종료를 확인했다 (저장했거나 버리기로 했다).</summary>
    private void ConfirmedExit()
    {
        _quitConfirmed = true;
        Exit();
    }

    /// <summary>
    /// 창 닫기 버튼 등으로 종료할 때: 원정 중이면 항상 저장하고 끝낸다.
    /// 마을에서 저장하지 않은 변경이 있으면 종료를 취소하고 마을에서 확인 창을 띄운다.
    /// </summary>
    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        if (!_quitConfirmed && _options.ScreenshotPath is null && _session is not null)
        {
            _runs?.Save();
            if (Company.OnExpedition)
            {
                Session.Save();
            }
            else if (Session.Unsaved)
            {
                args.Cancel = true;
                _scene = _village;
                _village.RequestQuit();
                return;
            }
        }

        base.OnExiting(sender, args);
    }

    protected override void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.Pressed(Keys.F11))
        {
            ToggleFullScreen();
        }

        _screen.Resize(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        _scene.Update(gameTime, _input);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Theme.Background);
        _scene.Draw(_spriteBatch);

        // 글자는 처음 그릴 때 글꼴 텍스처에 새겨지므로 첫 프레임에는 빠질 수 있다. 몇 프레임 뒤에 찍는다.
        if (_options.ScreenshotPath is { } path && ++_framesDrawn == 3)
        {
            SaveScreenshot(path);
            Exit();
        }

        base.Draw(gameTime);
    }

    /// <summary>창 크기와 상관없이 QHD로 찍는다.</summary>
    private void SaveScreenshot(string path)
    {
        using var target = new RenderTarget2D(GraphicsDevice, WindowWidth, WindowHeight);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Theme.Background);
        _screen.Resize(WindowWidth, WindowHeight);
        _scene.Draw(_spriteBatch);
        GraphicsDevice.SetRenderTarget(null);

        using var file = File.Create(path);
        target.SaveAsPng(file, WindowWidth, WindowHeight);
    }

    protected override void UnloadContent()
    {
        _ui.Dispose();
        _spriteBatch.Dispose();
        base.UnloadContent();
    }
}
