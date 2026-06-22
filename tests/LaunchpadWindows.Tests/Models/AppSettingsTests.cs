using LaunchpadWindows.Models;

namespace LaunchpadWindows.Tests.Models;

public sealed class AppSettingsTests
{
    [Fact]
    public void CreateDefault_UsesCtrlAltSpaceAndAutostartOff()
    {
        AppSettings settings = AppSettings.CreateDefault();

        Assert.Equal(1, settings.SchemaVersion);
        Assert.Equal("Ctrl + Alt + Space", settings.Hotkey.ToString());
        Assert.False(settings.AutostartEnabled);
        Assert.Empty(settings.OrderedItemIds);
        Assert.Empty(settings.ManualItems);
        Assert.Empty(settings.HiddenDesktopItemIds);
    }

    [Fact]
    public void ManualItem_CarriesStableGeneratedId()
    {
        LaunchItem item = LaunchItem.CreateManual(
            displayName: "Notes",
            kind: LaunchItemKind.File,
            pathOrUrl: @"C:\Users\hp\Desktop\notes.txt");

        Assert.StartsWith("manual:", item.Id);
        Assert.Equal(LaunchItemSource.Manual, item.Source);
        Assert.Equal("Notes", item.DisplayName);
    }

    [Fact]
    public void ManualShortcut_CanCarryResolvedTarget()
    {
        LaunchItem item = LaunchItem.CreateManual(
            displayName: "Notepad",
            kind: LaunchItemKind.Shortcut,
            pathOrUrl: @"C:\Users\hp\Desktop\Notepad.lnk",
            resolvedTargetPath: @"C:\Windows\System32\notepad.exe");

        Assert.Equal(@"C:\Windows\System32\notepad.exe", item.ResolvedTargetPath);
    }

    [Fact]
    public void TryParse_AcceptsEditableHotkeyText()
    {
        bool parsed = HotkeyGesture.TryParse("Ctrl + Shift + L", out HotkeyGesture? gesture);

        Assert.True(parsed);
        Assert.NotNull(gesture);
        Assert.True(gesture.Control);
        Assert.True(gesture.Shift);
        Assert.False(gesture.Alt);
        Assert.Equal("L", gesture.Key);
    }
}
