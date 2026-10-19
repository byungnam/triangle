using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI;
using Triangle.Core.Runs;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>
/// 해금과 기록: 왼쪽은 판을 넘어 남는 기록(판 수, 승리, 가장 멀리 간 막, 만난 적), 오른쪽은 해금 목록.
/// 명성을 내고 열면 다음 판부터 그 클래스·카드·아이템이 나온다.
/// </summary>
internal sealed class RunUnlockScene : RunScene
{
    private const int RecordWidth = 380;

    private readonly Action _back;

    public RunUnlockScene(Ui ui, RunSession session, Rectangle bounds, Action back)
        : base(ui, session, bounds, () => { })
    {
        _back = back;
    }

    private Profile Profile => Session.Profile;

    protected override string Title => "해금 · 기록";

    protected override string? Status => $"명성 {Profile.Renown}";

    protected override string Help => "연 해금은 다음 판부터 나옵니다. 판을 넘어 남는 것은 해금과 기록뿐입니다.     Esc  돌아가기";

    private Rectangle RecordArea => new(Body.Left, Body.Top, RecordWidth, Body.Height);

    private Rectangle UnlockArea => new(RecordArea.Right + Margin, Body.Top, Body.Right - RecordArea.Right - Margin, Body.Height);

    protected override void OnEscape() => _back();

    protected override void DrawBack(SpriteBatch batch)
    {
        Ui.Panel(batch, RecordArea);
        Ui.Panel(batch, UnlockArea);
    }

    protected override Widget Build()
    {
        var root = new Panel();
        root.Widgets.Add(Place(BuildRecords(), RecordArea.X + 16, RecordArea.Y + 12, RecordArea.Width - 32));
        var list = new VerticalStackPanel { Spacing = 8 };
        list.Widgets.Add(W.Header("lock", "해금", "명성을 내고 연다"));
        foreach (var unlock in Data.Unlocks.Values)
        {
            list.Widgets.Add(BuildUnlock(unlock, UnlockArea.Width - 48));
        }

        root.Widgets.Add(Place(new ScrollPane { Content = list, ShowHorizontalScrollBar = false }, UnlockArea.X + 16, UnlockArea.Y + 12, UnlockArea.Width - 24, UnlockArea.Height - 24));
        root.Widgets.Add(Place(Button("돌아가기", _back, width: 160), RecordArea.X + 16, RecordArea.Bottom - 16 - 40));
        return root;
    }

    private Widget BuildRecords()
    {
        var width = RecordWidth - 32;
        var rules = Data.Run!.Renown;
        var list = new VerticalStackPanel { Spacing = 8 };
        list.Widgets.Add(W.Header("tactics", "기록"));
        list.Widgets.Add(W.Label($"원정 {Profile.Runs}번 · 성공 {Profile.Victories}번", 18, Theme.Text, bold: true));
        list.Widgets.Add(W.Label(Profile.BestActs == 0 ? "아직 통과한 막이 없습니다" : $"가장 멀리: {Profile.BestActs}/{Data.Run.Acts.Count}막 통과", 16, Theme.Text));
        list.Widgets.Add(W.Label($"받은 명성 모두 {Profile.TotalRenown}", 16, Theme.Text));
        list.Widgets.Add(new Panel { Height = 6 });
        list.Widgets.Add(W.Header("node.fight", "만난 적", $"{Profile.Encounters.Count}종"));
        foreach (var (id, record) in Profile.Encounters.OrderByDescending(e => e.Value.Met).ThenBy(e => e.Key, StringComparer.Ordinal).Take(8))
        {
            list.Widgets.Add(W.Label($"{Data.Encounters[id].Name} — {record.Met}번 만나 {record.Wins}번 이김", 14, Theme.TextDim, width: width, wrap: true));
        }

        list.Widgets.Add(new Panel { Height = 6 });
        list.Widgets.Add(W.Header("node.boss", "명성 받는 법"));
        list.Widgets.Add(W.Label($"판이 끝나면 이긴 전투마다 보통 {rules.Fight}, 정예 {rules.Elite}, 보스 {rules.Boss}, 원정에 성공하면 {rules.Victory}을 더 받습니다.",
            14, Theme.Text, width: width, wrap: true));
        return list;
    }

    private Widget BuildUnlock(UnlockDefinition unlock, int width)
    {
        var opened = Profile.Unlocked.Contains(unlock.Id);
        var row = new HorizontalStackPanel { Spacing = 12 };
        var text = new VerticalStackPanel { Spacing = 2, Width = width - 180 };
        var title = new HorizontalStackPanel { Spacing = 8 };
        title.Widgets.Add(W.Label(unlock.Name, 18, opened ? Theme.Gold : Theme.Text, bold: true));
        if (opened)
        {
            title.Widgets.Add(W.Chip("열림", Theme.Selected, 12));
        }

        text.Widgets.Add(title);
        if (unlock.Description is { Length: > 0 } description)
        {
            text.Widgets.Add(W.Label(description, 14, Theme.Text, width: width - 180, wrap: true));
        }

        if (Opens(unlock) is { Length: > 0 } opens)
        {
            text.Widgets.Add(W.Label(opens, 13, Theme.TextDim, width: width - 180, wrap: true));
        }

        row.Widgets.Add(text);
        var why = Profile.WhyCannotUnlock(Data, unlock.Id);
        var button = opened
            ? Button("열림", () => { }, "이미 열었다", width: 168)
            : Button($"열기 · 명성 {unlock.Cost}", () =>
            {
                Session.Notice = null;
                Session.Unlock(unlock.Id);
                Refresh();
            }, why, accent: why is null, width: 168);
        button.VerticalAlignment = VerticalAlignment.Center;
        row.Widgets.Add(button);

        var box = Box(width + 24, spacing: 0, background: opened ? Theme.Selected * 0.3f : Theme.BarBack);
        box.Widgets.Add(row);
        return box;
    }

    /// <summary>"클래스: 전사 · 카드: 저격, 화살비 · 시작 골드 +50".</summary>
    private string Opens(UnlockDefinition unlock)
    {
        var parts = new List<string>();
        if (unlock.Classes.Count > 0)
        {
            parts.Add("클래스: " + string.Join(", ", unlock.Classes.Select(id => Data.Classes[id].Name)));
        }

        if (unlock.Cards.Count > 0)
        {
            parts.Add("카드: " + string.Join(", ", unlock.Cards.Select(id => Data.Actions[id].Name)));
        }

        if (unlock.Items.Count > 0)
        {
            parts.Add("아이템: " + string.Join(", ", unlock.Items.Select(id => Data.Items[id].Name)));
        }

        if (unlock.StartGold > 0)
        {
            parts.Add($"시작 골드 +{unlock.StartGold}");
        }

        return string.Join(" · ", parts);
    }
}
