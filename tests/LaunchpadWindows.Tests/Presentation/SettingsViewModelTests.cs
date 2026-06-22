using LaunchpadWindows.Models;
using LaunchpadWindows.Presentation;
using LaunchpadWindows.Shortcuts;
using LaunchpadWindows.SystemIntegration;

namespace LaunchpadWindows.Tests.Presentation;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void AddManualFile_AddsManualItemAndRaisesSave()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(settings, new FakeAutostart(), @"C:\Users\hp\Desktop", []);
        int saves = 0;
        vm.SaveRequested += (_, _) => saves++;

        vm.AddManualItem("Readme", LaunchItemKind.File, @"C:\readme.txt");

        Assert.Single(settings.ManualItems);
        Assert.Single(vm.ManualItems);
        Assert.Equal("Readme", settings.ManualItems[0].DisplayName);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void AddManualShortcut_StoresResolvedTarget()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(
            settings,
            new FakeAutostart(),
            @"C:\Users\hp\Desktop",
            [],
            shortcutResolver: new FakeShortcutResolver(@"C:\Windows\System32\notepad.exe"));

        vm.AddManualItem("Notepad", LaunchItemKind.Shortcut, @"C:\Users\hp\Desktop\Notepad.lnk");

        Assert.Single(settings.ManualItems);
        Assert.Equal(@"C:\Windows\System32\notepad.exe", settings.ManualItems[0].ResolvedTargetPath);
    }

    [Fact]
    public void AddManualShortcut_WhenResolverThrowsExpectedFailure_PreservesManualItemAndRaisesSave()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(
            settings,
            new FakeAutostart(),
            @"C:\Users\hp\Desktop",
            [],
            shortcutResolver: new ThrowingShortcutResolver(new InvalidOperationException("bad shortcut")));
        int saves = 0;
        vm.SaveRequested += (_, _) => saves++;

        Exception? exception = Record.Exception(() =>
            vm.AddManualItem("Bad Shortcut", LaunchItemKind.Shortcut, @"C:\Users\hp\Desktop\Bad.lnk"));

        Assert.Null(exception);
        Assert.Single(settings.ManualItems);
        Assert.Single(vm.ManualItems);
        Assert.Null(settings.ManualItems[0].ResolvedTargetPath);
        Assert.Contains("bad shortcut", vm.ErrorMessage);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void AddManualUrl_StoresResolvedUrl()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(
            settings,
            new FakeAutostart(),
            @"C:\Users\hp\Desktop",
            [],
            shortcutResolver: new FakeShortcutResolver("https://example.com"));

        vm.AddManualItem("Site", LaunchItemKind.Url, @"C:\Users\hp\Desktop\Site.url");

        Assert.Single(settings.ManualItems);
        Assert.Equal("https://example.com", settings.ManualItems[0].ResolvedTargetPath);
    }

    [Fact]
    public void ToggleAutostart_OnlyUpdatesSettingWhenRegistryWriteSucceeds()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(settings, new FakeAutostart(), @"C:\Users\hp\Desktop", []);

        vm.SetAutostart(true);

        Assert.True(settings.AutostartEnabled);
    }

    [Fact]
    public void ToggleAutostart_LeavesSettingFalseWhenRegistryWriteFails()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(settings, new FakeAutostart(success: false), @"C:\Users\hp\Desktop", []);

        vm.SetAutostart(true);

        Assert.False(settings.AutostartEnabled);
        Assert.Equal("registry denied", vm.ErrorMessage);
    }

    [Fact]
    public void SetHotkeyText_UpdatesSettingWhenRegistrationSucceeds()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(
            settings,
            new FakeAutostart(),
            @"C:\Users\hp\Desktop",
            [],
            gesture => HotkeyRegistrationResult.Ok());
        int saves = 0;
        vm.SaveRequested += (_, _) => saves++;

        vm.SetHotkeyText("Ctrl + Shift + L");

        Assert.Equal("Ctrl + Shift + L", settings.Hotkey.ToString());
        Assert.Equal(1, saves);
    }

    [Fact]
    public void SetHotkeyText_AllowsDigitKeyAcceptedByHotkeyService()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(
            settings,
            new FakeAutostart(),
            @"C:\Users\hp\Desktop",
            [],
            gesture => HotkeyRegistrationResult.Ok());

        vm.SetHotkeyText("Ctrl + Alt + 1");

        Assert.Equal("Ctrl + Alt + 1", settings.Hotkey.ToString());
    }

    [Fact]
    public void SetHotkeyText_RejectsHotkeyWithoutModifier()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(settings, new FakeAutostart(), @"C:\Users\hp\Desktop", []);

        vm.SetHotkeyText("L");

        Assert.Equal("Ctrl + Alt + Space", settings.Hotkey.ToString());
        Assert.Equal("Enter a hotkey such as Ctrl + Alt + Space.", vm.ErrorMessage);
    }

    [Fact]
    public void SaveWindowBounds_PersistsSettingsGeometry()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(settings, new FakeAutostart(), @"C:\Users\hp\Desktop", []);

        vm.SaveWindowBounds(left: 100, top: 80, width: 900, height: 640);

        Assert.Equal(100, settings.SettingsWindowLeft);
        Assert.Equal(80, settings.SettingsWindowTop);
        Assert.Equal(900, settings.SettingsWindowWidth);
        Assert.Equal(640, settings.SettingsWindowHeight);
    }

    [Fact]
    public void HideAndRestoreDesktopItem_UpdatesCollectionsAndSettings()
    {
        LaunchItem item = new("desktop:A", "A", LaunchItemSource.DesktopScan, LaunchItemKind.File, @"C:\Users\hp\Desktop\A.txt", null, "A", DateTimeOffset.UtcNow);
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(settings, new FakeAutostart(), @"C:\Users\hp\Desktop", [item]);

        vm.HideDesktopItem("desktop:A");

        Assert.Empty(vm.VisibleDesktopItems);
        Assert.Equal(["desktop:A"], settings.HiddenDesktopItemIds);

        vm.RestoreHiddenDesktopItem("desktop:A");

        Assert.Single(vm.VisibleDesktopItems);
        Assert.Empty(settings.HiddenDesktopItemIds);
    }

    private sealed class FakeAutostart(bool success = true) : IAutostartService
    {
        public AutostartResult SetEnabled(bool enabled) =>
            success ? AutostartResult.Ok() : AutostartResult.Fail("registry denied");
    }

    private sealed class FakeShortcutResolver(string? targetPath) : IShortcutResolver
    {
        public ShortcutResolution Resolve(string shortcutPath) => new(targetPath);
    }

    private sealed class ThrowingShortcutResolver(Exception exception) : IShortcutResolver
    {
        public ShortcutResolution Resolve(string shortcutPath) => throw exception;
    }
}
