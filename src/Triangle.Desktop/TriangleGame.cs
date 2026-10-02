using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI.Styles;
using Triangle.Core.Data;
using Triangle.Core.Progress;
using Triangle.Desktop.Rendering;
using Triangle.Desktop.Scenes;

namespace Triangle.Desktop;

/// <param name="ScreenshotPath">지정하면 첫 화면을 PNG에 저장하고 종료한다.</param>
/// <param name="ScreenshotLines">전투 화면 스크린샷 전에 진행할 로그 줄 수. null이면 전투 끝까지.</param>
/// <param name="StartInCombat">전술 편집 대신 전투 화면으로 시작한다.</param>
/// <param name="SavePath">세이브 파일 경로. null이면 OS별 기본 위치.</param>
public sealed record LaunchOptions(
    string? ScreenshotPath = null, int? ScreenshotLines = null, bool StartInCombat = false, string? SavePath = null);

public class TriangleGame : Game
{
    private const int Width = 1280;
    private const int Height = 720;
    private const string DefaultEncounter = "training";

    private readonly GraphicsDeviceManager _graphics;
    private readonly Input _input = new();
    private readonly LaunchOptions _options;

    private SpriteBatch _spriteBatch = null!;
    private Ui _ui = null!;
    private IScene _scene = null!;
    private GameData _data = null!;
    private Party _party = null!;
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
        try
        {
            _data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        }
        catch (GameDataException e)
        {
            return new ErrorScene(_ui, "게임 데이터를 읽지 못했습니다", e.Errors);
        }

        var store = new SaveStore(_options.SavePath ?? SaveStore.DefaultPath);
        var loaded = store.Load(_data, DemoParty.Create);
        _party = loaded.Party;
        _editor = new TacticEditorScene(_ui, _data, _party, store, Bounds, StartCombat, ConfirmedExit, LoadNotice(loaded));

        if (!_options.StartInCombat)
        {
            return _editor;
        }

        var combat = CreateCombat(DefaultEncounter);
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

    private CombatLogScene CreateCombat(string encounterId) =>
        new(_ui, _data, _party.ToCombatantSetups(_data), encounterId, seed: 1, Bounds, back: () => _scene = _editor);

    private void StartCombat(string encounterId) => _scene = CreateCombat(encounterId);

    /// <summary>편집 화면이 종료를 확인했다 (저장했거나 버리기로 했다).</summary>
    private void ConfirmedExit()
    {
        _quitConfirmed = true;
        Exit();
    }

    /// <summary>
    /// 창 닫기 버튼 등으로 종료할 때, 저장하지 않은 변경이 있으면 종료를 취소하고
    /// 편집 화면에서 확인 창을 띄운다.
    /// </summary>
    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        if (!_quitConfirmed && _options.ScreenshotPath is null && _editor is { HasUnsavedChanges: true })
        {
            args.Cancel = true;
            _scene = _editor;
            _editor.RequestQuit();
            return;
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
