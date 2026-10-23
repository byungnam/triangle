using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using Triangle.Core.Data;
using Triangle.Core.Runs;
using Triangle.Desktop.Rendering;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 한 판(로그라이트 재편) 화면의 공통 틀: 위에 제목과 막·골드, 아래에 도움말과 안내, 그 사이는 화면마다 그린다.
/// 판을 바꾸는 조작은 <see cref="Act"/>로 한다: 안내를 지우고, 바꾸고, 저장하고, 다음 화면을 고른다.
/// </summary>
internal abstract class RunScene : IScene
{
    protected const int Margin = 24;
    protected const int HeaderHeight = 64;
    protected const int FooterHeight = 40;

    private readonly MyraDesktop _desktop = new();
    private readonly Action _changed;
    private bool _dirty = true;

    /// <param name="changed">판이 바뀌었다. 저장한 뒤 판의 단계에 맞는 화면으로 간다.</param>
    protected RunScene(Ui ui, RunSession session, Rectangle bounds, Action changed)
    {
        Ui = ui;
        W = new Widgets(ui);
        Session = session;
        Bounds = bounds;
        _changed = changed;
    }

    protected Ui Ui { get; }

    protected Widgets W { get; }

    protected RunSession Session { get; }

    protected Rectangle Bounds { get; }

    protected GameData Data => Session.Data;

    protected Run Run => Session.Run!;

    protected abstract string Title { get; }

    /// <summary>아래 줄 왼쪽의 도움말.</summary>
    protected virtual string Help => "";

    /// <summary>제목 오른쪽: 기본은 막과 골드.</summary>
    protected virtual string? Status => Session.Run is { Finished: false } run
        ? $"{RunText.Act(run, Data)}{(run.Chart.IsBase ? "" : $" · {run.Chart.Name(Data)} 위험도 {run.Chart.Risk(Data)}")}     골드 {run.Gold}"
        : null;

    /// <summary>제목과 아래 줄 사이의 영역.</summary>
    protected Rectangle Body => new(Bounds.Left + Margin, Bounds.Top + HeaderHeight, Bounds.Width - Margin * 2, Bounds.Height - HeaderHeight - FooterHeight - 8);

    /// <summary>위젯을 다시 만든다 (다음 프레임에).</summary>
    protected void Refresh() => _dirty = true;

    /// <summary>판을 바꾼다: 안내를 지우고 <paramref name="change"/>한 뒤 저장하고 다음 화면으로 간다.</summary>
    protected void Act(Action change)
    {
        Session.Notice = null;
        change();
        _changed();
    }

    /// <summary>할 수 없는 이유를 아래 줄에 보여준다.</summary>
    protected void Refuse(string reason)
    {
        Session.Notice = (reason, Theme.Enemy);
        Refresh();
    }

    /// <summary>화면 전체의 위젯.</summary>
    protected abstract Widget Build();

    /// <summary>위젯 아래에 직접 그리는 것 (판, 지도 등).</summary>
    protected virtual void DrawBack(SpriteBatch batch)
    {
    }

    protected virtual void OnEscape()
    {
    }

    protected virtual void OnUpdate(Input input)
    {
    }

    /// <summary>위에 뜨는 창을 띄운다 (다시 만들 때마다 <see cref="Build"/> 뒤에 더한다).</summary>
    protected virtual Widget? BuildPopup() => null;

    public virtual void Update(GameTime gameTime, Input input)
    {
        if (input.Pressed(Keys.Escape))
        {
            OnEscape();
        }

        OnUpdate(input);
        if (_dirty)
        {
            Rebuild();
        }
    }

    public void Draw(SpriteBatch batch)
    {
        if (_dirty)
        {
            Rebuild();
        }

        batch.Begin(transformMatrix: Ui.Screen.Transform);
        Ui.Text(batch, Ui.BoldFont(28), Title, new Vector2(Bounds.Left + Margin, Bounds.Top + 18), Theme.Text);
        if (Status is { } status)
        {
            var font = Ui.BoldFont(22);
            Ui.Text(batch, font, status, new Vector2(Bounds.Right - Margin - font.MeasureString(status).X, Bounds.Top + 24), Theme.Cover);
        }

        DrawBack(batch);
        // 알림이 있으면 도움말 대신 보여 준다 (둘이 길면 겹친다). 알림은 다음 행동 때 사라진다.
        var footer = Bounds.Bottom - FooterHeight + 10;
        if (Session.Notice is { } notice)
        {
            Ui.Text(batch, Ui.Font(16), notice.Text, new Vector2(Bounds.Left + Margin, footer), notice.Color);
        }
        else
        {
            Ui.Text(batch, Ui.Font(16), Help, new Vector2(Bounds.Left + Margin, footer), Theme.TextDim);
        }

        batch.End();
        Ui.Screen.Render(_desktop);
    }

    private void Rebuild()
    {
        _dirty = false;
        _desktop.Root = Build();
        if (BuildPopup() is { } popup)
        {
            popup.IsModal = true;
            _desktop.Widgets.Add(popup);
        }
    }

    /// <summary>화면 좌표에 놓는 위젯 묶음 (왼쪽 위 기준).</summary>
    protected static T Place<T>(T widget, int left, int top, int? width = null, int? height = null)
        where T : Widget
    {
        widget.Left = left;
        widget.Top = top;
        widget.Width = width ?? widget.Width;
        widget.Height = height ?? widget.Height;
        widget.HorizontalAlignment = HorizontalAlignment.Left;
        widget.VerticalAlignment = VerticalAlignment.Top;
        return widget;
    }

    /// <summary>화면을 어둡게 덮고 가운데에 뜨는 창.</summary>
    protected Panel Overlay(Widget content)
    {
        var overlay = new Panel
        {
            Left = Bounds.Left,
            Top = Bounds.Top,
            Width = Bounds.Width,
            Height = Bounds.Height,
            Background = new Myra.Graphics2D.Brushes.SolidBrush(Color.Black * 0.7f),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        overlay.Widgets.Add(content);
        return overlay;
    }

    /// <summary>테두리와 배경이 있는 세로 묶음 (카드, 판).</summary>
    protected static VerticalStackPanel Box(int width, int? height = null, Color? border = null, Color? background = null, int spacing = 6) => new()
    {
        Spacing = spacing,
        Width = width,
        Height = height,
        Padding = new Myra.Graphics2D.Thickness(12, 10),
        Background = new Myra.Graphics2D.Brushes.SolidBrush(background ?? Theme.Panel),
        Border = new Myra.Graphics2D.Brushes.SolidBrush(border ?? Theme.PanelBorder),
        BorderThickness = new Myra.Graphics2D.Thickness(border is null ? 1 : 2),
    };

    /// <summary>설명 줄들을 라벨로 (넓이를 넘으면 줄바꿈).</summary>
    protected void AddLines(VerticalStackPanel panel, IEnumerable<TextLine> lines, int width)
    {
        foreach (var line in lines)
        {
            panel.Widgets.Add(W.Label(line.Text, line.Size, line.Color, line.Bold, width, wrap: true));
        }
    }

    /// <summary>버튼. 이유가 있으면 누를 수 없고 툴팁으로 이유를 보여준다.</summary>
    protected Button Button(string text, Action click, string? whyNot = null, bool accent = false, int? width = null, int size = 17)
    {
        var button = W.TextButton(text, accent ? Theme.Accent : Theme.Button, accent ? Theme.AccentHover : Theme.ButtonHover, bold: accent, size: size);
        button.Width = width;
        button.Enabled = whyNot is null;
        button.Tooltip = whyNot;
        button.Click += (_, _) => click();
        return button;
    }
}
