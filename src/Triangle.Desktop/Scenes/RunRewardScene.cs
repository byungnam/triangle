using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;
using Triangle.Core.Runs;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 보상: 후보 카드 중 한 장을 고르거나 건너뛴다 (전투, 휴식의 단련, 이벤트).
/// 액티브는 고르면 바로 파티의 사본이 되고, 패시브와 줄 상한은 받을 멤버를, 강화는 걸 사본을 한 번 더 고른다.
/// 전투 뒤라면 위에 결과(골드, 봉인, 죽음)를 보여준다.
/// </summary>
internal sealed class RunRewardScene : RunScene
{
    private const int CardHeight = 380;
    private const int Gap = 14;

    private readonly RunBattle? _battle;
    private int? _chosen;

    /// <param name="battle">이 보상을 준 전투 (전투 뒤가 아니면 null).</param>
    public RunRewardScene(Ui ui, RunSession session, Rectangle bounds, Action changed, RunBattle? battle)
        : base(ui, session, bounds, changed)
    {
        _battle = battle;
    }

    protected override string Title => _battle is null ? "보상 — 카드 한 장 고르기" : "전투 보상 — 카드 한 장 고르기";

    protected override string Help => "액티브 사본은 같은 클래스 누구나 전술 한 줄에 놓습니다. 패시브와 줄 상한은 받을 멤버를, 강화는 걸 사본을 고릅니다.     Esc  고르기 취소";

    private int Top => Body.Top + (_battle is null ? 0 : 96);

    private int CardWidth => Math.Min(240, (Body.Width - Gap * (Run.Offer.Count - 1)) / Math.Max(1, Run.Offer.Count));

    protected override void OnEscape()
    {
        _chosen = null;
        Refresh();
    }

    protected override void DrawBack(SpriteBatch batch)
    {
        if (_battle is null)
        {
            return;
        }

        var area = new Rectangle(Body.Left, Body.Top, Body.Width, 84);
        Ui.Panel(batch, area);
        var y = area.Top + 12;
        var lines = RunText.Battle(_battle, Run, Data);
        Ui.Text(batch, Ui.BoldFont(20), lines[0].Text, new Vector2(area.Left + 16, y), lines[0].Color);
        var x = area.Left + 16f;
        y += 34;
        foreach (var line in lines.Skip(1))
        {
            var font = Ui.Font(16);
            Ui.Text(batch, font, line.Text, new Vector2(x, y), line.Color);
            x += font.MeasureString(line.Text).X + 28;
        }
    }

    protected override Widget Build()
    {
        var root = new Panel();
        var total = CardWidth * Run.Offer.Count + Gap * (Run.Offer.Count - 1);
        var x = Body.Left + (Body.Width - total) / 2;
        for (var i = 0; i < Run.Offer.Count; i++)
        {
            root.Widgets.Add(Place(Card(i), x, Top, CardWidth, CardHeight));
            x += CardWidth + Gap;
        }

        if (_chosen is { } chosen)
        {
            var top = Top + CardHeight + 16;
            root.Widgets.Add(Run.Offer[chosen].Kind == RunCardKind.Upgrade
                ? Place(new ScrollPane { Content = CopyPicker(chosen), ShowHorizontalScrollBar = false }, Body.Left, top, Body.Width - 176, Body.Bottom - top)
                : Place(MemberPicker(chosen), Body.Left, top, Body.Width));
        }

        root.Widgets.Add(Place(Button("건너뛰기", () => Act(Run.Skip), width: 160), Body.Right - 160, Body.Bottom - 44));
        return root;
    }

    private Widget Card(int index)
    {
        var card = Run.Offer[index];
        var chosen = _chosen == index;
        var width = CardWidth;
        var inner = width - 24;
        var box = Box(width, CardHeight, border: chosen ? Theme.Gold : null, background: chosen ? Theme.Selected * 0.5f : null);

        var head = new HorizontalStackPanel { Spacing = 8 };
        head.Widgets.Add(W.Chip(RunText.CardKind(card.Kind), RunText.CardColor(card.Kind) * 0.55f));
        if (card.ClassId is { } classId)
        {
            head.Widgets.Add(W.Label(Data.Classes[classId].Name, 14, Theme.TextDim));
        }

        box.Widgets.Add(head);
        var title = new HorizontalStackPanel { Spacing = 8 };
        var (icon, color) = RunText.CardIcon(card, Data);
        title.Widgets.Add(W.IconTile(icon, 44, color));
        title.Widgets.Add(W.Label(RunText.CardName(card, Data), 19, Theme.Text, bold: true, width: inner - 52, wrap: true));
        box.Widgets.Add(title);
        AddLines(box, RunText.Card(card, Run, Data).Select(l => l with { Size = Math.Min(l.Size, 14) }), inner);

        var pick = Button(chosen ? (card.Kind == RunCardKind.Upgrade ? "걸 사본을 고르세요 ▼" : "받을 멤버를 고르세요 ▼") : "고르기", () => Choose(index), width: inner, accent: !chosen);
        return new Panel { Widgets = { box, Place(pick, 12, CardHeight - 50) } };
    }

    private void Choose(int index)
    {
        if (Run.Offer[index].Kind == RunCardKind.Active)
        {
            Act(() => Run.Take(Data, index));
            return;
        }

        _chosen = index;
        Refresh();
    }

    private Widget MemberPicker(int index)
    {
        var card = Run.Offer[index];
        var row = new HorizontalStackPanel { Spacing = 10 };
        row.Widgets.Add(W.Label($"{Korean.EulReul(RunText.CardName(card, Data))} 받을 멤버:", 17, Theme.Gold, bold: true));
        foreach (var member in Run.Living)
        {
            var why = Run.WhyCannotTake(Data, index, member);
            var label = card.Kind == RunCardKind.Passive
                ? $"{Run.Name(Data, member)} (Lv{member.Level(card.Id!)})"
                : $"{Run.Name(Data, member)} ({RunText.Lines(member).Text})";
            row.Widgets.Add(Button(label, () => Act(() => Run.Take(Data, index, member)), why, accent: why is null));
        }

        return row;
    }

    /// <summary>강화 카드를 걸 사본: 그 강화가 맞는 사본마다(줄에 놓인 것 먼저) 단추. 마우스를 올리면 바뀌는 수치가 보인다.</summary>
    private Widget CopyPicker(int index)
    {
        const int PerRow = 4;
        var card = Run.Offer[index];
        var kind = Enum.Parse<RunUpgradeKind>(card.Id!);
        var panel = new VerticalStackPanel { Spacing = 8 };
        panel.Widgets.Add(W.Label($"{Korean.EulReul(RunText.CardName(card, Data))} 걸 사본:", 17, Theme.Gold, bold: true));
        var copies = Run.UpgradableCopies(Data).Where(c => RunUpgrades.Fits(RunUpgrades.Action(Data, c), kind))
            .OrderBy(c => Run.UpgradeHolder(c) is null ? 1 : 0).ToList();
        if (copies.Count == 0)
        {
            panel.Widgets.Add(W.Label("이 강화를 걸 수 있는 사본이 없습니다. 건너뛰거나 다른 카드를 고르세요.", 15, Theme.TextDim));
        }

        var width = (Body.Width - 176 - 24 - 10 * (PerRow - 1)) / PerRow;
        foreach (var chunk in copies.Chunk(PerRow))
        {
            var row = new HorizontalStackPanel { Spacing = 10 };
            foreach (var copy in chunk)
            {
                var why = Run.WhyCannotTake(Data, index, null, copy);
                var button = Button(RunText.Copy(Run, Data, copy), () => Act(() => Run.Take(Data, index, copy: copy)), why, accent: why is null, width: width, size: 15);
                button.Tooltip = why ?? RunUpgrades.Describe(RunUpgrades.Action(Data, copy), kind);
                row.Widgets.Add(button);
            }

            panel.Widgets.Add(row);
        }

        return panel;
    }
}
