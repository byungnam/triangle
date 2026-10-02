using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Triangle.Core.Combat;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Units;
using Triangle.Desktop.Rendering;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 모집: 후보 3명의 이름, 스탯, 장비, 기본 전술, 가격. 골드가 모자라면 고용할 수 없다.
/// 후보는 원정이 끝날 때마다 새로 바뀐다.
/// </summary>
internal sealed class RecruitScene : IScene
{
    private const int Margin = 24;
    private const int HeaderHeight = 64;
    private const int FooterHeight = 40;
    private const int CardGap = 24;

    private readonly Ui _ui;
    private readonly Widgets _w;
    private readonly GameSession _session;
    private readonly Rectangle _bounds;
    private readonly Action _back;
    private readonly MyraDesktop _desktop = new();
    private bool _dirty = true;

    public RecruitScene(Ui ui, GameSession session, Rectangle bounds, Action back)
    {
        _ui = ui;
        _w = new Widgets(ui);
        _session = session;
        _bounds = bounds;
        _back = back;
    }

    private Company Company => _session.Company;

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
        _ui.Text(batch, _ui.BoldFont(28), "모집", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        var gold = $"골드 {Company.Gold}";
        var goldFont = _ui.BoldFont(24);
        _ui.Text(batch, goldFont, gold, new Vector2(_bounds.Right - Margin - goldFont.MeasureString(gold).X, _bounds.Top + 22), Theme.Cover);
        const string help = "신입은 숙련 0, 패시브 없이 기본 전술로 시작한다. 후보는 원정이 끝날 때마다 바뀐다.     Esc  마을로";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);

        foreach (var card in Cards())
        {
            _ui.Panel(batch, card);
        }

        batch.End();
        _desktop.Render();
    }

    private IEnumerable<Rectangle> Cards()
    {
        var width = (_bounds.Width - Margin * 2 - CardGap * (Recruitment.OfferCount - 1)) / Recruitment.OfferCount;
        var top = _bounds.Top + HeaderHeight + 8;
        var height = _bounds.Height - HeaderHeight - FooterHeight - 24;
        for (var i = 0; i < Company.RecruitOffers.Count; i++)
        {
            yield return new Rectangle(_bounds.Left + Margin + i * (width + CardGap), top, width, height);
        }
    }

    private void Rebuild()
    {
        var root = new Panel();
        var cards = Cards().ToList();
        if (cards.Count == 0)
        {
            var empty = _w.Label("남은 후보가 없습니다. 원정을 다녀오면 새 후보가 옵니다.", 18, Theme.TextDim);
            empty.Left = _bounds.Left + Margin;
            empty.Top = _bounds.Top + HeaderHeight + 16;
            root.Widgets.Add(empty);
        }

        for (var i = 0; i < cards.Count; i++)
        {
            var card = BuildCard(i, cards[i].Width - 32);
            card.Left = cards[i].X + 16;
            card.Top = cards[i].Y + 14;
            root.Widgets.Add(card);
        }

        _desktop.Root = root;
    }

    private Widget BuildCard(int index, int width)
    {
        var data = _session.Data;
        var offer = Company.RecruitOffers[index];
        var template = data.Recruits[offer.TemplateId];
        var panel = new VerticalStackPanel { Spacing = 8, Width = width };

        panel.Widgets.Add(_w.Label(offer.Name, 24, Theme.Ally, bold: true));
        panel.Widgets.Add(_w.Label($"{template.Name} · {(template.Row == Row.Front ? "전위" : "후위")}", 17, Theme.Text));

        var s = offer.Stats;
        var rules = CombatRules.Default;
        var noSkills = new SkillSet(SkillSet.NoSkills, data.Skills);
        panel.Widgets.Add(_w.Label($"근력 {s.Str}   민첩 {s.Dex}   체력 {s.Vital}", 16, Theme.TextDim));
        panel.Widgets.Add(_w.Label($"지능 {s.Intel}   신속 {s.Speed}", 16, Theme.TextDim));
        panel.Widgets.Add(_w.Label($"HP {rules.MaxHp(s, noSkills)}   MP {rules.MaxMp(s, noSkills)}", 16, Theme.TextDim));

        string Item(string? id) => id is null ? "없음" : data.Items[id].Name;
        panel.Widgets.Add(_w.Label($"장비: {Item(template.Weapon)} / {Item(template.Armor)}", 16, Theme.Text));

        panel.Widgets.Add(_w.Label("기본 전술", 16, Theme.Text));
        foreach (var tactic in template.Tactics.OrderBy(t => t.Priority))
        {
            var condition = TacticText.ConditionLabel(tactic.Condition);
            var value = TacticText.HasValue(tactic.Condition) ? $" {tactic.Value}" : "";
            panel.Widgets.Add(_w.Label($"  {tactic.Priority}. {condition}{value} → {data.Actions[tactic.ActionId].Name}", 15, Theme.TextDim));
        }

        if (template.Description is { } description)
        {
            var label = _w.Label(description, 14, Theme.TextDim);
            label.Wrap = true;
            label.Width = width;
            panel.Widgets.Add(label);
        }

        var price = Company.HirePrice(index);
        var priceText = price == 0 && offer.Price > 0 ? $"무료 (원래 {offer.Price}골드, 로스터가 비었습니다)" : $"{price}골드";
        panel.Widgets.Add(_w.Label(priceText, 20, Company.CanHire(index) ? Theme.Cover : Theme.Enemy, bold: true));

        var hire = _w.TextButton("고용", Theme.Accent, Theme.AccentHover, bold: true);
        hire.Width = 140;
        hire.Enabled = Company.CanHire(index);
        hire.Margin = new Thickness(0, 4, 0, 0);
        hire.Click += (_, _) =>
        {
            if (Company.Hire(index, data) is { } member)
            {
                _session.MarkChanged();
                _session.Notice = ($"{Korean.EulReul(member.Name)} 고용했습니다", Theme.Heal);
                _dirty = true;
            }
        };
        panel.Widgets.Add(hire);
        if (!Company.CanHire(index))
        {
            panel.Widgets.Add(_w.Label("골드가 모자랍니다", 15, Theme.Enemy));
        }

        return panel;
    }
}
