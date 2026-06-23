using System.Drawing;
using System.Windows.Forms;
using DrawingPoint = System.Drawing.Point;

namespace LaunchpadWindows.SystemIntegration;

public readonly record struct MonitorBounds(int Left, int Top, int Width, int Height);

public sealed class MonitorService
{
    private readonly Func<DrawingPoint> _cursorProvider;
    private readonly Func<DrawingPoint, Rectangle> _screenBoundsProvider;

    public MonitorService()
        : this(() => Cursor.Position, point => Screen.FromPoint(point).Bounds)
    {
    }

    public MonitorService(Func<DrawingPoint> cursorProvider, Func<DrawingPoint, Rectangle> screenBoundsProvider)
    {
        _cursorProvider = cursorProvider;
        _screenBoundsProvider = screenBoundsProvider;
    }

    public MonitorBounds GetMouseMonitorBounds()
    {
        Rectangle bounds = _screenBoundsProvider(_cursorProvider());
        return new MonitorBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }
}
