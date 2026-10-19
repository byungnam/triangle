using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Triangle.Core.Runs;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>판의 끝: 승리나 패배, 간 곳까지, 파티. "새 원정"을 누르면 판을 치우고 파티 고르기로 간다.</summary>
internal sealed class RunEndScene : RunScene
{
    private const int Width = 720;

    private readonly Action _newRun;

    public RunEndScene(Ui ui, RunSession session, Rectangle bounds, Action newRun)
        : base(ui, session, bounds, () => { })
    {
        _newRun = newRun;
    }

    private bool Won => Run.Phase == RunPhase.Victory;

    protected override string Title => Won ? "원정 성공" : "원정 실패";

    protected override string Help => "다음 판에는 아무것도 이어지지 않습니다 (해금은 아직 없습니다).";

    protected override Widget Build()
    {
        var root = new Panel();
        var box = Box(Width, spacing: 10, border: Won ? Theme.Gold : Theme.Enemy);
        box.Widgets.Add(W.Label(Won ? "마지막 막의 보스를 물리쳤습니다!" : "원정이 끝났습니다.", 26, Won ? Theme.Gold : Theme.Enemy, bold: true));
        var acts = Data.Run!.Acts.Count;
        box.Widgets.Add(W.Label(Won ? $"{acts}막 모두 통과" : $"{RunText.Act(Run, Data)}에서 끝남 ({Run.Act}/{acts}막 통과)", 18, Theme.Text));
        box.Widgets.Add(W.Label($"전투 {Run.Fights}번 · 남은 골드 {Run.Gold} · 사본 {Run.Copies.Values.Sum()}장", 16, Theme.TextDim));
        box.Widgets.Add(new Panel { Height = 6 });
        box.Widgets.Add(W.Header("figure", "파티"));
        foreach (var member in Run.Members)
        {
            var passives = member.Passives.Count == 0 ? "" : " · " + string.Join(", ", member.Passives.Select(p => $"{Data.Skills[p.Key].Name} Lv{p.Value}"));
            var (lines, color) = RunText.Lines(member);
            box.Widgets.Add(W.Label($"{Run.Name(Data, member)} — {lines}{passives}", 16, member.Dead ? Theme.Death : color, width: Width - 24, wrap: true));
        }

        box.Widgets.Add(new Panel { Height = 6 });
        box.Widgets.Add(Button("새 원정", _newRun, accent: true, width: 200, size: 20));
        root.Widgets.Add(Place(box, Body.Left + (Body.Width - Width) / 2, Body.Top + 30));
        return root;
    }
}
