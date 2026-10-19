using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>이벤트: 이름, 글, 선택지. 고를 수 없는 선택지는 이유를 함께 보여준다.</summary>
internal sealed class RunEventScene : RunScene
{
    private const int Width = 760;

    public RunEventScene(Ui ui, RunSession session, Rectangle bounds, Action changed)
        : base(ui, session, bounds, changed)
    {
    }

    protected override string Title => $"이벤트 — {Run.Event!.Name}";

    protected override Widget Build()
    {
        var root = new Panel();
        var e = Run.Event!;
        var box = Box(Width, spacing: 14);
        box.Widgets.Add(W.Header("node.event", e.Name, size: 24));
        if (e.Text is { Length: > 0 } text)
        {
            box.Widgets.Add(W.Label(text, 18, Theme.Text, width: Width - 24, wrap: true));
        }

        box.Widgets.Add(new Panel { Height = 6 });
        for (var i = 0; i < e.Options.Count; i++)
        {
            var option = i;
            var why = Run.WhyCannotChoose(option);
            var button = Button(e.Options[option].Text, () => Act(() => Run.Choose(Data, option)), why, width: Width - 24);
            box.Widgets.Add(button);
            if (why is not null)
            {
                box.Widgets.Add(W.Label($"  고를 수 없음: {why}", 14, Theme.Enemy));
            }
        }

        root.Widgets.Add(Place(box, Body.Left + (Body.Width - Width) / 2, Body.Top + 40));
        return root;
    }
}
