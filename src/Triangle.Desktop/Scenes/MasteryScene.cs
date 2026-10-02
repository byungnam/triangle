using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Triangle.Core.Data;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Desktop.Rendering;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 한 유닛의 숙련(장비 계열)과 패시브 트리. 숙련은 그 장비로 싸워야 오르고,
/// 숙련 레벨 1당 생기는 포인트로 그 트리의 패시브를 배운다 (되돌릴 수 없다).
/// </summary>
internal sealed class MasteryScene : IScene
{
    private const int Margin = 24;
    private const int HeaderHeight = 96;
    private const int FooterHeight = 40;
    private const int ListWidth = 300;

    private readonly Ui _ui;
    private readonly Widgets _w;
    private readonly GameData _data;
    private readonly PartyMember _member;
    private readonly Rectangle _bounds;
    private readonly Action _back;
    private readonly Action _changed;
    private readonly MyraDesktop _desktop = new();

    private string _selected;
    private bool _dirty = true;
    private (string Text, Color Color)? _notice;

    /// <param name="changed">파티가 바뀌었을 때 (저장하지 않은 변경 표시용).</param>
    public MasteryScene(Ui ui, GameData data, PartyMember member, Rectangle bounds, Action back, Action changed)
    {
        _ui = ui;
        _w = new Widgets(ui);
        _data = data;
        _member = member;
        _bounds = bounds;
        _back = back;
        _changed = changed;
        _selected = member.Weapon ?? data.Masteries.Keys.First();
    }

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
        _ui.Text(batch, _ui.BoldFont(28), $"숙련·패시브 — {_member.Name}", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        var gear = $"장비: {Name(_member.Weapon)} / {Name(_member.Armor)}";
        _ui.Text(batch, _ui.Font(16), gear, new Vector2(_bounds.Left + Margin, _bounds.Top + 58), Theme.TextDim);

        const string help = "장착한 장비로 싸우면 그 숙련이 오른다. 숙련 레벨 1당 포인트 1점. 배운 패시브는 되돌릴 수 없다.     Esc  전술 편집으로";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);
        if (_notice is { } notice)
        {
            var font = _ui.Font(16);
            _ui.Text(batch, font, notice.Text, new Vector2(_bounds.Right - Margin - font.MeasureString(notice.Text).X, _bounds.Top + 58), notice.Color);
        }

        _ui.Panel(batch, TreeArea);
        batch.End();

        _desktop.Render();
    }

    private Rectangle TreeArea => new(
        _bounds.Left + Margin * 2 + ListWidth, _bounds.Top + HeaderHeight,
        _bounds.Width - Margin * 3 - ListWidth, _bounds.Height - HeaderHeight - FooterHeight - 8);

    private string Name(string? masteryId) => masteryId is null ? "없음" : _data.Masteries[masteryId].Name;

    // ── 위젯 트리 ──────────────────────────────────────────

    private void Rebuild()
    {
        var root = new Panel();

        var list = BuildMasteryList();
        list.Left = _bounds.Left + Margin;
        list.Top = _bounds.Top + HeaderHeight;
        root.Widgets.Add(list);

        var area = TreeArea;
        root.Widgets.Add(new ScrollViewer
        {
            Content = BuildTree(_data.Masteries[_selected]),
            ShowHorizontalScrollBar = false,
            Left = area.X + 16,
            Top = area.Y + 12,
            Width = area.Width - 32,
            Height = area.Height - 24,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        });

        _desktop.Root = root;
    }

    private Widget BuildMasteryList()
    {
        var list = new VerticalStackPanel
        {
            Spacing = 6,
            Width = ListWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        foreach (var (slot, label) in new[] { (EquipmentSlot.Weapon, "무기"), (EquipmentSlot.Armor, "방어구") })
        {
            list.Widgets.Add(_w.Label(label, 17, Theme.Ally, bold: true));
            foreach (var mastery in _data.MasteriesFor(slot))
            {
                list.Widgets.Add(BuildMasteryButton(mastery));
            }
        }

        return list;
    }

    private Widget BuildMasteryButton(MasteryDefinition mastery)
    {
        var level = _member.MasteryLevel(mastery.Id);
        var points = _member.PointsAvailable(mastery.Id, _data);
        var equipped = mastery.Id == _member.Weapon || mastery.Id == _member.Armor;
        var selected = mastery.Id == _selected;

        var content = new VerticalStackPanel { Spacing = 2 };
        var title = new HorizontalStackPanel { Spacing = 8 };
        title.Widgets.Add(_w.Label(mastery.Name, 17, selected ? Theme.Text : Theme.Ally, bold: true, width: 70));
        title.Widgets.Add(_w.Label($"Lv {level}", 16, Theme.Text, width: 50));
        title.Widgets.Add(_w.Label(points > 0 ? $"포인트 {points}" : "", 15, Theme.Cover, width: 70));
        title.Widgets.Add(_w.Label(equipped ? "장착" : "", 14, Theme.Heal));
        content.Widgets.Add(title);
        content.Widgets.Add(_w.Label(XpText(mastery.Id), 13, Theme.TextDim));

        var button = Widgets.StyledButton(content, selected ? Theme.Selected : Theme.Panel, Theme.ButtonHover);
        button.Width = ListWidth;
        button.Padding = new Thickness(12, 6);
        button.Click += (_, _) =>
        {
            _selected = mastery.Id;
            _dirty = true;
        };
        return button;
    }

    private string XpText(string masteryId)
    {
        var xp = _member.MasteryXp.GetValueOrDefault(masteryId);
        var level = MasteryProgression.LevelFor(xp);
        return level >= MasteryProgression.MaxLevel
            ? $"XP {SkillText.Number(xp)} (최대)"
            : $"XP {SkillText.Number(xp)} / {SkillText.Number(MasteryProgression.XpForLevel(level + 1))}";
    }

    private Widget BuildTree(MasteryDefinition mastery)
    {
        var panel = new VerticalStackPanel { Spacing = 8 };
        var points = _member.PointsAvailable(mastery.Id, _data);

        var header = new HorizontalStackPanel { Spacing = 16 };
        header.Widgets.Add(_w.Label($"{mastery.Name} 숙련 Lv {_member.MasteryLevel(mastery.Id)}", 22, Theme.Text, bold: true));
        header.Widgets.Add(_w.Label($"남은 포인트 {points}", 18, points > 0 ? Theme.Cover : Theme.TextDim));
        header.Widgets.Add(_w.Label(XpText(mastery.Id), 15, Theme.TextDim));
        panel.Widgets.Add(header);

        var skills = _data.Skills.Values.Where(s => s.Mastery == mastery.Id).ToList();
        if (skills.Count == 0)
        {
            panel.Widgets.Add(_w.Label("이 트리에는 아직 패시브가 없다.", 15, Theme.TextDim));
        }

        foreach (var skill in skills)
        {
            panel.Widgets.Add(BuildSkillRow(skill));
        }

        if (mastery.Slot == EquipmentSlot.Weapon)
        {
            panel.Widgets.Add(new Panel { Height = 8 });
            panel.Widgets.Add(_w.Label($"{Korean.EuroRo(mastery.Name)} 쓰는 행동", 17, Theme.Ally, bold: true));
            var set = _member.Skills(_data);
            foreach (var action in _data.Actions.Values.Where(a => a.Weapon == mastery.Id))
            {
                var missing = set.Missing(action.Requirements);
                var text = missing.Count == 0
                    ? $"{action.Name} — 배움"
                    : $"{action.Name} — {string.Join(", ", missing.Select(m => $"{_data.Skills[m.SkillId].Name} {m.Level}"))} 필요";
                panel.Widgets.Add(_w.Label(text, 15, missing.Count == 0 ? Theme.Heal : Theme.TextDim));
            }
        }

        return panel;
    }

    private Widget BuildSkillRow(SkillDefinition skill)
    {
        var row = new HorizontalStackPanel { Spacing = 12 };
        var level = _member.SkillLevels.GetValueOrDefault(skill.Id);

        row.Widgets.Add(_w.Label(skill.Name, 17, level > 0 ? Theme.Text : Theme.TextDim, bold: true, width: 130));
        row.Widgets.Add(_w.Label(SkillText.LevelPips(level), 16, Theme.Cover, width: 80));
        row.Widgets.Add(_w.Label($"Lv당 {skill.Rank}점", 15, Theme.TextDim, width: 70));
        row.Widgets.Add(_w.Label(string.Join(", ", skill.Bonuses.Select(b => SkillText.BonusLabel(b, _data))), 15, Theme.Text, width: 210));

        switch (_member.WhyCannotLearn(skill.Id, _data))
        {
            case null:
                var button = _w.TextButton($"배우기 Lv {level + 1}", Theme.Accent, Theme.AccentHover, size: 15);
                button.Padding = new Thickness(10, 4);
                button.Click += (_, _) => Learn(skill);
                row.Widgets.Add(button);
                break;
            case { Kind: LearnBlockerKind.MaxLevel }:
                row.Widgets.Add(_w.Label("최대", 15, Theme.Heal));
                break;
            case { Kind: LearnBlockerKind.Prerequisites } blocker:
                var text = "선행: " + string.Join(", ", blocker.Missing.Select(m => $"{_data.Skills[m.SkillId].Name} {m.Level}"));
                row.Widgets.Add(_w.Label(text, 15, Theme.Enemy));
                break;
            default:
                row.Widgets.Add(_w.Label($"포인트 부족 ({skill.Rank}점 필요)", 15, Theme.TextDim));
                break;
        }

        return row;
    }

    private void Learn(SkillDefinition skill)
    {
        if (!_member.Learn(skill.Id, _data))
        {
            return;
        }

        _notice = ($"{Korean.EulReul($"{skill.Name} Lv {_member.SkillLevels[skill.Id]}")} 배웠습니다 (되돌릴 수 없음)", Theme.Heal);
        _changed();
        _dirty = true;
    }
}
