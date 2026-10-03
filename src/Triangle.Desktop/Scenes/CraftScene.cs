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
/// 제작: 창고의 재료와 골드로 T3~T4 장비를 만든다. 부위 탭으로 거르고, 줄마다 재료 보유/필요를 보여준다.
/// 만든 장비는 창고로 간다. 마을에서만 열린다.
/// </summary>
internal sealed class CraftScene : IScene
{
    private const int Margin = 24;
    private const int HeaderHeight = 96;
    private const int FooterHeight = 40;

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

    public CraftScene(Ui ui, GameSession session, Rectangle bounds, Action back)
    {
        _ui = ui;
        _w = new Widgets(ui);
        _session = session;
        _bounds = bounds;
        _back = back;
    }

    private Company Company => _session.Company;

    private Rectangle ListArea => new(
        _bounds.Left + Margin, _bounds.Top + HeaderHeight,
        _bounds.Width - Margin * 2, _bounds.Height - HeaderHeight - FooterHeight - 8);

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

        var data = _session.Data;
        batch.Begin();
        _ui.Text(batch, _ui.BoldFont(28), "제작", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        var gold = $"골드 {Company.Gold}";
        var goldFont = _ui.BoldFont(24);
        _ui.Text(batch, goldFont, gold, new Vector2(_bounds.Right - Margin - goldFont.MeasureString(gold).X, _bounds.Top + 22), Theme.Cover);

        var materials = data.Items.Values.Where(i => !i.IsEquipment).Select(i => $"{i.Name} {Company.StashCount(i.Id)}");
        _ui.Text(batch, _ui.Font(17), $"창고의 재료: {string.Join(" · ", materials)}", new Vector2(_bounds.Left + Margin, _bounds.Top + 60), Theme.Text);

        _ui.Panel(batch, ListArea);

        const string help = "재료는 전투지역에서 떨어진다. 고대 파편은 재의 왕도에서만 나온다. 만든 장비는 창고로 간다.     Esc  마을로";
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
        var area = ListArea;
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

        var tabs = new HorizontalStackPanel { Spacing = 6 };
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

        var data = _session.Data;
        var list = new VerticalStackPanel { Spacing = 8 };
        var recipes = data.Recipes.Values
            .Select(r => (Recipe: r, Item: data.Items[r.Result]))
            .Where(p => _tab is null || p.Item.Slot == _tab)
            .OrderBy(p => p.Item.Slot)
            .ThenBy(p => p.Item.Mastery)
            .ThenBy(p => p.Item.Tier);
        foreach (var (recipe, item) in recipes)
        {
            list.Widgets.Add(RecipeRow(recipe, item, area.Width - 60));
        }

        panel.Widgets.Add(new ScrollViewer { Content = list, ShowHorizontalScrollBar = false });
        StackPanel.SetProportionType(panel.Widgets[^1], ProportionType.Fill);

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop.Root = root;
    }

    private Widget RecipeRow(RecipeDefinition recipe, ItemDefinition item, int width)
    {
        var data = _session.Data;
        var row = new HorizontalStackPanel { Spacing = 10 };
        var text = new VerticalStackPanel { Spacing = 1, Width = width - 130 };
        text.Widgets.Add(_w.Label($"{ItemText.SlotLabel(item.Slot)} · {ItemText.Title(item, data)}", 17, Theme.Text));

        var details = new[] { ItemText.Bonuses(item, data), ItemText.Requirements(item, data) }.Where(s => s.Length > 0);
        var anyone = Company.Roster.Any(m => m.Skills(data).Missing(item.Requirements).Count == 0);
        text.Widgets.Add(_w.Label(string.Join(" · ", details), 14, anyone ? Theme.TextDim : Theme.Damage));

        // 재료마다 "철 조각 3/6" (모자라면 빨간색).
        var materials = new HorizontalStackPanel { Spacing = 14 };
        foreach (var material in recipe.Materials)
        {
            var have = Company.StashCount(material.ItemId);
            materials.Widgets.Add(_w.Label($"{data.Items[material.ItemId].Name} {have}/{material.Count}", 15, have >= material.Count ? Theme.Heal : Theme.Enemy));
        }

        materials.Widgets.Add(_w.Label($"골드 {recipe.Gold}", 15, Company.Gold >= recipe.Gold ? Theme.Cover : Theme.Enemy));
        text.Widgets.Add(materials);
        row.Widgets.Add(text);

        var craft = _w.TextButton("제작", Theme.Accent, Theme.AccentHover, size: 16);
        craft.Width = 110;
        craft.VerticalAlignment = VerticalAlignment.Center;
        craft.Enabled = Company.WhyCannotCraft(recipe.Result, data) is null;
        craft.Click += (_, _) =>
        {
            if (Company.Craft(recipe.Result, data))
            {
                _session.MarkChanged();
                _session.Notice = ($"{ItemText.ShortName(item)} 제작 (창고로)", Theme.Heal);
                _dirty = true;
            }
        };
        row.Widgets.Add(craft);
        return row;
    }
}
