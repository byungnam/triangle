using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Triangle.Desktop.Rendering;

/// <summary>사각형, 막대, 글자를 그리는 공용 도구. 한글은 FontStashSharp로 필요한 글자만 그린다.</summary>
internal sealed class Ui : IDisposable
{
    private readonly Texture2D _pixel;
    private readonly FontSystem _regular;
    private readonly FontSystem _bold;

    /// <param name="fontResolution">글자를 이 배수로 크게 새겨 두었다가 줄여 그린다. 화면을 키워도 글자가 흐려지지 않도록 화면 배율 이상으로 준다.</param>
    public Ui(GraphicsDevice device, Screen screen, string fontDirectory, float fontResolution)
    {
        Screen = screen;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData([Color.White]);

        _regular = LoadFont(Path.Combine(fontDirectory, "NanumGothic-Regular.ttf"), fontResolution);
        _bold = LoadFont(Path.Combine(fontDirectory, "NanumGothic-Bold.ttf"), fontResolution);
    }

    public Screen Screen { get; }

    public SpriteFontBase Font(int size) => _regular.GetFont(size);

    public SpriteFontBase BoldFont(int size) => _bold.GetFont(size);

    public void Fill(SpriteBatch batch, Rectangle rect, Color color) => batch.Draw(_pixel, rect, color);

    public void Panel(SpriteBatch batch, Rectangle rect)
    {
        Fill(batch, rect, Theme.PanelBorder);
        rect.Inflate(-1, -1);
        Fill(batch, rect, Theme.Panel);
    }

    public void Bar(SpriteBatch batch, Rectangle rect, int value, int max, Color color)
    {
        Fill(batch, rect, Theme.BarBack);
        if (max > 0 && value > 0)
        {
            Fill(batch, rect with { Width = (int)((long)rect.Width * value / max) }, color);
        }
    }

    public void Text(SpriteBatch batch, SpriteFontBase font, string text, Vector2 position, Color color) =>
        batch.DrawString(font, text, position, color);

    public void Dispose()
    {
        _pixel.Dispose();
        _regular.Dispose();
        _bold.Dispose();
    }

    private static FontSystem LoadFont(string path, float resolution)
    {
        var system = new FontSystem(new FontSystemSettings
        {
            FontResolutionFactor = resolution,
            TextureWidth = 2048,
            TextureHeight = 2048,
        });
        system.AddFont(File.ReadAllBytes(path));
        return system;
    }
}
