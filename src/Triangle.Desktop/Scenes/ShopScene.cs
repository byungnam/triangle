using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using Triangle.Core.Items;
using Triangle.Core.Progress;
using Triangle.Desktop.Rendering;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 상점과 창고: 왼쪽은 상점(T1~T2 장비, 부위 탭, 구매), 오른쪽은 창고(끼지 않은 아이템과 재료, 판매).
/// 상점은 마을에서만 열린다.
/// </summary>
internal sealed class ShopScene : IScene
{
    private const int Margin = 24;
    private const int HeaderHeight = 64;
    private const int FooterHeight = 40;
    private const int TabsHeight = 44;

    private static readonly EquipmentSlot?[] Tabs =
        [null, EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Feet];

    private readonly Ui _ui;
    private readonly Widgets _w;
    private readonly GameSession _session;
    private readonly Rectangle _bounds;
    private readonly Action _back;
    private readonly MyraDesktop _desktop = new();
    private EquipmentSlot? _tab;
    private bool _dirty = true;

    public ShopScene(Ui ui, GameSession session, Rectangle bounds, Action back)
    {
        _ui = ui;
        _w = new Widgets(ui);
        _session = session;
        _bounds = bounds;
        _back = back;
    }

    private Company Company => _session.Company;

    private Rectangle ShopArea => new(
        _bounds.Left + Margin, _bounds.Top + HeaderHeight,
        (_bounds.Width - Margin * 3) / 2, _bounds.Height - HeaderHeight - FooterHeight - 8);

    private Rectangle StashArea => new(
        ShopArea.Right + Margin, ShopArea.Top, _bounds.Right - Margin - ShopArea.Right - Margin, ShopArea.Height);

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

        batch.Begin();
        _ui.Text(batch, _ui.BoldFont(28), "상점·창고", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        var gold = $"골드 {Company.Gold}";
        var goldFont = _ui.BoldFont(24);
        _ui.Text(batch, goldFont, gold, new Vector2(_bounds.Right - Margin - goldFont.MeasureString(gold).X, _bounds.Top + 22), Theme.Cover);

        _ui.Panel(batch, ShopArea);
        _ui.Panel(batch, StashArea);

        var help = $"상점은 T1~T{Shop.MaxTier} 장비를 팝니다. 창고의 아이템은 가격의 {Shop.SellPercent}%에 팝니다. 낀 장비는 전술 편집에서 빼야 팔 수 있습니다.     Esc  마을로";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);
        if (_session.Notice is { } notice)
        {
            var font = _ui.Font(16);
            _ui.Text(batch, font, notice.Text, new Vector2(_bounds.Right - Margin - goldFont.MeasureString(gold).X - 24 - font.MeasureString(notice.Text).X, _bounds.Top + 28), notice.Color);
        }

        batch.End();
        _desktop.Render();
    }

    private void Rebuild()
    {
        var root = new Panel();
        root.Widgets.Add(BuildShop(ShopArea));
        root.Widgets.Add(BuildStash(StashArea));
        _desktop.Root = root;
    }

    private Widget BuildShop(Rectangle area)
    {
        var panel = new VerticalStackPanel
        {
            Spacing = 8,
            Left = area.X + 16,
            Top = area.Y + 12,
            Width = area.Width - 32,
            Height = area.Height - 24,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.Widgets.Add(_w.Label("상점", 22, Theme.Cover, bold: true));

        var tabs = new HorizontalStackPanel { Spacing = 6, Height = TabsHeight - 8 };
        foreach (var tab in Tabs)
        {
            var label = tab is { } slot ? ItemText.SlotLabel(slot) : "전체";
            var button = _w.TextButton(label, tab == _tab ? Theme.Selected : Theme.Button, Theme.ButtonHover, size: 16);
            button.Click += (_, _) =>
            {
                _tab = tab;
                _dirty = true;
            };
            tabs.Widgets.Add(button);
        }

        panel.Widgets.Add(tabs);

        var list = new VerticalStackPanel { Spacing = 6 };
        foreach (var item in Shop.Stock(_session.Data).Where(i => _tab is null || i.Slot == _tab))
        {
            list.Widgets.Add(ShopRow(item, area.Width - 60));
        }

        panel.Widgets.Add(new ScrollViewer { Content = list, ShowHorizontalScrollBar = false });
        StackPanel.SetProportionType(panel.Widgets[^1], ProportionType.Fill);
        return panel;
    }

    private Widget ShopRow(ItemDefinition item, int width)
    {
        var data = _session.Data;
        var row = new HorizontalStackPanel { Spacing = 10 };
        var text = new VerticalStackPanel { Spacing = 1, Width = width - 110 };
        text.Widgets.Add(_w.Label($"{ItemText.SlotLabel(item.Slot)} · {ItemText.Title(item, data)}", 17, Theme.Text));

        var details = new List<string>();
        if (item.Bonuses.Count > 0)
        {
            details.Add(ItemText.Bonuses(item, data));
        }

        if (item.Requirements.Count > 0)
        {
            details.Add(ItemText.Requirements(item, data));
        }

        if (details.Count > 0)
        {
            // 로스터 누구도 조건을 못 채우면 요구를 빨간색으로 보여준다.
            var anyone = Company.Roster.Any(m => m.Skills(data).Missing(item.Requirements).Count == 0);
            text.Widgets.Add(_w.Label(string.Join(" · ", details), 14, anyone ? Theme.TextDim : Theme.Damage));
        }

        row.Widgets.Add(text);

        var why = Company.WhyCannotBuy(item.Id, data);
        var buy = _w.TextButton($"{item.Price}G 구매", Theme.Accent, Theme.AccentHover, size: 16);
        buy.Width = 100;
        buy.Enabled = why is null;
        buy.Click += (_, _) =>
        {
            if (Company.Buy(item.Id, data))
            {
                _session.MarkChanged();
                _session.Notice = ($"{ItemText.ShortName(item)} 구매 (-{item.Price}골드)", Theme.Heal);
                _dirty = true;
            }
        };
        row.Widgets.Add(buy);
        return row;
    }

    private Widget BuildStash(Rectangle area)
    {
        var data = _session.Data;
        var panel = new VerticalStackPanel
        {
            Spacing = 8,
            Left = area.X + 16,
            Top = area.Y + 12,
            Width = area.Width - 32,
            Height = area.Height - 24,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.Widgets.Add(_w.Label($"창고 ({Company.Stash.Values.Sum()}개)", 22, Theme.Ally, bold: true));

        var list = new VerticalStackPanel { Spacing = 6 };
        var items = Company.Stash.Keys
            .Select(id => data.Items[id])
            .OrderBy(i => i.Slot)
            .ThenBy(i => i.Mastery)
            .ThenBy(i => i.Tier)
            .ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToList();
        if (items.Count == 0)
        {
            list.Widgets.Add(_w.Label("창고가 비었습니다", 17, Theme.TextDim));
        }

        foreach (var item in items)
        {
            list.Widgets.Add(StashRow(item, area.Width - 60));
        }

        panel.Widgets.Add(new ScrollViewer { Content = list, ShowHorizontalScrollBar = false });
        StackPanel.SetProportionType(panel.Widgets[^1], ProportionType.Fill);
        return panel;
    }

    private Widget StashRow(ItemDefinition item, int width)
    {
        var data = _session.Data;
        var count = Company.StashCount(item.Id);
        var row = new HorizontalStackPanel { Spacing = 10 };
        var text = new VerticalStackPanel { Spacing = 1, Width = width - 110 };
        var name = count == 1 ? ItemText.Title(item, data) : $"{ItemText.Title(item, data)} × {count}";
        text.Widgets.Add(_w.Label($"{ItemText.SlotLabel(item.Slot)} · {name}", 17, Theme.Text));
        if (item.Bonuses.Count > 0 || item.Requirements.Count > 0)
        {
            var details = new[] { ItemText.Bonuses(item, data), ItemText.Requirements(item, data) }.Where(s => s.Length > 0);
            text.Widgets.Add(_w.Label(string.Join(" · ", details), 14, Theme.TextDim));
        }

        row.Widgets.Add(text);

        var price = Shop.SellPrice(item);
        var sell = _w.TextButton($"{price}G 판매", Theme.Button, Theme.ButtonHover, size: 16);
        sell.Width = 100;
        sell.Enabled = !Company.OnExpedition;
        sell.Click += (_, _) =>
        {
            if (Company.Sell(item.Id, data))
            {
                _session.MarkChanged();
                _session.Notice = ($"{ItemText.ShortName(item)} 판매 (+{price}골드)", Theme.Cover);
                _dirty = true;
            }
        };
        row.Widgets.Add(sell);
        return row;
    }
}
