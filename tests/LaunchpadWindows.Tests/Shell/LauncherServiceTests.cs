using System.ComponentModel;
using LaunchpadWindows.Models;
using LaunchpadWindows.Shell;

namespace LaunchpadWindows.Tests.Shell;

public sealed class LauncherServiceTests
{
    [Fact]
    public void Launch_ReturnsFailureWhenLocalPathIsMissing()
    {
        FakeProcessStarter starter = new();
        LauncherService service = new(starter, path => false, path => false);
        LaunchItem item = LaunchItem.CreateManual("Missing", LaunchItemKind.File, @"C:\missing.txt");

        LaunchResult result = service.Launch(item);

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.Message);
        Assert.Empty(starter.StartedPaths);
    }

    [Fact]
    public void Launch_UsesShellExecuteForExistingFile()
    {
        FakeProcessStarter starter = new();
        LauncherService service = new(starter, path => true, path => false);
        LaunchItem item = LaunchItem.CreateManual("File", LaunchItemKind.File, @"C:\file.txt");

        LaunchResult result = service.Launch(item);

        Assert.True(result.Success);
        Assert.Equal(@"C:\file.txt", starter.StartedPaths.Single());
    }

    [Fact]
    public void Launch_ReturnsFailureWhenShortcutTargetIsMissing()
    {
        FakeProcessStarter starter = new();
        LauncherService service = new(starter, path => path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase), path => false);
        LaunchItem item = new(
            "desktop:broken",
            "Broken",
            LaunchItemSource.DesktopScan,
            LaunchItemKind.Shortcut,
            @"C:\Users\hp\Desktop\Broken.lnk",
            @"C:\missing-target.exe",
            "Broken.lnk",
            DateTimeOffset.UtcNow);

        LaunchResult result = service.Launch(item);

        Assert.False(result.Success);
        Assert.Contains("Shortcut target does not exist", result.Message);
        Assert.Empty(starter.StartedPaths);
    }

    [Fact]
    public void Launch_UsesShortcutFileItselfWhenResolvedTargetExists()
    {
        FakeProcessStarter starter = new();
        LauncherService service = new(starter, path => path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase), path => false);
        LaunchItem item = new(
            "desktop:notepad",
            "Notepad",
            LaunchItemSource.DesktopScan,
            LaunchItemKind.Shortcut,
            @"C:\Users\hp\Desktop\Notepad.lnk",
            @"C:\Windows\System32\notepad.exe",
            "Notepad.lnk",
            DateTimeOffset.UtcNow);

        LaunchResult result = service.Launch(item);

        Assert.True(result.Success);
        Assert.Equal(@"C:\Users\hp\Desktop\Notepad.lnk", starter.StartedPaths.Single());
    }

    [Fact]
    public void Launch_UsesShortcutFileItselfWhenResolvedTargetIsProtocolBased()
    {
        FakeProcessStarter starter = new();
        LauncherService service = new(starter, path => path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase), path => false);
        LaunchItem item = new(
            "desktop:settings",
            "Settings",
            LaunchItemSource.DesktopScan,
            LaunchItemKind.Shortcut,
            @"C:\Users\hp\Desktop\Settings.lnk",
            "ms-settings:",
            "Settings.lnk",
            DateTimeOffset.UtcNow);

        LaunchResult result = service.Launch(item);

        Assert.True(result.Success);
        Assert.Equal(@"C:\Users\hp\Desktop\Settings.lnk", starter.StartedPaths.Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Launch_UsesShortcutFileItselfWhenResolvedTargetIsMissing(string? resolvedTargetPath)
    {
        FakeProcessStarter starter = new();
        LauncherService service = new(starter, path => path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase), path => false);
        LaunchItem item = new(
            "desktop:shortcut",
            "Shortcut",
            LaunchItemSource.DesktopScan,
            LaunchItemKind.Shortcut,
            @"C:\Users\hp\Desktop\Shortcut.lnk",
            resolvedTargetPath,
            "Shortcut.lnk",
            DateTimeOffset.UtcNow);

        LaunchResult result = service.Launch(item);

        Assert.True(result.Success);
        Assert.Equal(@"C:\Users\hp\Desktop\Shortcut.lnk", starter.StartedPaths.Single());
    }

    [Fact]
    public void Launch_UsesResolvedUrlWhenAvailable()
    {
        FakeProcessStarter starter = new();
        LauncherService service = new(starter, path => false, path => false);
        LaunchItem item = new(
            "desktop:site",
            "Site",
            LaunchItemSource.DesktopScan,
            LaunchItemKind.Url,
            @"C:\Users\hp\Desktop\Site.url",
            "https://example.com",
            "Site.url",
            DateTimeOffset.UtcNow);

        LaunchResult result = service.Launch(item);

        Assert.True(result.Success);
        Assert.Equal("https://example.com", starter.StartedPaths.Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Launch_UsesUrlFileWhenResolvedTargetIsMissing(string? resolvedTargetPath)
    {
        FakeProcessStarter starter = new();
        LauncherService service = new(starter, path => false, path => false);
        LaunchItem item = new(
            "desktop:site",
            "Site",
            LaunchItemSource.DesktopScan,
            LaunchItemKind.Url,
            @"C:\Users\hp\Desktop\Site.url",
            resolvedTargetPath,
            "Site.url",
            DateTimeOffset.UtcNow);

        LaunchResult result = service.Launch(item);

        Assert.True(result.Success);
        Assert.Equal(@"C:\Users\hp\Desktop\Site.url", starter.StartedPaths.Single());
    }

    [Fact]
    public void Launch_UsesShellExecuteForExistingFolder()
    {
        FakeProcessStarter starter = new();
        LauncherService service = new(starter, path => false, path => true);
        LaunchItem item = LaunchItem.CreateManual("Folder", LaunchItemKind.Folder, @"C:\Users\hp\Documents");

        LaunchResult result = service.Launch(item);

        Assert.True(result.Success);
        Assert.Equal(@"C:\Users\hp\Documents", starter.StartedPaths.Single());
    }

    [Fact]
    public void Launch_ReturnsFailureWhenShellReportsNoAssociatedApp()
    {
        FakeProcessStarter starter = new(new Win32Exception("no associated app"));
        LauncherService service = new(starter, path => true, path => false);
        LaunchItem item = LaunchItem.CreateManual("Unknown", LaunchItemKind.File, @"C:\file.unknown");

        LaunchResult result = service.Launch(item);

        Assert.False(result.Success);
        Assert.Contains("no associated app", result.Message);
    }

    [Fact]
    public void Launch_ReturnsFailureWhenPermissionDenied()
    {
        FakeProcessStarter starter = new(new UnauthorizedAccessException("permission denied"));
        LauncherService service = new(starter, path => true, path => false);
        LaunchItem item = LaunchItem.CreateManual("Secret", LaunchItemKind.File, @"C:\secret.txt");

        LaunchResult result = service.Launch(item);

        Assert.False(result.Success);
        Assert.Contains("permission denied", result.Message);
    }

    private sealed class FakeProcessStarter(Exception? exceptionToThrow = null) : IProcessStarter
    {
        public List<string> StartedPaths { get; } = [];

        public void Start(string pathOrUrl)
        {
            if (exceptionToThrow is not null)
            {
                throw exceptionToThrow;
            }

            StartedPaths.Add(pathOrUrl);
        }
    }
}
