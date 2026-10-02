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
public sealed record LaunchOptions(string? ScreenshotPath = null, int? ScreenshotLines = null, bool StartInCombat = false);

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

        _party = DemoParty.Create();
        _editor = new TacticEditorScene(_ui, _data, _party, Bounds, StartCombat, Exit);

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

    private CombatLogScene CreateCombat(string encounterId) =>
        new(_ui, _data, _party.ToCombatantSetups(_data), encounterId, seed: 1, Bounds, back: () => _scene = _editor);

    private void StartCombat(string encounterId) => _scene = CreateCombat(encounterId);

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
