using Microsoft.Xna.Framework.Input;

namespace Triangle.Desktop;

/// <summary>프레임 단위 키 입력. 이번 프레임에 새로 눌린 키를 구분한다.</summary>
internal sealed class Input
{
    private KeyboardState _previous;
    private KeyboardState _current;

    public void Update()
    {
        _previous = _current;
        _current = Keyboard.GetState();
    }

    public bool Pressed(Keys key) => _current.IsKeyDown(key) && !_previous.IsKeyDown(key);
}
