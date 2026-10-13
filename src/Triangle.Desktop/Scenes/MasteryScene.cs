using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Triangle.Core.Actions;
using Triangle.Core.Data;
using Triangle.Core.Items;
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

    /// <summary>오른쪽에서 설명을 보는 관련 행동.</summary>
    private string? _action;
    private ScrollViewer? _list;
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
        _selected = member.WeaponMastery(data) ?? data.Masteries.Keys.First();
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

        batch.Begin(transformMatrix: _ui.Screen.Transform);
        _ui.Text(batch, _ui.BoldFont(28), $"숙련·패시브 — {_member.Name}", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        var gear = $"장비: {ItemText.Gear(_member.Equipment, _data)}";
        _ui.Text(batch, _ui.Font(16), gear, new Vector2(_bounds.Left + Margin, _bounds.Top + 58), Theme.TextDim);

        const string help = "장착한 장비로 싸우면 그 숙련이 오른다. 숙련 레벨 1당 포인트 1점. 배운 패시브는 되돌릴 수 없다.     Esc  편성으로";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);
        if (_notice is { } notice)
        {
            var font = _ui.Font(16);
            _ui.Text(batch, font, notice.Text, new Vector2(_bounds.Right - Margin - font.MeasureString(notice.Text).X, _bounds.Top + 58), notice.Color);
        }

        _ui.Panel(batch, TreeArea);
        batch.End();

        _ui.Screen.Render(_desktop);
    }

    private Rectangle TreeArea => new(
        _bounds.Left + Margin * 2 + ListWidth, _bounds.Top + HeaderHeight,
        _bounds.Width - Margin * 3 - ListWidth, _bounds.Height - HeaderHeight - FooterHeight - 8);

    /// <summary>그 계열의 아이템 중 행동 칸에 그 행동이 있는 것.</summary>
    private IEnumerable<ItemDefinition> ItemsOf(string masteryId, string actionId) =>
        _data.ItemsGranting(actionId).Where(i => i.Mastery == masteryId);

    // ── 위젯 트리 ──────────────────────────────────────────

    private void Rebuild()
    {
        var root = new Panel();

        // 숙련이 21개라 화면 높이를 넘는다. 다시 만들어도 스크롤 위치를 지킨다.
        var area = TreeArea;
        var listPosition = _list?.ScrollPosition ?? Point.Zero;
        _list = new ScrollPane
        {
            Content = BuildMasteryList(),
            ShowHorizontalScrollBar = false,
            Left = _bounds.Left + Margin,
            Top = area.Y,
            Width = ListWidth + 16,
            Height = area.Height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _list.ScrollPosition = listPosition;
        root.Widgets.Add(_list);

        root.Widgets.Add(new ScrollPane
        {
            Content = BuildTree(_data.Masteries[_selected], area.Width - 52),
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

        foreach (var (kind, label) in new[] { (MasteryKind.Weapon, "무기"), (MasteryKind.Armor, "방어구") })
        {
            list.Widgets.Add(_w.Label(label, 17, Theme.Ally, bold: true));
            foreach (var mastery in _data.MasteriesOf(kind))
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
        var equipped = mastery.Id == _member.WeaponMastery(_data) || mastery.Id == _member.ArmorMastery(_data);
        var selected = mastery.Id == _selected;

        var content = new VerticalStackPanel { Spacing = 2 };
        var title = new HorizontalStackPanel { Spacing = 8 };
        title.Widgets.Add(_w.Label(mastery.Name, 17, selected ? Theme.Text : Theme.Ally, bold: true, width: 70));
        title.Widgets.Add(_w.Label($"Lv {level}", 16, Theme.Text, width: 50));
        title.Widgets.Add(_w.Label(points > 0 ? $"포인트 {points}" : "", 15, Theme.Cover, width: 70));
        title.Widgets.Add(_w.Label(equipped ? "장착" : "", 14, Theme.Heal));
        content.Widgets.Add(title);
        var (current, needed) = XpProgress(mastery.Id);
        content.Widgets.Add(Widgets.Bar(current, needed, ListWidth - 70, 4, equipped ? Theme.Heal : Theme.Cover));
        content.Widgets.Add(_w.Label(XpText(mastery.Id), 13, Theme.TextDim));

        var withIcon = new HorizontalStackPanel { Spacing = 8 };
        withIcon.Widgets.Add(_w.IconTile(GameIcons.Mastery(mastery.Id), 40, GameIcons.MasteryColor(mastery.Id),
            border: equipped ? Theme.Heal : null, padding: 5));
        withIcon.Widgets.Add(content);
        var button = Widgets.StyledButton(withIcon, selected ? Theme.Selected : Theme.Panel, Theme.ButtonHover);
        button.Width = ListWidth;
        button.Padding = new Thickness(6, 5);
        button.Click += (_, _) =>
        {
            _selected = mastery.Id;
            _action = null;
            _dirty = true;
        };
        return button;
    }

    /// <summary>이번 레벨 안에서 쌓인 경험치와 다음 레벨까지 필요한 양.</summary>
    private (int Current, int Needed) XpProgress(string masteryId)
    {
        var xp = _member.MasteryXp.GetValueOrDefault(masteryId);
        var level = MasteryProgression.LevelFor(xp);
        if (level >= MasteryProgression.MaxLevel)
        {
            return (1, 1);
        }

        var floor = level == 0 ? 0 : MasteryProgression.XpForLevel(level);
        return (xp - floor, MasteryProgression.XpForLevel(level + 1) - floor);
    }

    private string XpText(string masteryId)
    {
        var xp = _member.MasteryXp.GetValueOrDefault(masteryId);
        var level = MasteryProgression.LevelFor(xp);
        return level >= MasteryProgression.MaxLevel
            ? $"XP {SkillText.Number(xp)} (최대)"
            : $"XP {SkillText.Number(xp)} / {SkillText.Number(MasteryProgression.XpForLevel(level + 1))}";
    }

    private const int ActionColumnWidth = 340;

    /// <summary>
    /// 왼쪽: 숙련 레벨·경험치와 패시브 카드(레벨당 보너스, 지금 → 다음, 선행, 여는 행동, 착용 조건).
    /// 오른쪽: 이 계열과 관련된 행동 목록과 고른 행동의 설명.
    /// </summary>
    private Widget BuildTree(MasteryDefinition mastery, int width)
    {
        var row = new HorizontalStackPanel { Spacing = 16 };
        var skillsWidth = width - ActionColumnWidth - 16;
        row.Widgets.Add(BuildSkillColumn(mastery, skillsWidth));
        row.Widgets.Add(BuildActionColumn(mastery));
        return row;
    }

    private Widget BuildSkillColumn(MasteryDefinition mastery, int width)
    {
        var panel = new VerticalStackPanel { Spacing = 8, Width = width };
        var level = _member.MasteryLevel(mastery.Id);
        var points = _member.PointsAvailable(mastery.Id, _data);

        var header = new HorizontalStackPanel { Spacing = 16 };
        header.Widgets.Add(_w.Label($"{mastery.Name} 숙련 Lv {level}", 22, Theme.Text, bold: true));
        header.Widgets.Add(_w.Label($"남은 포인트 {points}", 18, points > 0 ? Theme.Cover : Theme.TextDim, bold: points > 0));
        panel.Widgets.Add(header);

        var xp = new HorizontalStackPanel { Spacing = 10 };
        var (current, next) = XpProgress(mastery.Id);
        xp.Widgets.Add(Widgets.Bar(current, next, 260, 10, Theme.Cover));
        xp.Widgets.Add(_w.Label(XpText(mastery.Id), 14, Theme.TextDim));
        panel.Widgets.Add(xp);

        var how = mastery.Kind == MasteryKind.Armor
            ? "이 재질 방어구를 입고 피해를 받으면 오른다 (부위마다 1/3)."
            : "이 계열 무기·보조 아이템을 들고 싸우면 오른다.";
        panel.Widgets.Add(_w.Label(
            $"{how} 레벨 1당 패시브 포인트 1점, 이 계열 장비 보너스 +{ItemDefinition.PowerPercentPerMasteryLevel}% (지금 +{level * ItemDefinition.PowerPercentPerMasteryLevel}%).",
            14, Theme.TextDim, width: width, wrap: true));

        var skills = _data.Skills.Values.Where(s => s.Mastery == mastery.Id).ToList();
        if (skills.Count == 0)
        {
            panel.Widgets.Add(_w.Label("이 트리에는 아직 패시브가 없다.", 15, Theme.TextDim));
        }

        foreach (var skill in skills)
        {
            panel.Widgets.Add(BuildSkillCard(skill, width));
        }

        return panel;
    }

    /// <summary>이 계열 아이템이 주는 행동, 이 계열 무기가 주는 행동, 이 트리의 패시브가 여는 행동.</summary>
    private List<ActionDefinition> RelatedActions(MasteryDefinition mastery)
    {
        var treeSkills = _data.Skills.Values.Where(s => s.Mastery == mastery.Id).Select(s => s.Id).ToHashSet();
        return _data.Actions.Values
            .Where(a => !a.SummonOnly && (ItemsOf(mastery.Id, a.Id).Any() || a.Weapon == mastery.Id || a.Requirements.Any(r => treeSkills.Contains(r.SkillId))))
            .ToList();
    }

    private Widget BuildActionColumn(MasteryDefinition mastery)
    {
        var panel = new VerticalStackPanel { Spacing = 6, Width = ActionColumnWidth };
        var related = RelatedActions(mastery);
        panel.Widgets.Add(_w.Header("action.melee", "관련 행동", "눌러서 설명 보기"));
        if (related.Count == 0)
        {
            panel.Widgets.Add(_w.Label("없음", 15, Theme.TextDim));
            return panel;
        }

        var set = _member.Skills(_data);
        var shown = related.FirstOrDefault(a => a.Id == _action) ?? related[0];
        foreach (var action in related)
        {
            var missing = set.Missing(action.Requirements);
            var content = new HorizontalStackPanel { Spacing = 8 };
            var (icon, color) = GameIcons.Action(action, _data);
            content.Widgets.Add(_w.IconTile(missing.Count == 0 ? icon : "lock", 30, missing.Count == 0 ? color : Theme.Enemy, padding: 4));
            content.Widgets.Add(_w.Label(action.Name, 15, missing.Count == 0 ? Theme.Heal : Theme.Text, bold: true));
            content.Widgets.Add(_w.Label(missing.Count == 0
                ? "쓸 수 있음"
                : string.Join(", ", missing.Select(m => $"{_data.Skills[m.SkillId].Name} {m.Level}")) + " 필요", 13, missing.Count == 0 ? Theme.TextDim : Theme.Enemy));
            var button = Widgets.StyledButton(content, action == shown ? Theme.Selected : Theme.Button, Theme.ButtonHover);
            button.Width = ActionColumnWidth;
            button.Padding = new Thickness(5, 3);
            button.Click += (_, _) =>
            {
                _action = action.Id;
                _dirty = true;
            };
            panel.Widgets.Add(button);
        }

        panel.Widgets.Add(new Panel { Height = 4 });
        panel.Widgets.Add(_w.Card(ActionText.Details(shown, _data, null, set), ActionColumnWidth));
        return panel;
    }

    private Widget BuildSkillCard(SkillDefinition skill, int width)
    {
        var level = _member.SkillLevels.GetValueOrDefault(skill.Id);
        var card = new VerticalStackPanel { Spacing = 3, Width = width, Padding = new Thickness(12, 8), Background = new SolidBrush(Theme.BarBack) };

        var top = new HorizontalStackPanel { Spacing = 12 };
        top.Widgets.Add(_w.Label(skill.Name, 18, level > 0 ? Theme.Text : Theme.TextDim, bold: true, width: 140));
        top.Widgets.Add(_w.Label(SkillText.LevelPips(level), 16, Theme.Cover, width: 80));
        top.Widgets.Add(_w.Label($"Lv당 {skill.Rank}점", 14, Theme.TextDim, width: 64));

        switch (_member.WhyCannotLearn(skill.Id, _data))
        {
            case null:
                var button = _w.TextButton($"배우기 → Lv {level + 1}", Theme.Accent, Theme.AccentHover, size: 15);
                button.Padding = new Thickness(10, 3);
                button.Click += (_, _) => Learn(skill);
                top.Widgets.Add(button);
                break;
            case { Kind: LearnBlockerKind.MaxLevel }:
                top.Widgets.Add(_w.Label("최대 레벨", 15, Theme.Heal));
                break;
            case { Kind: LearnBlockerKind.Prerequisites } blocker:
                top.Widgets.Add(_w.Label("선행: " + string.Join(", ", blocker.Missing.Select(m => $"{_data.Skills[m.SkillId].Name} {m.Level}")), 14, Theme.Enemy));
                break;
            default:
                top.Widgets.Add(_w.Label($"포인트 부족 ({skill.Rank}점 필요)", 14, Theme.TextDim));
                break;
        }

        card.Widgets.Add(top);

        // 보너스: 레벨당, 지금, 다음 레벨
        foreach (var bonus in skill.Bonuses)
        {
            var now = SkillText.BonusLabel(bonus.Kind, bonus.PercentPerLevel * level, bonus.Tag, _data);
            var text = $"레벨당 {SkillText.BonusLabel(bonus, _data)}";
            text += level >= SkillDefinition.MaxLevel ? $" · 지금 {now} (최대)"
                : level == 0 ? $" · 배우면 {SkillText.BonusLabel(bonus.Kind, bonus.PercentPerLevel, bonus.Tag, _data)}"
                : $" · 지금 {now} → 다음 {SkillText.BonusLabel(bonus.Kind, bonus.PercentPerLevel * (level + 1), bonus.Tag, _data)}";
            card.Widgets.Add(_w.Label(text, 14, level > 0 ? Theme.Text : Theme.TextDim, width: width - 24, wrap: true));
        }

        if (skill.Prerequisites.Count > 0)
        {
            var prerequisites = string.Join(", ", skill.Prerequisites.Select(p => $"{_data.Skills[p.SkillId].Name} Lv{p.Level}"));
            card.Widgets.Add(_w.Label($"선행 패시브: {prerequisites}", 13, Theme.TextDim, width: width - 24, wrap: true));
        }

        var opens = _data.Actions.Values.Where(a => !a.SummonOnly)
            .SelectMany(a => a.Requirements.Where(r => r.SkillId == skill.Id).Select(r => (a, r.Level)))
            .Select(x => $"{x.a.Name} (Lv{x.Level}{(level >= x.Level ? ", 열림" : "")})").Distinct().ToList();
        if (opens.Count > 0)
        {
            card.Widgets.Add(_w.Label($"여는 행동: {string.Join(", ", opens)}", 13, Theme.Ally, width: width - 24, wrap: true));
        }

        var gear = _data.Items.Values
            .SelectMany(i => i.Requirements.Where(r => r.SkillId == skill.Id).Select(r => (i, r.Level)))
            .Select(x => $"T{x.i.Tier} {x.i.Name} Lv{x.Level}").ToList();
        if (gear.Count > 0)
        {
            var text = gear.Count > 4 ? $"{string.Join(", ", gear.Take(4))} 외 {gear.Count - 4}개" : string.Join(", ", gear);
            card.Widgets.Add(_w.Label($"착용 조건이 되는 장비: {text}", 13, Theme.TextDim, width: width - 24, wrap: true));
        }

        return card;
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
