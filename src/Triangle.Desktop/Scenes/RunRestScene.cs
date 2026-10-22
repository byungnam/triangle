using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Triangle.Core.Runs;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>휴식: 모두의 봉인 풀기와 단련(패시브 카드 고르기) 중 하나를 고른다.</summary>
internal sealed class RunRestScene : RunScene
{
    private const int ChoiceWidth = 420;
    private const int ChoiceHeight = 340;

    public RunRestScene(Ui ui, RunSession session, Rectangle bounds, Action changed)
        : base(ui, session, bounds, changed)
    {
    }

    protected override string Title => "휴식 — 둘 중 하나";

    protected override string Help => "봉인은 쓰러졌다 일어날 때마다 하나씩 생기고, 봉인 수만큼 전술 줄이 맨 아래부터 꺼집니다.";

    protected override Widget Build()
    {
        var root = new Panel();
        var rules = Data.Run!.Rest;
        var x = Body.Left + (Body.Width - ChoiceWidth * 2 - 32) / 2;
        var top = Body.Top + 40;

        var unseal = Box(ChoiceWidth, ChoiceHeight, spacing: 10);
        unseal.Widgets.Add(W.Header("node.rest", "봉인 풀기", size: 22));
        var unseals = Run.RestUnseals(Data);
        unseal.Widgets.Add(W.Label($"모든 멤버의 봉인을 {unseals}개씩 풉니다.", 17, Theme.Text, width: ChoiceWidth - 24, wrap: true));
        if (!Run.Living.Any(m => m.Sealed > 0))
        {
            unseal.Widgets.Add(W.Label("지금 봉인된 멤버가 없습니다.", 15, Theme.Cover));
        }

        foreach (var member in Run.Living)
        {
            var (text, color) = RunText.Lines(member);
            var after = member.Sealed > 0 ? $" → 봉인 {Math.Max(0, member.Sealed - unseals)}" : "";
            unseal.Widgets.Add(W.Label($"{Run.Name(Data, member)}: {text}{after}", 15, member.Sealed > 0 ? color : Theme.TextDim));
        }

        root.Widgets.Add(Place(new Panel
        {
            Widgets = { unseal, Place(Button("봉인 풀기", () => Act(() => Run.RestUnseal(Data)), accent: true, width: ChoiceWidth - 24), 12, ChoiceHeight - 52) },
        }, x, top, ChoiceWidth, ChoiceHeight));

        var train = Box(ChoiceWidth, ChoiceHeight, spacing: 10);
        train.Widgets.Add(W.Header("mastery", "단련", size: 22));
        train.Widgets.Add(W.Label($"살아 있는 멤버의 클래스 패시브 {rules.TrainCards}장 중 하나를 고릅니다(건너뛸 수도 있습니다).", 17, Theme.Text, width: ChoiceWidth - 24, wrap: true));
        train.Widgets.Add(W.Label("봉인은 그대로 남습니다.", 15, Theme.TextDim));
        root.Widgets.Add(Place(new Panel
        {
            Widgets = { train, Place(Button("단련", () => Act(() => Run.RestTrain(Data)), accent: true, width: ChoiceWidth - 24), 12, ChoiceHeight - 52) },
        }, x + ChoiceWidth + 32, top, ChoiceWidth, ChoiceHeight));
        return root;
    }
}
