using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Triangle.Core.Data;
using Triangle.Desktop.Rendering;
using Triangle.Desktop.Scenes;

namespace Triangle.Desktop;

public class TriangleGame : Game
{
    private const int Width = 1280;
    private const int Height = 720;

    private readonly GraphicsDeviceManager _graphics;
    private readonly Input _input = new();
    private readonly string? _screenshotPath;
    private readonly int? _screenshotLines;

    private SpriteBatch _spriteBatch = null!;
    private Ui _ui = null!;
    private IScene _scene = null!;

    /// <param name="screenshotPath">지정하면 첫 화면을 PNG에 저장하고 종료한다.</param>
    /// <param name="screenshotLines">스크린샷 전에 진행할 로그 줄 수. null이면 전투 끝까지.</param>
    public TriangleGame(string? screenshotPath = null, int? screenshotLines = null)
    {
        _screenshotPath = screenshotPath;
        _screenshotLines = screenshotLines;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = Width,
            PreferredBackBufferHeight = Height,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Triangle";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _ui = new Ui(GraphicsDevice, Path.Combine(AppContext.BaseDirectory, "Fonts"));
        _scene = CreateFirstScene();
    }

    private IScene CreateFirstScene()
    {
        try
        {
            var data = GameDataLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
            var scene = new CombatLogScene(_ui, data, "training", seed: 1, new Rectangle(0, 0, Width, Height));
            if (_screenshotPath is not null)
            {
                scene.RevealLines(_screenshotLines ?? int.MaxValue);
            }

            return scene;
        }
        catch (GameDataException e)
        {
            return new ErrorScene(_ui, "게임 데이터를 읽지 못했습니다", e.Errors);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.Pressed(Keys.Escape))
        {
            Exit();
        }

        _scene.Update(gameTime, _input);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_screenshotPath is not null)
        {
            SaveScreenshot(_screenshotPath);
            Exit();
            return;
        }

        GraphicsDevice.Clear(Theme.Background);
        _scene.Draw(_spriteBatch);
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
