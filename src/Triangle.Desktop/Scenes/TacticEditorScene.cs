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
using Triangle.Core.Masteries;
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 파티 유닛의 전열과 전술을 편집하고, 바로 전투로 시험한다.
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
    private readonly GameData _data;
    private readonly Party _party;
    private readonly Rectangle _bounds;
    private readonly SaveStore _store;
    private readonly Action<string> _startCombat;
    private readonly Action<PartyMember> _openTraining;
    private readonly Action _quit;
    private readonly MyraDesktop _desktop = new();
    private readonly Widgets _widgets;
    private readonly List<ComboView> _combos = [];

    private int _selected;
    private string _encounterId;
    private bool _dirty = true;
    private bool _unsaved;
    private (string Text, Color Color)? _notice;
    private Window? _quitDialog;

    /// <param name="notice">처음에 보여줄 안내 (세이브를 복구했다는 등).</param>
    public TacticEditorScene(
        Ui ui, GameData data, Party party, SaveStore store, Rectangle bounds,
        Action<string> startCombat, Action<PartyMember> openTraining, Action quit, (string Text, Color Color)? notice = null)
    {
        _ui = ui;
        _widgets = new Widgets(ui);
        _data = data;
        _party = party;
        _store = store;
        _bounds = bounds;
        _notice = notice;
        _startCombat = startCombat;
        _openTraining = openTraining;
        _quit = quit;
        _encounterId = data.Encounters.Keys.First();
    }

    private PartyMember Selected => _party.Members[_selected];

    public bool HasUnsavedChanges => _unsaved;

    /// <summary>다른 화면(훈련, 전투 보상)에서 파티를 바꿨다.</summary>
    public void NotifyPartyChanged() => MarkChanged();

    /// <summary>종료를 요청한다. 저장하지 않은 변경이 있으면 먼저 확인 창을 띄운다.</summary>
    public void RequestQuit()
    {
        if (_unsaved)
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

        // 드롭다운이 열려 있을 때의 Esc는 드롭다운을 닫는 데 쓴다.
        if (input.Pressed(Keys.Escape) && !_combos.Any(c => c.IsExpanded))
        {
            RequestQuit();
            return;
        }

        if (input.Pressed(Keys.S) && input.IsDown(Keys.LeftControl, Keys.RightControl))
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
        // 확인 창이 떠 있는 동안에는 트리를 갈아끼우지 않는다 (창이 함께 사라지지 않도록).
        if (_dirty && _quitDialog is null)
        {
            Rebuild();
            _dirty = false;
        }

        batch.Begin();
        _ui.Text(batch, _ui.BoldFont(28), "전술 편집", new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        const string help = "조건이 참인 첫 전술을 쓴다. 대상은 행동이 정한다.     Ctrl+S  저장     Esc  종료";
        _ui.Text(batch, _ui.Font(16), help, new Vector2(_bounds.Left + Margin, _bounds.Bottom - FooterHeight + 10), Theme.TextDim);

        if (_notice is { } notice)
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

    /// <summary>파티가 바뀌었다. 저장 전까지 "저장하지 않은 변경"으로 표시한다.</summary>
    private void MarkChanged()
    {
        _unsaved = true;
        _notice = null;
        MarkDirty();
    }

    private bool Save()
    {
        var saved = false;
        try
        {
            _store.Save(_party);
            _unsaved = false;
            saved = true;
            _notice = ($"저장했습니다 ({DateTime.Now:HH:mm:ss})", Theme.Heal);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _notice = ($"저장하지 못했습니다: {e.Message}", Theme.Enemy);
        }

        MarkDirty();
        return saved;
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
            if (Save())
            {
                _quit();
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

        var combatBar = BuildCombatBar();
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
        list.Widgets.Add(Label("파티", 20, Theme.Ally, bold: true));

        for (var i = 0; i < _party.Members.Count; i++)
        {
            var member = _party.Members[i];
            var index = i;
            var selected = i == _selected;

            var content = new VerticalStackPanel { Spacing = 4 };
            content.Widgets.Add(Label(member.Name, 20, selected ? Theme.Text : Theme.Ally, bold: true));
            content.Widgets.Add(Label($"{RowLabel(member.Row)} · 전술 {member.Tactics.Count}개", 15, Theme.TextDim));
            if (member.LockedTacticIndexes(_data).Count is > 0 and var locked)
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
        var training = TextButton("숙련·패시브", Theme.Button, Theme.ButtonHover);
        training.Click += (_, _) => _openTraining(member);
        title.Widgets.Add(training);
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
        for (var i = 0; i < member.Tactics.Count; i++)
        {
            tactics.Widgets.Add(BuildTacticRow(member, i));
        }

        if (member.Tactics.Count == 0)
        {
            tactics.Widgets.Add(Label("전술이 없으면 이 유닛은 매 턴 기다린다.", 16, Theme.TextDim));
        }

        // 새 전술은 쓸 수 있는 첫 행동으로 시작한다.
        var firstUsable = _data.Actions.Values.FirstOrDefault(a => a.IsUsableBy(member.Weapon, skills));
        var addButton = TextButton("+ 전술 추가", Theme.Button, Theme.ButtonHover);
        addButton.Enabled = member.Tactics.Count < MaxTactics && firstUsable is not null;
        addButton.Click += (_, _) =>
        {
            member.AddTactic(Condition.Always, 0, firstUsable!.Id);
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
        header.Widgets.Add(Label("조건", 15, Theme.TextDim, width: 230));
        header.Widgets.Add(Label("값", 15, Theme.TextDim, width: 120));
        header.Widgets.Add(Label("행동", 15, Theme.TextDim, width: 300));
        return header;
    }

    private Widget BuildTacticRow(PartyMember member, int index)
    {
        var tactic = member.Tactics[index];
        var row = new HorizontalStackPanel { Spacing = 8 };

        row.Widgets.Add(Label($"{tactic.Priority}", 18, Theme.Text, width: 40));

        // 조건
        var conditions = TacticText.Conditions;
        var conditionCombo = Combo(conditions.Select(TacticText.ConditionLabel), conditions.ToList().IndexOf(tactic.Condition), 230);
        conditionCombo.SelectedIndexChanged += (_, _) =>
        {
            var next = conditions[conditionCombo.SelectedIndex ?? 0];
            var value = TacticText.ValueAfterConditionChange(tactic.Condition, next, tactic.Value);
            member.ReplaceTactic(index, next, value, tactic.ActionId);
            MarkChanged();
        };
        row.Widgets.Add(conditionCombo);

        // 값
        if (TacticText.HasValue(tactic.Condition))
        {
            var values = TacticText.ValueOptions(tactic.Condition, tactic.Value);
            var valueCombo = Combo(values.Select(v => TacticText.ValueLabel(tactic.Condition, v)), values.ToList().IndexOf(tactic.Value), 120);
            valueCombo.SelectedIndexChanged += (_, _) =>
            {
                member.ReplaceTactic(index, tactic.Condition, values[valueCombo.SelectedIndex ?? 0], tactic.ActionId);
                MarkChanged();
            };
            row.Widgets.Add(valueCombo);
        }
        else
        {
            row.Widgets.Add(Label("—", 17, Theme.TextDim, width: 120));
        }

        // 행동: 쓸 수 있는 행동을 먼저, 잠긴 행동은 아래에 (고를 수 없음)
        var skills = member.Skills(_data);
        bool Usable(ActionDefinition a) => a.IsUsableBy(member.Weapon, skills);
        var actions = _data.Actions.Values.OrderBy(a => Usable(a) ? 0 : 1).ToList();
        var items = actions.Select(a => Usable(a)
            ? (ActionLabel(a), Theme.Text)
            : ($"(잠김) {a.Name} — {LockReason(a, member.Weapon, skills)}", Theme.Enemy));
        var actionCombo = Combo(items, actions.FindIndex(a => a.Id == tactic.ActionId), 300);
        actionCombo.SelectedIndexChanged += (_, _) =>
        {
            var chosen = actions[actionCombo.SelectedIndex ?? 0];
            if (Usable(chosen))
            {
                member.ReplaceTactic(index, tactic.Condition, tactic.Value, chosen.Id);
                MarkChanged();
            }
            else
            {
                MarkDirty(); // 잠긴 행동은 고를 수 없다. 원래 선택으로 되돌린다.
            }
        };
        row.Widgets.Add(actionCombo);

        // 순서 / 삭제
        row.Widgets.Add(SmallButton("▲", index > 0, () => member.MoveTactic(index, -1)));
        row.Widgets.Add(SmallButton("▼", index < member.Tactics.Count - 1, () => member.MoveTactic(index, 1)));
        row.Widgets.Add(SmallButton("삭제", true, () => member.RemoveTactic(index), width: 56));

        return row;
    }

    private Widget BuildCombatBar()
    {
        var bar = new HorizontalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        bar.Widgets.Add(Label("상대", 18, Theme.Text));

        var encounters = _data.Encounters.Values.ToList();
        var encounterCombo = Combo(encounters.Select(e => e.Name), encounters.FindIndex(e => e.Id == _encounterId), 220);
        encounterCombo.SelectedIndexChanged += (_, _) => _encounterId = encounters[encounterCombo.SelectedIndex ?? 0].Id;
        bar.Widgets.Add(encounterCombo);

        // 게임 데이터가 바뀌어 잠긴 행동이 든 전술이 있으면 고칠 때까지 전투를 막는다.
        var hasLocked = _party.Members.Any(m => m.LockedTacticIndexes(_data).Count > 0);
        var start = TextButton("전투 시험  ▶", Theme.Accent, Theme.AccentHover, bold: true);
        start.Width = 180;
        start.Enabled = !hasLocked;
        start.Click += (_, _) => _startCombat(_encounterId);
        bar.Widgets.Add(start);

        var save = TextButton("저장", _unsaved ? Theme.Accent : Theme.Button, _unsaved ? Theme.AccentHover : Theme.ButtonHover);
        save.Width = 100;
        save.Click += (_, _) => Save();
        bar.Widgets.Add(save);
        bar.Widgets.Add(hasLocked
            ? Label("잠긴 행동이 든 전술을 고쳐야 전투할 수 있습니다", 16, Theme.Enemy)
            : Label(_unsaved ? "저장하지 않은 변경이 있습니다" : "", 16, Theme.Cover));

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

    /// <summary>무기·방어구 계열 선택. 무기를 바꾸면 그 무기가 필요한 전술이 잠길 수 있다(빨간색으로 표시).</summary>
    private Widget BuildEquipmentSelector(PartyMember member)
    {
        var row = new HorizontalStackPanel { Spacing = 8 };
        foreach (var (slot, label) in new[] { (EquipmentSlot.Weapon, "무기"), (EquipmentSlot.Armor, "방어구") })
        {
            var options = _data.MasteriesFor(slot).ToList();
            var current = slot == EquipmentSlot.Weapon ? member.Weapon : member.Armor;
            row.Widgets.Add(Label(label, 17, Theme.Text, width: slot == EquipmentSlot.Weapon ? 48 : 60));
            var combo = Combo(
                options.Select(m => ($"{m.Name}  (숙련 Lv {member.MasteryLevel(m.Id)})", Theme.Text)),
                options.FindIndex(m => m.Id == current), 190);
            combo.SelectedIndexChanged += (_, _) =>
            {
                var chosen = options[combo.SelectedIndex ?? 0].Id;
                if (slot == EquipmentSlot.Weapon)
                {
                    member.Weapon = chosen;
                }
                else
                {
                    member.Armor = chosen;
                }

                MarkChanged();
            };
            row.Widgets.Add(combo);
        }

        return row;
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
