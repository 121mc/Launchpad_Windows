namespace LaunchpadWindows.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public HotkeyGesture Hotkey { get; set; } = HotkeyGesture.Default;
    public bool AutostartEnabled { get; set; }
    public List<string> OrderedItemIds { get; set; } = [];
    public List<LaunchItem> ManualItems { get; set; } = [];
    public List<string> HiddenDesktopItemIds { get; set; } = [];
    public TimeSpan FadeDuration { get; set; } = TimeSpan.FromMilliseconds(180);
    public double SettingsWindowWidth { get; set; } = 720;
    public double SettingsWindowHeight { get; set; } = 520;
    public double? SettingsWindowLeft { get; set; }
    public double? SettingsWindowTop { get; set; }

    public static AppSettings CreateDefault() => new();
}
