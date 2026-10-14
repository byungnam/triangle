using Microsoft.Xna.Framework;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;

namespace Triangle.Desktop.Rendering;

/// <summary>
/// 설계 해상도(1280×720)와 실제 창 크기 사이의 변환.
/// 화면은 모두 설계 해상도 좌표로 배치하고, 그릴 때 창에 맞게 같은 비율로 키운다 (QHD 2560×1440이면 2배).
/// 창의 가로세로 비율이 16:9가 아니면 남는 쪽을 배경색으로 비워 가운데에 놓는다.
/// </summary>
internal sealed class Screen
{
    public const int Width = 1280;
    public const int Height = 720;

    private readonly Func<Rectangle> _myraBounds;

    public Screen() => _myraBounds = () => new Rectangle(Offset.X, Offset.Y, Width, Height);

    /// <summary>설계 해상도 좌표의 화면 전체.</summary>
    public static Rectangle Bounds => new(0, 0, Width, Height);

    public float Scale { get; private set; } = 1f;

    /// <summary>창에서 화면이 시작하는 위치 (픽셀).</summary>
    public Point Offset { get; private set; }

    /// <summary>SpriteBatch.Begin에 넘기는 설계 좌표 → 창 좌표 변환.</summary>
    public Matrix Transform { get; private set; } = Matrix.Identity;

    /// <summary>창 크기가 바뀌었다.</summary>
    public void Resize(int windowWidth, int windowHeight)
    {
        Scale = Math.Max(0.1f, Math.Min(windowWidth / (float)Width, windowHeight / (float)Height));
        Offset = new Point((int)((windowWidth - Width * Scale) / 2), (int)((windowHeight - Height * Scale) / 2));
        Transform = Matrix.CreateScale(Scale, Scale, 1f) * Matrix.CreateTranslation(Offset.X, Offset.Y, 0f);
    }

    /// <summary>창 좌표(마우스 위치)를 설계 해상도 좌표로 바꾼다.</summary>
    public Vector2 ToDesign(Point window) => new((window.X - Offset.X) / Scale, (window.Y - Offset.Y) / Scale);

    /// <summary>Myra 화면을 같은 변환으로 그린다 (마우스 위치도 Myra가 같은 변환으로 되돌린다).</summary>
    public void Render(MyraDesktop desktop)
    {
        desktop.BoundsFetcher = _myraBounds;
        desktop.TransformOrigin = Vector2.Zero; // 기본값은 가운데 기준 확대라 왼쪽 위 기준으로 바꾼다
        desktop.Scale = new Vector2(Scale);
        desktop.Render();
    }
}
