using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Triangle.Desktop.Scenes;

internal interface IScene
{
    void Update(GameTime gameTime, Input input);

    void Draw(SpriteBatch spriteBatch);
}
