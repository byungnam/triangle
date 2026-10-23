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
/// </summary>
internal sealed class RunPartyScene : RunScene
{
    private const int CardHeight = 392;
    private const int Gap = 16;

    private readonly List<(string ClassId, Row Row)> _picked;
    private readonly Action<IReadOnlyList<string>, IReadOnlyList<Row>> _start;
    private readonly Action _openUnlocks;
    private readonly RunUnlocks _unlocks;

    /// <summary>클래스마다 시작 상태의 전투 입력 (HP·MP 표시용).</summary>
    private readonly Dictionary<string, UnitPreview> _previews;

    /// <param name="start">고른 클래스로 판을 시작한다.</param>
    /// <param name="openUnlocks">해금·기록 화면을 연다.</param>
    public RunPartyScene(Ui ui, RunSession session, Rectangle bounds, Action<IReadOnlyList<string>, IReadOnlyList<Row>> start, Action openUnlocks)
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
        var start = Button("출발  ▶", () => _start(classIds, _picked.Select(p => p.Row).ToList()), Run.WhyCannotStart(Data, classIds, _unlocks), accent: true, width: 220, size: 22);
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
        return root;
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

        var start = Data.Actions[c.StartingSkill];
        var (actionIcon, actionColor) = GameIcons.Action(start, Data);
        var skill = new HorizontalStackPanel { Spacing = 6 };
        skill.Widgets.Add(W.Icon(actionIcon, 18, actionColor * dim));
        skill.Widgets.Add(W.Label($"시작: {start.Name}", 14, Theme.Cover * dim, bold: true));
        card.Widgets.Add(skill);
        card.Widgets.Add(W.Label(ActionText.Summary(start, Data), 12, Theme.TextDim * dim, width: inner, wrap: true));
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
