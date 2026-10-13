using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Triangle.Core.Data;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Triangle.Core.Units;
using Triangle.Desktop.Rendering;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 탐사 기록: 지역마다 만난 적 조합(만난 수/전체), 클리어 기록과 단서. 왼쪽에서 만난 조합을 고르면
/// 오른쪽에 그 조합의 대원, 줄, 장비, 쓰는 행동을 보여준다. 만나지 않은 조합은 "???"다(정찰은 없다).
/// </summary>
internal sealed class ExplorationScene : IScene
{
    private const int Margin = 24;
    private const int HeaderHeight = 64;
    private const int FooterHeight = 40;
    private const int ColumnGap = 32;

    private readonly Ui _ui;
    private readonly Widgets _w;
    private readonly GameSession _session;
    private readonly Rectangle _bounds;
    private readonly Action _back;
    private readonly MyraDesktop _desktop = new();
    private string? _encounterId;
    private bool _dirty = true;

    public ExplorationScene(Ui ui, GameSession session, Rectangle bounds, Action back)
    {
        _ui = ui;
        _w = new Widgets(ui);
        _session = session;
        _bounds = bounds;
        _back = back;
        _encounterId = Depths.Ordered(session.Data)
            .SelectMany(Exploration.Entries)
            .FirstOrDefault(e => Company.HasMet(e.EncounterId))?.EncounterId;
    }

    private Company Company => _session.Company;
    private GameData Data => _session.Data;

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
        _ui.Text(batch, _ui.BoldFont(28), "탐사 기록", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        var met = Company.EncounterRecords.Count;
        var total = Depths.Ordered(Data).SelectMany(Exploration.Entries).Select(e => e.EncounterId).Distinct().Count();
        var count = $"만난 적 {met}/{total}";
        var countFont = _ui.BoldFont(24);
        _ui.Text(batch, countFont, count, new Vector2(_bounds.Right - Margin - countFont.MeasureString(count).X, _bounds.Top + 22), Theme.Cover);
        const string help = "정찰은 없다. 적은 원정에서 직접 싸워 봐야 기록에 남는다.     Esc  돌아가기";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);

        foreach (var area in new[] { LeftArea, RightArea })
        {
            _ui.Panel(batch, new Rectangle(area.X - 16, area.Y - 12, area.Width + 32, area.Height + 24));
        }

        batch.End();
        _ui.Screen.Render(_desktop);
    }

    private int ContentTop => _bounds.Top + HeaderHeight + 12;
    private int ContentBottom => _bounds.Bottom - FooterHeight - 12;
    private int ColumnWidth => (_bounds.Width - Margin * 2 - 32 - ColumnGap) / 2;

    private Rectangle LeftArea => new(_bounds.Left + Margin + 16, ContentTop, ColumnWidth, ContentBottom - ContentTop);
    private Rectangle RightArea => new(LeftArea.Right + 32 + ColumnGap, ContentTop, ColumnWidth - 16, ContentBottom - ContentTop);

    private void Rebuild()
    {
        var root = new Panel();
        root.Widgets.Add(Place(BuildZones(LeftArea), LeftArea));
        root.Widgets.Add(Place(BuildDetail(RightArea), RightArea));
        _desktop.Root = root;
    }

    private static Widget Place(Widget widget, Rectangle area)
    {
        widget.Left = area.X;
        widget.Top = area.Y;
        widget.Width = area.Width;
        widget.Height = area.Height;
        return widget;
    }

    /// <summary>지역마다 만난 조합 수, 클리어 기록과 단서, 조합 목록 (만난 조합만 고를 수 있다).</summary>
    private Widget BuildZones(Rectangle area)
    {
        var list = new VerticalStackPanel { Spacing = 6 };
        foreach (var zone in Depths.Ordered(Data))
        {
            var (met, total) = Exploration.Progress(Company, zone);
            var locked = Depths.WhyZoneLocked(Company, Data, zone.Id) is not null;
            list.Widgets.Add(_w.Label($"{zone.Name}  ·  만난 조합 {met}/{total}", 20, locked ? Theme.TextDim : Theme.Ally, bold: true));
            if (Company.ClearRecords.GetValueOrDefault(zone.Id) is { } cleared)
            {
                list.Widgets.Add(_w.Label($"클리어 {cleared.Clears}회 · {ExpeditionText.ClearRecord(cleared)}", 14, Theme.Cover));
                if (zone.Boss?.Clue is { } clue)
                {
                    list.Widgets.Add(Wrapped($"단서: {clue}", 14, Theme.TextDim, area.Width - 20));
                }
            }
            else if (locked)
            {
                list.Widgets.Add(_w.Label("아직 들어가지 못한 지역", 14, Theme.TextDim));
            }

            foreach (var entry in Exploration.Entries(zone))
            {
                list.Widgets.Add(EntryButton(entry, area.Width));
            }

            list.Widgets.Add(new HorizontalSeparator { Margin = new Thickness(0, 6) });
        }

        return new ScrollViewer { Content = list, ShowHorizontalScrollBar = false };
    }

    private Widget EntryButton(ZoneEntry entry, int width)
    {
        var boss = entry.IsBoss ? "보스 · " : "";
        if (Company.EncounterRecords.GetValueOrDefault(entry.EncounterId) is not { } record)
        {
            var unknown = _w.Label($"   {boss}???", 17, Theme.TextDim);
            unknown.Padding = new Thickness(14, 4);
            return unknown;
        }

        var selected = entry.EncounterId == _encounterId;
        var row = new HorizontalStackPanel { Spacing = 12 };
        row.Widgets.Add(_w.Label($"{boss}{Data.Encounters[entry.EncounterId].Name}", 17, entry.IsBoss ? Theme.Enemy : Theme.Text, bold: selected));
        row.Widgets.Add(_w.Label($"만남 {record.Met} · 승리 {record.Wins}", 15, Theme.TextDim));
        var button = Widgets.StyledButton(row, selected ? Theme.Selected : Theme.Panel, Theme.ButtonHover);
        button.Width = width;
        button.Padding = new Thickness(14, 4);
        button.Click += (_, _) =>
        {
            _encounterId = entry.EncounterId;
            _dirty = true;
        };
        return button;
    }

    /// <summary>고른 조합의 대원: 이름, 줄, 장비, 쓰는 행동.</summary>
    private Widget BuildDetail(Rectangle area)
    {
        var panel = new VerticalStackPanel { Spacing = 8 };
        if (_encounterId is null || !Company.HasMet(_encounterId))
        {
            panel.Widgets.Add(Wrapped("아직 만난 적이 없습니다. 원정에서 싸운 적 조합이 여기에 남습니다.", 17, Theme.TextDim, area.Width));
            return panel;
        }

        var encounter = Data.Encounters[_encounterId];
        var record = Company.EncounterRecords[_encounterId];
        panel.Widgets.Add(_w.Label(encounter.Name, 24, Theme.Enemy, bold: true));
        panel.Widgets.Add(_w.Label($"만남 {record.Met}회 · 승리 {record.Wins}회 · 대원 {encounter.Units.Count}명", 16, Theme.TextDim));
        foreach (var row in new[] { Row.Front, Row.Back })
        {
            var units = encounter.Units.Where(u => u.Row == row).ToList();
            if (units.Count == 0)
            {
                continue;
            }

            panel.Widgets.Add(new HorizontalSeparator { Margin = new Thickness(0, 4) });
            panel.Widgets.Add(_w.Label(row == Row.Front ? "전위" : "후위", 18, Theme.Text, bold: true));
            foreach (var unit in units)
            {
                panel.Widgets.Add(_w.Label(unit.Name, 18, Theme.Text, bold: true));
                panel.Widgets.Add(Wrapped($"장비: {ItemText.Gear(unit.Equipment, Data)}", 15, Theme.TextDim, area.Width - 20));
                var actions = Exploration.UnitActions(unit).Select(id => Data.Actions[id].Name).ToList();
                if (actions.Count > 0)
                {
                    panel.Widgets.Add(Wrapped($"행동: {string.Join(", ", actions)}", 15, Theme.TextDim, area.Width - 20));
                }
            }
        }

        return new ScrollViewer { Content = panel, ShowHorizontalScrollBar = false };
    }

    private Label Wrapped(string text, int size, Color color, int width)
    {
        var label = _w.Label(text, size, color);
        label.Wrap = true;
        label.Width = width;
        return label;
    }
}
