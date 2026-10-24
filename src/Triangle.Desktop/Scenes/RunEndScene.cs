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

    protected override string Help => "다음 판에는 명성으로 연 해금과 기록만 이어집니다. 명성은 파티 고르기의 \"해금 · 기록\"에서 씁니다.";

    /// <summary>"보통 전투 4 × 1 + 정예 1 × 3 + …".</summary>
    private string RenownBreakdown()
    {
        var rules = Data.Run!.Renown;
        var won = Run.History.Where(b => b.Outcome == Triangle.Core.Combat.CombatOutcome.Victory).ToList();
        var parts = new List<string>();
        foreach (var (kind, name, each) in new[] { (RunNodeKind.Fight, "보통 전투", rules.Fight), (RunNodeKind.Elite, "정예", rules.Elite), (RunNodeKind.Boss, "보스", rules.Boss) })
        {
            var count = won.Count(b => b.Kind == kind);
            if (count > 0)
            {
                parts.Add($"{name} {count}번 × {each}");
            }
        }

        if (Won)
        {
            parts.Add($"원정 성공 {rules.Victory}");
        }

        var bonus = Run.Terms.Renown != 0 ? $" (지도 {Run.Terms.Renown:+0;-0}%)" : "";
        return parts.Count == 0 ? "이긴 전투가 없습니다." : string.Join(" + ", parts) + bonus;
    }

    protected override Widget Build()
    {
        var root = new Panel();
        var box = Box(Width, spacing: 10, border: Won ? Theme.Gold : Theme.Enemy);
        box.Widgets.Add(W.Label(Won ? "마지막 막의 보스를 물리쳤습니다!" : "원정이 끝났습니다.", 26, Won ? Theme.Gold : Theme.Enemy, bold: true));
        var acts = Data.Run!.Acts.Count;
        var where = Won ? $"{acts}막 모두 통과" : $"{RunText.Act(Run, Data)}에서 끝남 ({Run.Act}/{acts}막 통과)";
        box.Widgets.Add(W.Label(Run.Chart.IsBase ? where : $"{Run.Chart.Name(Data)} (위험도 {Run.Chart.Risk(Data)}) · {where}", 18, Theme.Text));
        box.Widgets.Add(W.Label($"전투 {Run.Fights}번 · 남은 골드 {Run.Gold} (판이 끝나면 사라짐) · 사본 {Run.Copies.Values.Sum()}장", 16, Theme.TextDim));
        box.Widgets.Add(new Panel { Height = 6 });
        var renown = Profile.RenownFor(Run, Data);
        box.Widgets.Add(W.Header("node.boss", $"명성 +{renown}", $"지금 {Session.Profile.Renown} → {Session.Profile.Renown + renown}"));
        box.Widgets.Add(W.Label(RenownBreakdown(), 15, Theme.Text, width: Width - 24, wrap: true));
        var tier = Session.Profile.MapTierAfter(Run, Data);
        if (tier > Session.Profile.MapTier)
        {
            // 처음 이기면 지도 등급 1이, 수식어가 등급만큼 붙은 지도로 이기면 다음 등급이 열린다.
            box.Widgets.Add(new Panel { Height = 6 });
            box.Widgets.Add(W.Header("lock", $"지도 등급 {tier} 열림", $"수식어가 {tier}개까지 붙은 지도"));
            box.Widgets.Add(W.Label("다음 원정부터 파티를 고른 뒤 원정 지도를 고릅니다. 수식어가 판을 어렵게 하는 만큼 보상과 명성이 늡니다.", 15, Theme.Text, width: Width - 24, wrap: true));
        }
        box.Widgets.Add(new Panel { Height = 6 });
        box.Widgets.Add(W.Header("figure", "파티"));
        foreach (var member in Run.Members)
        {
            var passives = member.Passives.Count == 0 ? "" : " · " + string.Join(", ", member.Passives.Select(p => $"{Data.Skills[p.Key].Name} Lv{p.Value}"));
            var (lines, color) = RunText.Lines(member);
            box.Widgets.Add(W.Label($"{Run.Name(Data, member)} — {lines}{passives}", 16, member.Dead ? Theme.Death : color, width: Width - 24, wrap: true));
        }

        box.Widgets.Add(new Panel { Height = 6 });
        box.Widgets.Add(Button("명성 받고 새 원정", _newRun, accent: true, width: 260, size: 20));
        root.Widgets.Add(Place(box, Body.Left + (Body.Width - Width) / 2, Body.Top + 30));
        return root;
    }
}
