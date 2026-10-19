using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;
using Triangle.Core.Runs;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 판 안의 상점: 왼쪽은 아이템과 스킬 사본, 오른쪽은 동료 합류와 봉인 풀기. 산 아이템은 가방으로 가고 편성에서 낀다.
/// </summary>
internal sealed class RunShopScene : RunScene
{
    private const int Gap = 16;

    public RunShopScene(Ui ui, RunSession session, Rectangle bounds, Action changed)
        : base(ui, session, bounds, changed)
    {
    }

    protected override string Title => "상점";

    protected override string Help => "산 아이템은 가방에 들어갑니다. 지도에서 편성을 열어 낍니다.     Esc  나가기";

    private Rectangle GoodsArea => new(Body.Left, Body.Top, Body.Width * 3 / 5 - Gap / 2, Body.Height);

    private Rectangle ServiceArea => new(GoodsArea.Right + Gap, Body.Top, Body.Right - GoodsArea.Right - Gap, Body.Height);

    protected override void OnEscape() => Act(Run.LeaveShop);

    protected override void DrawBack(SpriteBatch batch)
    {
        Ui.Panel(batch, GoodsArea);
        Ui.Panel(batch, ServiceArea);
    }

    protected override Widget Build()
    {
        var root = new Panel();
        var width = GoodsArea.Width - 32;
        var goods = new VerticalStackPanel { Spacing = 8 };
        goods.Widgets.Add(W.Header("equipment", "아이템", $"이 막의 등급 {Data.Run!.Acts[Run.Act].ShopTier}"));
        AddStock(goods, RunStockKind.Item, width);
        goods.Widgets.Add(new Panel { Height = 4 });
        goods.Widgets.Add(W.Header("card", "스킬 사본", "파티 클래스의 카드"));
        AddStock(goods, RunStockKind.Skill, width);
        root.Widgets.Add(Place(new ScrollPane { Content = goods, ShowHorizontalScrollBar = false }, GoodsArea.X + 16, GoodsArea.Y + 12, GoodsArea.Width - 24, GoodsArea.Height - 24));

        var services = new VerticalStackPanel { Spacing = 8 };
        var serviceWidth = ServiceArea.Width - 32;
        services.Widgets.Add(W.Header("figure", "동료 합류"));
        AddStock(services, RunStockKind.Recruit, serviceWidth);
        services.Widgets.Add(new Panel { Height = 4 });
        var price = Data.Run!.Shop.UnsealPrice;
        services.Widgets.Add(W.Header("lock", "봉인 풀기", $"하나에 {price} 골드"));
        var sealedMembers = Run.Living.Where(m => m.Sealed > 0).ToList();
        if (sealedMembers.Count == 0)
        {
            services.Widgets.Add(W.Label("봉인된 멤버가 없습니다.", 15, Theme.TextDim));
        }

        foreach (var member in sealedMembers)
        {
            var row = new HorizontalStackPanel { Spacing = 10 };
            row.Widgets.Add(W.Label($"{Run.Name(Data, member)} · {RunText.Lines(member).Text}", 16, Theme.Text, width: serviceWidth - 150));
            row.Widgets.Add(Button("봉인 1 풀기", () => Act(() => Run.Unseal(Data, member)), Run.WhyCannotUnseal(Data, member), width: 140, size: 15));
            services.Widgets.Add(row);
        }

        root.Widgets.Add(Place(services, ServiceArea.X + 16, ServiceArea.Y + 12, serviceWidth));
        root.Widgets.Add(Place(Button("나가기", () => Act(Run.LeaveShop), accent: true, width: 160), ServiceArea.Right - 16 - 160, ServiceArea.Bottom - 16 - 40));
        return root;
    }

    private void AddStock(VerticalStackPanel list, RunStockKind kind, int width)
    {
        for (var i = 0; i < Run.Stock.Count; i++)
        {
            if (Run.Stock[i].Kind == kind)
            {
                list.Widgets.Add(StockRow(i, width));
            }
        }
    }

    private Widget StockRow(int index, int width)
    {
        var stock = Run.Stock[index];
        var (icon, color, lines) = stock.Kind switch
        {
            RunStockKind.Item => Describe(GameIcons.Item(Data.Items[stock.Id]), Brief(RunText.Item(Data.Items[stock.Id], Data))),
            RunStockKind.Skill => Describe(GameIcons.Action(Data.Actions[stock.Id], Data),
                [.. RunText.Action(Data.Actions[stock.Id], Data).Take(2), new TextLine(ActionText.Summary(Data.Actions[stock.Id], Data), Theme.Text, 14)]),
            _ => Describe(RunText.Portrait(Data.Classes[stock.Id], Data),
                [new TextLine(Data.Classes[stock.Id].Name, Theme.Ally, 18, Bold: true), new TextLine($"시작 카드 {Data.Actions[Data.Classes[stock.Id].StartingSkill].Name}, 기본 장비 (줄은 편성에서 고른다)", Theme.Text, 14)]),
        };

        var row = new HorizontalStackPanel { Spacing = 10 };
        row.Widgets.Add(W.IconTile(icon, 48, stock.Sold ? Theme.TextDim : color));
        var text = new VerticalStackPanel { Spacing = 2, Width = width - 48 - 140 - 20 };
        foreach (var line in lines)
        {
            text.Widgets.Add(W.Label(line.Text, Math.Min(line.Size, 17), stock.Sold ? Theme.TextDim : line.Color, line.Bold, width - 48 - 140 - 20, wrap: true));
        }

        row.Widgets.Add(text);
        var buy = stock.Sold
            ? Button("팔림", () => { }, "이미 팔렸다", width: 140)
            : Button($"{stock.Price} 골드", () => Act(() => Run.Buy(Data, index)), Run.WhyCannotBuy(Data, index), accent: Run.WhyCannotBuy(Data, index) is null, width: 140);
        buy.VerticalAlignment = VerticalAlignment.Center;
        row.Widgets.Add(buy);
        return row;
    }

    /// <summary>아이템 줄을 줄인다: 이름, 종류, 나머지(보너스, 효과, 바뀌는 기본 공격)를 한 줄로. 가격은 버튼에 있으니 뺀다.</summary>
    private static IReadOnlyList<TextLine> Brief(IReadOnlyList<TextLine> lines)
    {
        var rest = lines.Skip(2).Where(l => !l.Text.StartsWith("가격")).ToList();
        return rest.Count == 0 ? lines.Take(2).ToList() : [.. lines.Take(2), new TextLine(string.Join(" · ", rest.Select(l => l.Text)), rest[0].Color, 14)];
    }

    private static (string Icon, Color Color, IReadOnlyList<TextLine> Lines) Describe((string Icon, Color Color) icon, IReadOnlyList<TextLine> lines) =>
        (icon.Icon, icon.Color, lines);
}
