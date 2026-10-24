using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;
using Triangle.Core.Runs;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 갈림길 지도: 왼쪽은 파티(줄 상한과 봉인), 가운데는 이 막의 지도(아래가 첫 층, 맨 위가 보스), 오른쪽은 막 보스와 지점 설명.
/// 갈 수 있는 지점은 금색 테두리이고 누르면 들어간다. 전술과 장비는 "편성"에서 고친다.
/// 지도는 한 화면보다 길어서(막마다 15층) 마우스 휠로 위아래로 움직이고, 처음에는 지금 위치를 가운데에 둔다.
/// </summary>
internal sealed class RunMapScene : RunScene
{
    private const int SideWidth = 300;
    private const int BarHeight = 56;
    private const int NodeSize = 44;
    private const int BossSize = 64;

    /// <summary>층 사이 간격 (설계 좌표).</summary>
    private const int FloorStep = 64;

    /// <summary>첫 층 아래 여백.</summary>
    private const int BottomPad = 40;

    private static readonly RasterizerState Clip = new() { ScissorTestEnable = true, CullMode = CullMode.None };

    private readonly Action<int> _enter;
    private readonly Action _openEditor;
    private readonly Action _abandon;
    private RunNode? _hover;
    private bool _confirmAbandon;

    /// <summary>지도를 위로 민 양 (0이면 첫 층이 아래에 붙는다).</summary>
    private float _scroll;

    /// <param name="enter">지점에 들어간다 (전투면 전투 기록을 보여준다).</param>
    public RunMapScene(Ui ui, RunSession session, Rectangle bounds, Action<int> enter, Action openEditor, Action abandon)
        : base(ui, session, bounds, () => { })
    {
        _enter = enter;
        _openEditor = openEditor;
        _abandon = abandon;
        var floor = session.Run!.Position is { } at ? session.Run.Map.Node(at).Floor : 0;
        ScrollTo(floor);
    }

    protected override string Title => $"원정 — {RunText.Act(Run, Data)}";

    protected override string? Status => $"골드 {Run.Gold}";

    protected override string Help => "금색 테두리 지점을 눌러 나아갑니다. 보통 전투의 적은 들어가야 압니다.     휠  지도 위아래";

    private Rectangle PartyArea => new(Body.Left, Body.Top, SideWidth, Body.Height);

    private Rectangle InfoArea => new(Body.Right - SideWidth, Body.Top, SideWidth, Body.Height);

    private Rectangle MapArea => new(PartyArea.Right + Margin, Body.Top, InfoArea.Left - Margin - PartyArea.Right - Margin, Body.Height - BarHeight);

    /// <summary>층의 세로 위치 (설계 좌표, 스크롤 반영). 아래가 첫 층이다.</summary>
    private float FloorY(int floor) => MapArea.Bottom - BottomPad - floor * FloorStep + _scroll;

    /// <summary>지도를 끝까지 민 양: 보스가 지도 칸 위에 붙을 때.</summary>
    private float MaxScroll => Math.Max(0, Run.Map.Floors * FloorStep + BottomPad + BossSize - MapArea.Height + 12);

    /// <summary>그 층이 지도 칸 가운데보다 한 층 아래에 오게 민다 (앞으로 갈 층들이 보이도록).</summary>
    private void ScrollTo(int floor) =>
        _scroll = Math.Clamp(MapArea.Center.Y + FloorStep - (MapArea.Bottom - BottomPad - floor * FloorStep), 0, MaxScroll);

    /// <summary>지점의 가운데 (설계 좌표). 아래가 첫 층이고 보스는 맨 위 가운데다.</summary>
    private Vector2 Center(RunNode node)
    {
        var area = MapArea;
        var y = FloorY(node.Floor);
        if (node.Kind == RunNodeKind.Boss)
        {
            return new Vector2(area.Center.X, y - 8);
        }

        // 이어 하는 판의 지도는 폭을 바꾸기 전에 만들어졌을 수 있다 (넓은 지도도 칸 안에 그린다).
        var width = Math.Max(Data.Run!.Map.Width, Run.Map.Nodes.Max(n => n.Lane) + 1);
        return new Vector2(area.Left + 14 + (node.Lane + 0.5f) * (area.Width - 14) / width, y);
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
        var inside = MapArea.Contains(mouse.ToPoint());
        if (inside && input.WheelDelta != 0)
        {
            _scroll = Math.Clamp(_scroll + input.WheelDelta / 120f * FloorStep * 1.5f, 0, MaxScroll);
        }

        var hover = inside ? Run.Map.Nodes.FirstOrDefault(n => Vector2.Distance(mouse, Center(n)) <= Size(n) / 2f + 4) : null;
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

        // 지도는 지도 칸 안에만 그린다 (넘치는 층은 잘라낸다).
        batch.End();
        var device = batch.GraphicsDevice;
        var screen = Ui.Screen;
        var clip = new Rectangle(
            screen.Offset.X + (int)(area.X * screen.Scale), screen.Offset.Y + (int)(area.Y * screen.Scale),
            (int)(area.Width * screen.Scale), (int)(area.Height * screen.Scale));
        device.ScissorRectangle = Rectangle.Intersect(clip, device.Viewport.Bounds);
        batch.Begin(transformMatrix: screen.Transform, rasterizerState: Clip);
        DrawMap(batch);
        batch.End();
        batch.Begin(transformMatrix: screen.Transform);

        // 오른쪽 가장자리의 스크롤 막대: 지금 보이는 부분.
        if (MaxScroll > 0)
        {
            var total = area.Height + MaxScroll;
            var length = Math.Max(24, (int)(area.Height * area.Height / total));
            var top = area.Y + (int)((MaxScroll - _scroll) / total * area.Height);
            Ui.Fill(batch, new Rectangle(area.Right - 8, area.Y + 4, 4, area.Height - 8), Theme.BarBack);
            Ui.Fill(batch, new Rectangle(area.Right - 8, Math.Clamp(top, area.Y + 4, area.Bottom - 4 - length), 4, length), Theme.PanelBorder);
        }
    }

    private void DrawMap(SpriteBatch batch)
    {
        var area = MapArea;
        var map = Run.Map;
        var font = Ui.Font(12);
        for (var floor = 0; floor < map.Floors; floor++)
        {
            var y = FloorY(floor);
            if (y > area.Top - 20 && y < area.Bottom + 20)
            {
                Ui.Text(batch, font, $"{floor + 1}", new Vector2(area.Left + 6, y - 7), Theme.TextDim * 0.7f);
            }
        }

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
            name.Widgets.Add(W.Chip(RunText.Row(member.Row), member.Dead ? Theme.BarBack : RunText.RowColor(member.Row), size: 12));
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
        if (!Run.Chart.IsBase)
        {
            // 원정 지도: 판 내내 걸리는 수식어와 보상.
            list.Widgets.Add(W.Header("tactics", Run.Chart.Name(Data), $"위험도 {Run.Chart.Risk(Data)}"));
            list.Widgets.Add(W.Label(string.Join(" · ", Run.Chart.Hardship(Data).Parts(Data)), 13, Theme.Enemy, width: width, wrap: true));
            list.Widgets.Add(W.Label(string.Join(" · ", Run.Chart.Bounty(Data).Parts(Data)), 13, Theme.Heal, width: width, wrap: true));
            list.Widgets.Add(new Panel { Height = 4 });
        }

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
