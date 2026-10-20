using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Triangle.Desktop;

/// <summary>프레임 단위 키·마우스 입력. 이번 프레임에 새로 눌린 키와 클릭, 휠을 굴린 양을 구분한다.</summary>
internal sealed class Input
{
    private KeyboardState _previous;
    private KeyboardState _current;
    private MouseState _previousMouse;
    private MouseState _currentMouse;

    public void Update()
    {
        _previous = _current;
        _current = Keyboard.GetState();
        _previousMouse = _currentMouse;
        _currentMouse = Mouse.GetState();
    }

    public bool Pressed(Keys key) => _current.IsKeyDown(key) && !_previous.IsKeyDown(key);

    /// <summary>창 좌표의 마우스 위치 (설계 좌표로는 <see cref="Rendering.Screen.ToDesign"/>로 바꾼다).</summary>
    public Point MousePosition => _currentMouse.Position;

    /// <summary>이번 프레임에 왼쪽 버튼을 눌렀다.</summary>
    public bool Clicked => _currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;

    /// <summary>이번 프레임에 휠을 굴린 양. 한 칸이 120이고 위로 굴리면 양수다 (터치패드는 한 칸보다 작게 올 수 있다).</summary>
    public int WheelDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    /// <summary>이번 프레임에 새로 눌린 키가 있다. 음악·화면 키(F8~F11)와 조합키는 빼고 센다.</summary>
    public bool AnyKeyPressed => _current.GetPressedKeys().Any(key => !_previous.IsKeyDown(key) && !IgnoredForAnyKey.Contains(key));

    private static readonly Keys[] IgnoredForAnyKey =
    [
        Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.LeftShift, Keys.RightShift, Keys.LeftControl, Keys.RightControl, Keys.LeftAlt, Keys.RightAlt,
        Keys.LeftWindows, Keys.RightWindows,
    ];

    /// <summary>주어진 키 중 하나라도 눌려 있다 (Ctrl 좌우 같은 조합키 확인용).</summary>
    public bool IsDown(params Keys[] keys) => keys.Any(_current.IsKeyDown);
}
