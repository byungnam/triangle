using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using MyraDesktop = Myra.Graphics2D.UI.Desktop;
using Triangle.Core.Actions;
using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Items;
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 캐릭터의 장비, 전열, 전술을 편집한다. 마을에서는 로스터 전원을, 원정 중에는 출전 멤버만 보여준다.
/// 원정 중에는 전술(세트 선택 포함)과 전열만 바꿀 수 있다: 장비는 잠그고, 숙련·패시브와 저장 버튼은 숨긴다.
/// 위젯은 Myra로 그린다. 편집할 때마다 위젯 트리를 다시 만든다(화면이 작아서 충분히 빠르다).
/// </summary>
internal sealed class TacticEditorScene : IScene
{
    private const int Margin = 24;
    private const int HeaderHeight = 64;
    private const int FooterHeight = 40;
    private const int PartyWidth = 250;
    private const int CombatBarHeight = 56;
    private const int MaxTactics = 10;

    private readonly Ui _ui;
    private readonly GameSession _session;
    private readonly GameData _data;
    private readonly Company _company;
    private readonly Rectangle _bounds;
    private readonly Action<PartyMember> _openTraining;
    private readonly Action _back;
    private readonly MyraDesktop _desktop = new();
    private readonly Widgets _widgets;
    private readonly List<ComboView> _combos = [];

    private int _selected;
    private bool _dirty = true;
    private Button? _saveButton;
    private Label? _statusLabel;

    /// <param name="back">Esc나 "돌아가기"를 누르면 호출된다 (마을이나 원정 화면으로).</param>
    public TacticEditorScene(Ui ui, GameSession session, Rectangle bounds, Action<PartyMember> openTraining, Action back)
    {
        _ui = ui;
        _widgets = new Widgets(ui);
        _session = session;
        _data = session.Data;
        _company = session.Company;
        _bounds = bounds;
        _openTraining = openTraining;
        _back = back;
    }

    private bool OnExpedition => _company.OnExpedition;

    /// <summary>왼쪽 목록의 캐릭터: 마을에서는 로스터 전원, 원정 중에는 출전 멤버.</summary>
    private IReadOnlyList<PartyMember> Members => OnExpedition ? _company.LineupMembers : _company.Roster;

    private PartyMember Selected => Members[Math.Min(_selected, Members.Count - 1)];

    /// <summary>지금 고른 세트의 전술 (편집도 전투도 이 세트로 한다).</summary>
    private TacticList TacticsOf(PartyMember member) => member.TacticSets[_company.ActiveTacticSet];

    /// <summary>다른 화면에서 돌아왔다. 목록과 값을 다시 그린다.</summary>
    public void Refresh() => MarkDirty();

    public void Update(GameTime gameTime, Input input)
    {
        // 드롭다운이 열려 있을 때의 Esc는 드롭다운을 닫는 데 쓴다.
        if (input.Pressed(Keys.Escape) && !_combos.Any(c => c.IsExpanded))
        {
            _back();
            return;
        }

        if (!OnExpedition && input.Pressed(Keys.S) && input.IsDown(Keys.LeftControl, Keys.RightControl))
        {
            Save();
        }

        // 위젯 이벤트 처리 중에 트리를 갈아끼우지 않도록 다음 프레임에 다시 만든다.
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
        var title = OnExpedition ? "전술 편집 — 원정 중 (전술과 전열만 바꿀 수 있습니다)" : "전술 편집";
        _ui.Text(batch, _ui.BoldFont(28), title, new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        var help = OnExpedition
            ? "조건이 참인 첫 전술을 쓴다. 대상은 행동이 정한다.     Esc  원정으로"
            : "조건이 참인 첫 전술을 쓴다. 대상은 행동이 정한다.     Ctrl+S  저장     Esc  마을로";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);

        if (_session.Notice is { } notice)
        {
            var font = _ui.Font(16);
            var width = font.MeasureString(notice.Text).X;
            _ui.Text(batch, font, notice.Text, new Vector2(_bounds.Right - Margin - width, _bounds.Bottom - FooterHeight + 10), notice.Color);
        }

        var memberArea = MemberArea;
        _ui.Panel(batch, new Rectangle(memberArea.X - 16, memberArea.Y - 12, memberArea.Width + 32, memberArea.Height + 24));
        batch.End();

        _desktop.Render();
    }

    private Rectangle MemberArea
    {
        get
        {
            var left = _bounds.Left + Margin + PartyWidth + Margin + 16;
            var top = _bounds.Top + HeaderHeight + 12;
            var right = _bounds.Right - Margin - 16;
            var bottom = _bounds.Bottom - FooterHeight - CombatBarHeight - 12;
            return new Rectangle(left, top, right - left, bottom - top);
        }
    }

    private void MarkDirty() => _dirty = true;

    /// <summary>
    /// 화면을 다시 만들지 않고 "저장하지 않은 변경" 표시만 갱신한다 (값 입력칸에서 입력 중일 때).
    /// </summary>
    private void MarkChangedInPlace()
    {
        _session.MarkChanged();
        if (_saveButton is not null)
        {
            _saveButton.Background = new SolidBrush(Theme.Accent);
            _saveButton.OverBackground = new SolidBrush(Theme.AccentHover);
        }

        if (_statusLabel is not null && !_company.HasLockedTactics(_data))
        {
            _statusLabel.Text = StatusText;
            _statusLabel.TextColor = Theme.Cover;
        }
    }

    private void MarkChanged()
    {
        _session.MarkChanged();
        MarkDirty();
    }

    private void Save()
    {
        _session.Save();
        MarkDirty();
    }

    /// <summary>잠긴 전술이 없을 때의 상태 줄. 원정 중에는 다음 전투 전에 자동 저장된다.</summary>
    private string StatusText => !_session.Unsaved ? "" : OnExpedition ? "바꾼 전술은 다음 전투에 쓰입니다" : "저장하지 않은 변경이 있습니다";

    // ── 위젯 트리 ──────────────────────────────────────────

    private void Rebuild()
    {
        _combos.Clear();
        var root = new Panel();

        var partyList = BuildPartyList();
        partyList.Left = _bounds.Left + Margin;
        partyList.Top = _bounds.Top + HeaderHeight;
        root.Widgets.Add(partyList);

        var memberArea = MemberArea;
        var editor = BuildMemberEditor(memberArea);
        editor.Left = memberArea.X;
        editor.Top = memberArea.Y;
        root.Widgets.Add(editor);

        var combatBar = BuildBottomBar();
        combatBar.Left = memberArea.X - 16;
        combatBar.Top = _bounds.Bottom - FooterHeight - CombatBarHeight + 8;
        root.Widgets.Add(combatBar);

        _desktop.Root = root;
    }

    private Widget BuildPartyList()
    {
        var list = new VerticalStackPanel
        {
            Spacing = 8,
            Width = PartyWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        list.Widgets.Add(Label(OnExpedition ? "출전 멤버" : "로스터", 20, Theme.Ally, bold: true));
        list.Widgets.Add(BuildTacticSetSelector());

        for (var i = 0; i < Members.Count; i++)
        {
            var member = Members[i];
            var index = i;
            var selected = member == Selected;

            var content = new VerticalStackPanel { Spacing = 4 };
            content.Widgets.Add(Label(member.Name, 20, selected ? Theme.Text : Theme.Ally, bold: true));
            var state = _company.Expedition?.MemberState(member.Id) is { Down: true } ? " · 쓰러짐"
                : !OnExpedition && _company.IsInLineup(member.Id) ? " · 출전" : "";
            content.Widgets.Add(Label($"{RowLabel(member.Row)} · 전술 {TacticsOf(member).Count}개{state}", 15, Theme.TextDim));
            if (member.LockedTacticIndexes(_data, _company.ActiveTacticSet).Count is > 0 and var locked)
            {
                content.Widgets.Add(Label($"잠긴 전술 {locked}개", 15, Theme.Enemy));
            }

            var button = StyledButton(content, selected ? Theme.Selected : Theme.Panel, Theme.ButtonHover);
            button.Width = PartyWidth;
            button.Padding = new Thickness(14, 10);
            button.Click += (_, _) =>
            {
                _selected = index;
                MarkDirty();
            };
            list.Widgets.Add(button);
        }

        return list;
    }

    private Widget BuildMemberEditor(Rectangle area)
    {
        var member = Selected;
        var rules = CombatRules.Default;
        var skills = member.Skills(_data);

        var panel = new VerticalStackPanel { Spacing = 12, Width = area.Width, Height = area.Height };

        var title = new HorizontalStackPanel { Spacing = 12 };
        title.Widgets.Add(Label(member.Name, 26, Theme.Text, bold: true));
        if (!OnExpedition)
        {
            var training = TextButton("숙련·패시브", Theme.Button, Theme.ButtonHover);
            training.Click += (_, _) => _openTraining(member);
            title.Widgets.Add(training);
        }

        title.Widgets.Add(Label(SkillSummary(skills), 16, Theme.TextDim));
        panel.Widgets.Add(title);

        panel.Widgets.Add(BuildEquipmentSelector(member));

        var s = member.Stats;
        panel.Widgets.Add(Label(
            $"근력 {s.Str}   민첩 {s.Dex}   체력 {s.Vital}   지능 {s.Intel}   신속 {s.Speed}" +
            $"        HP {rules.MaxHp(s, skills)}   MP {rules.MaxMp(s, skills)}",
            16, Theme.TextDim));

        panel.Widgets.Add(BuildRowSelector(member));
        panel.Widgets.Add(new HorizontalSeparator());
        panel.Widgets.Add(BuildTacticHeader());

        var tactics = new VerticalStackPanel { Spacing = 6 };
        for (var i = 0; i < TacticsOf(member).Count; i++)
        {
            tactics.Widgets.Add(BuildTacticRow(member, i));
        }

        if (TacticsOf(member).Count == 0)
        {
            tactics.Widgets.Add(Label("전술이 없으면 이 유닛은 매 턴 기다린다.", 16, Theme.TextDim));
        }

        // 새 전술은 쓸 수 있는 첫 행동으로 시작한다.
        var firstUsable = _data.Actions.Values.FirstOrDefault(a => a.IsUsableBy(member.WeaponMastery(_data), skills));
        var addButton = TextButton("+ 전술 추가", Theme.Button, Theme.ButtonHover);
        addButton.Enabled = TacticsOf(member).Count < MaxTactics && firstUsable is not null;
        addButton.Click += (_, _) =>
        {
            TacticsOf(member).Add(Condition.Always, 0, firstUsable!.Id);
            MarkChanged();
        };
        tactics.Widgets.Add(addButton);

        panel.Widgets.Add(new ScrollViewer
        {
            Content = tactics,
            ShowHorizontalScrollBar = false,
        });
        StackPanel.SetProportionType(panel.Widgets[^1], ProportionType.Fill);

        return panel;
    }

    /// <summary>
    /// 전술 세트 선택. 파티 전원이 같은 번호의 세트로 바뀌고, 편집도 전투도 그 세트로 한다.
    /// 상대에 따라 세트를 바꿔 쓰는 용도. 고른 세트는 세이브에 함께 저장된다.
    /// </summary>
    private Widget BuildTacticSetSelector()
    {
        var row = new HorizontalStackPanel { Spacing = 6 };
        row.Widgets.Add(Label("전술 세트", 16, Theme.Text, width: 76));
        for (var set = 0; set < PartyMember.TacticSetCount; set++)
        {
            var index = set;
            var active = _company.ActiveTacticSet == set;
            var button = TextButton($"{set + 1}", active ? Theme.Selected : Theme.Button, Theme.ButtonHover, bold: active);
            button.Width = 48;
            button.Click += (_, _) =>
            {
                if (_company.ActiveTacticSet != index)
                {
                    _company.ActiveTacticSet = index;
                    MarkChanged();
                }
            };
            row.Widgets.Add(button);
        }

        return row;
    }

    private Widget BuildRowSelector(PartyMember member)
    {
        var row = new HorizontalStackPanel { Spacing = 8 };
        row.Widgets.Add(Label("전열", 17, Theme.Text, width: 48));

        foreach (var option in new[] { Row.Front, Row.Back })
        {
            var active = member.Row == option;
            var button = TextButton(RowLabel(option), active ? Theme.Selected : Theme.Button, Theme.ButtonHover);
            button.Width = 80;
            button.Click += (_, _) =>
            {
                member.Row = option;
                MarkChanged();
            };
            row.Widgets.Add(button);
        }

        row.Widgets.Add(Label("   후위를 노린 단일 공격은 살아있는 전위가 대신 받는다.", 15, Theme.TextDim));
        return row;
    }

    private Widget BuildTacticHeader()
    {
        var header = new HorizontalStackPanel { Spacing = 8 };
        header.Widgets.Add(Label("순위", 15, Theme.TextDim, width: 40));
        header.Widgets.Add(Label("조건", 15, Theme.TextDim, width: 265));
        header.Widgets.Add(Label("값", 15, Theme.TextDim, width: ValueColumnWidth));
        header.Widgets.Add(Label("행동", 15, Theme.TextDim, width: 270));
        header.Widgets.Add(Label($"전술 세트 {_company.ActiveTacticSet + 1} 편집 중", 15, Theme.Cover));
        return header;
    }

    private Widget BuildTacticRow(PartyMember member, int index)
    {
        var tactic = TacticsOf(member)[index];
        var row = new HorizontalStackPanel { Spacing = 8 };

        // 값 입력은 화면을 다시 만들지 않고 바로 반영하므로, 핸들러에서는 항상 현재 전술을 다시 읽는다.
        Tactic Current() => TacticsOf(member)[index];

        row.Widgets.Add(Label($"{tactic.Priority}", 18, Theme.Text, width: 40));

        // 조건
        var conditions = TacticText.Conditions;
        var conditionCombo = Combo(conditions.Select(TacticText.ConditionLabel), conditions.ToList().IndexOf(tactic.Condition), 265);
        conditionCombo.SelectedIndexChanged += (_, _) =>
        {
            var current = Current();
            var next = conditions[conditionCombo.SelectedIndex ?? 0];
            var value = TacticText.ValueAfterConditionChange(current.Condition, next, current.Value);
            TacticsOf(member).Replace(index, next, value, current.ActionId);
            MarkChanged();
        };
        row.Widgets.Add(conditionCombo);

        // 값
        row.Widgets.Add(TacticText.HasValue(tactic.Condition)
            ? BuildValueInput(member, index)
            : Label("—", 17, Theme.TextDim, width: ValueColumnWidth));

        // 행동: 쓸 수 있는 행동을 먼저, 잠긴 행동은 아래에 (고를 수 없음)
        var skills = member.Skills(_data);
        bool Usable(ActionDefinition a) => a.IsUsableBy(member.WeaponMastery(_data), skills);
        var actions = _data.Actions.Values.OrderBy(a => Usable(a) ? 0 : 1).ToList();
        var items = actions.Select(a => Usable(a)
            ? (ActionLabel(a), Theme.Text)
            : ($"(잠김) {a.Name} — {LockReason(a, member.WeaponMastery(_data), skills)}", Theme.Enemy));
        var actionCombo = Combo(items, actions.FindIndex(a => a.Id == tactic.ActionId), 270);
        actionCombo.SelectedIndexChanged += (_, _) =>
        {
            var chosen = actions[actionCombo.SelectedIndex ?? 0];
            if (Usable(chosen))
            {
                var current = Current();
                TacticsOf(member).Replace(index, current.Condition, current.Value, chosen.Id);
                MarkChanged();
            }
            else
            {
                MarkDirty(); // 잠긴 행동은 고를 수 없다. 원래 선택으로 되돌린다.
            }
        };
        row.Widgets.Add(actionCombo);

        // 순서 / 삭제
        row.Widgets.Add(SmallButton("▲", index > 0, () => TacticsOf(member).Move(index, -1)));
        row.Widgets.Add(SmallButton("▼", index < TacticsOf(member).Count - 1, () => TacticsOf(member).Move(index, 1)));
        row.Widgets.Add(SmallButton("삭제", true, () => TacticsOf(member).Remove(index), width: 56));

        return row;
    }

    private const int ValueColumnWidth = 150;

    /// <summary>
    /// 값 입력칸. 정수만 입력할 수 있다(수치 조건은 음수도).
    /// 백분율은 0–100, 턴 주기는 1 이상, 횟수·턴은 0 이상을 검사하고, 수치 조건은 검사하지 않는다.
    /// 올바른 값은 바로 반영하고(화면은 다시 만들지 않는다 — 입력칸 포커스를 지키려고),
    /// 잘못된 값은 빨간색으로 표시하고 반영하지 않는다.
    /// </summary>
    private Widget BuildValueInput(PartyMember member, int index)
    {
        var tactic = TacticsOf(member)[index];
        var group = new HorizontalStackPanel { Spacing = 6, Width = ValueColumnWidth };

        var box = new TextBox
        {
            Text = tactic.Value.ToString(),
            Width = 64,
            Font = _ui.Font(17),
            TextColor = Theme.Text,
            FocusedTextColor = Theme.Text,
            Background = new SolidBrush(Theme.BarBack),
            Padding = new Thickness(6, 3),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var hint = Label(TacticText.Unit(tactic.Condition), 15, Theme.TextDim);

        var allowNegative = tactic.Condition.IsAmount();
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
            var current = TacticsOf(member)[index];
            var error = int.TryParse(box.Text, out var value)
                ? TacticText.ValidationError(current.Condition, value)
                : "숫자만";
            if (error is not null)
            {
                box.TextColor = Theme.Enemy;
                box.FocusedTextColor = Theme.Enemy;
                hint.Text = error;
                hint.TextColor = Theme.Enemy;
                return;
            }

            box.TextColor = Theme.Text;
            box.FocusedTextColor = Theme.Text;
            hint.Text = TacticText.Unit(current.Condition);
            hint.TextColor = Theme.TextDim;
            if (value != current.Value)
            {
                TacticsOf(member).Replace(index, current.Condition, value, current.ActionId);
                MarkChangedInPlace();
            }
        };

        group.Widgets.Add(box);
        group.Widgets.Add(hint);
        return group;
    }

    private Widget BuildBottomBar()
    {
        var bar = new HorizontalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var back = TextButton(OnExpedition ? "원정으로" : "마을로", Theme.Accent, Theme.AccentHover, bold: true);
        back.Width = 140;
        back.Click += (_, _) => _back();
        bar.Widgets.Add(back);

        if (!OnExpedition)
        {
            var save = TextButton("저장", _session.Unsaved ? Theme.Accent : Theme.Button, _session.Unsaved ? Theme.AccentHover : Theme.ButtonHover);
            _saveButton = save;
            save.Width = 100;
            save.Click += (_, _) => Save();
            bar.Widgets.Add(save);
        }
        else
        {
            _saveButton = null;
        }

        // 게임 데이터나 장비가 바뀌어 잠긴 행동이 든 전술이 있으면 고칠 때까지 출정·전투를 막는다.
        _statusLabel = _company.HasLockedTactics(_data)
            ? Label("잠긴 행동이 든 전술을 고쳐야 전투할 수 있습니다", 16, Theme.Enemy)
            : Label(StatusText, 16, Theme.Cover);
        bar.Widgets.Add(_statusLabel);

        return bar;
    }

    // ── 위젯 도우미 ────────────────────────────────────────

    private static string RowLabel(Row row) => row == Row.Front ? "전위" : "후위";

    private static string ActionLabel(ActionDefinition action)
    {
        var cost = new List<string>();
        if (action.MpCost > 0)
        {
            cost.Add($"MP {action.MpCost}");
        }

        if (action.HpCost > 0)
        {
            cost.Add($"HP {action.HpCost}");
        }

        return cost.Count == 0 ? action.Name : $"{action.Name} ({string.Join(", ", cost)})";
    }

    /// <summary>행동을 못 쓰는 이유, 예: "활 필요, 정밀 사격 1".</summary>
    private string LockReason(ActionDefinition action, string? weapon, SkillSet skills)
    {
        var reasons = new List<string>();
        if (action.Weapon is not null && action.Weapon != weapon)
        {
            reasons.Add($"{_data.Masteries[action.Weapon].Name} 필요");
        }

        reasons.AddRange(skills.Missing(action.Requirements).Select(r => $"{_data.Skills[r.SkillId].Name} {r.Level}"));
        return string.Join(", ", reasons);
    }

    /// <summary>
    /// 무기·방어구 선택: 지금 장착한 아이템과 창고에 있는 아이템. 고르면 창고에서 꺼내 끼고, 끼고 있던 것은 창고로 간다.
    /// 무기 계열이 바뀌면 그 무기가 필요한 전술이 잠길 수 있다(빨간색으로 표시).
    /// </summary>
    private Widget BuildEquipmentSelector(PartyMember member)
    {
        var row = new HorizontalStackPanel { Spacing = 8 };
        foreach (var (slot, label) in new[] { (EquipmentSlot.Weapon, "무기"), (EquipmentSlot.Armor, "방어구") })
        {
            var current = slot == EquipmentSlot.Weapon ? member.Weapon : member.Armor;
            var options = _data.ItemsFor(slot)
                .Where(i => i.Id == current || _company.StashCount(i.Id) > 0)
                .ToList();
            row.Widgets.Add(Label(label, 17, Theme.Text, width: slot == EquipmentSlot.Weapon ? 48 : 60));
            if (options.Count == 0)
            {
                row.Widgets.Add(Label("없음 (창고에 맞는 아이템이 없다)", 16, Theme.TextDim, width: 280));
                continue;
            }

            var combo = Combo(
                options.Select(i => (EquipmentLabel(member, i), Theme.Text)),
                options.FindIndex(i => i.Id == current), 280);
            combo.Enabled = !OnExpedition;
            combo.SelectedIndexChanged += (_, _) =>
            {
                var chosen = options[combo.SelectedIndex ?? 0].Id;
                if (chosen != current && _company.Equip(member.Id, chosen, _data))
                {
                    MarkChanged();
                }
                else
                {
                    MarkDirty();
                }
            };
            row.Widgets.Add(combo);
        }

        return row;
    }

    /// <summary>"낡은 검 · 검 Lv 3 · 창고 2"처럼 아이템, 계열 숙련, 창고 개수.</summary>
    private string EquipmentLabel(PartyMember member, ItemDefinition item)
    {
        var mastery = _data.Masteries[item.Mastery];
        var label = $"{item.Name} · {mastery.Name} Lv {member.MasteryLevel(mastery.Id)}";
        var stored = _company.StashCount(item.Id);
        return stored > 0 ? $"{label} · 창고 {stored}" : label;
    }

    /// <summary>배운 스킬을 데이터 순서대로 "활 숙련 4 · 정밀 사격 1"처럼 보여준다.</summary>
    private string SkillSummary(SkillSet skills)
    {
        var learned = _data.Skills.Values.Where(s => skills.Level(s.Id) > 0).Select(s => $"{s.Name} {skills.Level(s.Id)}").ToList();
        return learned.Count == 0 ? "배운 스킬 없음" : string.Join(" · ", learned);
    }

    private Label Label(string text, int size, Color color, bool bold = false, int? width = null) =>
        _widgets.Label(text, size, color, bold, width);

    private ComboView Combo(IEnumerable<string> items, int selectedIndex, int width) =>
        Combo(items.Select(i => (i, Theme.Text)), selectedIndex, width);

    private ComboView Combo(IEnumerable<(string Text, Color Color)> items, int selectedIndex, int width)
    {
        var combo = _widgets.Combo(items, selectedIndex, width);
        _combos.Add(combo);
        return combo;
    }

    private Button TextButton(string text, Color background, Color hover, bool bold = false) =>
        _widgets.TextButton(text, background, hover, bold);

    private Button SmallButton(string text, bool enabled, Func<bool> action, int width = 36) =>
        SmallButton(text, enabled, () => { action(); }, width);

    private Button SmallButton(string text, bool enabled, Action action, int width = 36)
    {
        var button = TextButton(text, Theme.Button, Theme.ButtonHover);
        button.Width = width;
        button.Padding = new Thickness(0, 6);
        button.Enabled = enabled;
        button.Click += (_, _) =>
        {
            action();
            MarkChanged();
        };
        return button;
    }

    private static Button StyledButton(Widget content, Color background, Color hover) =>
        Widgets.StyledButton(content, background, hover);
}
