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

    public Ui(GraphicsDevice device, string fontDirectory)
    {
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData([Color.White]);

        _regular = LoadFont(Path.Combine(fontDirectory, "NanumGothic-Regular.ttf"));
        _bold = LoadFont(Path.Combine(fontDirectory, "NanumGothic-Bold.ttf"));
    }

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

    private static FontSystem LoadFont(string path)
    {
        var system = new FontSystem();
        system.AddFont(File.ReadAllBytes(path));
        return system;
    }
}
