using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 게임을 켜면 처음 나오는 오프닝: 어둠 속에 서문이 한 줄씩 떠오른 뒤, 삼각형이 그려지고 제목이 나온다.
/// 서문 중에 키를 누르거나 클릭하면 제목으로 건너뛰고, 제목에서 누르면 <paramref name="done"/>으로 넘어간다.
/// 트라이앵글이 무엇인지는 설명하지 않는다 (조금씩 드러내는 것이 설정의 규칙).
/// </summary>
internal sealed class OpeningScene(Ui ui, Rectangle bounds, Action done, bool startAtTitle = false) : IScene
{
    private static readonly string[] Prologue =
    [
        "세상의 한가운데에는 아무도 끝까지 걸어 본 적 없는 땅이 있다.",
        "지도에는 그곳이 세 개의 선으로만 그려져 있다.",
        "사람들은 그 땅을 트라이앵글이라 부른다.",
        "",
        "안으로 들어간 목소리는 곧 끊긴다. 명령도, 구조 요청도 닿지 않는다.",
        "그래서 지휘관은 떠나기 전에 모든 것을 정해 둔다.",
        "누가 앞에 서고, 누가 뒤를 지키며, 무엇부터 휘두를지.",
        "",
        "그리고 부대를 들여보낸다.",
    ];

    /// <summary>서문 한 줄이 나오는 간격과 떠오르는 데 걸리는 시간 (초).</summary>
    private const float LineInterval = 2.6f;
    private const float LineFade = 1.4f;

    /// <summary>빈 줄은 짧게 쉰다.</summary>
    private const float PauseInterval = 1.2f;

    /// <summary>마지막 줄 뒤에 머무는 시간과 서문이 사라지는 시간.</summary>
    private const float PrologueHold = 3f;
    private const float PrologueFadeOut = 1.6f;

    /// <summary>제목 화면: 삼각형을 다 그리는 시간, 제목이 떠오르는 시간.</summary>
    private const float TriangleDraw = 2.8f;
    private const float TitleFade = 1.5f;

    private const int EmberCount = 60;

    private readonly float[] _lineStarts = LineStarts();
    private readonly Ember[] _embers = CreateEmbers(bounds);
    private bool _title = startAtTitle;

    /// <summary>지금 단계(서문 또는 제목)에 들어온 뒤 지난 시간. 제목부터 시작하면 다 그려진 상태로 시작한다.</summary>
    private float _time = startAtTitle ? TriangleDraw + TitleFade : 0f;

    private static float PrologueEnd => LineStarts()[^1] + LineFade + PrologueHold;

    private static float[] LineStarts()
    {
        var starts = new float[Prologue.Length];
        var t = 0.8f;
        for (var i = 0; i < Prologue.Length; i++)
        {
            starts[i] = t;
            t += Prologue[i].Length == 0 ? PauseInterval : LineInterval;
        }

        return starts;
    }

    public void Update(GameTime gameTime, Input input)
    {
        var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _time += elapsed;
        foreach (ref var ember in _embers.AsSpan())
        {
            ember.Update(elapsed, bounds);
        }

        var pressed = input.AnyKeyPressed || input.Clicked;
        if (!_title)
        {
            if (pressed || _time >= PrologueEnd + PrologueFadeOut)
            {
                _title = true;
                _time = pressed ? TriangleDraw * 0.6f : 0f;
            }

            return;
        }

        if (pressed)
        {
            // 그리는 중에 누르면 먼저 다 그리고, 다 그린 뒤에 누르면 넘어간다.
            if (_time < TriangleDraw + TitleFade)
            {
                _time = TriangleDraw + TitleFade;
            }
            else
            {
                done();
            }
        }
    }

    public void Draw(SpriteBatch batch)
    {
        batch.Begin(transformMatrix: ui.Screen.Transform);
        ui.Fill(batch, bounds, Theme.Background);
        var emberAlpha = _title ? 1f : Math.Clamp(_time / 6f, 0f, 0.6f);
        foreach (var ember in _embers)
        {
            ember.Draw(ui, batch, bounds, emberAlpha);
        }

        if (_title)
        {
            DrawTitle(batch);
        }
        else
        {
            DrawPrologue(batch);
        }

        batch.End();
    }

    private void DrawPrologue(SpriteBatch batch)
    {
        var font = ui.Font(22);
        const float lineHeight = 40f;
        var y = bounds.Center.Y - Prologue.Length * lineHeight / 2f;
        var fadeOut = 1f - Math.Clamp((_time - PrologueEnd) / PrologueFadeOut, 0f, 1f);
        var newest = Array.FindLastIndex(_lineStarts, start => start <= _time);
        for (var i = 0; i < Prologue.Length; i++, y += lineHeight)
        {
            var appear = Math.Clamp((_time - _lineStarts[i]) / LineFade, 0f, 1f);
            if (appear <= 0f || Prologue[i].Length == 0)
            {
                continue;
            }

            // 막 떠오른 줄은 밝게, 지나간 줄은 조금 어둡게.
            var color = Color.Lerp(Theme.TextDim, Theme.Text, i == newest ? 1f : 0.35f);
            var size = font.MeasureString(Prologue[i]);
            var position = new Vector2(bounds.Center.X - size.X / 2f, y + (1f - appear) * 6f);
            ui.Text(batch, font, Prologue[i], position, color * (appear * fadeOut));
        }

        var hint = ui.Font(14);
        const string skip = "아무 키나 누르면 건너뜁니다";
        ui.Text(batch, hint, skip, new Vector2(bounds.Right - 24 - hint.MeasureString(skip).X, bounds.Bottom - 36),
            Theme.TextDim * (0.5f * Math.Clamp(_time - 2f, 0f, 1f) * fadeOut));
    }

    private void DrawTitle(SpriteBatch batch)
    {
        // 위로 꼭짓점이 난 정삼각형. 위 꼭짓점에서 시계 방향으로 한 변씩 그려 나간다.
        var center = new Vector2(bounds.Center.X, bounds.Center.Y - 70);
        const float radius = 150f;
        var corners = Enumerable.Range(0, 3)
            .Select(i => center + radius * new Vector2(MathF.Cos(-MathHelper.PiOver2 + i * MathHelper.TwoPi / 3), MathF.Sin(-MathHelper.PiOver2 + i * MathHelper.TwoPi / 3)))
            .ToArray();
        var drawn = Math.Clamp(_time / TriangleDraw, 0f, 1f) * 3f;
        var pulse = 0.75f + 0.25f * MathF.Sin(_time * 1.6f);
        for (var side = 0; side < 3; side++)
        {
            var part = Math.Clamp(drawn - side, 0f, 1f);
            if (part <= 0f)
            {
                break;
            }

            var from = corners[side];
            var to = Vector2.Lerp(from, corners[(side + 1) % 3], part);
            ui.Line(batch, from, to, Theme.Accent * 0.35f, 9f);
            ui.Line(batch, from, to, Theme.Gold * pulse, 2.5f);
        }

        // 다 그리면 꼭짓점이 밝아진다.
        var complete = Math.Clamp((_time - TriangleDraw) / 0.6f, 0f, 1f);
        foreach (var corner in corners)
        {
            ui.Fill(batch, new Rectangle((int)corner.X - 4, (int)corner.Y - 4, 8, 8), Theme.Holy * complete);
        }

        var titleAlpha = Math.Clamp((_time - TriangleDraw) / TitleFade, 0f, 1f);
        var titleFont = ui.BoldFont(64);
        const string title = "TRIANGLE";
        var spaced = string.Join(" ", title.ToCharArray());
        var titleSize = titleFont.MeasureString(spaced);
        var titleY = center.Y + radius * 0.5f + 70;
        ui.Text(batch, titleFont, spaced, new Vector2(bounds.Center.X - titleSize.X / 2f, titleY), Theme.Text * titleAlpha);

        var subFont = ui.Font(20);
        const string subtitle = "세 개의 선 너머로";
        var subSize = subFont.MeasureString(subtitle);
        ui.Text(batch, subFont, subtitle, new Vector2(bounds.Center.X - subSize.X / 2f, titleY + titleSize.Y + 20), Theme.Gold * (titleAlpha * 0.85f));

        var prompt = Math.Clamp((_time - TriangleDraw - TitleFade) / 0.8f, 0f, 1f);
        if (prompt > 0f)
        {
            var promptFont = ui.Font(18);
            const string start = "아무 키나 눌러 시작";
            var blink = 0.55f + 0.45f * MathF.Sin(_time * 3f);
            var promptSize = promptFont.MeasureString(start);
            ui.Text(batch, promptFont, start, new Vector2(bounds.Center.X - promptSize.X / 2f, bounds.Bottom - 90), Theme.TextDim * (prompt * blink));
        }
    }

    private static Ember[] CreateEmbers(Rectangle bounds)
    {
        var random = new Random(7);
        return Enumerable.Range(0, EmberCount).Select(_ => Ember.Random(random, bounds, anywhere: true)).ToArray();
    }

    /// <summary>바닥에서 천천히 떠오르며 흔들리는 불티.</summary>
    private struct Ember
    {
        private Vector2 _position;
        private float _speed;
        private float _sway;
        private float _phase;
        private float _brightness;
        private int _size;

        public static Ember Random(Random random, Rectangle bounds, bool anywhere) => new()
        {
            _position = new Vector2(random.Next(bounds.Left, bounds.Right),
                anywhere ? random.Next(bounds.Top, bounds.Bottom) : bounds.Bottom + random.Next(0, 40)),
            _speed = 10f + random.NextSingle() * 25f,
            _sway = 6f + random.NextSingle() * 14f,
            _phase = random.NextSingle() * MathHelper.TwoPi,
            _brightness = 0.25f + random.NextSingle() * 0.6f,
            _size = random.Next(0, 3) == 0 ? 3 : 2,
        };

        public void Update(float elapsed, Rectangle bounds)
        {
            _position.Y -= _speed * elapsed;
            _phase += elapsed;
            if (_position.Y < bounds.Top - 10)
            {
                this = Random(System.Random.Shared, bounds, anywhere: false);
            }
        }

        public readonly void Draw(Ui ui, SpriteBatch batch, Rectangle bounds, float alpha)
        {
            var x = _position.X + MathF.Sin(_phase * 0.9f) * _sway;
            // 위로 올라갈수록 사그라든다.
            var fade = Math.Clamp((_position.Y - bounds.Top) / bounds.Height, 0.15f, 1f);
            ui.Fill(batch, new Rectangle((int)x, (int)_position.Y, _size, _size), Theme.Damage * (_brightness * fade * alpha));
        }
    }
}
