using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using DrawingPoint = System.Drawing.Point;

namespace LaunchpadWindows.SystemIntegration;

public sealed class MonitorService
{
    private const int MdtEffectiveDpi = 0;
    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(DrawingPoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    public Rect GetMouseMonitorBounds()
    {
        DrawingPoint cursor = Cursor.Position;
        Screen screen = Screen.FromPoint(cursor);
        (double scaleX, double scaleY) = GetMonitorScale(cursor);
        return new Rect(
            screen.Bounds.Left / scaleX,
            screen.Bounds.Top / scaleY,
            screen.Bounds.Width / scaleX,
            screen.Bounds.Height / scaleY);
    }

    private static (double ScaleX, double ScaleY) GetMonitorScale(DrawingPoint point)
    {
        nint monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
        if (monitor != nint.Zero &&
            GetDpiForMonitor(monitor, MdtEffectiveDpi, out uint dpiX, out uint dpiY) == 0 &&
            dpiX > 0 &&
            dpiY > 0)
        {
            return (dpiX / 96.0, dpiY / 96.0);
        }

        return (1.0, 1.0);
    }
}
