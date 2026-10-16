using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Triangle.Core.Progress;
using Triangle.Desktop.Rendering;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 치료소 (2026-10-16): 왼쪽은 시설 레벨과 효과, 다음 레벨 가격과 올리기. 오른쪽은 다친 멤버와 쉬기.
/// 다친 멤버는 다른 멤버로 원정을 다녀오거나 쉬면 낫는다. 다 나을 때까지 출정할 수 없다.
/// </summary>
internal sealed class InfirmaryScene : IScene
{
    private const int Margin = 24;
    private const int HeaderHeight = 64;
    private const int FooterHeight = 40;
    private const int ColumnGap = 24;

    private readonly Ui _ui;
    private readonly Widgets _w;
    private readonly GameSession _session;
    private readonly Rectangle _bounds;
    private readonly Action _back;
    private readonly MyraDesktop _desktop = new();
    private bool _dirty = true;

    public InfirmaryScene(Ui ui, GameSession session, Rectangle bounds, Action back)
    {
        _ui = ui;
        _w = new Widgets(ui);
        _session = session;
        _bounds = bounds;
        _back = back;
    }

    private Company Company => _session.Company;

    private static FacilityDefinition Infirmary => Facilities.Find(Facilities.Infirmary)!;

    public void Update(GameTime gameTime, Input input)
    {
        if (input.Pressed(Keys.Escape) || input.Pressed(Keys.Back))
        {
            _back();
            return;
        }

        if (_dirty)
        {
            Rebuild();
            _dirty = false;
        }
    }

    public void Draw(SpriteBatch batch)
    {
        if (_dirty)
        {
            Rebuild();
            _dirty = false;
        }

        batch.Begin(transformMatrix: _ui.Screen.Transform);
        _ui.Text(batch, _ui.BoldFont(28), "치료소", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        var gold = $"골드 {Company.Gold}";
        var goldFont = _ui.BoldFont(24);
        _ui.Text(batch, goldFont, gold, new Vector2(_bounds.Right - Margin - goldFont.MeasureString(gold).X, _bounds.Top + 22), Theme.Cover);
        const string help = "원정에서 쓰러진 멤버는 다친다. 다른 멤버로 원정을 다녀오거나 쉬면 낫는다.     Esc  마을로";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);
        if (_session.Notice is { } notice)
        {
            var font = _ui.Font(16);
            _ui.Text(batch, font, notice.Text, new Vector2(_bounds.Right - Margin - font.MeasureString(notice.Text).X, _bounds.Bottom - FooterHeight + 10), notice.Color);
        }

        _ui.Panel(batch, LeftArea);
        _ui.Panel(batch, RightArea);
        batch.End();
        _ui.Screen.Render(_desktop);
    }

    private int ContentTop => _bounds.Top + HeaderHeight + 8;
    private int ContentHeight => _bounds.Height - HeaderHeight - FooterHeight - 24;
    private int ColumnWidth => (_bounds.Width - Margin * 2 - ColumnGap) / 2;
    private Rectangle LeftArea => new(_bounds.Left + Margin, ContentTop, ColumnWidth, ContentHeight);
    private Rectangle RightArea => new(LeftArea.Right + ColumnGap, ContentTop, ColumnWidth, ContentHeight);

    private void Rebuild()
    {
        var root = new Panel();
        root.Widgets.Add(Place(BuildFacility(LeftArea.Width - 32), LeftArea));
        root.Widgets.Add(Place(BuildInjured(RightArea.Width - 32), RightArea));
        _desktop.Root = root;
    }

    private static Widget Place(Widget widget, Rectangle area)
    {
        widget.Left = area.X + 16;
        widget.Top = area.Y + 14;
        widget.HorizontalAlignment = HorizontalAlignment.Left;
        widget.VerticalAlignment = VerticalAlignment.Top;
        return widget;
    }

    /// <summary>시설 레벨, 지금 효과, 다음 레벨의 효과와 가격, 올리기.</summary>
    private Widget BuildFacility(int width)
    {
        var panel = new VerticalStackPanel { Spacing = 10, Width = width };
        var level = Company.FacilityLevel(Infirmary.Id);
        panel.Widgets.Add(_w.Label($"{Infirmary.Name}  Lv {level}/{Infirmary.MaxLevel}", 22, Theme.Text, bold: true));
        panel.Widgets.Add(_w.Label(Infirmary.Description, 15, Theme.TextDim, width: width, wrap: true));

        panel.Widgets.Add(_w.Label("지금", 16, Theme.Cover, bold: true));
        panel.Widgets.Add(_w.Label($"원정이 끝날 때 나가지 않은 다친 멤버가 {Company.ExpeditionRecovery}단계 낫는다.", 15, Theme.Text, width: width, wrap: true));
        panel.Widgets.Add(_w.Label(
            $"쉬기 한 번에 {Company.RestRecovery}단계 낫는다. 다친 멤버 한 명당 {Company.RestCostPerInjured}골드.",
            15, Theme.Text, width: width, wrap: true));

        if (level < Infirmary.MaxLevel)
        {
            var next = Infirmary.At(level + 1);
            panel.Widgets.Add(_w.Label($"Lv {level + 1}  ({next.Price}골드)", 16, Theme.Cover, bold: true));
            panel.Widgets.Add(_w.Label(LevelText(next), 15, Theme.Text, width: width, wrap: true));

            var upgrade = _w.TextButton($"Lv {level + 1}로 올리기 ({next.Price}골드)", Theme.Accent, Theme.AccentHover, bold: true);
            upgrade.Width = 280;
            var why = Company.WhyCannotUpgrade(Infirmary.Id);
            upgrade.Enabled = why is null;
            upgrade.Click += (_, _) =>
            {
                if (Company.Upgrade(Infirmary.Id))
                {
                    _session.MarkChanged();
                    _session.Notice = ($"{Infirmary.Name}를 Lv {Company.FacilityLevel(Infirmary.Id)}로 올렸습니다", Theme.Heal);
                    _dirty = true;
                }
            };
            panel.Widgets.Add(upgrade);
            if (why is not null)
            {
                panel.Widgets.Add(_w.Label(why, 15, Theme.Enemy));
            }
        }
        else
        {
            panel.Widgets.Add(_w.Label("최고 레벨입니다.", 16, Theme.TextDim));
        }

        return panel;
    }

    /// <summary>다친 멤버와 낫기까지 남은 원정 수, 쉬기 버튼.</summary>
    private Widget BuildInjured(int width)
    {
        var panel = new VerticalStackPanel { Spacing = 10, Width = width };
        var injured = Company.Roster.Where(m => m.Injured).ToList();
        panel.Widgets.Add(_w.Label($"다친 멤버  {injured.Count}명", 22, injured.Count > 0 ? Theme.Enemy : Theme.Text, bold: true));
        if (injured.Count == 0)
        {
            panel.Widgets.Add(_w.Label("다친 멤버가 없습니다.", 16, Theme.TextDim));
            return panel;
        }

        foreach (var member in injured)
        {
            var expeditions = (member.Injury + Company.ExpeditionRecovery - 1) / Company.ExpeditionRecovery;
            var rests = (member.Injury + Company.RestRecovery - 1) / Company.RestRecovery;
            var row = new VerticalStackPanel { Spacing = 2 };
            row.Widgets.Add(_w.Label($"{member.Name}  ·  부상 {member.Injury}단계", 18, Theme.Text, bold: true));
            row.Widgets.Add(_w.Label($"다른 멤버로 원정 {expeditions}번, 또는 쉬기 {rests}번이면 낫는다.", 15, Theme.TextDim));
            panel.Widgets.Add(row);
        }

        panel.Widgets.Add(new HorizontalSeparator { Margin = new Thickness(0, 6) });
        var cost = Company.RestCost;
        var rest = _w.TextButton($"쉬기 ({cost}골드)", Theme.Accent, Theme.AccentHover, bold: true);
        rest.Width = 200;
        rest.Enabled = Company.WhyCannotRest() is null;
        rest.Click += (_, _) =>
        {
            var recovered = Company.Rest();
            _session.MarkChanged();
            _session.Notice = recovered.Count > 0
                ? ($"쉬었습니다. 다 나음: {string.Join(", ", recovered.Select(m => m.Name))}", Theme.Heal)
                : ("쉬었습니다", Theme.Heal);
            _dirty = true;
        };
        panel.Widgets.Add(rest);
        panel.Widgets.Add(_w.Label(
            Company.Gold >= cost
                ? $"다친 멤버가 모두 {Company.RestRecovery}단계 낫는다."
                : $"골드가 모자라 가진 골드({Company.Gold})를 모두 내고 쉰다. 다친 멤버가 모두 {Company.RestRecovery}단계 낫는다.",
            15, Company.Gold >= cost ? Theme.TextDim : Theme.Cover, width: width, wrap: true));
        return panel;
    }

    /// <summary>"원정 회복 +1단계 · 쉬기 비용 -25%"처럼 그 레벨의 효과 (0인 항목은 뺀다).</summary>
    private static string LevelText(FacilityLevel level)
    {
        var parts = new List<string>();
        if (level.ExpeditionRecovery > 0)
        {
            parts.Add($"원정 회복 +{level.ExpeditionRecovery}단계");
        }

        if (level.RestRecovery > 0)
        {
            parts.Add($"쉬기 회복 +{level.RestRecovery}단계");
        }

        if (level.RestDiscountPercent > 0)
        {
            parts.Add($"쉬기 비용 -{level.RestDiscountPercent}%");
        }

        return string.Join(" · ", parts);
    }
}
