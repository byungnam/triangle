using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;
using Triangle.Core.Combat;
using Triangle.Core.Runs;
using Triangle.Core.Units;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 새 원정: 클래스로 파티를 고른다 (같은 클래스 여럿 가능). 위는 클래스 카드, 아래는 고른 자리와 출발·해금 버튼.
/// 자리마다 전위·후위를 고른다(클래스와 상관없이, 처음 값은 클래스가 정한다). 아직 열지 않은 클래스는 흐리게 보이고 넣을 수 없다.
/// 판을 이긴 적이 있으면 출발할 때 원정 지도를 고른다(기본 지도와 지도 등급에서 굴린 지도들, 수식어와 보상을 보여 주는 창).
/// </summary>
internal sealed class RunPartyScene : RunScene
{
    private const int CardHeight = 392;
    private const int Gap = 16;

    private readonly List<(string ClassId, Row Row)> _picked;
    private readonly Action<IReadOnlyList<string>, IReadOnlyList<Row>, RunChart> _start;
    private readonly Action _openUnlocks;
    private readonly RunUnlocks _unlocks;

    /// <summary>원정 지도를 고르는 창이 떠 있다.</summary>
    private bool _choosingChart;

    /// <summary>클래스마다 시작 상태의 전투 입력 (HP·MP 표시용).</summary>
    private readonly Dictionary<string, UnitPreview> _previews;

    /// <param name="start">고른 클래스, 자리마다의 줄, 원정 지도로 판을 시작한다.</param>
    /// <param name="openUnlocks">해금·기록 화면을 연다.</param>
    public RunPartyScene(Ui ui, RunSession session, Rectangle bounds, Action<IReadOnlyList<string>, IReadOnlyList<Row>, RunChart> start, Action openUnlocks)
        : base(ui, session, bounds, () => { })
    {
        _start = start;
        _openUnlocks = openUnlocks;
        _unlocks = session.Profile.UnlocksForRun(session.Data);
        _picked = session.Data.Classes.Values.Where(c => !_unlocks.Locked.Contains(c.Id)).Take(session.Data.Run!.PartySize).Select(c => (c.Id, c.Row)).ToList();
        _previews = session.Data.Classes.Keys.ToDictionary(id => id, id =>
        {
            var run = Run.Start(session.Data, [id], 0);
            return UnitPreview.For(run.Setup(session.Data, run.Members[0]), Run.Catalog(session.Data), CombatRules.Default);
        });
    }

    private int PartySize => Data.Run!.PartySize;

    protected override string Title => "새 원정 — 파티 고르기";

    protected override string? Status => $"파티 {_picked.Count}/{PartySize}";

    protected override string Help => "같은 클래스를 여럿 데려가도 됩니다. 전위·후위는 자리마다 고르고 원정 중에도 편성에서 바꿉니다. 보스는 출발할 때 정해집니다.";

    private int CardWidth => (Body.Width - Gap * (Data.Classes.Count - 1)) / Data.Classes.Count;

    protected override void DrawBack(SpriteBatch batch)
    {
        var top = Body.Top + CardHeight + Gap;
        Ui.Panel(batch, new Rectangle(Body.Left, top, Body.Width, Body.Bottom - top));
    }

    protected override Widget Build()
    {
        var root = new Panel();
        var x = Body.Left;
        foreach (var c in Data.Classes.Values)
        {
            root.Widgets.Add(Place(ClassCard(c), x, Body.Top, CardWidth, CardHeight));
            x += CardWidth + Gap;
        }

        var top = Body.Top + CardHeight + Gap;
        var party = new HorizontalStackPanel { Spacing = 12 };
        for (var i = 0; i < PartySize; i++)
        {
            party.Widgets.Add(Slot(i));
        }

        root.Widgets.Add(Place(party, Body.Left + 16, top + 16));

        var classIds = _picked.Select(p => p.ClassId).ToList();
        var tier = Session.Profile.MapTier;
        root.Widgets.Add(Place(W.Label(tier == 0
            ? "원정을 처음 성공하면 원정 지도가 열립니다. 적을 강하게 하거나 파티를 약하게 하는 수식어가 붙는 대신 보상과 명성이 는다."
            : $"지도 등급 {tier}: 출발하면 기본 지도와 수식어가 {tier}개까지 붙은 지도 {Session.Charts.Count - 1}장 중 하나를 고릅니다.", 15, Theme.TextDim,
            width: Body.Width - 32 - 220 - 16, wrap: true), Body.Left + 16, Body.Bottom - 12 - 44));
        var start = Button("출발  ▶", () => Depart(classIds), Run.WhyCannotStart(Data, classIds, _unlocks), accent: true, width: 220, size: 22);
        start.Height = 64;
        root.Widgets.Add(Place(start, Body.Right - 16 - 220, Body.Bottom - 16 - 64));
        var unlocks = Button($"해금 · 기록 (명성 {Session.Profile.Renown})", _openUnlocks, width: 220);
        root.Widgets.Add(Place(unlocks, Body.Right - 16 - 220, top + 16));
        // 연 해금이 이 판에 주는 것: 시작 골드와 편의.
        var boons = (_unlocks.StartGold > 0 ? [$"시작 골드 +{_unlocks.StartGold}"] : Array.Empty<string>()).Concat(_unlocks.Boons.Parts()).ToList();
        if (boons.Count > 0)
        {
            root.Widgets.Add(Place(W.Label(string.Join("\n", boons), 15, Theme.Cover, width: 220, wrap: true), Body.Right - 16 - 220, top + 62));
        }

        if (_choosingChart)
        {
            root.Widgets.Add(ChartPicker());
        }

        return root;
    }

    /// <summary>출발: 고를 원정 지도가 기본 지도뿐이면 바로, 아니면 지도를 고르는 창을 띄운다.</summary>
    private void Depart(IReadOnlyList<string> classIds)
    {
        if (Session.Charts.Count <= 1)
        {
            _start(classIds, _picked.Select(p => p.Row).ToList(), RunChart.Base);
            return;
        }

        _choosingChart = true;
        Refresh();
    }

    protected override void OnEscape()
    {
        _choosingChart = false;
        Refresh();
    }

    /// <summary>원정 지도를 고르는 창: 지도마다 이름, 위험도, 수식어(이름과 거는 것), 보상, 출발 단추.</summary>
    private Widget ChartPicker()
    {
        var charts = Session.Charts;
        const int Gap = 12;
        var width = (Body.Width - 48 - Gap * (charts.Count - 1)) / charts.Count;
        var window = Box(Body.Width - 24, spacing: 10, border: Theme.Gold);
        window.Widgets.Add(W.Label("원정 지도 고르기", 24, Theme.Gold, bold: true));
        window.Widgets.Add(W.Label("수식어는 판 내내 걸립니다. 위험도가 높을수록 지도의 보상과 판이 끝날 때 받는 명성이 큽니다.     Esc  파티로 돌아가기", 15, Theme.TextDim));
        var row = new HorizontalStackPanel { Spacing = Gap };
        var classIds = _picked.Select(p => p.ClassId).ToList();
        foreach (var chart in charts)
        {
            row.Widgets.Add(ChartCard(chart, width, () => _start(classIds, _picked.Select(p => p.Row).ToList(), chart)));
        }

        window.Widgets.Add(row);
        return Overlay(window);
    }

    private Widget ChartCard(RunChart chart, int width, Action choose)
    {
        const int Height = 470;
        var inner = width - 24;
        var risk = chart.Risk(Data);
        var card = Box(width, Height, spacing: 6, border: chart.IsBase ? null : Theme.PanelBorder);
        var head = new HorizontalStackPanel { Spacing = 8 };
        head.Widgets.Add(W.Label(chart.Name(Data), 20, chart.IsBase ? Theme.Text : Theme.Gold, bold: true));
        if (!chart.IsBase)
        {
            head.Widgets.Add(W.Chip($"위험도 {risk}", Theme.Enemy * 0.6f, 13));
        }

        card.Widgets.Add(head);
        if (chart.IsBase)
        {
            card.Widgets.Add(W.Label("수식어도 보상도 없는 처음 그대로의 원정.", 14, Theme.TextDim, width: inner, wrap: true));
        }

        var rules = Data.Run!.Charts;
        foreach (var id in chart.Modifiers)
        {
            var modifier = rules.Modifiers.First(m => m.Id == id);
            card.Widgets.Add(W.Label(modifier.Name, 15, Theme.Enemy, bold: true, width: inner, wrap: true));
            card.Widgets.Add(W.Label(string.Join(" · ", modifier.Terms.Parts(Data)), 13, Theme.TextDim, width: inner, wrap: true));
        }

        if (!chart.IsBase)
        {
            card.Widgets.Add(new Panel { Height = 4 });
            card.Widgets.Add(W.Label("보상", 15, Theme.Heal, bold: true));
            foreach (var part in chart.Bounty(Data).Parts(Data))
            {
                card.Widgets.Add(W.Label(part, 14, Theme.Heal, width: inner, wrap: true));
            }
        }

        var go = Button("이 지도로 출발", choose, accent: true, width: inner);
        return new Panel { Width = width, Height = Height, Widgets = { card, Place(go, 12, Height - 52) } };
    }

    private Widget ClassCard(Triangle.Core.Runs.ClassDefinition c)
    {
        var width = CardWidth;
        var inner = width - 24;
        var locked = _unlocks.Locked.Contains(c.Id);
        var card = Box(width, CardHeight, spacing: 6, background: locked ? Theme.BarBack : null);
        var dim = locked ? 0.5f : 1f;

        var head = new HorizontalStackPanel { Spacing = 8 };
        var (icon, color) = locked ? ("lock", Theme.TextDim) : RunText.Portrait(c, Data);
        head.Widgets.Add(W.IconTile(icon, 52, color));
        var names = new VerticalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        names.Widgets.Add(W.Label(c.Name, 22, locked ? Theme.TextDim : Theme.Ally, bold: true));
        if (locked)
        {
            names.Widgets.Add(W.Chip("잠김", Theme.Accent, 13));
        }
        head.Widgets.Add(names);
        card.Widgets.Add(head);

        if (c.Description is { Length: > 0 } description)
        {
            card.Widgets.Add(W.Label(description, 13, Theme.Text * dim, width: inner, wrap: true));
        }

        var s = c.Stats;
        var preview = _previews[c.Id];
        card.Widgets.Add(W.Label($"HP {preview.MaxHp}   MP {preview.MaxMp}", 15, Theme.Text * dim, bold: true));
        card.Widgets.Add(W.Label($"근력 {s.Str} · 민첩 {s.Dex} · 체력 {s.Vital} · 지능 {s.Intel} · 신속 {s.Speed}", 12, Theme.TextDim * dim, width: inner, wrap: true));
        card.Widgets.Add(W.Label($"{Data.Items[c.Weapon].Name} · {Data.Items[c.Armor].Name}", 13, Theme.Text * dim, width: inner, wrap: true));

        card.Widgets.Add(W.Label($"시작 카드 {c.StartingSkills.Count}장", 13, Theme.Cover * dim, bold: true));
        foreach (var id in c.StartingSkills)
        {
            var start = Data.Actions[id];
            var (actionIcon, actionColor) = GameIcons.Action(start, Data);
            var skill = new HorizontalStackPanel { Spacing = 6 };
            skill.Widgets.Add(W.Icon(actionIcon, 16, actionColor * dim));
            skill.Widgets.Add(W.Label(start.Name, 14, Theme.Text * dim, bold: true));
            skill.Widgets.Add(W.Label(start.MpCost > 0 ? $"MP {start.MpCost}" : start.HpCost > 0 ? $"HP {start.HpCost}" : "비용 없음", 12, Theme.TextDim * dim));
            card.Widgets.Add(skill);
        }
        var pool = c.Skills.Count(id => !_unlocks.Locked.Contains(id));
        var lockedCards = c.Skills.Count - pool;
        card.Widgets.Add(W.Label($"카드 {pool}장 · 패시브 {c.Passives.Count}개", 13, Theme.Text * dim));
        if (lockedCards > 0)
        {
            card.Widgets.Add(W.Label($"(잠긴 카드 {lockedCards}장)", 12, Theme.TextDim));
        }

        var unlock = Data.Unlocks.Values.FirstOrDefault(u => u.Classes.Contains(c.Id));
        var why = locked ? $"해금에서 연다 (명성 {unlock?.Cost})" : _picked.Count >= PartySize ? $"파티는 {PartySize}명까지다" : null;
        var add = Button(locked ? $"잠김 · 명성 {unlock?.Cost}" : "+ 파티에 넣기", () =>
        {
            _picked.Add((c.Id, c.Row));
            Refresh();
        }, why, width: inner, size: 15);
        return new Panel { Widgets = { card, Place(add, 12, CardHeight - 48) } };
    }

    private Widget Slot(int index)
    {
        var slot = Box(196, 112, spacing: 6, background: Theme.BarBack);
        if (index >= _picked.Count)
        {
            slot.Widgets.Add(W.Label($"{index + 1}. 빈 자리", 17, Theme.TextDim));
            return slot;
        }

        var (classId, chosenRow) = _picked[index];
        var c = Data.Classes[classId];
        var head = new HorizontalStackPanel { Spacing = 8 };
        var (icon, color) = RunText.Portrait(c, Data);
        head.Widgets.Add(W.IconTile(icon, 40, color));
        head.Widgets.Add(W.Label($"{index + 1}. {c.Name}", 18, Theme.Ally, bold: true));
        slot.Widgets.Add(head);

        // 줄 고르기와 빼기
        var buttons = new HorizontalStackPanel { Spacing = 4 };
        foreach (var row in new[] { Row.Front, Row.Back })
        {
            var chosen = row == chosenRow;
            var button = W.TextButton(RunText.Row(row), chosen ? RunText.RowColor(row) : Theme.Button, chosen ? RunText.RowColor(row) : Theme.ButtonHover, bold: chosen, size: 14);
            button.Width = 56;
            button.Padding = new Myra.Graphics2D.Thickness(0, 4);
            button.Click += (_, _) =>
            {
                _picked[index] = (classId, row);
                Refresh();
            };
            buttons.Widgets.Add(button);
        }

        buttons.Widgets.Add(new Panel { Width = 4 });
        buttons.Widgets.Add(Button("빼기", () =>
        {
            _picked.RemoveAt(index);
            Refresh();
        }, width: 56, size: 14));
        slot.Widgets.Add(buttons);
        return slot;
    }
}
