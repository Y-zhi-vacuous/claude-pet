using System.Windows;
using System.Windows.Input;

namespace ClaudePet.Utils;

public class MouseDragHelper
{
    private readonly Window _window;
    private bool _isDragging;
    private Point _dragStartPoint;

    public MouseDragHelper(Window window)
    {
        _window = window;
    }

    public void OnMouseDown(Point mousePos)
    {
        _isDragging = true;
        _dragStartPoint = mousePos;
        _window.CaptureMouse();
    }

    public void OnMouseMove(Point mousePos)
    {
        if (!_isDragging) return;

        var deltaX = mousePos.X - _dragStartPoint.X;
        var deltaY = mousePos.Y - _dragStartPoint.Y;

        _window.Left += deltaX;
        _window.Top += deltaY;
    }

    public void OnMouseUp()
    {
        _isDragging = false;
        _window.ReleaseMouseCapture();
    }
}
