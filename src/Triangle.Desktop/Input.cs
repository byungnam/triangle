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

    /// <summary>주어진 키 중 하나라도 눌려 있다 (Ctrl 좌우 같은 조합키 확인용).</summary>
    public bool IsDown(params Keys[] keys) => keys.Any(_current.IsKeyDown);
}
