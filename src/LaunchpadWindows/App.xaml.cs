using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LaunchpadWindows.Desktop;
using LaunchpadWindows.Models;
using LaunchpadWindows.Presentation;
using LaunchpadWindows.Shell;
using LaunchpadWindows.Shortcuts;
using LaunchpadWindows.Storage;
using LaunchpadWindows.SystemIntegration;

namespace LaunchpadWindows;

public partial class App : System.Windows.Application
{
    private JsonSettingsStore _settingsStore = null!;
    private AppSettings _settings = null!;
    private TrayService _tray = null!;
    private HotkeyService _hotkey = null!;
    private HwndSource _messageSource = null!;
    private LaunchpadWindow? _launchpadWindow;
    private bool _isOpeningLaunchpad;

    private sealed record DesktopScanResult(bool Success, IReadOnlyList<LaunchItem> Items);

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsStore = new JsonSettingsStore(JsonSettingsStore.GetDefaultSettingsPath());
        _settings = await _settingsStore.LoadAsync();

        _hotkey = new HotkeyService(new NativeHotkeyRegistrar());
        _tray = new TrayService(OpenLaunchpad, OpenSettings, Shutdown);

        HwndSourceParameters parameters = new("LaunchpadWindowsHotkeySink");
        _messageSource = new HwndSource(parameters);
        _messageSource.AddHook(OnWindowMessage);
        HotkeyRegistrationResult hotkeyResult = _hotkey.Register(_messageSource.Handle, _settings.Hotkey);
        _hotkey.Activated += (_, _) => ToggleLaunchpad();
        if (!hotkeyResult.Success)
        {
            _tray.ShowMessage("Launchpad Windows", hotkeyResult.Message);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _launchpadWindow?.Close();
        _hotkey.Unregister();
        _messageSource.Dispose();
        _tray.Dispose();
        base.OnExit(e);
    }

    private nint OnWindowMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        _hotkey.HandleHotkeyMessage(msg, wParam.ToInt32());
        return nint.Zero;
    }

    private void ToggleLaunchpad()
    {
        if (_launchpadWindow?.IsVisible == true)
        {
            _launchpadWindow.FadeOutAndClose();
        }
        else
        {
            OpenLaunchpad();
        }
    }

    private async void OpenLaunchpad()
    {
        if (_launchpadWindow?.IsVisible == true)
        {
            _launchpadWindow.Activate();
            return;
        }

        if (_isOpeningLaunchpad)
        {
            return;
        }

        _isOpeningLaunchpad = true;
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            DesktopScanResult scan = ScanDesktopOrReport(desktopPath);
            IReadOnlyList<LaunchItem> items;
            if (scan.Success)
            {
                items = ItemMerger.Merge(_settings, scan.Items);
                await _settingsStore.SaveAsync(_settings);
            }
            else
            {
                items = _settings.ManualItems.ToList();
            }

            LaunchpadViewModel viewModel = new(new LauncherService(new ShellProcessStarter()));
            viewModel.SetItems(items);
            if (scan.Success)
            {
                viewModel.OrderChanged += async (_, ids) =>
                {
                    _settings.OrderedItemIds = ids.ToList();
                    await _settingsStore.SaveAsync(_settings);
                };
            }

            Rect bounds = new MonitorService().GetMouseMonitorBounds();
            LaunchpadWindow window = new(viewModel, _settings.FadeDuration)
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height
            };
            viewModel.CloseRequested += (_, _) => window.FadeOutAndClose();
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_launchpadWindow, window))
                {
                    _launchpadWindow = null;
                }
            };
            _launchpadWindow = window;
            window.Show();
            window.Activate();
        }
        finally
        {
            _isOpeningLaunchpad = false;
        }
    }

    private void OpenSettings()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string executablePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        DesktopScanResult scan = ScanDesktopOrReport(desktopPath);
        SettingsViewModel viewModel = new(
            _settings,
            new AutostartService(new RegistryRunKey(), () => executablePath),
            desktopPath,
            scan.Items,
            RegisterHotkeyFromSettings,
            new ShellShortcutResolver());
        viewModel.SaveRequested += async (_, _) => await _settingsStore.SaveAsync(_settings);
        new SettingsWindow(viewModel).Show();
    }

    private DesktopScanResult ScanDesktopOrReport(string desktopPath)
    {
        try
        {
            DesktopScanner scanner = new(new FileSystemDesktopReader(), new ShellShortcutResolver());
            return new DesktopScanResult(true, scanner.Scan(desktopPath, DateTimeOffset.UtcNow));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            _tray.ShowMessage("Launchpad Windows", $"Desktop scan failed: {ex.Message}");
            return new DesktopScanResult(false, []);
        }
    }

    private HotkeyRegistrationResult RegisterHotkeyFromSettings(HotkeyGesture gesture)
    {
        HotkeyRegistrationResult result = _hotkey.Register(_messageSource.Handle, gesture);
        if (!result.Success)
        {
            _tray.ShowMessage("Launchpad Windows", result.Message);
        }
        return result;
    }
}
