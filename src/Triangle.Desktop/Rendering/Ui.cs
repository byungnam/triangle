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
    private readonly GraphicsDevice _device;
    private readonly string _iconDirectory;
    private readonly Dictionary<string, Texture2D?> _icons = [];

    /// <param name="fontResolution">글자를 이 배수로 크게 새겨 두었다가 줄여 그린다. 화면을 키워도 글자가 흐려지지 않도록 화면 배율 이상으로 준다.</param>
    public Ui(GraphicsDevice device, Screen screen, string fontDirectory, float fontResolution)
    {
        Screen = screen;
        _device = device;
        _iconDirectory = Path.Combine(Path.GetDirectoryName(fontDirectory)!, "Icons");
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData([Color.White]);

        _regular = LoadFont(Path.Combine(fontDirectory, "NanumGothic-Regular.ttf"), fontResolution);
        _bold = LoadFont(Path.Combine(fontDirectory, "NanumGothic-Bold.ttf"), fontResolution);
    }

    public Screen Screen { get; }

    public SpriteFontBase Font(int size) => _regular.GetFont(size);

    public SpriteFontBase BoldFont(int size) => _bold.GetFont(size);

    public void Fill(SpriteBatch batch, Rectangle rect, Color color) => batch.Draw(_pixel, rect, color);

    /// <summary>테두리 장식이 있는 판: 어두운 바깥선, 청동 테두리, 안쪽 어두운 선, 모서리 금색 장식.</summary>
    public void Panel(SpriteBatch batch, Rectangle rect)
    {
        Fill(batch, rect, Theme.BarBack);
        rect.Inflate(-1, -1);
        Fill(batch, rect, Theme.PanelBorder);
        rect.Inflate(-2, -2);
        Fill(batch, rect, Theme.BarBack);
        rect.Inflate(-1, -1);
        Fill(batch, rect, Theme.Panel);

        // 모서리 장식
        const int size = 7;
        rect.Inflate(4, 4);
        foreach (var corner in new[]
                 {
                     new Point(rect.Left - 1, rect.Top - 1), new Point(rect.Right - size + 1, rect.Top - 1),
                     new Point(rect.Left - 1, rect.Bottom - size + 1), new Point(rect.Right - size + 1, rect.Bottom - size + 1),
                 })
        {
            Fill(batch, new Rectangle(corner.X, corner.Y, size, size), Theme.BarBack);
            Fill(batch, new Rectangle(corner.X + 1, corner.Y + 1, size - 2, size - 2), Theme.Gold);
            Fill(batch, new Rectangle(corner.X + 3, corner.Y + 3, 1, 1), Theme.BarBack);
        }
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

    /// <summary>
    /// 흰색 아이콘 (Icons/{name}.png, 그릴 때 색을 입힌다). 없으면 null.
    /// PNG는 알파를 곱하지 않은 값이라, SpriteBatch 기본 섞기에 맞게 불러올 때 곱해 둔다.
    /// </summary>
    public Texture2D? Icon(string name)
    {
        if (_icons.TryGetValue(name, out var cached))
        {
            return cached;
        }

        var path = Path.Combine(_iconDirectory, name + ".png");
        Texture2D? texture = null;
        if (File.Exists(path))
        {
            texture = Texture2D.FromFile(_device, path);
            var pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.FromNonPremultiplied(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);
            }

            texture.SetData(pixels);
        }

        _icons[name] = texture;
        return texture;
    }

    /// <summary>아이콘을 사각형에 맞춰 색을 입혀 그린다.</summary>
    public void DrawIcon(SpriteBatch batch, string name, Rectangle rect, Color color)
    {
        if (Icon(name) is { } texture)
        {
            batch.Draw(texture, rect, color);
        }
    }

    public void Dispose()
    {
        foreach (var icon in _icons.Values)
        {
            icon?.Dispose();
        }

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
