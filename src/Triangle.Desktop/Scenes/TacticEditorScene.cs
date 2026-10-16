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
using Triangle.Core.Progress;
using Triangle.Core.Skills;
using Triangle.Core.Tactics;
using Triangle.Core.Units;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 캐릭터의 장비, 능력치, 전술, 전열을 편집한다. 마을에서는 로스터 전원을, 원정 중에는 출전 멤버만 보여준다.
/// - 왼쪽: 전술 세트와 멤버 목록. 멤버는 전위·후위·대기로 나눠 보여 주고, 옆 버튼으로 줄을 바꾼다.
/// - 오른쪽: 고른 멤버를 장비·능력치·전술 탭으로 본다.
///   - 장비: 부위 카드 5개, 고른 부위의 아이템 설명과 주는 행동, 행동 설명. 창고 아이템을 골라 미리 보고 낀다.
///   - 능력치: 기본 스탯과 그 효과, 전투 능력치(HP, 행동 간격, 위력·방어 배율), 패시브·장비 보너스 합계.
///   - 전술: 조건을 대상 · 이상/이하 · %/수치 세 칸으로 고르고, 행동은 장비별로 묶인 목록에서 설명을 보며 고른다.
/// 원정 중에는 전술(세트 선택·관리 포함)과 전열만 바꿀 수 있다: 장비는 잠그고, 숙련·패시브와 저장 버튼은 숨긴다.
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
    private const int Gap = 12;

    /// <summary>탭 줄과 이름 줄을 뺀 내용 높이.</summary>
    private const int MemberHeaderHeight = 140;

    private const int PortraitSize = 76;

    private enum Tab
    {
        Equipment,
        Stats,
        Tactics,
    }

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

    private Tab _tab = Tab.Tactics;
    private EquipmentSlot _slot = EquipmentSlot.MainHand;

    /// <summary>장비 탭에서 미리 보는 창고 아이템 (아직 끼지 않음).</summary>
    private string? _candidate;

    /// <summary>장비 탭에서 설명을 보는 행동. null이면 보이는 아이템의 첫 행동.</summary>
    private string? _detailAction;

    /// <summary>행동을 고르는 중인 전술 번호.</summary>
    private int? _picking;

    private ScrollPane? _partyScroll;
    private ScrollPane? _tacticScroll;

    /// <summary>고른 멤버의 전투 입력 (미리보기 계산용, 다시 만들 때마다 갱신).</summary>
    private CombatantSetup? _setup;

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
    public void Refresh()
    {
        _candidate = null;
        _picking = null;
        MarkDirty();
    }

    /// <summary>
    /// 시작 화면 지정용: 탭 이름(equipment, stats, tactics), picker(첫 전술의 행동 고르기),
    /// preview(장비 탭에서 창고의 첫 주무기를 미리 보기).
    /// </summary>
    public void Show(string? view)
    {
        _tab = view switch
        {
            "equipment" or "preview" => Tab.Equipment,
            "stats" => Tab.Stats,
            _ => Tab.Tactics,
        };
        _picking = view == "picker" && TacticsOf(Selected).Count > 0 ? 0 : null;
        _candidate = view == "preview"
            ? _data.ItemsFor(EquipmentSlot.MainHand).FirstOrDefault(i => _company.StashCount(i.Id) > 0 && CanWear(Selected, i))?.Id
            : null;
        MarkDirty();
    }

    public void Update(GameTime gameTime, Input input)
    {
        // 드롭다운이 열려 있을 때의 Esc는 드롭다운을 닫는 데 쓴다. 행동 고르기 중이면 그것을 닫는다.
        if (input.Pressed(Keys.Escape) && !_combos.Any(c => c.IsExpanded))
        {
            if (_picking is not null)
            {
                _picking = null;
                MarkDirty();
                return;
            }

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

        batch.Begin(transformMatrix: _ui.Screen.Transform);
        var title = OnExpedition ? "편성 — 원정 중 (전술과 전열만 바꿀 수 있습니다)" : "편성 · 전술";
        _ui.Text(batch, _ui.BoldFont(28), title, new Vector2(_bounds.Left + Margin, _bounds.Top + 18), Theme.Text);
        var help = OnExpedition
            ? "조건이 모두 참인 첫 전술을 쓴다. 대상은 행동이 정한다.     Esc  원정으로"
            : "조건이 모두 참인 첫 전술을 쓴다. 대상은 행동이 정한다.     Ctrl+S  저장     Esc  마을로";
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

        _ui.Screen.Render(_desktop);
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

        if (_statusLabel is not null && _company.WhyLineupCannotFight(_data) is null)
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

    private void Select(int index)
    {
        _selected = index;
        _candidate = null;
        _detailAction = null;
        _picking = null;
        MarkDirty();
    }

    private ActionPreview Preview(ActionDefinition action) => ActionPreview.For(_setup!, action, _data.Catalog);

    // ── 위젯 트리 ──────────────────────────────────────────

    private void Rebuild()
    {
        _combos.Clear();
        _setup = Selected.ToCombatantSetup(_data, _company.ActiveTacticSet);
        var root = new Panel();

        var partyPosition = _partyScroll?.ScrollPosition ?? Point.Zero;
        _partyScroll = new ScrollPane
        {
            Content = BuildPartyList(),
            ShowHorizontalScrollBar = false,
            Left = _bounds.Left + Margin,
            Top = _bounds.Top + HeaderHeight,
            Width = PartyWidth + 18,
            Height = _bounds.Height - HeaderHeight - FooterHeight - 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _partyScroll.ScrollPosition = partyPosition;
        root.Widgets.Add(_partyScroll);

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

        // 행동 고르기는 화면 위에 뜨는 팝업이다. 떠 있는 동안 뒤의 화면은 누를 수 없다.
        _pickerDetail = null;
        if (_picking is { } picking && picking < TacticsOf(Selected).Count)
        {
            var picker = BuildActionPicker(Selected, picking);
            picker.IsModal = true;
            _desktop.Widgets.Add(picker);
        }
    }

    // ── 왼쪽: 전술 세트와 진형 판 (후위 · 전위 · 대기) ──────────────

    private const int TokenSize = 56;

    private Widget BuildPartyList()
    {
        var list = new VerticalStackPanel
        {
            Spacing = 10,
            Width = PartyWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        list.Widgets.Add(BuildTacticSetSelector());

        var lineup = Members.Where(m => OnExpedition || _company.IsInLineup(m.Id)).ToList();
        list.Widgets.Add(_widgets.Header("figure", $"진형 {lineup.Count}명", "오른쪽이 적 쪽"));
        list.Widgets.Add(BuildFormationBoard(lineup));

        var bench = Members.Where(m => !lineup.Contains(m)).ToList();
        if (bench.Count > 0)
        {
            list.Widgets.Add(_widgets.Header("portrait.none", $"대기 {bench.Count}명", "출전 안 함", size: 15));
            const int perRow = 4;
            for (var i = 0; i < bench.Count; i += perRow)
            {
                var line = new HorizontalStackPanel { Spacing = 4 };
                foreach (var member in bench.Skip(i).Take(perRow))
                {
                    line.Widgets.Add(BuildToken(member, 44, PartyWidth / perRow - 4));
                }

                list.Widgets.Add(line);
            }
        }

        return list;
    }

    /// <summary>
    /// 진형 판: 왼쪽 칸이 후위, 오른쪽 칸이 전위(적 쪽). 칸마다 그 줄 색으로 칠하고, 말 아래 버튼으로 줄을 바꾼다 (원정 중에도 된다).
    /// </summary>
    private Widget BuildFormationBoard(IReadOnlyList<PartyMember> lineup)
    {
        var board = new HorizontalStackPanel { Spacing = 6 };
        var columnWidth = (PartyWidth - 6) / 2;
        foreach (var row in new[] { Row.Back, Row.Front })
        {
            var column = new VerticalStackPanel
            {
                Spacing = 8,
                Width = columnWidth,
                Padding = new Thickness(4, 6),
                Background = new SolidBrush(RowColor(row) * 0.22f),
                Border = new SolidBrush(RowColor(row)),
                BorderThickness = new Thickness(1),
            };

            var title = Label(row == Row.Front ? "전위 ▶" : "후위", 17, Theme.Text, bold: true);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            column.Widgets.Add(title);
            var note = Label(row == Row.Front ? "먼저 맞고 엄호" : "전위가 엄호", 12, Theme.TextDim);
            note.HorizontalAlignment = HorizontalAlignment.Center;
            column.Widgets.Add(note);

            var inRow = lineup.Where(m => m.Row == row).ToList();
            if (inRow.Count == 0)
            {
                var empty = Label(row == Row.Front ? "비어 있음\n후위가 바로 맞는다" : "비어 있음", 12, Theme.TextDim);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.Margin = new Thickness(0, 12);
                column.Widgets.Add(empty);
            }

            foreach (var member in inRow)
            {
                var token = new VerticalStackPanel { Spacing = 2 };
                token.Widgets.Add(BuildToken(member, TokenSize, columnWidth - 8));

                var other = row == Row.Front ? Row.Back : Row.Front;
                var move = _widgets.TextButton(other == Row.Back ? "◀ 후위로" : "전위로 ▶", Theme.Button, Theme.ButtonHover, size: 12);
                move.Padding = new Thickness(0, 2);
                move.Width = columnWidth - 8;
                move.Click += (_, _) =>
                {
                    member.Row = other;
                    MarkChanged();
                };
                token.Widgets.Add(move);
                column.Widgets.Add(token);
            }

            board.Widgets.Add(column);
        }

        return board;
    }

    /// <summary>진형 판의 말: 주무기로 고른 초상, 이름, 문제(잠긴 전술·착용 불가)가 있으면 빨간 테두리와 한 줄.</summary>
    private Widget BuildToken(PartyMember member, int size, int width)
    {
        var index = Members.ToList().IndexOf(member);
        var selected = member == Selected;
        var locked = member.LockedTacticIndexes(_data, _company.ActiveTacticSet).Count;
        var unwearable = member.UnwearableSlots(_data).Count;
        var down = _company.Expedition?.MemberState(member.Id) is { Down: true };
        var (icon, color) = GameIcons.Portrait(member.WeaponMastery(_data));

        var content = new VerticalStackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        var tile = _widgets.IconTile(icon, size, down ? Theme.Death : color,
            border: selected ? Theme.Gold : locked + unwearable > 0 ? Theme.Enemy : null,
            background: selected ? Theme.Selected : Theme.BarBack, padding: 5);
        tile.HorizontalAlignment = HorizontalAlignment.Center;
        content.Widgets.Add(tile);

        var name = Label(member.Name, size >= TokenSize ? 15 : 13, selected ? Theme.Gold : Theme.Text, bold: true);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        content.Widgets.Add(name);

        var problem = down ? "쓰러짐"
            : locked > 0 ? $"잠긴 전술 {locked}"
            : unwearable > 0 ? $"착용 불가 {unwearable}"
            : null;
        if (problem is not null)
        {
            var warning = Label(problem, 12, down ? Theme.Death : Theme.Enemy);
            warning.HorizontalAlignment = HorizontalAlignment.Center;
            content.Widgets.Add(warning);
        }

        var button = new Button
        {
            Content = content,
            Width = width,
            Padding = new Thickness(0, 4),
            Background = new SolidBrush(Color.Transparent),
            OverBackground = new SolidBrush(Theme.ButtonHover * 0.6f),
            PressedBackground = new SolidBrush(Theme.ButtonPressed),
        };
        button.Click += (_, _) => Select(index);
        return button;
    }

    /// <summary>
    /// 전술 세트 선택과 관리. 세트는 이름 붙은 전술 계획이고 수에 제한이 없다.
    /// 고르면 파티 전원이 그 세트로 바뀌고, 편집도 전투도 그 세트로 한다. 고른 세트는 세이브에 함께 저장된다.
    /// "+"는 지금 세트를 복사해 만들고, 마지막 남은 세트는 지울 수 없다. 이름은 입력칸에서 바로 바꾼다.
    /// </summary>
    private Widget BuildTacticSetSelector()
    {
        var panel = new VerticalStackPanel { Spacing = 6, Width = PartyWidth };
        panel.Widgets.Add(_widgets.Header("tactics", "전술 세트", $"{_company.TacticSetNames.Count}개"));

        var top = new HorizontalStackPanel { Spacing = 4 };
        var combo = Combo(_company.TacticSetNames, _company.ActiveTacticSet, PartyWidth - 2 * 36 - 8);
        combo.SelectedIndexChanged += (_, _) =>
        {
            if (combo.SelectedIndex is { } index && index != _company.ActiveTacticSet)
            {
                _company.ActiveTacticSet = index;
                _picking = null;
                MarkChanged();
            }
        };
        top.Widgets.Add(combo);

        var add = _widgets.TextButton("+", Theme.Button, Theme.ButtonHover, bold: true);
        add.Width = 36;
        add.Padding = new Thickness(0, 4);
        add.Tooltip = "지금 세트를 복사해 새 세트를 만든다";
        add.Click += (_, _) =>
        {
            _company.AddTacticSet();
            MarkChanged();
        };
        top.Widgets.Add(add);

        var remove = _widgets.TextButton("×", Theme.Button, Theme.ButtonHover, bold: true);
        remove.Width = 36;
        remove.Padding = new Thickness(0, 4);
        remove.Tooltip = "이 세트를 지운다";
        remove.Enabled = _company.TacticSetNames.Count > 1;
        remove.Click += (_, _) =>
        {
            if (_company.RemoveTacticSet(_company.ActiveTacticSet))
            {
                _picking = null;
                MarkChanged();
            }
        };
        top.Widgets.Add(remove);
        panel.Widgets.Add(top);

        var name = new TextBox
        {
            Text = _company.TacticSetNames[_company.ActiveTacticSet],
            Width = PartyWidth - 46,
            Font = _ui.Font(16),
            TextColor = Theme.Text,
            FocusedTextColor = Theme.Text,
            Background = new SolidBrush(Theme.BarBack),
            Border = new SolidBrush(Theme.PanelBorder),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 3),
        };
        name.ValueChanging += (_, e) =>
        {
            if ((e.NewValue ?? "").Length > Company.MaxTacticSetNameLength)
            {
                e.Cancel = true;
            }
        };
        name.TextChangedByUser += (_, _) =>
        {
            // 비어 있으면 이전 이름을 둔다. 목록의 이름은 입력칸을 벗어나거나 Enter를 누르면 갱신한다.
            if (_company.RenameTacticSet(_company.ActiveTacticSet, name.Text ?? ""))
            {
                MarkChangedInPlace();
            }
        };
        name.KeyboardFocusChanged += (_, _) =>
        {
            if (!name.IsKeyboardFocused)
            {
                MarkDirty();
            }
        };
        name.KeyDown += (_, e) =>
        {
            if (e.Data == Keys.Enter)
            {
                MarkDirty();
            }
        };
        var nameRow = new HorizontalStackPanel { Spacing = 6 };
        nameRow.Widgets.Add(Label("이름", 14, Theme.TextDim, width: 40));
        nameRow.Widgets.Add(name);
        panel.Widgets.Add(nameRow);

        return panel;
    }

    // ── 오른쪽: 멤버 머리줄과 탭 ────────────────────────────────

    private Widget BuildMemberEditor(Rectangle area)
    {
        var member = Selected;
        var panel = new VerticalStackPanel { Spacing = 10, Width = area.Width, Height = area.Height };
        panel.Widgets.Add(BuildMemberHeader(member));
        panel.Widgets.Add(BuildTabs(member));

        var height = area.Height - MemberHeaderHeight;
        panel.Widgets.Add(_tab switch
            {
                Tab.Equipment => BuildEquipmentTab(member, area.Width, height),
                Tab.Stats => BuildStatsTab(member, area.Width, height),
                _ => BuildTacticsTab(member, height),
            });

        return panel;
    }

    /// <summary>큰 초상, 이름, 전위·후위, 숙련 버튼, 아이콘으로 된 능력치 줄 (자세한 것은 능력치 탭).</summary>
    private Widget BuildMemberHeader(PartyMember member)
    {
        var header = new HorizontalStackPanel { Spacing = 14 };
        var (icon, color) = GameIcons.Portrait(member.WeaponMastery(_data));
        header.Widgets.Add(_widgets.IconTile(icon, PortraitSize, color, Theme.Gold, Theme.Selected, padding: 8));

        var info = new VerticalStackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var title = new HorizontalStackPanel { Spacing = 12 };
        title.Widgets.Add(Label(member.Name, 26, Theme.Text, bold: true));
        title.Widgets.Add(BuildRowSelector(member));
        if (!OnExpedition)
        {
            var content = new HorizontalStackPanel { Spacing = 6 };
            content.Widgets.Add(_widgets.Icon("mastery", 18, Theme.Gold));
            content.Widgets.Add(Label("숙련·패시브", 16, Theme.Text));
            var training = StyledButton(content, Theme.Button, Theme.ButtonHover);
            training.Padding = new Thickness(10, 5);
            training.Click += (_, _) => _openTraining(member);
            title.Widgets.Add(training);
        }

        info.Widgets.Add(title);

        var unit = UnitPreview.For(_setup!, _data.Catalog, CombatRules.Default);
        var s = member.Stats;
        var strip = new HorizontalStackPanel { Spacing = 16 };
        foreach (var (statIcon, iconColor, value, tip) in new[]
                 {
                     ("hp", Theme.HpBar, unit.MaxHp, "최대 HP"),
                     ("mp", Theme.MpBar, unit.MaxMp, "최대 MP"),
                     ("delay", Theme.Cover, (int)unit.BaseDelay, "행동 간격 (작을수록 자주 행동)"),
                     ("stat.str", Theme.Damage, s.Str, "근력: 물리 위력과 물리 방어"),
                     ("stat.dex", Theme.TextDim, s.Dex, "민첩: 아직 전투 규칙에 쓰이지 않는다"),
                     ("stat.vital", Theme.Heal, s.Vital, "체력: 최대 HP"),
                     ("stat.intel", Theme.Lightning, s.Intel, "지능: 마법·회복 위력, 마법 방어, 최대 MP"),
                     ("stat.speed", Theme.Frost, s.Speed, "신속: 행동 간격"),
                 })
        {
            var item = _widgets.IconText(statIcon, iconColor, $"{value}", 17, Theme.Text, bold: true);
            item.Tooltip = tip;
            strip.Widgets.Add(item);
        }

        info.Widgets.Add(strip);
        header.Widgets.Add(info);
        return header;
    }

    private Widget BuildTabs(PartyMember member)
    {
        var tabs = new HorizontalStackPanel { Spacing = 6 };
        var locked = member.LockedTacticIndexes(_data, _company.ActiveTacticSet).Count;
        var unwearable = member.UnwearableSlots(_data).Count;
        foreach (var (tab, icon, label, warn) in new[]
                 {
                     (Tab.Equipment, "equipment", "장비", unwearable > 0 ? $"착용 불가 {unwearable}" : null),
                     (Tab.Stats, "stats", "능력치", null),
                     (Tab.Tactics, "tactics", $"전술 {TacticsOf(member).Count}", locked > 0 ? $"잠김 {locked}" : null),
                 })
        {
            var active = _tab == tab;
            var content = new HorizontalStackPanel { Spacing = 6 };
            content.Widgets.Add(_widgets.Icon(icon, 20, active ? Theme.Gold : Theme.TextDim));
            content.Widgets.Add(Label(label, 17, active ? Theme.Text : Theme.TextDim, bold: active));
            if (warn is not null)
            {
                content.Widgets.Add(Label(warn, 13, Theme.Enemy));
            }

            var button = StyledButton(content, active ? Theme.Selected : Theme.Button, Theme.ButtonHover);
            button.Width = 170;
            button.Padding = new Thickness(12, 6);
            button.Click += (_, _) =>
            {
                _tab = tab;
                _picking = null;
                MarkDirty();
            };
            tabs.Widgets.Add(button);
        }

        if (_tab == Tab.Tactics)
        {
            tabs.Widgets.Add(Label($"  \"{_company.TacticSetNames[_company.ActiveTacticSet]}\" 세트 편집 중", 15, Theme.Cover));
        }

        return tabs;
    }

    /// <summary>전위·후위 버튼. 고른 줄은 그 줄 색으로 칠한다.</summary>
    private Widget BuildRowSelector(PartyMember member)
    {
        var row = new HorizontalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        foreach (var option in new[] { Row.Back, Row.Front })
        {
            var active = member.Row == option;
            var button = _widgets.TextButton(RowLabel(option), active ? RowColor(option) : Theme.Button, Theme.ButtonHover, bold: active);
            button.Width = 72;
            button.Click += (_, _) =>
            {
                member.Row = option;
                MarkChanged();
            };
            row.Widgets.Add(button);
        }

        return row;
    }

    // ── 장비 탭 ────────────────────────────────────────────

    private const int DollWidth = 280;
    private const int SlotTile = 60;
    private const int ItemColumnWidth = 300;

    /// <summary>장비 인형의 칸 위치 (칸 묶음의 왼쪽 위). 머리·몸·발은 가운데 세로로, 주무기는 왼쪽, 보조는 오른쪽.</summary>
    private static Point DollPosition(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Head => new Point(94, 0),
        EquipmentSlot.Body => new Point(94, 96),
        EquipmentSlot.Feet => new Point(94, 192),
        EquipmentSlot.MainHand => new Point(0, 96),
        _ => new Point(188, 96),
    };

    /// <summary>
    /// 왼쪽: 장비 인형(부위 칸 5개)과 고른 부위의 창고 아이템 칸. 가운데: 보이는 아이템(미리 보기 또는 낀 것)의 설명과 행동.
    /// 오른쪽: 고른 행동의 설명 (낀 아이템이면 지금 장비·패시브 기준 예상치도).
    /// 고르면 창고에서 꺼내 끼고, 끼고 있던 것은 창고로 간다. 아이템을 바꾸면 그 행동을 쓰는 전술이 잠길 수 있다.
    /// </summary>
    private Widget BuildEquipmentTab(PartyMember member, int width, int height)
    {
        var row = new HorizontalStackPanel { Spacing = Gap, Height = height };

        var left = new VerticalStackPanel { Spacing = 6, Width = DollWidth };
        left.Widgets.Add(BuildDoll(member));
        left.Widgets.Add(_widgets.Header("equipment", OnExpedition ? "원정 중에는 장비를 바꿀 수 없다" : $"창고 · {ItemText.SlotLabel(_slot)}", size: 15));
        if (!OnExpedition)
        {
            left.Widgets.Add(new ScrollPane { Content = BuildCandidates(member), ShowHorizontalScrollBar = false, Height = height - DollHeight - 36 });
        }

        row.Widgets.Add(left);

        var shown = _candidate ?? member.ItemIn(_slot);
        var item = shown is null ? null : _data.Items[shown];
        row.Widgets.Add(new ScrollPane
        {
            Content = BuildItemColumn(member, item),
            ShowHorizontalScrollBar = false,
            Width = ItemColumnWidth + 18,
            Height = height,
        });

        var actionWidth = width - DollWidth - ItemColumnWidth - 18 - Gap * 2;
        row.Widgets.Add(new ScrollPane
        {
            Content = BuildActionDetail(member, item, actionWidth - 18),
            ShowHorizontalScrollBar = false,
            Width = actionWidth,
            Height = height,
        });

        return row;
    }

    private const int DollHeight = 192 + SlotTile + 20;

    /// <summary>흐린 사람 그림 위에 부위 칸 5개. 칸 테두리는 등급색, 착용 불가면 빨강, 고른 칸은 금색.</summary>
    private Widget BuildDoll(PartyMember member)
    {
        var doll = new Panel { Width = DollWidth, Height = DollHeight };
        var figure = _widgets.Icon("figure", DollHeight - 10, Theme.PanelBorder * 0.45f);
        figure.HorizontalAlignment = HorizontalAlignment.Center;
        figure.VerticalAlignment = VerticalAlignment.Top;
        doll.Widgets.Add(figure);

        foreach (var slot in EquipmentSlots.All)
        {
            var cell = BuildSlotCell(member, slot);
            var position = DollPosition(slot);
            cell.Left = position.X;
            cell.Top = position.Y;
            doll.Widgets.Add(cell);
        }

        return doll;
    }

    private Widget BuildSlotCell(PartyMember member, EquipmentSlot slot)
    {
        const int cellWidth = 92;
        var id = member.ItemIn(slot);
        var item = id is null ? null : _data.Items[id];
        var blockedByTwoHanded = slot == EquipmentSlot.OffHand && item is null
            && member.ItemIn(EquipmentSlot.MainHand) is { } main && _data.Items[main].TwoHanded;
        var why = member.WhyCannotWear(slot, _data);

        var (icon, color) = item is not null ? GameIcons.Item(item)
            : blockedByTwoHanded ? ("lock", Theme.TextDim)
            : (GameIcons.EmptySlot(slot), Theme.PanelBorder);
        var border = item is null ? Theme.PanelBorder : why is not null ? Theme.Enemy : ItemText.TierColor(item.Tier);
        var tile = _widgets.IconButton(icon, SlotTile, color, border, slot == _slot);
        tile.HorizontalAlignment = HorizontalAlignment.Center;
        tile.Tooltip = item is null
            ? $"{ItemText.SlotLabel(slot)}: {(blockedByTwoHanded ? "두손 무기 사용 중" : "비어 있음")}"
            : $"{ItemText.SlotLabel(slot)}: {item.Name}{(why is null ? "" : $"\n착용 불가 — {why}")}";
        tile.Click += (_, _) =>
        {
            _slot = slot;
            _candidate = null;
            _detailAction = null;
            MarkDirty();
        };

        var cell = new VerticalStackPanel
        {
            Spacing = 2,
            Width = cellWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        cell.Widgets.Add(tile);
        var name = item is null
            ? Label(blockedByTwoHanded ? "두손 사용 중" : ItemText.SlotLabel(slot), 13, Theme.TextDim)
            : Label(item.Name, 13, why is null ? ItemText.TierColor(item.Tier) : Theme.Enemy, bold: true);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        cell.Widgets.Add(name);
        return cell;
    }

    /// <summary>고른 부위에 낄 수 있는 창고 아이템 칸 (데이터 순서, 오른쪽 아래에 개수). 누르면 가운데에서 미리 본다.</summary>
    private Widget BuildCandidates(PartyMember member)
    {
        const int size = 50;
        const int perRow = 5;
        var list = new VerticalStackPanel { Spacing = 5, Width = DollWidth };
        var current = member.ItemIn(_slot);
        var options = _data.ItemsFor(_slot).Where(i => i.Id != current && _company.StashCount(i.Id) > 0).ToList();
        if (options.Count == 0)
        {
            list.Widgets.Add(Label("창고에 이 부위 아이템이 없다", 14, Theme.TextDim));
        }

        for (var i = 0; i < options.Count; i += perRow)
        {
            var line = new HorizontalStackPanel { Spacing = 5 };
            foreach (var item in options.Skip(i).Take(perRow))
            {
                var wearable = CanWear(member, item);
                var (icon, color) = GameIcons.Item(item);
                var count = _company.StashCount(item.Id);
                var tile = _widgets.IconButton(icon, size, wearable ? color : Theme.Enemy, wearable ? ItemText.TierColor(item.Tier) : Theme.Enemy,
                    item.Id == _candidate, count > 1 ? $"{count}" : null);
                tile.Tooltip = $"{ItemText.ShortName(item)} · {item.TypeOrName}{(wearable ? "" : " (패시브 모자람)")}";
                tile.Click += (_, _) =>
                {
                    _candidate = item.Id;
                    _detailAction = null;
                    MarkDirty();
                };
                line.Widgets.Add(tile);
            }

            list.Widgets.Add(line);
        }

        return list;
    }

    /// <summary>보이는 아이템의 행동: 아이템 행동 칸, 주무기면 그 계열이 주는 행동(정령 소환 등)도.</summary>
    private IReadOnlyList<ActionDefinition> ActionsOf(ItemDefinition item)
    {
        var actions = item.Actions.Select(id => _data.Actions[id]).ToList();
        if (item.Slot == EquipmentSlot.MainHand && item.Mastery is { } mastery)
        {
            actions.AddRange(_data.Actions.Values.Where(a => a.Weapon == mastery && !a.SummonOnly && !actions.Contains(a)));
        }

        return actions;
    }

    /// <summary>아이콘 칸 옆에 큰 이름과 흐린 한 줄 (아이템·행동 설명의 머리).</summary>
    private Widget TitleWithIcon(string icon, Color iconColor, Color border, string title, Color titleColor, string subtitle, int width)
    {
        var row = new HorizontalStackPanel { Spacing = 10, Width = width };
        row.Widgets.Add(_widgets.IconTile(icon, 56, iconColor, border));
        var text = new VerticalStackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Widgets.Add(_widgets.Label(title, 19, titleColor, bold: true, width: width - 66, wrap: true));
        text.Widgets.Add(_widgets.Label(subtitle, 13, Theme.TextDim, width: width - 66, wrap: true));
        row.Widgets.Add(text);
        return row;
    }

    private Widget BuildItemColumn(PartyMember member, ItemDefinition? item)
    {
        var column = new VerticalStackPanel { Spacing = 8, Width = ItemColumnWidth };
        if (item is null)
        {
            column.Widgets.Add(TitleWithIcon(GameIcons.EmptySlot(_slot), Theme.PanelBorder, Theme.PanelBorder,
                $"{ItemText.SlotLabel(_slot)} — 비어 있음", Theme.TextDim, "왼쪽 아래 창고 칸에서 아이템을 고르면 여기서 보고 낄 수 있다.", ItemColumnWidth));
            return column;
        }

        var skills = member.Skills(_data);
        var previewing = _candidate is not null;
        if (previewing)
        {
            column.Widgets.Add(_widgets.Chip("미리 보기 · 아직 끼지 않음", Theme.Selected, 13, Theme.Gold));
        }

        var (icon, color) = GameIcons.Item(item);
        var details = ItemText.Details(item, _data, member.MasteryLevel(item.Mastery!), skills, withActions: false);
        column.Widgets.Add(TitleWithIcon(icon, color, ItemText.TierColor(item.Tier), details[0].Text, details[0].Color, details[1].Text, ItemColumnWidth));
        if (details.Count > 2)
        {
            column.Widgets.Add(_widgets.Card(details.Skip(2), ItemColumnWidth));
        }

        if (!previewing && member.WhyCannotWear(_slot, _data) is { } why)
        {
            column.Widgets.Add(_widgets.Label($"착용 불가 — {why}", 14, Theme.Enemy, width: ItemColumnWidth, wrap: true));
        }

        var buttons = new HorizontalStackPanel { Spacing = 8 };
        if (previewing)
        {
            var equip = TextButton("장착", Theme.Accent, Theme.AccentHover, bold: true);
            equip.Width = 100;
            equip.Click += (_, _) => Equip(member, item.Id);
            buttons.Widgets.Add(equip);

            var cancel = TextButton("취소", Theme.Button, Theme.ButtonHover);
            cancel.Click += (_, _) =>
            {
                _candidate = null;
                _detailAction = null;
                MarkDirty();
            };
            buttons.Widgets.Add(cancel);

            if (member.ItemIn(_slot) is { } worn)
            {
                buttons.Widgets.Add(Label($"지금: {ItemText.ShortName(_data.Items[worn])}", 14, Theme.TextDim));
            }
        }
        else if (!OnExpedition)
        {
            var unequip = TextButton("해제 (창고로)", Theme.Button, Theme.ButtonHover);
            unequip.Click += (_, _) =>
            {
                if (_company.Unequip(member.Id, _slot, _data))
                {
                    _detailAction = null;
                    MarkChanged();
                }
            };
            buttons.Widgets.Add(unequip);
        }

        column.Widgets.Add(buttons);

        var actions = ActionsOf(item);
        if (actions.Count == 0)
        {
            return column;
        }

        column.Widgets.Add(_widgets.Header("action.melee", $"행동 {actions.Count}개", "모두 쓸 수 있다 · 눌러서 설명", size: 15));
        var shownAction = _detailAction ?? actions[0].Id;
        foreach (var action in actions)
        {
            var meets = skills.Meets(action.Requirements);
            var (actionIcon, actionColor) = GameIcons.Action(action, _data);
            var content = new HorizontalStackPanel { Spacing = 8 };
            content.Widgets.Add(_widgets.IconTile(meets ? actionIcon : "lock", 36, meets ? actionColor : Theme.Enemy, padding: 4));
            var text = new VerticalStackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            var top = new HorizontalStackPanel { Spacing = 8 };
            top.Widgets.Add(Label(action.Name, 15, meets ? Theme.Text : Theme.Enemy, bold: true));
            top.Widgets.Add(Label(ActionText.Cost(action), 12, Theme.Cover));
            text.Widgets.Add(top);
            text.Widgets.Add(Label(meets ? ActionText.Target(action, _data) : $"잠김: {LockReason(action, null, skills)}", 12, meets ? Theme.TextDim : Theme.Enemy));
            content.Widgets.Add(text);

            var button = StyledButton(content, action.Id == shownAction ? Theme.Selected : Theme.Button, Theme.ButtonHover);
            button.Width = ItemColumnWidth;
            button.Padding = new Thickness(5, 4);
            button.Click += (_, _) =>
            {
                _detailAction = action.Id;
                MarkDirty();
            };
            column.Widgets.Add(button);
        }

        return column;
    }

    private Widget BuildActionDetail(PartyMember member, ItemDefinition? item, int width)
    {
        var column = new VerticalStackPanel { Spacing = 8, Width = width };
        var actions = item is null ? [] : ActionsOf(item);
        var action = _detailAction is { } id && actions.Any(a => a.Id == id) ? _data.Actions[id] : actions.FirstOrDefault();
        if (action is null)
        {
            column.Widgets.Add(TitleWithIcon("action.buff", Theme.PanelBorder, Theme.PanelBorder, "행동 없음", Theme.TextDim,
                item is null ? "아이템을 끼면 그 아이템의 행동이 여기 나온다." : "이 아이템은 보너스만 준다.", width));
            return column;
        }

        // 낀 아이템의 행동만 지금 장비 기준 예상치를 보여 준다 (미리 보는 아이템은 끼면 달라진다).
        var preview = _candidate is null && member.WhyCannotWear(_slot, _data) is null ? Preview(action) : null;
        var details = ActionText.Details(action, _data, preview, member.Skills(_data));
        var (icon, color) = GameIcons.Action(action, _data);
        column.Widgets.Add(TitleWithIcon(icon, color, Theme.PanelBorder, details[0].Text, Theme.Text, details[1].Text, width));
        column.Widgets.Add(_widgets.Card(details.Skip(2), width));
        if (preview is null)
        {
            column.Widgets.Add(_widgets.Label("끼고 나면 이 멤버의 스탯·패시브로 계산한 예상치가 나온다.", 13, Theme.TextDim, width: width, wrap: true));
        }

        return column;
    }

    /// <summary>창고의 그 아이템을 낀다. 같은 아이템이 여럿이면 아이템 레벨이 가장 높은 한 점을 낀다.</summary>
    private void Equip(PartyMember member, string itemId)
    {
        var gear = _company.BestGear(itemId);
        if (gear is not null && _company.Equip(member.Id, gear.Uid, _data))
        {
            _candidate = null;
            _detailAction = null;
            MarkChanged();
            return;
        }

        if ((gear is null ? "창고에 없습니다" : _company.WhyCannotEquip(member.Id, gear.Uid, _data)) is { } reason)
        {
            _session.Notice = ($"{_data.Items[itemId].Name}: {reason}", Theme.Enemy);
        }

        MarkDirty();
    }

    private bool CanWear(PartyMember member, ItemDefinition item) => member.Skills(_data).Meets(item.Requirements);

    // ── 능력치 탭 ──────────────────────────────────────────

    /// <summary>
    /// 왼쪽: 기본 스탯(아이콘, 값, 그 효과). 가운데: 전투 능력치 타일(HP, MP, 행동 간격, 위력, 방어)과 진형 설명.
    /// 오른쪽: 패시브·장비 보너스 합계와 배운 패시브.
    /// </summary>
    private Widget BuildStatsTab(PartyMember member, int width, int height)
    {
        var rules = CombatRules.Default;
        var unit = UnitPreview.For(_setup!, _data.Catalog, rules);
        var s = member.Stats;
        var columnWidth = (width - Gap * 2) / 3;
        var inner = columnWidth - 18;

        // 기본 스탯
        var basics = new VerticalStackPanel { Spacing = 8, Width = inner };
        basics.Widgets.Add(_widgets.Header("stats", "기본 스탯"));
        foreach (var (icon, color, name, value, effect) in new[]
                 {
                     ("stat.str", Theme.Damage, "근력", s.Str, $"물리 위력 +{s.Str * rules.PowerScalingPercentPerPoint}% · 물리 방어 {s.Str}"),
                     ("stat.dex", Theme.TextDim, "민첩", s.Dex, "아직 전투 규칙에 쓰이지 않는다"),
                     ("stat.vital", Theme.Heal, "체력", s.Vital, $"최대 HP {s.Vital * rules.HpPerVital} (1당 {rules.HpPerVital})"),
                     ("stat.intel", Theme.Lightning, "지능", s.Intel,
                         $"마법·회복 위력 +{s.Intel * rules.PowerScalingPercentPerPoint}% · 마법 방어 {s.Intel} · 최대 MP {s.Intel * rules.MpPerIntel}"),
                     ("stat.speed", Theme.Frost, "신속", s.Speed, $"기본 행동 간격 {rules.TimeConstant} ÷ {s.Speed} = {rules.TimeConstant / Math.Max(1, s.Speed)}"),
                 })
        {
            var row = new HorizontalStackPanel { Spacing = 10 };
            row.Widgets.Add(_widgets.IconTile(icon, 42, color, padding: 5));
            var text = new VerticalStackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            var top = new HorizontalStackPanel { Spacing = 8 };
            top.Widgets.Add(Label(name, 15, Theme.TextDim));
            top.Widgets.Add(Label($"{value}", 20, Theme.Text, bold: true));
            text.Widgets.Add(top);
            text.Widgets.Add(_widgets.Label(effect, 12, Theme.TextDim, width: inner - 52, wrap: true));
            row.Widgets.Add(text);
            basics.Widgets.Add(row);
        }

        // 전투 능력치 타일 (두 칸씩)
        var combat = new VerticalStackPanel { Spacing = 8, Width = inner };
        combat.Widgets.Add(_widgets.Header("power", "전투 능력치"));
        var tiles = new (string Icon, Color Color, string Value, string Label)[]
        {
            ("hp", Theme.HpBar, $"{unit.MaxHp}", "최대 HP"),
            ("mp", Theme.MpBar, $"{unit.MaxMp}", "최대 MP"),
            ("delay", Theme.Cover, $"{unit.BaseDelay}", "행동 간격"),
            ("power", Theme.Damage, $"{unit.PhysicalPowerPercent}%", "물리 위력"),
            ("action.magic", Theme.Lightning, $"{unit.MagicPowerPercent}%", "마법·회복 위력"),
            ("defense", Theme.Text, $"-{unit.PhysicalReductionPercent}%", $"받는 물리 (방어 {unit.Defense})"),
            ("magic-defense", Theme.Frost, $"-{unit.MagicReductionPercent}%", $"받는 마법 (방어 {unit.MagicDefense})"),
        };
        var tileWidth = (inner - 8) / 2;
        for (var i = 0; i < tiles.Length; i += 2)
        {
            var line = new HorizontalStackPanel { Spacing = 8 };
            foreach (var tile in tiles.Skip(i).Take(2))
            {
                line.Widgets.Add(StatTile(tile.Icon, tile.Color, tile.Value, tile.Label, tileWidth));
            }

            combat.Widgets.Add(line);
        }

        var notes = new List<TextLine> { new("행동 간격: 대기 수치 1000인 행동 뒤. 작을수록 자주 행동한다", Theme.TextDim, 13) };
        if (unit.DamageTakenReductionPercent != 0)
        {
            notes.Add(new($"방어 뒤 받는 피해 -{unit.DamageTakenReductionPercent}%", Theme.Text, 14));
        }

        if (member.ItemIn(EquipmentSlot.MainHand) is { } mainId && _data.Items[mainId] is { } main)
        {
            if (main.PowerMultiplierPercent != 100)
            {
                notes.Add(new($"무기 위력 ×{main.PowerMultiplierPercent / 100.0:0.##} ({_data.Masteries[main.Mastery!].Name} 행동)", Theme.Damage, 14));
            }

            if (main.EffectDurationBonus > 0)
            {
                notes.Add(new($"거는 효과 지속 +{main.EffectDurationBonus}턴", Theme.Damage, 14));
            }
        }

        notes.Add(new($"진형: {RowLabel(member.Row)}", Theme.Text, 15, Bold: true));
        notes.Add(new(member.Row == Row.Front
            ? "적의 단일 공격이 후위를 노리면 전위 중 한 명이 대신 맞는다. 전위 우선 공격도 먼저 받는다."
            : "살아 있는 전위가 있으면 단일 공격을 전위가 대신 맞는다 (엄호 무시·전체 공격 제외).", Theme.TextDim, 13));
        combat.Widgets.Add(_widgets.Card(notes, inner));

        // 보너스와 패시브
        var bonuses = new VerticalStackPanel { Spacing = 8, Width = inner };
        bonuses.Widgets.Add(_widgets.Header("power", "보너스 합계", "패시브 + 장비"));
        var totals = BonusTotals(member);
        bonuses.Widgets.Add(_widgets.Card(totals.Count == 0
            ? [new TextLine("아직 없음", Theme.TextDim, 14)]
            : totals.Select(t => new TextLine(t, Theme.Text, 14)), inner));
        bonuses.Widgets.Add(_widgets.Header("mastery", "배운 패시브"));
        var learned = _data.Skills.Values.Where(k => member.SkillLevels.GetValueOrDefault(k.Id) > 0).ToList();
        if (learned.Count == 0)
        {
            bonuses.Widgets.Add(Label("배운 패시브 없음", 14, Theme.TextDim));
        }

        foreach (var skill in learned)
        {
            var row = new HorizontalStackPanel { Spacing = 8 };
            row.Widgets.Add(_widgets.IconTile(GameIcons.Mastery(skill.Mastery), 30, GameIcons.MasteryColor(skill.Mastery), padding: 4));
            var text = new VerticalStackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
            text.Widgets.Add(Label($"{skill.Name}  {SkillText.LevelPips(member.SkillLevels[skill.Id])}", 14, Theme.Text, bold: true));
            text.Widgets.Add(Label(_data.Masteries[skill.Mastery].Name, 12, Theme.TextDim));
            row.Widgets.Add(text);
            bonuses.Widgets.Add(row);
        }

        var columns = new HorizontalStackPanel { Spacing = Gap, Height = height };
        foreach (var content in new Widget[] { basics, combat, bonuses })
        {
            columns.Widgets.Add(new ScrollPane
            {
                Content = content,
                ShowHorizontalScrollBar = false,
                Width = columnWidth,
                Height = height,
            });
        }

        return columns;
    }

    /// <summary>능력치 타일: 아이콘, 큰 숫자, 아래 작은 이름.</summary>
    private Widget StatTile(string icon, Color color, string value, string label, int width)
    {
        var tile = new VerticalStackPanel
        {
            Spacing = 2,
            Width = width,
            Padding = new Thickness(8, 6),
            Background = new SolidBrush(Theme.BarBack),
            Border = new SolidBrush(Theme.PanelBorder),
            BorderThickness = new Thickness(1),
        };
        var top = new HorizontalStackPanel { Spacing = 6 };
        top.Widgets.Add(_widgets.Icon(icon, 24, color));
        top.Widgets.Add(Label(value, 21, Theme.Text, bold: true));
        tile.Widgets.Add(top);
        tile.Widgets.Add(Label(label, 12, Theme.TextDim));
        return tile;
    }

    /// <summary>"화염 위력 +23% (패시브 15 · 장비 8)"처럼 종류·태그별 합계.</summary>
    private List<string> BonusTotals(PartyMember member)
    {
        var totals = new Dictionary<(BonusKind Kind, string? Tag), (int Skill, int Item)>();
        foreach (var (skillId, level) in member.SkillLevels)
        {
            if (level <= 0 || !_data.Skills.TryGetValue(skillId, out var skill))
            {
                continue;
            }

            foreach (var bonus in skill.Bonuses)
            {
                var current = totals.GetValueOrDefault((bonus.Kind, bonus.Tag));
                totals[(bonus.Kind, bonus.Tag)] = (current.Skill + bonus.PercentPerLevel * level, current.Item);
            }
        }

        foreach (var bonus in member.ItemBonuses(_data))
        {
            var current = totals.GetValueOrDefault((bonus.Kind, bonus.Tag));
            totals[(bonus.Kind, bonus.Tag)] = (current.Skill, current.Item + bonus.Percent);
        }

        return totals
            .OrderBy(t => t.Key.Kind).ThenBy(t => t.Key.Tag)
            .Select(t =>
            {
                var label = SkillText.BonusLabel(t.Key.Kind, t.Value.Skill + t.Value.Item, t.Key.Tag, _data);
                var source = t.Value switch
                {
                    { Skill: > 0, Item: > 0 } => $" (패시브 {t.Value.Skill} · 장비 {t.Value.Item})",
                    { Item: > 0 } => " (장비)",
                    _ => " (패시브)",
                };
                return label + source;
            })
            .ToList();
    }

    // ── 전술 탭 ────────────────────────────────────────────

    private const int SubjectWidth = 170;

    /// <summary>조건 빼기(×) 칸. 조건이 하나뿐이어도 자리를 비워 두어 행동 버튼 위치를 맞춘다.</summary>
    private const int RemoveConditionWidth = 28;
    private const int CompareWidth = 64;
    private const int ValueColumnWidth = 104;
    private const int ActionButtonWidth = 230;

    private Widget BuildTacticsTab(PartyMember member, int height)
    {
        var panel = new VerticalStackPanel { Spacing = 6, Height = height };
        panel.Widgets.Add(BuildTacticHeader());

        var tactics = new VerticalStackPanel { Spacing = 8 };
        for (var i = 0; i < TacticsOf(member).Count; i++)
        {
            tactics.Widgets.Add(BuildTacticRow(member, i));
        }

        if (TacticsOf(member).Count == 0)
        {
            tactics.Widgets.Add(Label("전술이 없으면 이 유닛은 매 턴 기다린다.", 16, Theme.TextDim));
        }

        // 새 전술은 쓸 수 있는 첫 행동으로 시작한다.
        var skills = member.Skills(_data);
        var firstUsable = _data.Actions.Values.FirstOrDefault(a => !a.SummonOnly && a.IsUsableBy(member.GrantedActions(_data), skills));
        var addButton = TextButton("+ 전술 추가", Theme.Button, Theme.ButtonHover);
        addButton.Enabled = TacticsOf(member).Count < MaxTactics && firstUsable is not null;
        addButton.Click += (_, _) =>
        {
            TacticsOf(member).Add(Condition.Always, 0, firstUsable!.Id);
            MarkChanged();
        };
        tactics.Widgets.Add(addButton);

        var position = _tacticScroll?.ScrollPosition ?? Point.Zero;
        _tacticScroll = new ScrollPane { Content = tactics, ShowHorizontalScrollBar = false, Height = height - 30 };
        _tacticScroll.ScrollPosition = position;
        panel.Widgets.Add(_tacticScroll);
        return panel;
    }

    private Widget BuildTacticHeader()
    {
        var header = new HorizontalStackPanel { Spacing = 8 };
        header.Margin = new Thickness(9, 0, 0, 0);
        header.Widgets.Add(Label("순위", 13, Theme.TextDim, width: 30));
        header.Widgets.Add(Label("조건 (무엇을 · 이상/이하 · %/수치)", 14, Theme.TextDim, width: SubjectWidth + CompareWidth * 2 + 16));
        header.Widgets.Add(Label("값", 14, Theme.TextDim, width: ValueColumnWidth + 8 + RemoveConditionWidth));
        header.Widgets.Add(Label("행동 — 눌러서 고르기", 14, Theme.TextDim, width: ActionButtonWidth));
        return header;
    }

    private Widget BuildTacticRow(PartyMember member, int index)
    {
        var tactic = TacticsOf(member)[index];
        var conditions = tactic.Conditions;
        var skills = member.Skills(_data);
        var granted = member.GrantedActions(_data);
        var action = _data.Actions.GetValueOrDefault(tactic.ActionId);
        var usable = action is not null && action.IsUsableBy(granted, skills);

        // 첫 줄: 순위 · 첫 조건 · 행동 · 순서/삭제
        var first = new HorizontalStackPanel { Spacing = 8 };
        var badge = new Panel
        {
            Width = 30,
            Height = 30,
            Background = new SolidBrush(Theme.Gold),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var number = Label($"{tactic.Priority}", 17, Theme.BarBack, bold: true);
        number.HorizontalAlignment = HorizontalAlignment.Center;
        badge.Widgets.Add(number);
        first.Widgets.Add(badge);
        AddConditionEditor(first, member, index, 0);

        // 행동: 누르면 장비별 목록에서 고른다.
        var (actionIcon, actionColor) = action is null ? ("lock", Theme.Enemy) : GameIcons.Action(action, _data);
        var actionContent = new HorizontalStackPanel { Spacing = 6 };
        actionContent.Widgets.Add(_widgets.Icon(usable ? actionIcon : "lock", 22, usable ? actionColor : Theme.Enemy));
        actionContent.Widgets.Add(Label(usable ? action!.Name : $"(잠김) {action?.Name ?? tactic.ActionId}", 16, usable ? Theme.Text : Theme.Enemy, bold: true));
        actionContent.Widgets.Add(Label("▾", 14, Theme.TextDim));
        var actionButton = StyledButton(actionContent, Theme.Button, Theme.ButtonHover);
        actionButton.Width = ActionButtonWidth;
        actionButton.Padding = new Thickness(8, 4);
        actionButton.Click += (_, _) =>
        {
            _picking = index;
            MarkDirty();
        };
        first.Widgets.Add(actionButton);

        // 순서 / 삭제
        first.Widgets.Add(SmallButton("▲", index > 0, () => TacticsOf(member).Move(index, -1)));
        first.Widgets.Add(SmallButton("▼", index < TacticsOf(member).Count - 1, () => TacticsOf(member).Move(index, 1)));
        first.Widgets.Add(SmallButton("삭제", true, () => TacticsOf(member).Remove(index), width: 52));

        var block = new VerticalStackPanel
        {
            Spacing = 3,
            Padding = new Thickness(8, 6),
            Background = new SolidBrush(Theme.BarBack * 0.7f),
            Border = new SolidBrush(usable ? Theme.PanelBorder : Theme.Enemy),
            BorderThickness = new Thickness(1),
        };
        block.Widgets.Add(first);

        // 더 건 조건: 모두 참이어야 한다.
        for (var k = 1; k < conditions.Count; k++)
        {
            var row = new HorizontalStackPanel { Spacing = 8 };
            var and = Label("그리고", 12, Theme.Gold, bold: true, width: 30);
            and.VerticalAlignment = VerticalAlignment.Center;
            row.Widgets.Add(and);
            AddConditionEditor(row, member, index, k);
            block.Widgets.Add(row);
        }

        // 아래 줄: 조건 더하기 · 이 전술이 하는 일
        var bottom = new HorizontalStackPanel { Spacing = 8, Margin = new Thickness(38, 0, 0, 0) };
        if (conditions.Count < Tactic.MaxConditions)
        {
            var add = _widgets.TextButton("+ 그리고", Theme.Button, Theme.ButtonHover, size: 13);
            add.Width = 76;
            add.VerticalAlignment = VerticalAlignment.Top;
            add.Click += (_, _) =>
            {
                if (TacticsOf(member).AddCondition(index, Condition.Always, 0))
                {
                    MarkChanged();
                }
            };
            bottom.Widgets.Add(add);
        }

        var effectName = EffectName(action);
        var summary = action is null ? "없는 행동입니다 (데이터에서 지워짐)"
            : usable ? $"{TacticText.Sentence(tactic, effectName)} → {ActionText.Summary(action, _data, Preview(action))}"
                + (ActionText.Cost(action, Preview(action)) is { Length: > 0 } cost ? $" · {cost}" : "")
            : $"잠김: {LockReason(action, granted, skills)}";
        var note = Label(summary, 13, usable ? Theme.TextDim : Theme.Enemy);
        note.Wrap = true;
        note.Width = conditions.Count < Tactic.MaxConditions ? 736 : 820;
        bottom.Widgets.Add(note);
        block.Widgets.Add(bottom);

        // 효과를 걸지 않는 행동에 효과 조건을 걸면 그 줄은 쓰이지 않는다.
        if (action is not null && effectName is null && conditions.Any(c => c.Condition.UsesActionEffect()))
        {
            var warning = Label("이 행동은 효과를 걸지 않아 '효과 없음' 조건이 늘 거짓이다. 이 줄은 쓰이지 않는다.", 13, Theme.Enemy);
            warning.Margin = new Thickness(38, 0, 0, 0);
            block.Widgets.Add(warning);
        }

        return block;
    }

    /// <summary>
    /// 조건 하나의 편집 칸: 무엇을 보는가 · 이상/이하 · %/수치 · 값 · 빼기.
    /// 빼기(×)는 조건이 둘 이상일 때만 누를 수 있고, 첫 조건을 빼면 다음 조건이 첫 조건이 된다.
    /// </summary>
    private void AddConditionEditor(HorizontalStackPanel row, PartyMember member, int index, int conditionIndex)
    {
        var condition = TacticsOf(member)[index].Conditions[conditionIndex];

        // 값 입력은 화면을 다시 만들지 않고 바로 반영하므로, 핸들러에서는 항상 현재 조건을 다시 읽는다.
        TacticCondition Current() => TacticsOf(member)[index].Conditions[conditionIndex];

        void SetCondition(Condition next)
        {
            var current = Current();
            if (next == current.Condition)
            {
                return;
            }

            var value = TacticText.ValueAfterConditionChange(current.Condition, next, current.Value);
            TacticsOf(member).SetCondition(index, conditionIndex, next, value);
            MarkChanged();
        }

        var (subject, atLeast, percent) = TacticText.Split(condition.Condition);
        var subjectCombo = Combo(TacticText.Subjects.Select(s => (s.Label, s.Color)), subject, SubjectWidth);
        subjectCombo.DropdownMaximumHeight = 470;
        subjectCombo.SelectedIndexChanged += (_, _) =>
        {
            var next = subjectCombo.SelectedIndex ?? 0;
            var (_, a, p) = TacticText.Split(Current().Condition);
            var key = TacticText.Subjects[next].Key;
            // HP·MP는 보통 "이하", 수는 "이상"으로 시작한다.
            var hpMp = key.EndsWith("Hp") || key.EndsWith("Mp");
            SetCondition(TacticText.Compose(next, a ?? !hpMp, p ?? true));
        };
        row.Widgets.Add(subjectCombo);

        if (atLeast is { } isAtLeast)
        {
            var compare = Combo(["이상", "이하"], isAtLeast ? 0 : 1, CompareWidth);
            compare.SelectedIndexChanged += (_, _) =>
                SetCondition(TacticText.Compose(subject, compare.SelectedIndex == 0, percent ?? true));
            row.Widgets.Add(compare);
        }
        else
        {
            row.Widgets.Add(new Panel { Width = CompareWidth });
        }

        if (percent is { } isPercent)
        {
            var unit = Combo(["%", "수치"], isPercent ? 0 : 1, CompareWidth);
            unit.SelectedIndexChanged += (_, _) =>
                SetCondition(TacticText.Compose(subject, atLeast ?? true, unit.SelectedIndex == 0));
            row.Widgets.Add(unit);
        }
        else
        {
            row.Widgets.Add(new Panel { Width = CompareWidth });
        }

        row.Widgets.Add(TacticText.HasValue(condition.Condition)
            ? BuildValueInput(member, index, conditionIndex)
            : Label("—", 17, Theme.TextDim, width: ValueColumnWidth));

        if (TacticsOf(member)[index].Conditions.Count > 1)
        {
            row.Widgets.Add(SmallButton("×", true, () => TacticsOf(member).RemoveCondition(index, conditionIndex), width: RemoveConditionWidth));
        }
        else
        {
            row.Widgets.Add(new Panel { Width = RemoveConditionWidth });
        }
    }

    /// <summary>행동이 거는 첫 효과의 이름 (효과 조건의 문장에 쓴다). 효과를 걸지 않으면 null.</summary>
    private string? EffectName(ActionDefinition? action) =>
        action?.Applies.FirstOrDefault() is { } applied && _data.Effects.TryGetValue(applied.EffectId, out var effect) ? effect.Name : null;

    /// <summary>
    /// 값 입력칸. 정수만 입력할 수 있다(수치 조건은 음수도).
    /// 백분율은 0–100, 턴 주기는 1 이상, 횟수·턴은 0 이상을 검사하고, 수치 조건은 검사하지 않는다.
    /// 올바른 값은 바로 반영하고(화면은 다시 만들지 않는다 — 입력칸 포커스를 지키려고),
    /// 잘못된 값은 빨간색으로 표시하고 반영하지 않는다.
    /// </summary>
    private Widget BuildValueInput(PartyMember member, int index, int conditionIndex)
    {
        var tactic = TacticsOf(member)[index].Conditions[conditionIndex];
        var group = new HorizontalStackPanel { Spacing = 6, Width = ValueColumnWidth };

        var box = new TextBox
        {
            Text = tactic.Value.ToString(),
            Width = 56,
            Font = _ui.Font(17),
            TextColor = Theme.Text,
            FocusedTextColor = Theme.Text,
            Background = new SolidBrush(Theme.BarBack),
            Padding = new Thickness(6, 3),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var hint = Label(TacticText.Unit(tactic.Condition), 14, Theme.TextDim);

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
            var current = TacticsOf(member)[index].Conditions[conditionIndex];
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
                TacticsOf(member).SetCondition(index, conditionIndex, current.Condition, value);
                MarkChangedInPlace();
            }
        };

        group.Widgets.Add(box);
        group.Widgets.Add(hint);
        return group;
    }

    // ── 행동 고르기 (팝업) ──────────────────────────────────

    private const int PickerWidth = 860;
    private const int PickerHeight = 580;
    private const int PickerListWidth = 400;

    /// <summary>팝업 오른쪽의 행동 설명 칸. 목록에서 마우스를 올린 행동으로 바로 바꾼다(화면은 다시 만들지 않는다).</summary>
    private VerticalStackPanel? _pickerDetail;

    /// <summary>
    /// 전술 하나의 행동을 고르는 팝업. 화면을 어둡게 덮고 가운데에 뜬다.
    /// 왼쪽은 아이콘·이름·비용 목록(낀 장비별, 무기 계열, 공용으로 묶음), 오른쪽은 마우스를 올린 행동의 설명이다.
    /// 누르면 그 행동으로 정하고 닫는다. 패시브나 장비가 모자란 행동은 빨간 자물쇠이고 고를 수 없다.
    /// 지금 고른 행동이 장비에서 빠졌으면 따로 보여 준다. 닫기 버튼이나 Esc로 바꾸지 않고 닫는다.
    /// </summary>
    private Widget BuildActionPicker(PartyMember member, int index)
    {
        var tactic = TacticsOf(member)[index];
        var overlay = new Panel
        {
            Left = _bounds.Left,
            Top = _bounds.Top,
            Width = _bounds.Width,
            Height = _bounds.Height,
            Background = new SolidBrush(Color.Black * 0.7f),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var popup = new VerticalStackPanel
        {
            Spacing = 10,
            Width = PickerWidth,
            Height = PickerHeight,
            Padding = new Thickness(18, 14),
            Background = new SolidBrush(Theme.Panel),
            Border = new SolidBrush(Theme.Gold),
            BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        overlay.Widgets.Add(popup);

        // 머리: 제목, 조건, 닫기
        var head = new HorizontalStackPanel { Spacing = 12 };
        var titles = new VerticalStackPanel { Spacing = 2, Width = PickerWidth - 36 - 130 };
        titles.Widgets.Add(_widgets.Header("tactics", $"{tactic.Priority}순위 전술 · 쓸 행동 고르기", size: 20));
        titles.Widgets.Add(Label($"조건 \"{TacticText.Sentence(tactic)}\"이 모두 맞으면 이 행동을 쓴다. 눌러서 정한다.", 14, Theme.TextDim));
        head.Widgets.Add(titles);
        var close = TextButton("닫기 (Esc)", Theme.Button, Theme.ButtonHover);
        close.Width = 120;
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Click += (_, _) =>
        {
            _picking = null;
            MarkDirty();
        };
        head.Widgets.Add(close);
        popup.Widgets.Add(head);

        // 몸통: 왼쪽 목록, 오른쪽 설명
        var bodyHeight = PickerHeight - 28 - 60;
        var body = new HorizontalStackPanel { Spacing = 14, Height = bodyHeight };
        var list = new VerticalStackPanel { Spacing = 4, Width = PickerListWidth - 18 };
        var skills = member.Skills(_data);
        var granted = member.GrantedActions(_data);
        var shown = new HashSet<string>();
        void Section(string title, IEnumerable<ActionDefinition> actions, string icon)
        {
            var group = actions.Where(a => !a.SummonOnly && shown.Add(a.Id)).ToList();
            if (group.Count == 0)
            {
                return;
            }

            var header = _widgets.Header(icon, title, size: 15);
            header.Margin = new Thickness(0, list.Widgets.Count == 0 ? 0 : 8, 0, 2);
            list.Widgets.Add(header);
            foreach (var action in group)
            {
                list.Widgets.Add(BuildPickerRow(member, index, action, granted, skills));
            }
        }

        foreach (var (slot, item) in member.WornItems(_data))
        {
            Section($"{ItemText.SlotLabel(slot)} · {ItemText.ShortName(item)}", item.Actions.Select(id => _data.Actions[id]), GameIcons.Item(item).Icon);
        }

        if (member.WeaponMastery(_data) is { } weapon)
        {
            Section($"{_data.Masteries[weapon].Name} 무기 계열", _data.Actions.Values.Where(a => a.Weapon == weapon), GameIcons.Mastery(weapon));
        }

        Section("공용 (누구나)", _data.Actions.Values.Where(a => a.Universal), "figure");
        if (_data.Actions.TryGetValue(tactic.ActionId, out var chosen))
        {
            Section("지금 고른 행동 (낀 장비가 주지 않음)", [chosen], "lock");
        }

        body.Widgets.Add(new ScrollPane { Content = list, ShowHorizontalScrollBar = false, Width = PickerListWidth, Height = bodyHeight });

        var detailWidth = PickerWidth - 36 - PickerListWidth - 14;
        _pickerDetail = new VerticalStackPanel { Spacing = 8, Width = detailWidth - 18 };
        body.Widgets.Add(new ScrollPane { Content = _pickerDetail, ShowHorizontalScrollBar = false, Width = detailWidth, Height = bodyHeight });
        popup.Widgets.Add(body);

        var first = chosen ?? _data.Actions.Values.FirstOrDefault(a => shown.Contains(a.Id));
        if (first is not null)
        {
            ShowPickerDetail(member, first, granted, skills);
        }

        return overlay;
    }

    /// <summary>목록 한 줄: 아이콘, 이름, 비용. 지금 고른 행동은 금색 테두리.</summary>
    private Widget BuildPickerRow(PartyMember member, int index, ActionDefinition action, IReadOnlySet<string> granted, SkillSet skills)
    {
        var usable = action.IsUsableBy(granted, skills);
        var (icon, color) = GameIcons.Action(action, _data);
        var content = new HorizontalStackPanel { Spacing = 10 };
        content.Widgets.Add(_widgets.IconTile(usable ? icon : "lock", 38, usable ? color : Theme.Enemy, padding: 5));
        content.Widgets.Add(Label(action.Name, 17, usable ? Theme.Text : Theme.Enemy, bold: true, width: 170));
        var preview = usable ? Preview(action) : null;
        content.Widgets.Add(Label(ActionText.Cost(action, preview) is { Length: > 0 } cost ? cost : "비용 없음", 13, usable ? Theme.Cover : Theme.TextDim));

        var current = TacticsOf(member)[index].ActionId == action.Id;
        var button = StyledButton(content, current ? Theme.Selected : Theme.Button, Theme.ButtonHover);
        button.Width = PickerListWidth - 22;
        button.Padding = new Thickness(5, 4);
        button.MouseEntered += (_, _) => ShowPickerDetail(member, action, granted, skills);
        button.Click += (_, _) =>
        {
            if (!usable)
            {
                ShowPickerDetail(member, action, granted, skills);
                return;
            }

            var tactic = TacticsOf(member)[index];
            TacticsOf(member).SetAction(index, action.Id);
            _picking = null;
            MarkChanged();
        };
        return button;
    }

    private void ShowPickerDetail(PartyMember member, ActionDefinition action, IReadOnlySet<string> granted, SkillSet skills)
    {
        if (_pickerDetail is not { } detail)
        {
            return;
        }

        var width = detail.Width ?? 300;
        var usable = action.IsUsableBy(granted, skills);
        var lines = ActionText.Details(action, _data, usable ? Preview(action) : null, skills);
        var (icon, color) = GameIcons.Action(action, _data);
        detail.Widgets.Clear();
        detail.Widgets.Add(TitleWithIcon(usable ? icon : "lock", usable ? color : Theme.Enemy, usable ? Theme.PanelBorder : Theme.Enemy,
            lines[0].Text, usable ? Theme.Text : Theme.Enemy, lines[1].Text, width));
        if (!usable)
        {
            detail.Widgets.Add(_widgets.Label($"고를 수 없음: {LockReason(action, granted, skills)}", 15, Theme.Enemy, width: width, wrap: true));
        }

        detail.Widgets.Add(_widgets.Card(lines.Skip(2), width));
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

        // 게임 데이터나 장비가 바뀌어 잠긴 전술이나 착용 불가 장비가 있으면 고칠 때까지 출정·전투를 막는다.
        _statusLabel = _company.WhyLineupCannotFight(_data) is { } blocked
            ? Label($"{blocked} (전투할 수 없습니다)", 16, Theme.Enemy)
            : Label(StatusText, 16, Theme.Cover);
        bar.Widgets.Add(_statusLabel);

        return bar;
    }

    // ── 위젯 도우미 ────────────────────────────────────────

    private static string RowLabel(Row row) => row == Row.Front ? "전위" : "후위";

    private static Color RowColor(Row row) => row == Row.Front ? Theme.FrontRow : Theme.BackRow;

    /// <summary>행동을 못 쓰는 이유, 예: "단궁 필요, 정밀 사격 1". granted가 null이면 장비는 보지 않는다.</summary>
    private string LockReason(ActionDefinition action, IReadOnlySet<string>? granted, SkillSet skills)
    {
        var reasons = new List<string>();
        if (granted is not null && !action.IsGranted(granted))
        {
            var items = _data.ItemsGranting(action.Id).Select(i => i.TypeOrName).Distinct().Take(2).ToList();
            reasons.Add(action.Weapon is { } weapon ? $"{_data.Masteries[weapon].Name} 무기 필요"
                : items.Count == 0 ? "쓸 수 있는 장비 없음"
                : $"{string.Join("·", items)} 필요");
        }

        reasons.AddRange(skills.Missing(action.Requirements).Select(r => $"{_data.Skills[r.SkillId].Name} {r.Level}"));
        return string.Join(", ", reasons);
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
