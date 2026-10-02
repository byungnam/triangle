using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace Triangle.Desktop.Rendering;

/// <summary>Myra 위젯을 이 게임의 글꼴과 색으로 만드는 공용 도우미.</summary>
internal sealed class Widgets(Ui ui)
{
    public Label Label(string text, int size, Color color, bool bold = false, int? width = null) => new()
    {
        Text = text,
        Font = bold ? ui.BoldFont(size) : ui.Font(size),
        TextColor = color,
        Width = width,
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
}
