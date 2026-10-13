using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace Triangle.Desktop.Rendering;

/// <summary>Myra 위젯을 이 게임의 글꼴과 색으로 만드는 공용 도우미.</summary>
internal sealed class Widgets(Ui ui)
{
    /// <param name="wrap">참이면 넓이(<paramref name="width"/>)를 넘는 글을 줄바꿈한다.</param>
    public Label Label(string text, int size, Color color, bool bold = false, int? width = null, bool wrap = false) => new()
    {
        Text = text,
        Font = bold ? ui.BoldFont(size) : ui.Font(size),
        TextColor = color,
        Width = width,
        Wrap = wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public ComboView Combo(IEnumerable<(string Text, Color Color)> items, int selectedIndex, int width)
    {
        var combo = new ComboView { Width = width, DropdownMaximumHeight = 320 };
        foreach (var (text, color) in items)
        {
            combo.Widgets.Add(new Label
            {
                Text = text,
                Font = ui.Font(17),
                TextColor = color,
                Padding = new Thickness(8, 4),
            });
        }

        combo.SelectedIndex = Math.Max(0, selectedIndex);
        return combo;
    }

    public Button TextButton(string text, Color background, Color hover, bool bold = false, int size = 17)
    {
        var label = Label(text, size, Theme.Text, bold);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        var button = StyledButton(label, background, hover);
        button.Padding = new Thickness(12, 6);
        return button;
    }

    public static Button StyledButton(Widget content, Color background, Color hover) => new()
    {
        Content = content,
        Background = new SolidBrush(background),
        OverBackground = new SolidBrush(hover),
        PressedBackground = new SolidBrush(Theme.ButtonPressed),
        DisabledBackground = new SolidBrush(Theme.BarBack),
    };

    /// <summary>설명 카드: 줄마다 라벨, 넓이를 넘으면 줄바꿈한다.</summary>
    public Widget Card(IEnumerable<TextLine> lines, int width, Color? background = null)
    {
        var panel = new VerticalStackPanel
        {
            Spacing = 4,
            Width = width,
            Padding = new Thickness(12, 10),
            Background = new SolidBrush(background ?? Theme.BarBack),
        };
        foreach (var line in lines)
        {
            var label = Label(line.Text, line.Size, line.Color, line.Bold);
            label.Wrap = true;
            label.Width = width - 24;
            panel.Widgets.Add(label);
        }

        return panel;
    }

    /// <summary>색 바탕의 작은 꼬리표, 예: "전위".</summary>
    public Widget Chip(string text, Color background, int size = 14, Color? textColor = null)
    {
        var label = Label(text, size, textColor ?? Theme.Text, bold: true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return new Panel
        {
            Background = new SolidBrush(background),
            Padding = new Thickness(8, 2),
            Widgets = { label },
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>진행 막대 (값/최대).</summary>
    public static Widget Bar(int value, int max, int width, int height, Color color)
    {
        var fill = max <= 0 ? 0 : (int)Math.Clamp((long)width * value / max, 0, width);
        var bar = new Panel { Width = width, Height = height, Background = new SolidBrush(Theme.BarBack), VerticalAlignment = VerticalAlignment.Center };
        if (fill > 0)
        {
            bar.Widgets.Add(new Panel { Width = fill, Height = height, Background = new SolidBrush(color), HorizontalAlignment = HorizontalAlignment.Left });
        }

        return bar;
    }
}
