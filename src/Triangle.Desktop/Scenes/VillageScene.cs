using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Triangle.Core.Expeditions;
using Triangle.Core.Progress;
using Triangle.Core.Units;
using Triangle.Desktop.Rendering;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 마을 (첫 화면). 골드, 출전 명단(최대 5명), 로스터, 전투지역 목록을 보여주고
/// 전술 편집, 모집, 출정, 저장으로 이어진다. 마을에서는 수동 저장이다.
/// </summary>
internal sealed class VillageScene : IScene
{
    private const int Margin = 24;
    private const int HeaderHeight = 64;
    private const int FooterHeight = 40;
    private const int BarHeight = 56;
    private const int ColumnGap = 32;

    private readonly Ui _ui;
    private readonly Widgets _w;
    private readonly GameSession _session;
    private readonly Rectangle _bounds;
    private readonly Action _openEditor;
    private readonly Action _openRecruit;
    private readonly Action _openShop;
    private readonly Action<string> _depart;
    private readonly Action _quit;
    private readonly MyraDesktop _desktop = new();

    private string _zoneId;
    private bool _dirty = true;
    private Window? _quitDialog;

    /// <param name="depart">출정 (지역 ID). 부르기 전에 출정할 수 있는지 확인한다.</param>
    /// <param name="quit">종료가 확인되었다 (저장했거나 버리기로 했다).</param>
    public VillageScene(
        Ui ui, GameSession session, Rectangle bounds, Action openEditor, Action openRecruit, Action openShop, Action<string> depart, Action quit)
    {
        _ui = ui;
        _w = new Widgets(ui);
        _session = session;
        _bounds = bounds;
        _openEditor = openEditor;
        _openRecruit = openRecruit;
        _openShop = openShop;
        _depart = depart;
        _quit = quit;
        _zoneId = session.Data.Zones.Values.OrderBy(z => z.Difficulty).First().Id;
    }

    private Company Company => _session.Company;

    /// <summary>다른 화면에서 돌아왔다.</summary>
    public void Refresh() => _dirty = true;

    /// <summary>종료를 요청한다. 저장하지 않은 변경이 있으면 먼저 확인 창을 띄운다.</summary>
    public void RequestQuit()
    {
        if (_session.Unsaved)
        {
            ShowQuitDialog();
        }
        else
        {
            _quit();
        }
    }

    public void Update(GameTime gameTime, Input input)
    {
        // 확인 창이 떠 있으면 Esc는 취소다. 창을 띄운 Esc가 바로 창을 닫지 않도록
        // Myra의 CloseKey 대신 여기서 처리한다 (Pressed는 새로 누른 순간만 참).
        if (_quitDialog is not null)
        {
            if (input.Pressed(Keys.Escape))
            {
                _quitDialog.Close();
            }

            return;
        }

        if (input.Pressed(Keys.Escape))
        {
            RequestQuit();
            return;
        }

        if (input.Pressed(Keys.S) && input.IsDown(Keys.LeftControl, Keys.RightControl))
        {
            Save();
        }

        if (_dirty)
        {
            Rebuild();
            _dirty = false;
        }
    }

    public void Draw(SpriteBatch batch)
    {
        if (_dirty && _quitDialog is null)
        {
            Rebuild();
            _dirty = false;
        }

        batch.Begin();
        _ui.Text(batch, _ui.BoldFont(28), "마을", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        var gold = $"골드 {Company.Gold}";
        var goldFont = _ui.BoldFont(24);
        _ui.Text(batch, goldFont, gold, new Vector2(_bounds.Right - Margin - goldFont.MeasureString(gold).X, _bounds.Top + 22), Theme.Cover);

        const string help = "Ctrl+S  저장     Esc  종료";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);
        if (_session.Notice is { } notice)
        {
            var font = _ui.Font(16);
            var width = font.MeasureString(notice.Text).X;
            _ui.Text(batch, font, notice.Text, new Vector2(_bounds.Right - Margin - width, _bounds.Bottom - FooterHeight + 10), notice.Color);
        }

        foreach (var area in new[] { LeftArea, RightArea })
        {
            _ui.Panel(batch, new Rectangle(area.X - 16, area.Y - 12, area.Width + 32, area.Height + 24));
        }

        batch.End();
        _desktop.Render();
    }

    private int ContentTop => _bounds.Top + HeaderHeight + 12;
    private int ContentBottom => _bounds.Bottom - FooterHeight - BarHeight - 12;
    private int ColumnWidth => (_bounds.Width - Margin * 2 - 32 - ColumnGap) / 2;

    private Rectangle LeftArea => new(_bounds.Left + Margin + 16, ContentTop, ColumnWidth, ContentBottom - ContentTop);
    private Rectangle RightArea => new(LeftArea.Right + 32 + ColumnGap, ContentTop, ColumnWidth - 16, ContentBottom - ContentTop);

    private void MarkChanged()
    {
        _session.MarkChanged();
        _dirty = true;
    }

    private void Save()
    {
        _session.Save();
        _dirty = true;
    }

    // ── 위젯 트리 ──────────────────────────────────────────

    private void Rebuild()
    {
        var root = new Panel();
        root.Widgets.Add(Place(BuildCompany(LeftArea), LeftArea));
        root.Widgets.Add(Place(BuildZones(RightArea), RightArea));

        var bar = BuildBottomBar();
        bar.Left = LeftArea.X - 16;
        bar.Top = _bounds.Bottom - FooterHeight - BarHeight + 8;
        root.Widgets.Add(bar);
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

    /// <summary>출전 명단과 나머지 로스터. 명단은 최대 5명이고 넣기·빼기로 바꾼다.</summary>
    private Widget BuildCompany(Rectangle area)
    {
        var list = new VerticalStackPanel { Spacing = 10 };
        list.Widgets.Add(Label($"출전 명단  {Company.Lineup.Count}/{Company.MaxLineup}", 20, Theme.Ally, bold: true));
        if (Company.Lineup.Count == 0)
        {
            list.Widgets.Add(Label("출전 명단이 비어 있습니다. 아래 로스터에서 넣으세요.", 16, Theme.TextDim));
        }

        foreach (var member in Company.LineupMembers)
        {
            list.Widgets.Add(MemberRow(member, "빼기", true, () => Company.RemoveFromLineup(member.Id), area.Width));
        }

        var bench = Company.Roster.Where(m => !Company.IsInLineup(m.Id)).ToList();
        list.Widgets.Add(new HorizontalSeparator { Margin = new Thickness(0, 6) });
        list.Widgets.Add(Label($"대기  {bench.Count}명", 20, Theme.TextDim, bold: true));
        if (Company.Roster.Count == 0)
        {
            list.Widgets.Add(Label("캐릭터가 없습니다. 모집에서 고용하세요.", 16, Theme.Enemy));
        }

        var full = Company.Lineup.Count >= Company.MaxLineup;
        foreach (var member in bench)
        {
            list.Widgets.Add(MemberRow(member, "넣기", !full, () => Company.AddToLineup(member.Id), area.Width));
        }

        return new ScrollViewer { Content = list, ShowHorizontalScrollBar = false };
    }

    private Widget MemberRow(PartyMember member, string action, bool enabled, Func<bool> onClick, int width)
    {
        var row = new HorizontalStackPanel { Spacing = 10 };
        var text = new VerticalStackPanel { Spacing = 2, Width = width - 100 };
        text.Widgets.Add(Label($"{member.Name}  ·  {(member.Row == Row.Front ? "전위" : "후위")}", 18, Theme.Text, bold: true));
        text.Widgets.Add(Label(ItemText.Gear(member.Equipment, _session.Data), 15, Theme.TextDim));
        row.Widgets.Add(text);

        var button = TextButton(action, Theme.Button, Theme.ButtonHover);
        button.Width = 72;
        button.Enabled = enabled;
        button.Click += (_, _) =>
        {
            if (onClick())
            {
                MarkChanged();
            }
        };
        row.Widgets.Add(button);
        return row;
    }


    /// <summary>전투지역 목록 (난이도순). 고른 지역으로 출정한다. 영구 사망 지역은 빨간 경고를 단다.</summary>
    private Widget BuildZones(Rectangle area)
    {
        var list = new VerticalStackPanel { Spacing = 8 };
        list.Widgets.Add(Label("전투지역", 20, Theme.Enemy, bold: true));

        foreach (var zone in _session.Data.Zones.Values.OrderBy(z => z.Difficulty))
        {
            var selected = zone.Id == _zoneId;
            var content = new VerticalStackPanel { Spacing = 3 };
            content.Widgets.Add(Label(zone.Name, 20, selected ? Theme.Text : Theme.Ally, bold: true));
            content.Widgets.Add(Label($"난이도 {zone.Difficulty} · 전투 {zone.MaxBattles}회", 15, Theme.TextDim));
            content.Widgets.Add(zone.Permadeath
                ? Label($"영구 사망: 쓰러지면 캐릭터를 잃고, 장비가 {zone.EquipmentDestroyChance}% 확률로 파괴됩니다", 15, Theme.Enemy)
                : Label("사망 페널티 없음: 쓰러져도 원정이 끝나면 회복합니다", 15, Theme.Heal));
            if (zone.Description is { } description)
            {
                var label = Label(description, 14, Theme.TextDim);
                label.Wrap = true;
                label.Width = area.Width - 40;
                content.Widgets.Add(label);
            }

            var button = StyledButton(content, selected ? Theme.Selected : Theme.Panel, Theme.ButtonHover);
            button.Width = area.Width;
            button.Padding = new Thickness(14, 10);
            button.Click += (_, _) =>
            {
                _zoneId = zone.Id;
                _dirty = true;
            };
            list.Widgets.Add(button);
        }

        return new ScrollViewer { Content = list, ShowHorizontalScrollBar = false };
    }

    private Widget BuildBottomBar()
    {
        var bar = new HorizontalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var editor = TextButton("전술 편집", Theme.Button, Theme.ButtonHover);
        editor.Width = 130;
        editor.Enabled = Company.Roster.Count > 0;
        editor.Click += (_, _) => _openEditor();
        bar.Widgets.Add(editor);

        var recruit = TextButton($"모집 ({Company.RecruitOffers.Count})", Theme.Button, Theme.ButtonHover);
        recruit.Width = 130;
        recruit.Click += (_, _) => _openRecruit();
        bar.Widgets.Add(recruit);

        var shop = TextButton("상점·창고", Theme.Button, Theme.ButtonHover);
        shop.Width = 130;
        shop.Click += (_, _) => _openShop();
        bar.Widgets.Add(shop);

        var why = ExpeditionRules.WhyCannotStart(Company, _session.Data, _zoneId);
        var depart = TextButton($"{Korean.EuroRo(_session.Data.Zones[_zoneId].Name)} 출정  ▶", Theme.Accent, Theme.AccentHover, bold: true);
        depart.Width = 280;
        depart.Enabled = why is null;
        depart.Click += (_, _) => _depart(_zoneId);
        bar.Widgets.Add(depart);

        var save = TextButton("저장", _session.Unsaved ? Theme.Accent : Theme.Button, _session.Unsaved ? Theme.AccentHover : Theme.ButtonHover);
        save.Width = 100;
        save.Click += (_, _) => Save();
        bar.Widgets.Add(save);

        bar.Widgets.Add(why is not null
            ? Label(why, 16, Theme.Enemy)
            : Label(_session.Unsaved ? "저장하지 않은 변경이 있습니다" : "", 16, Theme.Cover));
        return bar;
    }

    private void ShowQuitDialog()
    {
        if (_quitDialog is not null)
        {
            return;
        }

        var content = new VerticalStackPanel { Spacing = 20, Padding = new Thickness(24, 16) };
        content.Widgets.Add(Label("저장하지 않은 변경이 있습니다. 저장할까요?", 18, Theme.Text));

        var buttons = new HorizontalStackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        var saveAndQuit = TextButton("저장하고 종료", Theme.Accent, Theme.AccentHover, bold: true);
        var discard = TextButton("저장하지 않고 종료", Theme.Button, Theme.ButtonHover);
        var cancel = TextButton("취소", Theme.Button, Theme.ButtonHover);
        buttons.Widgets.Add(saveAndQuit);
        buttons.Widgets.Add(discard);
        buttons.Widgets.Add(cancel);
        content.Widgets.Add(buttons);

        var window = new Window
        {
            Title = "종료",
            TitleFont = _ui.BoldFont(18),
            TitleTextColor = Theme.Text,
            Content = content,
            Background = new SolidBrush(Theme.Panel),
            Border = new SolidBrush(Theme.PanelBorder),
            BorderThickness = new Thickness(1),
            CloseKey = null,
        };

        saveAndQuit.Click += (_, _) =>
        {
            // 저장에 실패하면 종료하지 않고 창을 닫아 오류 안내를 보여준다.
            window.Close();
            if (_session.Save())
            {
                _quit();
            }
            else
            {
                _dirty = true;
            }
        };
        discard.Click += (_, _) =>
        {
            window.Close();
            _quit();
        };
        cancel.Click += (_, _) => window.Close();
        window.Closed += (_, _) => _quitDialog = null;

        _quitDialog = window;
        window.ShowModal(_desktop);
    }

    private Label Label(string text, int size, Color color, bool bold = false) => _w.Label(text, size, color, bold);

    private Button TextButton(string text, Color background, Color hover, bool bold = false) => _w.TextButton(text, background, hover, bold);

    private static Button StyledButton(Widget content, Color background, Color hover) => Widgets.StyledButton(content, background, hover);
}
