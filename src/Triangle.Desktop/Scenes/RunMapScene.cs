using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;
using Triangle.Core.Runs;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 갈림길 지도: 왼쪽은 파티(줄 상한과 봉인), 가운데는 이 막의 지도(아래가 첫 층, 맨 위가 보스), 오른쪽은 막 보스와 지점 설명.
/// 갈 수 있는 지점은 금색 테두리이고 누르면 들어간다. 전술과 장비는 "편성"에서 고친다.
/// </summary>
internal sealed class RunMapScene : RunScene
{
    private const int SideWidth = 300;
    private const int BarHeight = 56;
    private const int NodeSize = 44;
    private const int BossSize = 64;

    private readonly Action<int> _enter;
    private readonly Action _openEditor;
    private readonly Action _abandon;
    private RunNode? _hover;
    private bool _confirmAbandon;

    /// <param name="enter">지점에 들어간다 (전투면 전투 기록을 보여준다).</param>
    public RunMapScene(Ui ui, RunSession session, Rectangle bounds, Action<int> enter, Action openEditor, Action abandon)
        : base(ui, session, bounds, () => { })
    {
        _enter = enter;
        _openEditor = openEditor;
        _abandon = abandon;
    }

    protected override string Title => $"원정 — {RunText.Act(Run, Data)}";

    protected override string? Status => $"골드 {Run.Gold}";

    protected override string Help => "금색 테두리 지점을 눌러 나아갑니다. 보통 전투의 적은 들어가야 압니다.     편성에서 전술과 장비를 고칩니다.";

    private Rectangle PartyArea => new(Body.Left, Body.Top, SideWidth, Body.Height);

    private Rectangle InfoArea => new(Body.Right - SideWidth, Body.Top, SideWidth, Body.Height);

    private Rectangle MapArea => new(PartyArea.Right + Margin, Body.Top, InfoArea.Left - Margin - PartyArea.Right - Margin, Body.Height - BarHeight);

    /// <summary>지점의 가운데 (설계 좌표). 아래가 첫 층이고 보스는 맨 위 가운데다.</summary>
    private Vector2 Center(RunNode node)
    {
        var map = Run.Map;
        var area = MapArea;
        var step = (area.Height - 70f) / map.Floors;
        var y = area.Bottom - 40 - node.Floor * step;
        if (node.Kind == RunNodeKind.Boss)
        {
            return new Vector2(area.Center.X, y + 6);
        }

        var width = Data.Run!.Map.Width;
        return new Vector2(area.Left + (node.Lane + 0.5f) * area.Width / width, y);
    }

    private static int Size(RunNode node) => node.Kind == RunNodeKind.Boss ? BossSize : NodeSize;

    protected override void OnEscape()
    {
        if (_confirmAbandon)
        {
            _confirmAbandon = false;
            Refresh();
        }
    }

    protected override void OnUpdate(Input input)
    {
        if (_confirmAbandon)
        {
            return;
        }

        var mouse = Ui.Screen.ToDesign(input.MousePosition);
        var hover = Run.Map.Nodes.FirstOrDefault(n => Vector2.Distance(mouse, Center(n)) <= Size(n) / 2f + 4);
        if (hover != _hover)
        {
            _hover = hover;
        }

        if (input.Clicked && hover is not null && Run.Choices.Any(c => c.Id == hover.Id))
        {
            _enter(hover.Id);
        }
    }

    protected override void DrawBack(SpriteBatch batch)
    {
        Ui.Panel(batch, PartyArea);
        Ui.Panel(batch, InfoArea);
        var area = MapArea;
        Ui.Panel(batch, new Rectangle(area.X, area.Y, area.Width, area.Height + BarHeight));

        var map = Run.Map;
        var choices = Run.Choices.Select(n => n.Id).ToHashSet();
        var path = Run.Path;
        var walked = path.Zip(path.Skip(1)).ToHashSet();
        foreach (var node in map.Nodes)
        {
            foreach (var next in node.Next.Select(map.Node))
            {
                var color = walked.Contains((node.Id, next.Id)) ? Theme.Gold
                    : node.Id == Run.Position && choices.Contains(next.Id) ? Theme.Text * 0.8f
                    : Theme.PanelBorder * 0.8f;
                Ui.Line(batch, Center(node), Center(next), color, walked.Contains((node.Id, next.Id)) ? 3f : 2f);
            }
        }

        foreach (var node in map.Nodes)
        {
            DrawNode(batch, node, choices.Contains(node.Id), path.Contains(node.Id));
        }
    }

    private void DrawNode(SpriteBatch batch, RunNode node, bool reachable, bool visited)
    {
        var (icon, color, _) = RunText.Node(node.Kind);
        var size = Size(node);
        var center = Center(node);
        var rect = new Rectangle((int)(center.X - size / 2f), (int)(center.Y - size / 2f), size, size);
        var hovered = _hover == node && reachable;
        var current = node.Id == Run.Position;
        var border = current || reachable ? Theme.Gold : visited ? Theme.Cover * 0.7f : Theme.PanelBorder;
        Ui.Fill(batch, rect, border);
        rect.Inflate(reachable || current ? -3 : -2, reachable || current ? -3 : -2);
        Ui.Fill(batch, rect, hovered ? Theme.ButtonHover : visited ? Theme.Selected : Theme.BarBack);
        rect.Inflate(-6, -6);
        var dim = reachable || current || visited || node.Kind == RunNodeKind.Boss ? 1f : 0.45f;
        Ui.DrawIcon(batch, icon, rect, color * dim);
    }

    protected override Widget Build()
    {
        var root = new Panel();
        root.Widgets.Add(Place(BuildParty(), PartyArea.X + 14, PartyArea.Y + 12, PartyArea.Width - 28));
        root.Widgets.Add(Place(BuildInfo(), InfoArea.X + 14, InfoArea.Y + 12, InfoArea.Width - 28));

        var bar = new HorizontalStackPanel { Spacing = 12 };
        bar.Widgets.Add(Button("편성 · 전술과 장비", _openEditor, width: 220));
        bar.Widgets.Add(Button("원정 포기", () =>
        {
            _confirmAbandon = true;
            Refresh();
        }, width: 120));
        root.Widgets.Add(Place(bar, MapArea.X + 16, MapArea.Bottom + 8));
        return root;
    }

    protected override Widget? BuildPopup()
    {
        if (!_confirmAbandon)
        {
            return null;
        }

        var box = Box(460, spacing: 12, border: Theme.Gold);
        box.Widgets.Add(W.Label("원정을 포기할까요?", 22, Theme.Text, bold: true));
        box.Widgets.Add(W.Label("이 판은 패배로 끝납니다. 되돌릴 수 없습니다.", 16, Theme.TextDim, width: 430, wrap: true));
        var row = new HorizontalStackPanel { Spacing = 12 };
        row.Widgets.Add(Button("포기한다", _abandon, accent: true, width: 140));
        row.Widgets.Add(Button("계속한다 (Esc)", () =>
        {
            _confirmAbandon = false;
            Refresh();
        }, width: 160));
        box.Widgets.Add(row);
        return Overlay(box);
    }

    private Widget BuildParty()
    {
        var list = new VerticalStackPanel { Spacing = 10 };
        list.Widgets.Add(W.Header("figure", "파티", $"사본 {Run.Copies.Values.Sum()}장 · 가방 {Run.Bag.Count}개"));
        foreach (var member in Run.Members)
        {
            var c = Data.Classes[member.ClassId];
            var row = new HorizontalStackPanel { Spacing = 10 };
            var (icon, color) = member.Dead ? ("dead", Theme.Death) : RunText.Portrait(c, Data);
            row.Widgets.Add(W.IconTile(icon, 48, color));
            var text = new VerticalStackPanel { Spacing = 3 };
            var name = new HorizontalStackPanel { Spacing = 6 };
            name.Widgets.Add(W.Label(Run.Name(Data, member), 18, member.Dead ? Theme.Death : Theme.Ally, bold: true));
            name.Widgets.Add(W.Chip(RunText.Row(c), member.Dead ? Theme.BarBack : RunText.RowColor(c), size: 12));
            text.Widgets.Add(name);
            var (lines, linesColor) = RunText.Lines(member);
            text.Widgets.Add(W.Label(lines, 15, linesColor));
            if (!member.Dead)
            {
                var on = Math.Min(member.Lines, member.Tactics.Count);
                var off = member.Tactics.Count - on;
                text.Widgets.Add(W.Label(off > 0 ? $"전술 {on}줄 켜짐 · {off}줄 꺼짐" : $"전술 {on}줄", 14, off > 0 ? Theme.Cover : Theme.TextDim));
            }

            row.Widgets.Add(text);
            list.Widgets.Add(row);
        }

        return list;
    }

    private Widget BuildInfo()
    {
        var width = SideWidth - 28;
        var list = new VerticalStackPanel { Spacing = 8 };
        list.Widgets.Add(W.Header("node.boss", "막 보스", "출발할 때 정해짐"));
        var acts = Data.Run!.Acts;
        for (var i = 0; i < acts.Count; i++)
        {
            var zone = Data.Zones[acts[i].ZoneId];
            var boss = Data.Encounters[Run.Bosses[i]];
            var color = i < Run.Act ? Theme.TextDim : i == Run.Act ? Theme.Gold : Theme.Text;
            list.Widgets.Add(W.Label($"{i + 1}막 {zone.Name}", 14, Theme.TextDim));
            list.Widgets.Add(W.Label(i < Run.Act ? $"{boss.Name} (물리침)" : boss.Name, 17, color, bold: i == Run.Act, width: width, wrap: true));
        }

        list.Widgets.Add(new Panel { Height = 8 });
        if (_hover is { } node)
        {
            var (icon, color, name) = RunText.Node(node.Kind);
            list.Widgets.Add(W.Header(icon, name, node.Kind == RunNodeKind.Boss ? null : $"{node.Floor + 1}층"));
            if (node.Kind == RunNodeKind.Boss)
            {
                list.Widgets.Add(W.Label(Data.Encounters[Run.Bosses[Run.Act]].Name, 17, color, bold: true, width: width, wrap: true));
            }

            list.Widgets.Add(W.Label(RunText.NodeHint(node.Kind, Data.Run!), 15, Theme.Text, width: width, wrap: true));
            if (!Run.Choices.Contains(node))
            {
                list.Widgets.Add(W.Label("지금은 갈 수 없습니다.", 14, Theme.TextDim));
            }

            return list;
        }

        list.Widgets.Add(W.Header("tactics", "지점"));
        foreach (var kind in Enum.GetValues<RunNodeKind>())
        {
            var (icon, color, name) = RunText.Node(kind);
            list.Widgets.Add(W.IconText(icon, color, name, 15, Theme.Text));
        }

        list.Widgets.Add(W.Label("지점에 마우스를 올리면 설명이 나옵니다.", 14, Theme.TextDim, width: width, wrap: true));
        return list;
    }

    public override void Update(GameTime gameTime, Input input)
    {
        var before = _hover;
        base.Update(gameTime, input);
        if (before != _hover)
        {
            Refresh();
        }
    }
}
