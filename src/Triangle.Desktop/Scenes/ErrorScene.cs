using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Triangle.Desktop.Rendering;

namespace Triangle.Desktop.Scenes;

/// <summary>데이터 로딩 실패 등, 게임을 시작할 수 없을 때 이유를 보여준다.</summary>
internal sealed class ErrorScene(Ui ui, string title, IReadOnlyList<string> lines) : IScene
{
    public void Update(GameTime gameTime, Input input)
    {
    }

    public void Draw(SpriteBatch batch)
    {
        batch.Begin();
        ui.Text(batch, ui.BoldFont(26), title, new Vector2(24, 24), Theme.Enemy);
        var y = 72f;
        foreach (var line in lines)
        {
            ui.Text(batch, ui.Font(18), line, new Vector2(24, y), Theme.Text);
            y += 26;
        }

        batch.End();
    }
}
