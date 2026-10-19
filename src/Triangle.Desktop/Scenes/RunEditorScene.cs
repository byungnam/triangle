using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Items;
using Triangle.Core.Runs;
using Triangle.Core.Tactics;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 판의 편성: 왼쪽은 멤버와 파티의 스킬 사본, 오른쪽은 고른 멤버의 전술·장비 탭.
/// - 전술: 줄은 줄 상한의 최대치까지 적어 둘 수 있고, 줄 상한(봉인을 뺀 것)을 넘는 아래 줄은 꺼져 있다. 맨 아래에는 늘 "항상 → 기본 공격"이 붙는다.
///   행동은 공용이거나 그 클래스의 카드이고, 카드는 파티가 가진 사본 수만큼의 줄에만 놓을 수 있다.
/// - 장비: 무기·방어구·장신구 둘. 가방의 아이템을 끼고 벗는다.
/// 바꿀 때마다 판에 바로 반영하고 저장한다. 판 규칙이 허락하지 않는 변경은 하지 않고 아래 줄에 이유를 보여준다.
/// </summary>
internal sealed class RunEditorScene : RunScene
{
    private const int MemberListWidth = 250;
    private const int SubjectWidth = 170;
    private const int CompareWidth = 64;
    private const int ValueColumnWidth = 104;
    private const int RemoveConditionWidth = 28;
    private const int ActionButtonWidth = 230;
    private const int PickerWidth = 860;
    private const int PickerHeight = 580;
    private const int PickerListWidth = 400;

    private readonly Action _back;
    private readonly List<ComboView> _combos = [];
    private int _selected;
    private bool _equipmentTab;
    private int? _picking;
    private ScrollPane? _tacticScroll;
    private VerticalStackPanel? _pickerDetail;

    public RunEditorScene(Ui ui, RunSession session, Rectangle bounds, Action back)
        : base(ui, session, bounds, () => { })
    {
        _back = back;
        _selected = Math.Max(0, session.Run!.Members.FindIndex(m => !m.Dead));
    }

    private RunMember Member => Run.Members[_selected];

    /// <summary>시작 화면 지정용: equipment(장비 탭), picker(첫 줄의 행동 고르기).</summary>
    public void Show(string view)
    {
        _equipmentTab = view == "equipment";
        _picking = view == "picker" && Member.Tactics.Count > 0 ? 0 : null;
        Refresh();
    }

    private CombatantSetup Setup => Run.Setup(Data, Member);

    protected override string Title => "편성 — 전술과 장비";

    protected override string Help => "조건이 모두 맞는 첫 줄을 씁니다. 바꾼 것은 바로 저장됩니다.     Esc  지도로";

    private Rectangle EditorArea => new(Body.Left + MemberListWidth + Margin, Body.Top, Body.Width - MemberListWidth - Margin, Body.Height);

    protected override void OnEscape()
    {
        if (_combos.Any(c => c.IsExpanded))
        {
            return;
        }

        if (_picking is not null)
        {
            _picking = null;
            Refresh();
            return;
        }

        _back();
    }

    protected override void DrawBack(SpriteBatch batch)
    {
        Ui.Panel(batch, new Rectangle(Body.Left, Body.Top, MemberListWidth, Body.Height));
        Ui.Panel(batch, EditorArea);
    }

    protected override Widget Build()
    {
        _combos.Clear();
        var root = new Panel();
        root.Widgets.Add(Place(BuildMembers(), Body.Left + 12, Body.Top + 12, MemberListWidth - 24, Body.Height - 24));
        var area = EditorArea;
        var editor = new VerticalStackPanel { Spacing = 10 };
        editor.Widgets.Add(BuildHeader());
        editor.Widgets.Add(_equipmentTab ? BuildEquipment(area.Width - 32) : BuildTactics(area.Height - 24 - 120));
        root.Widgets.Add(Place(editor, area.X + 16, area.Y + 12, area.Width - 32, area.Height - 24));
        return root;
    }

    protected override Widget? BuildPopup() =>
        _picking is { } index && index < Member.Tactics.Count ? BuildActionPicker(index) : null;

    // ── 왼쪽: 멤버와 사본 ──────────────────────────────────

    private Widget BuildMembers()
    {
        var list = new VerticalStackPanel { Spacing = 8 };
        list.Widgets.Add(W.Header("figure", "멤버"));
        for (var i = 0; i < Run.Members.Count; i++)
        {
            var index = i;
            var member = Run.Members[i];
            var c = Data.Classes[member.ClassId];
            var content = new HorizontalStackPanel { Spacing = 8 };
            var (icon, color) = member.Dead ? ("dead", Theme.Death) : RunText.Portrait(c, Data);
            content.Widgets.Add(W.Icon(icon, 34, color));
            var text = new VerticalStackPanel { Spacing = 1 };
            text.Widgets.Add(W.Label(Run.Name(Data, member), 16, member.Dead ? Theme.Death : Theme.Ally, bold: true));
            var (lines, linesColor) = RunText.Lines(member);
            text.Widgets.Add(W.Label(lines, 13, linesColor));
            content.Widgets.Add(text);
            var button = Widgets.StyledButton(content, index == _selected ? Theme.Selected : Theme.Button, Theme.ButtonHover);
            button.Width = MemberListWidth - 24;
            button.Padding = new Thickness(8, 5);
            button.Enabled = !member.Dead;
            button.Click += (_, _) =>
            {
                _selected = index;
                _picking = null;
                Refresh();
            };
            list.Widgets.Add(button);
        }

        list.Widgets.Add(new Panel { Height = 6 });
        list.Widgets.Add(W.Header("card", "파티 사본", "놓음/가짐", size: 15));
        foreach (var (id, count) in Run.Copies.OrderBy(c => Data.Actions[c.Key].Name, StringComparer.Ordinal))
        {
            var placed = Placed(id);
            var color = placed < count ? Theme.Cover : Theme.TextDim;
            list.Widgets.Add(W.Label($"{Data.Actions[id].Name}  {placed}/{count}", 14, color));
        }

        return new ScrollPane { Content = list, ShowHorizontalScrollBar = false };
    }

    /// <summary>살아 있는 멤버 전원이 이 행동을 놓은 줄 수.</summary>
    private int Placed(string actionId) => Run.Living.Sum(m => m.Tactics.Count(t => t.ActionId == actionId));

    // ── 오른쪽 머리: 이름, 줄 상한, 탭 ──────────────────────────

    private Widget BuildHeader()
    {
        var member = Member;
        var c = Data.Classes[member.ClassId];
        var row = new HorizontalStackPanel { Spacing = 12 };
        var (icon, color) = RunText.Portrait(c, Data);
        row.Widgets.Add(W.IconTile(icon, 64, color));
        var names = new VerticalStackPanel { Spacing = 4, Width = 420 };
        var title = new HorizontalStackPanel { Spacing = 8 };
        title.Widgets.Add(W.Label(Run.Name(Data, member), 24, Theme.Ally, bold: true));
        foreach (var side in new[] { Triangle.Core.Units.Row.Front, Triangle.Core.Units.Row.Back })
        {
            var chosen = member.Row == side;
            var button = W.TextButton(RunText.Row(side), chosen ? RunText.RowColor(side) : Theme.Button, chosen ? RunText.RowColor(side) : Theme.ButtonHover, bold: chosen, size: 14);
            button.Width = 64;
            button.Padding = new Thickness(0, 3);
            button.Tooltip = chosen ? $"지금 {RunText.Row(side)}" : $"{RunText.Row(side)}로 옮긴다";
            button.Click += (_, _) =>
            {
                Run.SetRow(member, side);
                Session.Notice = null;
                Session.Save();
                Refresh();
            };
            title.Widgets.Add(button);
        }

        names.Widgets.Add(title);
        var preview = UnitPreview.For(Setup, Run.Catalog(Data), CombatRules.Default);
        var passives = member.Passives.Count == 0 ? "패시브 없음" : string.Join(", ", member.Passives.Select(p => $"{Data.Skills[p.Key].Name} Lv{p.Value}"));
        names.Widgets.Add(W.Label($"HP {preview.MaxHp} · MP {preview.MaxMp} · {passives}", 14, Theme.TextDim, width: 420, wrap: true));
        var (lines, linesColor) = RunText.Lines(member);
        names.Widgets.Add(W.Label($"{lines} — 위에서 {member.Lines}줄이 켜집니다 (최대 {Data.Run!.MaxLines}줄까지 적어 둘 수 있음)", 14, linesColor, width: 420, wrap: true));
        row.Widgets.Add(names);

        var tabs = new HorizontalStackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Bottom };
        foreach (var (label, equipment) in new[] { ("전술", false), ("장비", true) })
        {
            var tab = W.TextButton(label, equipment == _equipmentTab ? Theme.Selected : Theme.Button, Theme.ButtonHover, bold: equipment == _equipmentTab);
            tab.Width = 110;
            tab.Click += (_, _) =>
            {
                _equipmentTab = equipment;
                _picking = null;
                Refresh();
            };
            tabs.Widgets.Add(tab);
        }

        row.Widgets.Add(tabs);
        return row;
    }

    // ── 전술 ──────────────────────────────────────────────

    /// <summary>전술 줄을 바꾼다. 판 규칙이 허락하면 반영하고 저장한다.</summary>
    private bool Edit(Action<TacticList> change, bool rebuild = true)
    {
        var list = new TacticList(Member.Tactics);
        change(list);
        if (Run.WhyCannotSetTactics(Data, Member, list.ToList()) is { } why)
        {
            Refuse(why);
            return false;
        }

        Run.SetTactics(Data, Member, list.ToList());
        Session.Notice = null;
        Session.Save();
        if (rebuild)
        {
            Refresh();
        }

        return true;
    }

    private Widget BuildTactics(int height)
    {
        var member = Member;
        var rows = new VerticalStackPanel { Spacing = 8 };
        for (var i = 0; i < member.Tactics.Count; i++)
        {
            rows.Widgets.Add(BuildTacticRow(i));
        }

        var last = new HorizontalStackPanel { Spacing = 8, Margin = new Thickness(9, 0, 0, 0) };
        var badge = Badge("끝", Theme.PanelBorder);
        last.Widgets.Add(badge);
        last.Widgets.Add(W.Label("항상 → 기본 공격 (켜진 줄이 모두 맞지 않으면. 늘 맨 아래에 붙는다)", 15, Theme.TextDim));
        rows.Widgets.Add(last);

        var add = Button("+ 줄 추가", () => Edit(list => list.Add(Condition.Always, 0, FirstFreeAction() ?? "basic_attack")),
            member.Tactics.Count >= Data.Run!.MaxLines ? $"줄은 {Data.Run.MaxLines}개까지다" : null, width: 140);
        rows.Widgets.Add(add);

        var position = _tacticScroll?.ScrollPosition ?? Point.Zero;
        _tacticScroll = new ScrollPane { Content = rows, ShowHorizontalScrollBar = false, Height = height };
        _tacticScroll.ScrollPosition = position;
        return _tacticScroll;
    }

    /// <summary>새 줄에 놓을 행동: 남은 사본이 있는 첫 클래스 카드. 없으면 null.</summary>
    private string? FirstFreeAction() =>
        Data.Classes[Member.ClassId].Skills.FirstOrDefault(id => Run.Copies.GetValueOrDefault(id) > Placed(id));

    private Panel Badge(string text, Color color)
    {
        var badge = new Panel { Width = 30, Height = 30, Background = new SolidBrush(color), VerticalAlignment = VerticalAlignment.Center };
        var label = W.Label(text, text.Length > 1 ? 13 : 17, Theme.BarBack, bold: true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        badge.Widgets.Add(label);
        return badge;
    }

    private Widget BuildTacticRow(int index)
    {
        var member = Member;
        var tactic = member.Tactics[index];
        var on = index < member.Lines;
        var action = Data.Actions[tactic.ActionId];

        var first = new HorizontalStackPanel { Spacing = 8 };
        first.Widgets.Add(Badge($"{index + 1}", on ? Theme.Gold : Theme.TextDim));
        AddConditionEditor(first, index, 0);

        var (icon, color) = GameIcons.Action(action, Data);
        var content = new HorizontalStackPanel { Spacing = 6 };
        content.Widgets.Add(W.Icon(icon, 22, color));
        content.Widgets.Add(W.Label(action.Name, 16, Theme.Text, bold: true));
        content.Widgets.Add(W.Label("▾", 14, Theme.TextDim));
        var actionButton = Widgets.StyledButton(content, Theme.Button, Theme.ButtonHover);
        actionButton.Width = ActionButtonWidth;
        actionButton.Padding = new Thickness(8, 4);
        actionButton.Click += (_, _) =>
        {
            _picking = index;
            Refresh();
        };
        first.Widgets.Add(actionButton);
        first.Widgets.Add(SmallButton("▲", index > 0, () => Edit(list => list.Move(index, -1))));
        first.Widgets.Add(SmallButton("▼", index < member.Tactics.Count - 1, () => Edit(list => list.Move(index, 1))));
        first.Widgets.Add(SmallButton("삭제", true, () => Edit(list => list.Remove(index)), 52));

        var block = new VerticalStackPanel
        {
            Spacing = 3,
            Padding = new Thickness(8, 6),
            Background = new SolidBrush(on ? Theme.BarBack * 0.7f : Theme.BarBack * 0.35f),
            Border = new SolidBrush(on ? Theme.PanelBorder : Theme.TextDim * 0.5f),
            BorderThickness = new Thickness(1),
        };
        block.Widgets.Add(first);
        for (var k = 1; k < tactic.Conditions.Count; k++)
        {
            var row = new HorizontalStackPanel { Spacing = 8 };
            row.Widgets.Add(W.Label("그리고", 12, Theme.Gold, bold: true, width: 30));
            AddConditionEditor(row, index, k);
            block.Widgets.Add(row);
        }

        var bottom = new HorizontalStackPanel { Spacing = 8, Margin = new Thickness(38, 0, 0, 0) };
        if (tactic.Conditions.Count < Tactic.MaxConditions)
        {
            var and = W.TextButton("+ 그리고", Theme.Button, Theme.ButtonHover, size: 13);
            and.Width = 76;
            and.Click += (_, _) => Edit(list => list.AddCondition(index, Condition.Always, 0));
            bottom.Widgets.Add(and);
        }

        if (!on)
        {
            bottom.Widgets.Add(W.Chip("꺼짐", Theme.Accent, 12));
        }

        var preview = ActionPreview.For(Setup, action, Run.Catalog(Data));
        var effect = action.Applies.FirstOrDefault() is { } applied ? Data.Effects[applied.EffectId].Name : null;
        var summary = on
            ? $"{TacticText.Sentence(tactic, effect)} → {ActionText.Summary(action, Data, preview)}" + (ActionText.Cost(action, preview) is { Length: > 0 } cost ? $" · {cost}" : "")
            : $"줄 상한({member.Lines})을 넘어 꺼진 줄. 줄 상한이 늘거나 봉인이 풀리면 켜진다.";
        bottom.Widgets.Add(W.Label(summary, 13, on ? Theme.TextDim : Theme.Cover, width: 700, wrap: true));
        block.Widgets.Add(bottom);
        return block;
    }

    /// <summary>조건 하나: 무엇을 · 이상/이하 · %/수치 · 값 · 빼기 (옛 편성 화면과 같은 칸).</summary>
    private void AddConditionEditor(HorizontalStackPanel row, int index, int conditionIndex)
    {
        var condition = Member.Tactics[index].Conditions[conditionIndex];
        TacticCondition Current() => Member.Tactics[index].Conditions[conditionIndex];

        void SetCondition(Condition next)
        {
            var current = Current();
            if (next != current.Condition)
            {
                Edit(list => list.SetCondition(index, conditionIndex, next, TacticText.ValueAfterConditionChange(current.Condition, next, current.Value)));
            }
        }

        var (subject, atLeast, percent) = TacticText.Split(condition.Condition);
        var subjectCombo = Combo(TacticText.Subjects.Select(s => (s.Label, s.Color)), subject, SubjectWidth);
        subjectCombo.DropdownMaximumHeight = 470;
        subjectCombo.SelectedIndexChanged += (_, _) =>
        {
            var next = subjectCombo.SelectedIndex ?? 0;
            var (_, a, p) = TacticText.Split(Current().Condition);
            var key = TacticText.Subjects[next].Key;
            var hpMp = key.EndsWith("Hp") || key.EndsWith("Mp");
            SetCondition(TacticText.Compose(next, a ?? !hpMp, p ?? true));
        };
        row.Widgets.Add(subjectCombo);

        if (atLeast is { } isAtLeast)
        {
            var compare = Combo([("이상", Theme.Text), ("이하", Theme.Text)], isAtLeast ? 0 : 1, CompareWidth);
            compare.SelectedIndexChanged += (_, _) => SetCondition(TacticText.Compose(subject, compare.SelectedIndex == 0, percent ?? true));
            row.Widgets.Add(compare);
        }
        else
        {
            row.Widgets.Add(new Panel { Width = CompareWidth });
        }

        if (percent is { } isPercent)
        {
            var unit = Combo([("%", Theme.Text), ("수치", Theme.Text)], isPercent ? 0 : 1, CompareWidth);
            unit.SelectedIndexChanged += (_, _) => SetCondition(TacticText.Compose(subject, atLeast ?? true, unit.SelectedIndex == 0));
            row.Widgets.Add(unit);
        }
        else
        {
            row.Widgets.Add(new Panel { Width = CompareWidth });
        }

        row.Widgets.Add(TacticText.HasValue(condition.Condition)
            ? BuildValueInput(index, conditionIndex)
            : W.Label("—", 17, Theme.TextDim, width: ValueColumnWidth));

        if (Member.Tactics[index].Conditions.Count > 1)
        {
            row.Widgets.Add(SmallButton("×", true, () => Edit(list => list.RemoveCondition(index, conditionIndex)), RemoveConditionWidth));
        }
        else
        {
            row.Widgets.Add(new Panel { Width = RemoveConditionWidth });
        }
    }

    /// <summary>값 입력칸. 올바른 값은 화면을 다시 만들지 않고 바로 반영하고(입력 포커스를 지키려고), 틀린 값은 빨갛게 둔다.</summary>
    private Widget BuildValueInput(int index, int conditionIndex)
    {
        var condition = Member.Tactics[index].Conditions[conditionIndex];
        var group = new HorizontalStackPanel { Spacing = 6, Width = ValueColumnWidth };
        var box = new TextBox
        {
            Text = condition.Value.ToString(),
            Width = 56,
            Font = Ui.Font(17),
            TextColor = Theme.Text,
            FocusedTextColor = Theme.Text,
            Background = new SolidBrush(Theme.BarBack),
            Padding = new Thickness(6, 3),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var hint = W.Label(TacticText.Unit(condition.Condition), 14, Theme.TextDim);
        var allowNegative = condition.Condition.IsAmount();
        box.ValueChanging += (_, e) =>
        {
            var text = e.NewValue ?? "";
            var digits = allowNegative && text.StartsWith('-') ? text[1..] : text;
            if (text.Length > 9 || !digits.All(char.IsAsciiDigit))
            {
                e.Cancel = true;
            }
        };
        box.TextChangedByUser += (_, _) =>
        {
            var current = Member.Tactics[index].Conditions[conditionIndex];
            var error = int.TryParse(box.Text, out var value) ? TacticText.ValidationError(current.Condition, value) : "숫자만";
            var color = error is null ? Theme.Text : Theme.Enemy;
            box.TextColor = color;
            box.FocusedTextColor = color;
            hint.Text = error ?? TacticText.Unit(current.Condition);
            hint.TextColor = error is null ? Theme.TextDim : Theme.Enemy;
            if (error is null && value != current.Value)
            {
                Edit(list => list.SetCondition(index, conditionIndex, current.Condition, value), rebuild: false);
            }
        };

        group.Widgets.Add(box);
        group.Widgets.Add(hint);
        return group;
    }

    // ── 행동 고르기 (팝업) ──────────────────────────────────

    /// <summary>
    /// 한 줄의 행동을 고르는 팝업: 공용 행동과 이 클래스의 카드. 카드는 남은 사본(가진 수 - 다른 줄에 놓은 수)이 있어야 고른다.
    /// </summary>
    private Widget BuildActionPicker(int index)
    {
        var tactic = Member.Tactics[index];
        var popup = Box(PickerWidth, PickerHeight, border: Theme.Gold, spacing: 10);
        popup.Padding = new Thickness(18, 14);

        var head = new HorizontalStackPanel { Spacing = 12 };
        var titles = new VerticalStackPanel { Spacing = 2, Width = PickerWidth - 36 - 130 };
        titles.Widgets.Add(W.Header("tactics", $"{index + 1}번째 줄 · 쓸 행동 고르기", size: 20));
        titles.Widgets.Add(W.Label($"조건 \"{TacticText.Sentence(tactic)}\"이 모두 맞으면 이 행동을 쓴다.", 14, Theme.TextDim));
        head.Widgets.Add(titles);
        var close = Button("닫기 (Esc)", () =>
        {
            _picking = null;
            Refresh();
        }, width: 120);
        close.VerticalAlignment = VerticalAlignment.Top;
        head.Widgets.Add(close);
        popup.Widgets.Add(head);

        var bodyHeight = PickerHeight - 28 - 60;
        var body = new HorizontalStackPanel { Spacing = 14, Height = bodyHeight };
        var list = new VerticalStackPanel { Spacing = 4, Width = PickerListWidth - 18 };
        var c = Data.Classes[Member.ClassId];
        void Section(string title, string icon, IEnumerable<ActionDefinition> actions)
        {
            var header = W.Header(icon, title, size: 15);
            header.Margin = new Thickness(0, list.Widgets.Count == 0 ? 0 : 8, 0, 2);
            list.Widgets.Add(header);
            foreach (var action in actions)
            {
                list.Widgets.Add(BuildPickerRow(index, action));
            }
        }

        var owned = c.Skills.Where(id => Run.Copies.GetValueOrDefault(id) > 0).Select(id => Data.Actions[id]).ToList();
        Section($"{c.Name} 카드 (가진 사본)", "card", owned);
        if (owned.Count == 0)
        {
            list.Widgets.Add(W.Label("아직 사본이 없습니다. 보상과 상점에서 얻습니다.", 14, Theme.TextDim));
        }

        Section("공용 (카드 없이 누구나)", "figure", Data.Actions.Values.Where(a => a.Universal));
        body.Widgets.Add(new ScrollPane { Content = list, ShowHorizontalScrollBar = false, Width = PickerListWidth, Height = bodyHeight });

        var detailWidth = PickerWidth - 36 - PickerListWidth - 14;
        _pickerDetail = new VerticalStackPanel { Spacing = 8, Width = detailWidth - 18 };
        body.Widgets.Add(new ScrollPane { Content = _pickerDetail, ShowHorizontalScrollBar = false, Width = detailWidth, Height = bodyHeight });
        popup.Widgets.Add(body);
        ShowPickerDetail(index, Data.Actions[tactic.ActionId]);
        return Overlay(popup);
    }

    /// <summary>이 줄에 놓을 수 있는 남은 사본 (지금 이 줄의 행동이면 그 사본을 그대로 쓴다).</summary>
    private int FreeCopies(int index, ActionDefinition action) =>
        action.Universal ? int.MaxValue
        : Run.Copies.GetValueOrDefault(action.Id) - Placed(action.Id) + (Member.Tactics[index].ActionId == action.Id ? 1 : 0);

    private Widget BuildPickerRow(int index, ActionDefinition action)
    {
        var free = FreeCopies(index, action);
        var usable = free > 0;
        var (icon, color) = GameIcons.Action(action, Data);
        var content = new HorizontalStackPanel { Spacing = 10 };
        content.Widgets.Add(W.IconTile(usable ? icon : "lock", 38, usable ? color : Theme.TextDim, padding: 5));
        content.Widgets.Add(W.Label(action.Name, 17, usable ? Theme.Text : Theme.TextDim, bold: true, width: 170));
        var copies = action.Universal ? "공용" : $"사본 {Placed(action.Id)}/{Run.Copies.GetValueOrDefault(action.Id)} 놓음";
        content.Widgets.Add(W.Label(copies, 13, usable ? Theme.Cover : Theme.TextDim));

        var current = Member.Tactics[index].ActionId == action.Id;
        var button = Widgets.StyledButton(content, current ? Theme.Selected : Theme.Button, Theme.ButtonHover);
        button.Width = PickerListWidth - 22;
        button.Padding = new Thickness(5, 4);
        button.MouseEntered += (_, _) => ShowPickerDetail(index, action);
        button.Click += (_, _) =>
        {
            if (!usable)
            {
                ShowPickerDetail(index, action);
                return;
            }

            if (Edit(list => list.SetAction(index, action.Id)))
            {
                _picking = null;
            }
        };
        return button;
    }

    /// <summary>팝업 오른쪽 설명. 남은 사본이 없으면 그 이유를 먼저 보여준다.</summary>
    private void ShowPickerDetail(int index, ActionDefinition action)
    {
        if (_pickerDetail is not { } detail)
        {
            return;
        }

        var width = detail.Width ?? 300;
        detail.Widgets.Clear();
        if (FreeCopies(index, action) <= 0)
        {
            detail.Widgets.Add(W.Label("고를 수 없음: 남은 사본이 없다. 다른 줄(다른 멤버 포함)에서 빼거나 보상·상점에서 사본을 더 얻는다.", 15, Theme.Enemy, width: width, wrap: true));
        }

        AddLines(detail, RunText.Action(action, Data, ActionPreview.For(Setup, action, Run.Catalog(Data))), width);
    }

    // ── 장비 ──────────────────────────────────────────────

    private Widget BuildEquipment(int width)
    {
        var member = Member;
        var panel = new VerticalStackPanel { Spacing = 10 };
        var slots = new HorizontalStackPanel { Spacing = 10 };
        var slotWidth = (width - 30) / 4;
        slots.Widgets.Add(SlotCard("무기", member.Weapon, EquipmentSlot.MainHand, 0, slotWidth));
        slots.Widgets.Add(SlotCard("방어구", member.Armor, EquipmentSlot.Body, 0, slotWidth));
        for (var i = 0; i < Data.Run!.MaxTrinkets; i++)
        {
            slots.Widgets.Add(SlotCard($"장신구 {i + 1}", i < member.Trinkets.Count ? member.Trinkets[i] : null, EquipmentSlot.Trinket, i, slotWidth));
        }

        panel.Widgets.Add(slots);
        panel.Widgets.Add(W.Header("equipment", "가방", "파티가 함께 쓴다. 상점과 이벤트에서 얻고, 죽은 멤버의 장비도 여기로 온다."));
        if (Run.Bag.Count == 0)
        {
            panel.Widgets.Add(W.Label("가방이 비었습니다.", 15, Theme.TextDim));
        }

        var bag = new VerticalStackPanel { Spacing = 6 };
        for (var i = 0; i < Run.Bag.Count; i++)
        {
            var index = i;
            var item = Data.Items[Run.Bag[i]];
            var row = new HorizontalStackPanel { Spacing = 10 };
            var (icon, color) = GameIcons.Item(item);
            row.Widgets.Add(W.IconTile(icon, 40, color));
            var lines = RunText.Item(item, Data).Where(l => !l.Text.StartsWith("가격")).ToList();
            var text = new VerticalStackPanel { Spacing = 1, Width = width - 40 - 140 - 30 };
            text.Widgets.Add(W.Label(lines[0].Text, 16, lines[0].Color, bold: true));
            text.Widgets.Add(W.Label(string.Join(" · ", lines.Skip(1).Select(l => l.Text)), 13, Theme.TextDim, width: width - 40 - 140 - 30, wrap: true));
            row.Widgets.Add(text);
            var why = Run.WhyCannotEquip(Data, member, index);
            row.Widgets.Add(Button("끼기", () => Equip(index), why, accent: why is null, width: 120));
            bag.Widgets.Add(row);
        }

        panel.Widgets.Add(new ScrollPane { Content = bag, ShowHorizontalScrollBar = false, Height = 250 });
        return panel;
    }

    private void Equip(int bagIndex)
    {
        Run.Equip(Data, Member, bagIndex);
        Session.Notice = null;
        Session.Save();
        Refresh();
    }

    private Widget SlotCard(string label, string? itemId, EquipmentSlot slot, int trinket, int width)
    {
        var box = Box(width, 230, spacing: 5, background: Theme.BarBack);
        box.Widgets.Add(W.Label(label, 14, Theme.TextDim));
        if (itemId is null)
        {
            box.Widgets.Add(W.IconTile(slot == EquipmentSlot.Trinket ? "trinket" : GameIcons.EmptySlot(slot), 48, Theme.TextDim * 0.6f));
            box.Widgets.Add(W.Label("비어 있음", 16, Theme.TextDim));
            return box;
        }

        var item = Data.Items[itemId];
        var (icon, color) = GameIcons.Item(item);
        box.Widgets.Add(W.IconTile(icon, 48, color));
        var lines = RunText.Item(item, Data).Where(l => !l.Text.StartsWith("가격")).ToList();
        box.Widgets.Add(W.Label(lines[0].Text, 16, lines[0].Color, bold: true, width: width - 24, wrap: true));
        foreach (var line in lines.Skip(2))
        {
            box.Widgets.Add(W.Label(line.Text, 13, line.Color, width: width - 24, wrap: true));
        }

        var takeOff = Button("벗기", () =>
        {
            Run.Unequip(Member, slot, trinket);
            Session.Notice = null;
            Session.Save();
            Refresh();
        }, width: 80, size: 14);
        return new Panel { Widgets = { box, Place(takeOff, width - 92, 6) } };
    }

    // ── 작은 도구 ─────────────────────────────────────────

    private ComboView Combo(IEnumerable<(string Text, Color Color)> items, int selectedIndex, int width)
    {
        var combo = W.Combo(items, selectedIndex, width);
        _combos.Add(combo);
        return combo;
    }

    private Button SmallButton(string text, bool enabled, Action action, int width = 36)
    {
        var button = W.TextButton(text, Theme.Button, Theme.ButtonHover);
        button.Width = width;
        button.Padding = new Thickness(0, 6);
        button.Enabled = enabled;
        button.Click += (_, _) => action();
        return button;
    }
}
