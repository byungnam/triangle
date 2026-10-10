using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI.Styles;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Triangle.Desktop.Rendering;
using Triangle.Desktop.Scenes;

namespace Triangle.Desktop;

/// <param name="ScreenshotPath">지정하면 첫 화면을 PNG에 저장하고 종료한다.</param>
/// <param name="ScreenshotLines">전투 화면 스크린샷 전에 진행할 로그 줄 수. null이면 전투 끝까지.</param>
/// <param name="StartInCombat">마을 대신 전투 화면으로 시작한다 (출전 명단과 훈련 부대의 시험 전투, 상태는 바꾸지 않는다).</param>
/// <param name="SavePath">세이브 파일 경로. null이면 OS별 기본 위치.</param>
public sealed record LaunchOptions(
    string? ScreenshotPath = null, int? ScreenshotLines = null, bool StartInCombat = false, string? SavePath = null);

/// <summary>
/// 화면 흐름: 마을 ↔ (전술 편집, 숙련·패시브, 모집, 상점·창고, 제작), 마을 → 출정 → 원정 ↔ (전투 기록, 전술 편집) → 귀환 → 마을.
/// 원정 중에는 전투가 끝날 때마다, 출정과 귀환 때 자동 저장하고, 종료하면 항상 저장한다.
/// 마을에서는 수동 저장이고, 저장하지 않고 종료하면 확인 창을 띄운다.
/// </summary>
public class TriangleGame : Game
{
    private const int Width = 1280;
    private const int Height = 720;
    private const string TestEncounter = "training";

    private readonly GraphicsDeviceManager _graphics;
    private readonly Input _input = new();
    private readonly LaunchOptions _options;

    private SpriteBatch _spriteBatch = null!;
    private Ui _ui = null!;
    private IScene _scene = null!;
    private GameSession? _session;
    private VillageScene _village = null!;
    private ExpeditionScene _expedition = null!;
    private TacticEditorScene _editor = null!;
    private int _framesDrawn;
    private bool _quitConfirmed;

    public TriangleGame(LaunchOptions options)
    {
        _options = options;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = Width,
            PreferredBackBufferHeight = Height,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Triangle";
    }

    private static Rectangle Bounds => new(0, 0, Width, Height);

    private GameSession Session => _session!;
    private GameData Data => Session.Data;
    private Company Company => Session.Company;

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _ui = new Ui(GraphicsDevice, Path.Combine(AppContext.BaseDirectory, "Fonts"));
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

        var store = new SaveStore(_options.SavePath ?? SaveStore.DefaultPath);
        var loaded = store.Load(data, () => StartingCompany.Create(data, Random.Shared.Next()));
        _session = new GameSession(data, loaded.Company, store) { Notice = LoadNotice(loaded) };

        _village = new VillageScene(_ui, Session, Bounds, OpenEditor, OpenRecruit, OpenShop, OpenCraft, Depart, ConfirmedExit);
        _expedition = new ExpeditionScene(_ui, Session, Bounds, NextBattle);
        _editor = new TacticEditorScene(_ui, Session, Bounds, OpenTraining, ShowHome);

        if (!_options.StartInCombat || Company.Lineup.Count == 0)
        {
            return Home;
        }

        var result = CombatSimulator.Run(
            Company.Expedition is null ? Company.LineupSetups(Data) : ExpeditionRules.AllySetups(Company, Data),
            Data.CreateEncounterTeam(TestEncounter), Data.Catalog, seed: 1);
        var combat = new CombatLogScene(_ui, Data, result, TestEncounter, "시험 전투", Bounds, ShowHome, "돌아가기");
        if (_options.ScreenshotPath is not null)
        {
            combat.RevealLines(_options.ScreenshotLines ?? int.MaxValue);
        }

        return combat;
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
        _scene = new MasteryScene(_ui, Data, member, Bounds, back: OpenEditor, changed: Session.MarkChanged);

    private void OpenRecruit() => _scene = new RecruitScene(_ui, Session, Bounds, back: ShowHome);

    private void OpenShop() => _scene = new ShopScene(_ui, Session, Bounds, back: ShowHome);

    private void OpenCraft() => _scene = new CraftScene(_ui, Session, Bounds, back: ShowHome);

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
        var title = $"전투 {expedition.BattleIndex + 1}/{zone.MaxBattles}";

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

    private void SaveScreenshot(string path)
    {
        using var target = new RenderTarget2D(GraphicsDevice, Width, Height);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Theme.Background);
        _scene.Draw(_spriteBatch);
        GraphicsDevice.SetRenderTarget(null);

        using var file = File.Create(path);
        target.SaveAsPng(file, Width, Height);
    }

    protected override void UnloadContent()
    {
        _ui.Dispose();
        _spriteBatch.Dispose();
        base.UnloadContent();
    }
}
