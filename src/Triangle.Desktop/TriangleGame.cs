using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI.Styles;
using Triangle.Core.Data;
using Triangle.Core.Runs;
using Triangle.Core.Tactics;
using Triangle.Desktop.Audio;
using Triangle.Desktop.Rendering;
using Triangle.Desktop.Scenes;

namespace Triangle.Desktop;

/// <param name="ScreenshotPath">지정하면 첫 화면을 PNG에 저장하고 종료한다.</param>
/// <param name="ScreenshotLines">전투 기록 화면(run:combat) 스크린샷 전에 진행할 로그 줄 수. null이면 전투 끝까지.</param>
/// <param name="SaveDirectory">판 세이브(run.json)와 기록(profile.json)을 두는 폴더. null이면 OS별 기본 위치.</param>
/// <param name="StartScene">
/// 확인·스크린샷용으로 이 화면에서 시작한다: run:map, run:reward, run:shop, run:rest, run:event, run:editor, run:equipment, run:picker,
/// run:end, run:party, run:unlocks, run:combat (임시 폴더의 시험 판과 기록으로, 실제 세이브는 건드리지 않는다).
/// </param>
/// <param name="StartInSummary">run:combat으로 시작할 때 요약 탭을 연다.</param>
public sealed record LaunchOptions(
    string? ScreenshotPath = null, int? ScreenshotLines = null, string? SaveDirectory = null, string? StartScene = null, bool StartInSummary = false);

/// <summary>
/// 화면 흐름 (로그라이트 재편, 2026-10-19): 파티 고르기 ↔ 해금·기록, 파티 고르기 → 지도 ↔ (편성, 전투 기록 → 보상, 상점, 휴식, 이벤트)
/// → 결과 → 파티 고르기. 판은 바뀔 때마다 자동 저장하고(<see cref="RunSession"/>), 껐다 켜면 그 단계에서 이어 간다.
/// 옛 마을·원정·회사는 2026-10-19에 지웠다.
/// 배경음악은 화면에 따라 바뀌고 (파티 고르기·판 진행·전투), 어느 화면에서나 F8 음소거, F9·F10 음량 조절이다.
/// </summary>
public class TriangleGame : Game
{
    /// <summary>기본 창 크기 (QHD). 모니터에 들어가지 않으면 16:9를 지키며 줄인다.</summary>
    private const int WindowWidth = 2560;
    private const int WindowHeight = 1440;

    private readonly GraphicsDeviceManager _graphics;
    private readonly Input _input = new();
    private readonly LaunchOptions _options;
    private readonly Screen _screen = new();
    private Point _windowedSize;

    private SpriteBatch _spriteBatch = null!;
    private Ui _ui = null!;
    private IScene _scene = null!;
    private RunSession? _runs;
    private int _framesDrawn;
    private MusicPlayer? _music;
    private string _settingsPath = null!;

    /// <summary>음량을 바꾼 뒤 화면에 음량을 보여줄 남은 시간 (초).</summary>
    private float _volumeShown;

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

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        // 글자는 화면 배율 이상의 해상도로 새겨 두어야 키워도 선명하다. 전체 화면까지 고려해 모니터 기준으로 정한다.
        var fontResolution = Math.Max(2f, MathF.Ceiling(Math.Min(Display.Width / (float)Screen.Width, Display.Height / (float)Screen.Height)));
        _screen.Resize(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        _ui = new Ui(GraphicsDevice, _screen, Path.Combine(AppContext.BaseDirectory, "Fonts"), fontResolution);
        SetUpMyra();
        _scene = CreateFirstScene();
        // 스크린샷 확인 실행에서는 음악을 틀지 않는다.
        _settingsPath = _options.SaveDirectory is { } directory ? Path.Combine(directory, "settings.json") : Settings.PathNextTo(RunStore.DefaultPath);
        if (_options.ScreenshotPath is null)
        {
            var settings = Settings.Load(_settingsPath);
            _music = new MusicPlayer(Path.Combine(AppContext.BaseDirectory, "Music"), settings.MusicVolume, settings.MusicMuted);
        }
    }

    /// <summary>화면에 맞는 곡: 전투 기록은 전투, 판이 진행 중이면 원정, 그 밖에는 (파티 고르기·해금·결과) 대기실 곡. 오류 화면은 조용히.</summary>
    private string? TrackFor(IScene scene) => scene switch
    {
        ErrorScene => null,
        CombatLogScene => Tracks.Battle,
        _ when _runs?.Run is { Finished: false } => Tracks.Expedition,
        _ => Tracks.Lobby,
    };

    /// <summary>F8 음소거, F9 음량 줄이기, F10 음량 키우기. 바꾸면 잠시 음량을 보여주고 설정에 저장한다.</summary>
    private void UpdateMusic(GameTime gameTime)
    {
        if (_music is null)
        {
            return;
        }

        var changed = true;
        if (_input.Pressed(Keys.F8))
        {
            _music.ToggleMute();
        }
        else if (_input.Pressed(Keys.F9))
        {
            _music.ChangeVolume(-0.1f);
        }
        else if (_input.Pressed(Keys.F10))
        {
            _music.ChangeVolume(0.1f);
        }
        else
        {
            changed = false;
        }

        if (changed)
        {
            _volumeShown = 1.5f;
            new Settings(_music.Volume, _music.Muted).Save(_settingsPath);
        }

        _volumeShown = Math.Max(0f, _volumeShown - (float)gameTime.ElapsedGameTime.TotalSeconds);
        _music.Play(TrackFor(_scene));
        _music.Update(gameTime);
    }

    /// <summary>음량을 바꾼 직후 오른쪽 아래에 잠시 보여준다.</summary>
    private void DrawVolume()
    {
        if (_music is null || _volumeShown <= 0f)
        {
            return;
        }

        var text = _music.Muted ? "음악 꺼짐 (F8)" : $"음악 {_music.Volume * 100:0}%";
        var font = _ui.BoldFont(20);
        var size = font.MeasureString(text);
        var position = new Vector2(Bounds.Right - 24 - size.X, Bounds.Bottom - 80);
        _spriteBatch.Begin(transformMatrix: _screen.Transform);
        _ui.Fill(_spriteBatch, new Rectangle((int)position.X - 12, (int)position.Y - 6, (int)size.X + 24, (int)size.Y + 12), Theme.Panel * 0.9f);
        _ui.Text(_spriteBatch, font, text, position, Theme.Text);
        _spriteBatch.End();
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
        return _options.StartScene?.Split(':') is ["run", var view] ? DemoRunScene(view) : RunSceneFor(null);
    }

    /// <summary>판 세이브와 기록: 세이브 폴더의 run.json, profile.json. 확인용 시작(run:*)이면 임시 폴더를 쓴다.</summary>
    private RunSession CreateRunSession(GameData data)
    {
        var directory = _options.StartScene?.StartsWith("run:") == true ? Path.Combine(Path.GetTempPath(), $"triangle-demo-{Guid.NewGuid():N}")
            : _options.SaveDirectory;
        var store = new RunStore(directory is null ? RunStore.DefaultPath : Path.Combine(directory, "run.json"));
        var profiles = new ProfileStore(directory is null ? ProfileStore.DefaultPath : Path.Combine(directory, "profile.json"));
        var loaded = store.Load(data);
        var profile = profiles.Load(data);
        var session = new RunSession(data, store, profiles, profile.Profile);
        session.Resume(loaded.Run);
        if (loaded.Status == LoadStatus.Recovered && loaded.Errors?.Any(e => e.StartsWith(Triangle.Core.Runs.Run.ActCountChanged)) == true)
        {
            session.Notice = ($"게임의 막이 {data.Run!.Acts.Count}개로 바뀌어 진행 중이던 원정을 이어 할 수 없습니다. 새로 시작합니다. 원래 파일: {Path.GetFileName(loaded.BrokenFilePath)}",
                Theme.Cover);
        }
        else if (loaded.Status == LoadStatus.Recovered)
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

    private void StartRun(IReadOnlyList<string> classIds, IReadOnlyList<Triangle.Core.Units.Row> rows, int ascension)
    {
        Runs.Start(classIds, rows, ascension);
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

    /// <summary>지점에 들어간다. 전투면 결과를 저장한 뒤 전투 기록을 보여주고, 다 보면 보상(카드가 없으면 지도, 끝났으면 결과)으로 간다.</summary>
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
            () => ShowRun(battle), run.Finished ? "결과로" : run.Phase == RunPhase.Reward ? "보상으로" : "지도로");
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
            "reward" => (RunPhase.Reward, RunNodeKind.Elite),
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
                    var copy = run.UpgradableCopies(data).FirstOrDefault(c => run.WhyCannotTake(data, 0, null, c) is null);
                    if (run.Offer[0].Kind == RunCardKind.Active || member is not null || copy is not null)
                    {
                        run.Take(data, 0, member, copy);
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
            case "combat":
                EnterNode(run.Choices[0].Id);
                if (_scene is CombatLogScene combat)
                {
                    if (_options.ScreenshotPath is not null)
                    {
                        combat.RevealLines(_options.ScreenshotLines ?? int.MaxValue);
                    }

                    if (_options.StartInSummary)
                    {
                        combat.ShowSummary();
                    }
                }

                return _scene;
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
                // 강타 사본은 상점에서 강화 둘(위력·연쇄)을 건 것으로 두어 강화된 이름과 수치가 보이게 한다.
                var strike = RunUpgrades.Id("heavy_strike", [RunUpgradeKind.Power, RunUpgradeKind.Chain]);
                run.Copies[strike] = 1;
                run.SetTactics(data, knight, [.. knight.Tactics, Tactic.Create(9, [new(Condition.SelfHpAtLeast, 50)], strike),
                    Tactic.Create(9, [new(Condition.Always, 0)], "focus")]);
                var editor = new RunEditorScene(_ui, Runs, Bounds, back: () => ShowRun());
                editor.Show(view);
                return editor;
        }

        return RunSceneFor(phase == RunPhase.Reward ? battle : null);
    }

    /// <summary>창을 닫으면 판을 저장하고 끝낸다 (판은 바뀔 때마다 저장하므로 보통은 이미 저장되어 있다).</summary>
    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        if (_options.ScreenshotPath is null)
        {
            _runs?.Save();
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
        UpdateMusic(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Theme.Background);
        _scene.Draw(_spriteBatch);
        DrawVolume();

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
        _music?.Dispose();
        _ui.Dispose();
        _spriteBatch.Dispose();
        base.UnloadContent();
    }
}
