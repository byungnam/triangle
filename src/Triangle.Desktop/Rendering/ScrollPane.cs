using System.Reflection;
using Microsoft.Xna.Framework;
using Myra.Events;
using Myra.Graphics2D.UI;

namespace Triangle.Desktop.Rendering;

/// <summary>
/// 스크롤 막대를 끌 때 마우스를 정확히 따라가는 ScrollViewer.
/// Myra 1.6.6의 ScrollViewer는 막대를 끈 거리를 창 픽셀로 재고 막대 길이는 설계 좌표로 재서,
/// 화면을 키운 만큼(QHD면 2배) 막대가 마우스보다 빨리 움직인다. 끌기만 설계 좌표로 다시 처리한다.
/// </summary>
internal sealed class ScrollPane : ScrollViewer
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FieldInfo? DragStart = typeof(ScrollViewer).GetField("_startBoundsPos", Private);
    private static readonly FieldInfo? DragOrientation = typeof(ScrollViewer).GetField("_scrollbarOrientation", Private);
    private static readonly FieldInfo? ThumbMaximumX = typeof(ScrollViewer).GetField("_thumbMaximumX", Private);
    private static readonly FieldInfo? ThumbMaximumY = typeof(ScrollViewer).GetField("_thumbMaximumY", Private);

    private Myra.Graphics2D.UI.Desktop? _dragDesktop;
    private Point _grabPoint;
    private Point _grabScroll;
    private bool _vertical;

    public override void OnTouchDown()
    {
        base.OnTouchDown();

        // 막대를 잡지 않았거나 Myra 내부가 바뀌어 필드를 못 찾으면 Myra의 처리를 그대로 둔다.
        if (Desktop is null || DragStart?.GetValue(this) is null || DragOrientation is null || ThumbMaximumX is null || ThumbMaximumY is null)
        {
            return;
        }

        DragStart.SetValue(this, null); // Myra의 끌기는 끄고 아래에서 대신 처리한다
        _vertical = (Orientation)DragOrientation.GetValue(this)! == Orientation.Vertical;
        _grabPoint = ToLocal(Desktop.MousePosition);
        _grabScroll = ScrollPosition;
        _dragDesktop = Desktop;
        _dragDesktop.TouchMoved += OnDrag;
        _dragDesktop.TouchUp += OnRelease;
    }

    private void OnDrag(object? sender, MyraEventArgs args)
    {
        if (_dragDesktop is null)
        {
            return;
        }

        var point = ToLocal(_dragDesktop.MousePosition);
        var position = _grabScroll;
        if (_vertical)
        {
            var thumbMaximum = Math.Max(1, (int)ThumbMaximumY!.GetValue(this)!);
            position.Y = Math.Clamp(_grabScroll.Y + (point.Y - _grabPoint.Y) * ScrollMaximum.Y / thumbMaximum, 0, ScrollMaximum.Y);
        }
        else
        {
            var thumbMaximum = Math.Max(1, (int)ThumbMaximumX!.GetValue(this)!);
            position.X = Math.Clamp(_grabScroll.X + (point.X - _grabPoint.X) * ScrollMaximum.X / thumbMaximum, 0, ScrollMaximum.X);
        }

        ScrollPosition = position;
    }

    private void OnRelease(object? sender, MyraEventArgs args)
    {
        if (_dragDesktop is null)
        {
            return;
        }

        _dragDesktop.TouchMoved -= OnDrag;
        _dragDesktop.TouchUp -= OnRelease;
        _dragDesktop = null;
    }
}
