using System.Windows.Forms;

namespace LaunchpadWindows.SystemIntegration;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayService(Action openLaunchpad, Action openSettings, Action exit)
    {
        ContextMenuStrip menu = new();
        menu.Items.Add("Open Launchpad", null, (_, _) => openLaunchpad());
        menu.Items.Add("Settings", null, (_, _) => openSettings());
        menu.Items.Add("Exit", null, (_, _) => exit());

        _notifyIcon = new NotifyIcon
        {
            Text = "Launchpad Windows",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => openLaunchpad();
    }

    public void ShowMessage(string title, string message) =>
        _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
