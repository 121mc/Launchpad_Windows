using System.Drawing;
using LaunchpadWindows.SystemIntegration;

namespace LaunchpadWindows.Tests.SystemIntegration;

public sealed class MonitorServiceTests
{
    [Fact]
    public void GetMouseMonitorBounds_ReturnsExactPhysicalBoundsForCursorMonitor()
    {
        Point cursor = new(-100, 200);
        Rectangle screenBounds = new(-1920, 0, 1920, 1080);
        MonitorService service = new(
            () => cursor,
            point =>
            {
                Assert.Equal(cursor, point);
                return screenBounds;
            });

        MonitorBounds bounds = service.GetMouseMonitorBounds();

        Assert.Equal(new MonitorBounds(-1920, 0, 1920, 1080), bounds);
    }
}
