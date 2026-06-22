# Launchpad Windows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a native Windows 11 WPF launchpad that scans the current user's Desktop, supports manual items, opens with `Ctrl + Alt + Space`, displays a full-screen frosted overlay on the mouse monitor, launches entries with one click, and closes with fade-out.

**Architecture:** The app is a single WPF process with a tray shell, a transient full-screen launchpad window, and small services for hotkeys, scanning, persistence, launching, autostart, icons, and monitors. Business rules live in testable classes under `src/LaunchpadWindows`; WPF windows bind to view models and do not directly parse shortcuts, write registry values, or merge item lists.

**Tech Stack:** .NET SDK 10.0.102, `net10.0-windows`, WPF, Windows Forms `NotifyIcon`, xUnit, JSON settings, Windows registry `HKCU`, Win32 hotkey and shell APIs.

---

## Source Spec

Implement the approved design in `docs/superpowers/specs/2026-06-22-launchpad-windows-design.md`.

## Scope Check

The spec describes one cohesive desktop application. The work touches several Windows integrations, but they are not independent products. This plan keeps them in one implementation track and splits them by testable module.

## File Structure

Create or modify these files:

- `global.json`: pin the local SDK.
- `LaunchpadWindows.sln`: solution file.
- `src/LaunchpadWindows/LaunchpadWindows.csproj`: WPF app project.
- `src/LaunchpadWindows/App.xaml`: WPF application resource entry.
- `src/LaunchpadWindows/App.xaml.cs`: composition root, tray lifetime, startup/shutdown.
- `src/LaunchpadWindows/Models/AppSettings.cs`: persisted settings model and defaults.
- `src/LaunchpadWindows/Models/HotkeyGesture.cs`: hotkey model and formatting.
- `src/LaunchpadWindows/Models/LaunchItem.cs`: launch item model and item enums.
- `src/LaunchpadWindows/Storage/JsonSettingsStore.cs`: JSON load/save with corrupt-file backup.
- `src/LaunchpadWindows/Storage/ItemMerger.cs`: combines scanned and manual items with persisted order.
- `src/LaunchpadWindows/Desktop/DesktopScanner.cs`: current-user Desktop scanning.
- `src/LaunchpadWindows/Desktop/FileSystemDesktopReader.cs`: real filesystem reader.
- `src/LaunchpadWindows/Shortcuts/IShortcutResolver.cs`: shortcut resolution boundary.
- `src/LaunchpadWindows/Shortcuts/ShellShortcutResolver.cs`: `.lnk` and `.url` resolver.
- `src/LaunchpadWindows/Shell/LauncherService.cs`: shell launch behavior.
- `src/LaunchpadWindows/SystemIntegration/AutostartService.cs`: per-user Run key integration.
- `src/LaunchpadWindows/SystemIntegration/HotkeyService.cs`: global hotkey registration.
- `src/LaunchpadWindows/SystemIntegration/MonitorService.cs`: mouse-monitor selection.
- `src/LaunchpadWindows/SystemIntegration/TrayService.cs`: tray icon and menu.
- `src/LaunchpadWindows/Presentation/RelayCommand.cs`: simple command helper.
- `src/LaunchpadWindows/Presentation/LaunchpadViewModel.cs`: overlay state, launch, close, reorder.
- `src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml`: full-screen launchpad UI.
- `src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml.cs`: overlay window behavior.
- `src/LaunchpadWindows/Presentation/SettingsViewModel.cs`: settings commands.
- `src/LaunchpadWindows/Presentation/SettingsWindow.xaml`: settings UI.
- `src/LaunchpadWindows/Presentation/SettingsWindow.xaml.cs`: settings window wiring.
- `src/LaunchpadWindows/Presentation/IconProvider.cs`: system icon extraction with fallback.
- `src/LaunchpadWindows/Presentation/LaunchItemIconConverter.cs`: WPF converter that binds launch items to extracted system icons.
- `src/LaunchpadWindows/Presentation/AcrylicWindowHelper.cs`: Windows 11 backdrop helper.
- `tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj`: xUnit test project.
- `tests/LaunchpadWindows.Tests/Models/AppSettingsTests.cs`: settings defaults tests.
- `tests/LaunchpadWindows.Tests/Storage/JsonSettingsStoreTests.cs`: JSON persistence tests.
- `tests/LaunchpadWindows.Tests/Storage/ItemMergerTests.cs`: merge and ordering tests.
- `tests/LaunchpadWindows.Tests/Desktop/DesktopScannerTests.cs`: desktop scan tests.
- `tests/LaunchpadWindows.Tests/Shell/LauncherServiceTests.cs`: launch behavior tests.
- `tests/LaunchpadWindows.Tests/SystemIntegration/AutostartServiceTests.cs`: autostart tests with fake registry.
- `tests/LaunchpadWindows.Tests/SystemIntegration/HotkeyServiceTests.cs`: hotkey conflict tests.
- `tests/LaunchpadWindows.Tests/Presentation/LaunchpadViewModelTests.cs`: close, launch, reorder tests.
- `tests/LaunchpadWindows.Tests/Presentation/SettingsViewModelTests.cs`: settings commands tests.

---

## Task 1: Solution Skeleton

**Files:**
- Create: `global.json`
- Create: `LaunchpadWindows.sln`
- Create: `src/LaunchpadWindows/LaunchpadWindows.csproj`
- Create: `tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj`
- Modify: `src/LaunchpadWindows/App.xaml`
- Modify: `src/LaunchpadWindows/App.xaml.cs`
- Delete: `src/LaunchpadWindows/MainWindow.xaml`
- Delete: `src/LaunchpadWindows/MainWindow.xaml.cs`

- [ ] **Step 1: Create the solution and projects**

Run:

```powershell
dotnet new sln -n LaunchpadWindows
dotnet new wpf -n LaunchpadWindows -o src/LaunchpadWindows -f net10.0-windows
dotnet new xunit -n LaunchpadWindows.Tests -o tests/LaunchpadWindows.Tests -f net10.0-windows
dotnet sln LaunchpadWindows.sln add src/LaunchpadWindows/LaunchpadWindows.csproj
dotnet sln LaunchpadWindows.sln add tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj
dotnet add tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj reference src/LaunchpadWindows/LaunchpadWindows.csproj
```

Expected: both projects are created and added to the solution.

- [ ] **Step 2: Pin the SDK**

Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.102",
    "rollForward": "latestFeature"
  }
}
```

- [ ] **Step 3: Replace the WPF project file**

Replace `src/LaunchpadWindows/LaunchpadWindows.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <AssemblyName>LaunchpadWindows</AssemblyName>
    <RootNamespace>LaunchpadWindows</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Replace the generated app entry**

Replace `src/LaunchpadWindows/App.xaml`:

```xml
<Application x:Class="LaunchpadWindows.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources />
</Application>
```

Replace `src/LaunchpadWindows/App.xaml.cs`:

```csharp
using System.Windows;

namespace LaunchpadWindows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
}
```

Delete generated `src/LaunchpadWindows/MainWindow.xaml` and `src/LaunchpadWindows/MainWindow.xaml.cs`.

- [ ] **Step 5: Verify the blank app builds**

Run:

```powershell
dotnet build LaunchpadWindows.sln
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 6: Commit**

Run:

```powershell
git add global.json LaunchpadWindows.sln src/LaunchpadWindows tests/LaunchpadWindows.Tests
git commit -m "chore: scaffold WPF launchpad solution"
```

---

## Task 2: Models And Settings Defaults

**Files:**
- Create: `src/LaunchpadWindows/Models/AppSettings.cs`
- Create: `src/LaunchpadWindows/Models/HotkeyGesture.cs`
- Create: `src/LaunchpadWindows/Models/LaunchItem.cs`
- Create: `tests/LaunchpadWindows.Tests/Models/AppSettingsTests.cs`

- [ ] **Step 1: Write failing model tests**

Create `tests/LaunchpadWindows.Tests/Models/AppSettingsTests.cs`:

```csharp
using LaunchpadWindows.Models;

namespace LaunchpadWindows.Tests.Models;

public sealed class AppSettingsTests
{
    [Fact]
    public void CreateDefault_UsesCtrlAltSpaceAndAutostartOff()
    {
        AppSettings settings = AppSettings.CreateDefault();

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
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter AppSettingsTests
```

Expected: compile fails because `LaunchpadWindows.Models` does not exist.

- [ ] **Step 3: Add model code**

Create `src/LaunchpadWindows/Models/HotkeyGesture.cs`:

```csharp
namespace LaunchpadWindows.Models;

public sealed record HotkeyGesture(bool Control, bool Alt, bool Shift, bool Windows, string Key)
{
    public static HotkeyGesture Default { get; } = new(Control: true, Alt: true, Shift: false, Windows: false, Key: "Space");

    public override string ToString()
    {
        List<string> parts = [];
        if (Control) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Windows) parts.Add("Win");
        parts.Add(Key);
        return string.Join(" + ", parts);
    }
}
```

Create `src/LaunchpadWindows/Models/LaunchItem.cs`:

```csharp
namespace LaunchpadWindows.Models;

public enum LaunchItemSource
{
    DesktopScan,
    Manual
}

public enum LaunchItemKind
{
    Shortcut,
    Url,
    File,
    Folder
}

public sealed record LaunchItem(
    string Id,
    string DisplayName,
    LaunchItemSource Source,
    LaunchItemKind Kind,
    string PathOrUrl,
    string? ResolvedTargetPath,
    string IconCacheKey,
    DateTimeOffset? LastSeenAt)
{
    public static LaunchItem CreateManual(string displayName, LaunchItemKind kind, string pathOrUrl)
    {
        string id = $"manual:{Guid.NewGuid():N}";
        return new LaunchItem(id, displayName, LaunchItemSource.Manual, kind, pathOrUrl, null, pathOrUrl, null);
    }
}
```

Create `src/LaunchpadWindows/Models/AppSettings.cs`:

```csharp
namespace LaunchpadWindows.Models;

public sealed class AppSettings
{
    public HotkeyGesture Hotkey { get; set; } = HotkeyGesture.Default;
    public bool AutostartEnabled { get; set; }
    public List<string> OrderedItemIds { get; set; } = [];
    public List<LaunchItem> ManualItems { get; set; } = [];
    public List<string> HiddenDesktopItemIds { get; set; } = [];
    public TimeSpan FadeDuration { get; set; } = TimeSpan.FromMilliseconds(180);
    public double SettingsWindowWidth { get; set; } = 720;
    public double SettingsWindowHeight { get; set; } = 520;

    public static AppSettings CreateDefault() => new();
}
```

- [ ] **Step 4: Run tests and verify pass**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter AppSettingsTests
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/LaunchpadWindows/Models tests/LaunchpadWindows.Tests/Models
git commit -m "feat: add launchpad settings and item models"
```

---

## Task 3: JSON Settings Store

**Files:**
- Create: `src/LaunchpadWindows/Storage/JsonSettingsStore.cs`
- Create: `tests/LaunchpadWindows.Tests/Storage/JsonSettingsStoreTests.cs`

- [ ] **Step 1: Write failing persistence tests**

Create `tests/LaunchpadWindows.Tests/Storage/JsonSettingsStoreTests.cs`:

```csharp
using LaunchpadWindows.Models;
using LaunchpadWindows.Storage;

namespace LaunchpadWindows.Tests.Storage;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_CreatesDefaultWhenFileIsMissing()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        JsonSettingsStore store = new(path);

        AppSettings settings = await store.LoadAsync();

        Assert.Equal("Ctrl + Alt + Space", settings.Hotkey.ToString());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task SaveAsync_RoundTripsManualItems()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        JsonSettingsStore store = new(path);
        AppSettings settings = AppSettings.CreateDefault();
        settings.ManualItems.Add(LaunchItem.CreateManual("Docs", LaunchItemKind.Folder, @"C:\Docs"));

        await store.SaveAsync(settings);
        AppSettings loaded = await store.LoadAsync();

        Assert.Single(loaded.ManualItems);
        Assert.Equal("Docs", loaded.ManualItems[0].DisplayName);
    }

    [Fact]
    public async Task LoadAsync_BacksUpCorruptJsonAndReturnsDefault()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, "{ broken json");
        JsonSettingsStore store = new(path);

        AppSettings settings = await store.LoadAsync();

        Assert.Equal("Ctrl + Alt + Space", settings.Hotkey.ToString());
        Assert.Contains(Directory.EnumerateFiles(directory), file => Path.GetFileName(file).StartsWith("settings.json.corrupt-", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter JsonSettingsStoreTests
```

Expected: compile fails because `JsonSettingsStore` does not exist.

- [ ] **Step 3: Implement JSON settings store**

Create `src/LaunchpadWindows/Storage/JsonSettingsStore.cs`:

```csharp
using System.Text.Json;
using LaunchpadWindows.Models;

namespace LaunchpadWindows.Storage;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath;

    public JsonSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

        if (!File.Exists(_settingsPath))
        {
            AppSettings defaults = AppSettings.CreateDefault();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        try
        {
            await using FileStream stream = File.OpenRead(_settingsPath);
            AppSettings? settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken);
            return settings ?? AppSettings.CreateDefault();
        }
        catch (JsonException)
        {
            string backupPath = $"{_settingsPath}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            File.Move(_settingsPath, backupPath, overwrite: false);
            AppSettings defaults = AppSettings.CreateDefault();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        string tempPath = $"{_settingsPath}.tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken);
        }
        File.Move(tempPath, _settingsPath, overwrite: true);
    }

    public static string GetDefaultSettingsPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "LaunchpadWindows", "settings.json");
    }
}
```

- [ ] **Step 4: Run tests and verify pass**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter JsonSettingsStoreTests
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/LaunchpadWindows/Storage/JsonSettingsStore.cs tests/LaunchpadWindows.Tests/Storage/JsonSettingsStoreTests.cs
git commit -m "feat: persist launchpad settings as json"
```

---

## Task 4: Desktop Scanner

**Files:**
- Create: `src/LaunchpadWindows/Desktop/DesktopScanner.cs`
- Create: `src/LaunchpadWindows/Desktop/FileSystemDesktopReader.cs`
- Create: `src/LaunchpadWindows/Shortcuts/IShortcutResolver.cs`
- Create: `src/LaunchpadWindows/Shortcuts/ShellShortcutResolver.cs`
- Create: `tests/LaunchpadWindows.Tests/Desktop/DesktopScannerTests.cs`

- [ ] **Step 1: Write failing scanner tests**

Create `tests/LaunchpadWindows.Tests/Desktop/DesktopScannerTests.cs`:

```csharp
using LaunchpadWindows.Desktop;
using LaunchpadWindows.Models;
using LaunchpadWindows.Shortcuts;

namespace LaunchpadWindows.Tests.Desktop;

public sealed class DesktopScannerTests
{
    [Fact]
    public void Scan_ReturnsTopLevelDesktopEntriesOnly()
    {
        FakeDesktopReader reader = new([
            new DesktopEntry(@"C:\Users\hp\Desktop\App.lnk", "App.lnk", IsDirectory: false),
            new DesktopEntry(@"C:\Users\hp\Desktop\Web.url", "Web.url", IsDirectory: false),
            new DesktopEntry(@"C:\Users\hp\Desktop\Docs", "Docs", IsDirectory: true),
            new DesktopEntry(@"C:\Users\hp\Desktop\note.txt", "note.txt", IsDirectory: false)
        ]);
        DesktopScanner scanner = new(reader, new FakeShortcutResolver());

        IReadOnlyList<LaunchItem> items = scanner.Scan(@"C:\Users\hp\Desktop", DateTimeOffset.Parse("2026-06-22T00:00:00Z"));

        Assert.Collection(items,
            item => Assert.Equal(LaunchItemKind.Shortcut, item.Kind),
            item => Assert.Equal(LaunchItemKind.Url, item.Kind),
            item => Assert.Equal(LaunchItemKind.Folder, item.Kind),
            item => Assert.Equal(LaunchItemKind.File, item.Kind));
        Assert.All(items, item => Assert.StartsWith("desktop:", item.Id));
    }

    [Fact]
    public void Scan_UsesFriendlyNamesForShortcutsAndUrls()
    {
        FakeDesktopReader reader = new([
            new DesktopEntry(@"C:\Users\hp\Desktop\App.lnk", "App.lnk", IsDirectory: false),
            new DesktopEntry(@"C:\Users\hp\Desktop\Site.url", "Site.url", IsDirectory: false)
        ]);
        DesktopScanner scanner = new(reader, new FakeShortcutResolver());

        IReadOnlyList<LaunchItem> items = scanner.Scan(@"C:\Users\hp\Desktop", DateTimeOffset.UtcNow);

        Assert.Equal(["App", "Site"], items.Select(item => item.DisplayName).ToArray());
    }

    private sealed class FakeDesktopReader(IReadOnlyList<DesktopEntry> entries) : IDesktopReader
    {
        public IReadOnlyList<DesktopEntry> ReadTopLevelEntries(string desktopPath) => entries;
    }

    private sealed class FakeShortcutResolver : IShortcutResolver
    {
        public ShortcutResolution Resolve(string shortcutPath) => new(shortcutPath + ".target");
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter DesktopScannerTests
```

Expected: compile fails because scanner types do not exist.

- [ ] **Step 3: Implement scanner boundaries and scanner**

Create `src/LaunchpadWindows/Desktop/DesktopScanner.cs`:

```csharp
using LaunchpadWindows.Models;
using LaunchpadWindows.Shortcuts;

namespace LaunchpadWindows.Desktop;

public sealed record DesktopEntry(string FullPath, string Name, bool IsDirectory);

public interface IDesktopReader
{
    IReadOnlyList<DesktopEntry> ReadTopLevelEntries(string desktopPath);
}

public sealed class DesktopScanner
{
    private readonly IDesktopReader _reader;
    private readonly IShortcutResolver _shortcutResolver;

    public DesktopScanner(IDesktopReader reader, IShortcutResolver shortcutResolver)
    {
        _reader = reader;
        _shortcutResolver = shortcutResolver;
    }

    public IReadOnlyList<LaunchItem> Scan(string desktopPath, DateTimeOffset scannedAt)
    {
        return _reader.ReadTopLevelEntries(desktopPath)
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(entry => ToLaunchItem(entry, scannedAt))
            .ToList();
    }

    private LaunchItem ToLaunchItem(DesktopEntry entry, DateTimeOffset scannedAt)
    {
        string extension = Path.GetExtension(entry.Name);
        LaunchItemKind kind = entry.IsDirectory
            ? LaunchItemKind.Folder
            : extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                ? LaunchItemKind.Shortcut
                : extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
                    ? LaunchItemKind.Url
                    : LaunchItemKind.File;

        ShortcutResolution? resolution = kind is LaunchItemKind.Shortcut or LaunchItemKind.Url
            ? _shortcutResolver.Resolve(entry.FullPath)
            : null;

        string displayName = kind is LaunchItemKind.Shortcut or LaunchItemKind.Url
            ? Path.GetFileNameWithoutExtension(entry.Name)
            : entry.Name;

        string normalizedPath = Path.GetFullPath(entry.FullPath).ToUpperInvariant();
        string id = $"desktop:{normalizedPath}";

        return new LaunchItem(id, displayName, LaunchItemSource.DesktopScan, kind, entry.FullPath, resolution?.TargetPath, entry.FullPath, scannedAt);
    }
}
```

Create `src/LaunchpadWindows/Desktop/FileSystemDesktopReader.cs`:

```csharp
namespace LaunchpadWindows.Desktop;

public sealed class FileSystemDesktopReader : IDesktopReader
{
    public IReadOnlyList<DesktopEntry> ReadTopLevelEntries(string desktopPath)
    {
        if (!Directory.Exists(desktopPath))
        {
            return [];
        }

        return Directory.EnumerateFileSystemEntries(desktopPath, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DesktopEntry(path, Path.GetFileName(path), Directory.Exists(path)))
            .ToList();
    }
}
```

Create `src/LaunchpadWindows/Shortcuts/IShortcutResolver.cs`:

```csharp
namespace LaunchpadWindows.Shortcuts;

public sealed record ShortcutResolution(string? TargetPath);

public interface IShortcutResolver
{
    ShortcutResolution Resolve(string shortcutPath);
}
```

Create `src/LaunchpadWindows/Shortcuts/ShellShortcutResolver.cs`:

```csharp
using System.Runtime.InteropServices;

namespace LaunchpadWindows.Shortcuts;

public sealed class ShellShortcutResolver : IShortcutResolver
{
    public ShortcutResolution Resolve(string shortcutPath)
    {
        if (Path.GetExtension(shortcutPath).Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            string? url = File.ReadLines(shortcutPath)
                .FirstOrDefault(line => line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
            return new ShortcutResolution(url is null ? null : url[4..]);
        }

        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return new ShortcutResolution(null);
        }

        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcutPath]);
            string? targetPath = shortcut?.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            return new ShortcutResolution(string.IsNullOrWhiteSpace(targetPath) ? null : targetPath);
        }
        catch (COMException)
        {
            return new ShortcutResolution(null);
        }
        finally
        {
            if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
```

- [ ] **Step 4: Run tests and verify pass**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter DesktopScannerTests
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/LaunchpadWindows/Desktop src/LaunchpadWindows/Shortcuts tests/LaunchpadWindows.Tests/Desktop
git commit -m "feat: scan current user desktop entries"
```

---

## Task 5: Item Merge Rules

**Files:**
- Create: `src/LaunchpadWindows/Storage/ItemMerger.cs`
- Create: `tests/LaunchpadWindows.Tests/Storage/ItemMergerTests.cs`

- [ ] **Step 1: Write failing merge tests**

Create `tests/LaunchpadWindows.Tests/Storage/ItemMergerTests.cs`:

```csharp
using LaunchpadWindows.Models;
using LaunchpadWindows.Storage;

namespace LaunchpadWindows.Tests.Storage;

public sealed class ItemMergerTests
{
    [Fact]
    public void Merge_KeepsSavedOrderAndAppendsNewItems()
    {
        LaunchItem first = Desktop("desktop:A", "A");
        LaunchItem second = Desktop("desktop:B", "B");
        LaunchItem third = Desktop("desktop:C", "C");
        AppSettings settings = AppSettings.CreateDefault();
        settings.OrderedItemIds.AddRange(["desktop:B", "desktop:A"]);

        IReadOnlyList<LaunchItem> merged = ItemMerger.Merge(settings, [first, second, third]);

        Assert.Equal(["B", "A", "C"], merged.Select(item => item.DisplayName).ToArray());
    }

    [Fact]
    public void Merge_ExcludesHiddenDesktopItemsButKeepsManualItems()
    {
        LaunchItem scanned = Desktop("desktop:A", "A");
        LaunchItem manual = LaunchItem.CreateManual("Manual", LaunchItemKind.File, @"C:\Manual.txt");
        AppSettings settings = AppSettings.CreateDefault();
        settings.HiddenDesktopItemIds.Add("desktop:A");
        settings.ManualItems.Add(manual);

        IReadOnlyList<LaunchItem> merged = ItemMerger.Merge(settings, [scanned]);

        Assert.Single(merged);
        Assert.Equal("Manual", merged[0].DisplayName);
    }

    private static LaunchItem Desktop(string id, string name) =>
        new(id, name, LaunchItemSource.DesktopScan, LaunchItemKind.File, @$"C:\Users\hp\Desktop\{name}", null, name, DateTimeOffset.UtcNow);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter ItemMergerTests
```

Expected: compile fails because `ItemMerger` does not exist.

- [ ] **Step 3: Implement item merger**

Create `src/LaunchpadWindows/Storage/ItemMerger.cs`:

```csharp
using LaunchpadWindows.Models;

namespace LaunchpadWindows.Storage;

public static class ItemMerger
{
    public static IReadOnlyList<LaunchItem> Merge(AppSettings settings, IReadOnlyList<LaunchItem> scannedItems)
    {
        Dictionary<string, LaunchItem> available = scannedItems
            .Where(item => !settings.HiddenDesktopItemIds.Contains(item.Id, StringComparer.OrdinalIgnoreCase))
            .Concat(settings.ManualItems)
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<LaunchItem> ordered = [];
        foreach (string id in settings.OrderedItemIds)
        {
            if (available.Remove(id, out LaunchItem? item))
            {
                ordered.Add(item);
            }
        }

        ordered.AddRange(available.Values.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase));

        settings.OrderedItemIds = ordered.Select(item => item.Id).ToList();
        return ordered;
    }
}
```

- [ ] **Step 4: Run tests and verify pass**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter ItemMergerTests
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/LaunchpadWindows/Storage/ItemMerger.cs tests/LaunchpadWindows.Tests/Storage/ItemMergerTests.cs
git commit -m "feat: merge scanned and manual launch items"
```

---

## Task 6: Launcher Service

**Files:**
- Create: `src/LaunchpadWindows/Shell/LauncherService.cs`
- Create: `tests/LaunchpadWindows.Tests/Shell/LauncherServiceTests.cs`

- [ ] **Step 1: Write failing launcher tests**

Create `tests/LaunchpadWindows.Tests/Shell/LauncherServiceTests.cs`:

```csharp
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

    private sealed class FakeProcessStarter : IProcessStarter
    {
        public List<string> StartedPaths { get; } = [];
        public void Start(string pathOrUrl) => StartedPaths.Add(pathOrUrl);
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter LauncherServiceTests
```

Expected: compile fails because `LauncherService` does not exist.

- [ ] **Step 3: Implement launcher service**

Create `src/LaunchpadWindows/Shell/LauncherService.cs`:

```csharp
using System.Diagnostics;
using LaunchpadWindows.Models;

namespace LaunchpadWindows.Shell;

public sealed record LaunchResult(bool Success, string Message)
{
    public static LaunchResult Ok() => new(true, "");
    public static LaunchResult Fail(string message) => new(false, message);
}

public interface IProcessStarter
{
    void Start(string pathOrUrl);
}

public sealed class ShellProcessStarter : IProcessStarter
{
    public void Start(string pathOrUrl)
    {
        Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
    }
}

public sealed class LauncherService
{
    private readonly IProcessStarter _processStarter;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _directoryExists;

    public LauncherService(IProcessStarter processStarter, Func<string, bool>? fileExists = null, Func<string, bool>? directoryExists = null)
    {
        _processStarter = processStarter;
        _fileExists = fileExists ?? File.Exists;
        _directoryExists = directoryExists ?? Directory.Exists;
    }

    public LaunchResult Launch(LaunchItem item)
    {
        if (item.Kind != LaunchItemKind.Url && !_fileExists(item.PathOrUrl) && !_directoryExists(item.PathOrUrl))
        {
            return LaunchResult.Fail($"Path does not exist: {item.PathOrUrl}");
        }

        try
        {
            _processStarter.Start(item.PathOrUrl);
            return LaunchResult.Ok();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return LaunchResult.Fail(ex.Message);
        }
    }
}
```

- [ ] **Step 4: Run tests and verify pass**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter LauncherServiceTests
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/LaunchpadWindows/Shell tests/LaunchpadWindows.Tests/Shell
git commit -m "feat: launch items through Windows shell"
```

---

## Task 7: Autostart Service

**Files:**
- Create: `src/LaunchpadWindows/SystemIntegration/AutostartService.cs`
- Create: `tests/LaunchpadWindows.Tests/SystemIntegration/AutostartServiceTests.cs`

- [ ] **Step 1: Write failing autostart tests**

Create `tests/LaunchpadWindows.Tests/SystemIntegration/AutostartServiceTests.cs`:

```csharp
using LaunchpadWindows.SystemIntegration;

namespace LaunchpadWindows.Tests.SystemIntegration;

public sealed class AutostartServiceTests
{
    [Fact]
    public void SetEnabled_WritesQuotedExecutablePath()
    {
        FakeRunKey runKey = new();
        AutostartService service = new(runKey, () => @"C:\Apps\Launchpad Windows\LaunchpadWindows.exe");

        AutostartResult result = service.SetEnabled(true);

        Assert.True(result.Success);
        Assert.Equal("\"C:\\Apps\\Launchpad Windows\\LaunchpadWindows.exe\"", runKey.Values["LaunchpadWindows"]);
    }

    [Fact]
    public void SetEnabledFalse_RemovesValue()
    {
        FakeRunKey runKey = new();
        runKey.Values["LaunchpadWindows"] = "\"app.exe\"";
        AutostartService service = new(runKey, () => "app.exe");

        AutostartResult result = service.SetEnabled(false);

        Assert.True(result.Success);
        Assert.False(runKey.Values.ContainsKey("LaunchpadWindows"));
    }

    private sealed class FakeRunKey : IRunKey
    {
        public Dictionary<string, string> Values { get; } = [];
        public void SetValue(string name, string value) => Values[name] = value;
        public void DeleteValue(string name) => Values.Remove(name);
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter AutostartServiceTests
```

Expected: compile fails because `AutostartService` does not exist.

- [ ] **Step 3: Implement autostart service**

Create `src/LaunchpadWindows/SystemIntegration/AutostartService.cs`:

```csharp
using Microsoft.Win32;

namespace LaunchpadWindows.SystemIntegration;

public sealed record AutostartResult(bool Success, string Message)
{
    public static AutostartResult Ok() => new(true, "");
    public static AutostartResult Fail(string message) => new(false, message);
}

public interface IRunKey
{
    void SetValue(string name, string value);
    void DeleteValue(string name);
}

public sealed class RegistryRunKey : IRunKey
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public void SetValue(string name, string value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunPath, writable: true);
        key.SetValue(name, value);
    }

    public void DeleteValue(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

public sealed class AutostartService
{
    private const string RunValueName = "LaunchpadWindows";
    private readonly IRunKey _runKey;
    private readonly Func<string> _executablePathProvider;

    public AutostartService(IRunKey runKey, Func<string> executablePathProvider)
    {
        _runKey = runKey;
        _executablePathProvider = executablePathProvider;
    }

    public AutostartResult SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                _runKey.SetValue(RunValueName, Quote(_executablePathProvider()));
            }
            else
            {
                _runKey.DeleteValue(RunValueName);
            }

            return AutostartResult.Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            return AutostartResult.Fail(ex.Message);
        }
    }

    private static string Quote(string path) => $"\"{path}\"";
}
```

- [ ] **Step 4: Run tests and verify pass**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter AutostartServiceTests
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/LaunchpadWindows/SystemIntegration/AutostartService.cs tests/LaunchpadWindows.Tests/SystemIntegration/AutostartServiceTests.cs
git commit -m "feat: toggle per-user autostart"
```

---

## Task 8: Hotkey Service

**Files:**
- Create: `src/LaunchpadWindows/SystemIntegration/HotkeyService.cs`
- Create: `tests/LaunchpadWindows.Tests/SystemIntegration/HotkeyServiceTests.cs`

- [ ] **Step 1: Write failing hotkey tests**

Create `tests/LaunchpadWindows.Tests/SystemIntegration/HotkeyServiceTests.cs`:

```csharp
using LaunchpadWindows.Models;
using LaunchpadWindows.SystemIntegration;

namespace LaunchpadWindows.Tests.SystemIntegration;

public sealed class HotkeyServiceTests
{
    [Fact]
    public void Register_ReturnsConflictWhenNativeRegistrationFails()
    {
        FakeHotkeyRegistrar registrar = new(registerResult: false);
        HotkeyService service = new(registrar);

        HotkeyRegistrationResult result = service.Register(nint.Zero, HotkeyGesture.Default);

        Assert.False(result.Success);
        Assert.Contains("unavailable", result.Message);
    }

    [Fact]
    public void HandleHotkeyMessage_RaisesActivated()
    {
        FakeHotkeyRegistrar registrar = new(registerResult: true);
        HotkeyService service = new(registrar);
        int activations = 0;
        service.Activated += (_, _) => activations++;
        service.Register(nint.Zero, HotkeyGesture.Default);

        service.HandleHotkeyMessage(HotkeyService.WmHotkey, HotkeyService.DefaultHotkeyId);

        Assert.Equal(1, activations);
    }

    private sealed class FakeHotkeyRegistrar(bool registerResult) : IHotkeyRegistrar
    {
        public bool Register(nint windowHandle, int id, HotkeyModifiers modifiers, int virtualKey) => registerResult;
        public void Unregister(nint windowHandle, int id) { }
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter HotkeyServiceTests
```

Expected: compile fails because `HotkeyService` does not exist.

- [ ] **Step 3: Implement hotkey service**

Create `src/LaunchpadWindows/SystemIntegration/HotkeyService.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows.Input;
using LaunchpadWindows.Models;

namespace LaunchpadWindows.SystemIntegration;

[Flags]
public enum HotkeyModifiers
{
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

public sealed record HotkeyRegistrationResult(bool Success, string Message)
{
    public static HotkeyRegistrationResult Ok() => new(true, "");
    public static HotkeyRegistrationResult Conflict() => new(false, "Hotkey is unavailable.");
}

public interface IHotkeyRegistrar
{
    bool Register(nint windowHandle, int id, HotkeyModifiers modifiers, int virtualKey);
    void Unregister(nint windowHandle, int id);
}

public sealed class NativeHotkeyRegistrar : IHotkeyRegistrar
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    public bool Register(nint windowHandle, int id, HotkeyModifiers modifiers, int virtualKey) =>
        RegisterHotKey(windowHandle, id, (uint)modifiers, (uint)virtualKey);

    public void Unregister(nint windowHandle, int id) => UnregisterHotKey(windowHandle, id);
}

public sealed class HotkeyService
{
    public const int WmHotkey = 0x0312;
    public const int DefaultHotkeyId = 0x4C50;

    private readonly IHotkeyRegistrar _registrar;
    private nint _windowHandle;

    public HotkeyService(IHotkeyRegistrar registrar)
    {
        _registrar = registrar;
    }

    public event EventHandler? Activated;

    public HotkeyRegistrationResult Register(nint windowHandle, HotkeyGesture gesture)
    {
        _windowHandle = windowHandle;
        HotkeyModifiers modifiers = 0;
        if (gesture.Control) modifiers |= HotkeyModifiers.Control;
        if (gesture.Alt) modifiers |= HotkeyModifiers.Alt;
        if (gesture.Shift) modifiers |= HotkeyModifiers.Shift;
        if (gesture.Windows) modifiers |= HotkeyModifiers.Windows;

        int virtualKey = KeyInterop.VirtualKeyFromKey(Enum.Parse<Key>(gesture.Key));
        return _registrar.Register(windowHandle, DefaultHotkeyId, modifiers, virtualKey)
            ? HotkeyRegistrationResult.Ok()
            : HotkeyRegistrationResult.Conflict();
    }

    public void Unregister()
    {
        if (_windowHandle != nint.Zero)
        {
            _registrar.Unregister(_windowHandle, DefaultHotkeyId);
        }
    }

    public void HandleHotkeyMessage(int message, int id)
    {
        if (message == WmHotkey && id == DefaultHotkeyId)
        {
            Activated?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

- [ ] **Step 4: Run tests and verify pass**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter HotkeyServiceTests
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/LaunchpadWindows/SystemIntegration/HotkeyService.cs tests/LaunchpadWindows.Tests/SystemIntegration/HotkeyServiceTests.cs
git commit -m "feat: register global launchpad hotkey"
```

---

## Task 9: Launchpad View Model

**Files:**
- Create: `src/LaunchpadWindows/Presentation/RelayCommand.cs`
- Create: `src/LaunchpadWindows/Presentation/LaunchpadViewModel.cs`
- Create: `tests/LaunchpadWindows.Tests/Presentation/LaunchpadViewModelTests.cs`

- [ ] **Step 1: Write failing view model tests**

Create `tests/LaunchpadWindows.Tests/Presentation/LaunchpadViewModelTests.cs`:

```csharp
using LaunchpadWindows.Models;
using LaunchpadWindows.Presentation;
using LaunchpadWindows.Shell;

namespace LaunchpadWindows.Tests.Presentation;

public sealed class LaunchpadViewModelTests
{
    [Fact]
    public void LaunchCommand_ClosesAfterSuccessfulLaunch()
    {
        LaunchItem item = LaunchItem.CreateManual("File", LaunchItemKind.File, @"C:\file.txt");
        FakeLauncher launcher = new(LaunchResult.Ok());
        LaunchpadViewModel vm = new(launcher);
        bool closeRequested = false;
        vm.CloseRequested += (_, _) => closeRequested = true;
        vm.SetItems([item]);

        vm.LaunchCommand.Execute(item);

        Assert.True(closeRequested);
        Assert.Equal(item.Id, launcher.LaunchedItemIds.Single());
    }

    [Fact]
    public void Reorder_MovesItemAndRaisesOrderChanged()
    {
        LaunchItem a = LaunchItem.CreateManual("A", LaunchItemKind.File, @"C:\a.txt");
        LaunchItem b = LaunchItem.CreateManual("B", LaunchItemKind.File, @"C:\b.txt");
        LaunchpadViewModel vm = new(new FakeLauncher(LaunchResult.Ok()));
        string[]? order = null;
        vm.OrderChanged += (_, ids) => order = ids;
        vm.SetItems([a, b]);

        vm.MoveItem(fromIndex: 1, toIndex: 0);

        Assert.Equal(["B", "A"], vm.Items.Select(item => item.DisplayName).ToArray());
        Assert.Equal([b.Id, a.Id], order);
    }

    private sealed class FakeLauncher(LaunchResult result) : ILauncher
    {
        public List<string> LaunchedItemIds { get; } = [];
        public LaunchResult Launch(LaunchItem item)
        {
            LaunchedItemIds.Add(item.Id);
            return result;
        }
    }
}
```

- [ ] **Step 2: Add launcher interface to existing launcher file**

Modify `src/LaunchpadWindows/Shell/LauncherService.cs` by adding the interface and implementing it:

```csharp
public interface ILauncher
{
    LaunchResult Launch(LaunchItem item);
}

public sealed class LauncherService : ILauncher
{
    // keep the existing constructor and Launch method body
}
```

- [ ] **Step 3: Run tests and verify failure**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter LaunchpadViewModelTests
```

Expected: compile fails because presentation types do not exist.

- [ ] **Step 4: Implement command helper and view model**

Create `src/LaunchpadWindows/Presentation/RelayCommand.cs`:

```csharp
using System.Windows.Input;

namespace LaunchpadWindows.Presentation;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

Create `src/LaunchpadWindows/Presentation/LaunchpadViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LaunchpadWindows.Models;
using LaunchpadWindows.Shell;

namespace LaunchpadWindows.Presentation;

public sealed class LaunchpadViewModel : INotifyPropertyChanged
{
    private readonly ILauncher _launcher;
    private string? _errorMessage;

    public LaunchpadViewModel(ILauncher launcher)
    {
        _launcher = launcher;
        LaunchCommand = new RelayCommand(parameter => Launch((LaunchItem)parameter!));
        CloseCommand = new RelayCommand(_ => RequestClose());
    }

    public ObservableCollection<LaunchItem> Items { get; } = [];
    public ICommand LaunchCommand { get; }
    public ICommand CloseCommand { get; }
    public event EventHandler? CloseRequested;
    public event EventHandler<string[]>? OrderChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public void SetItems(IEnumerable<LaunchItem> items)
    {
        Items.Clear();
        foreach (LaunchItem item in items)
        {
            Items.Add(item);
        }
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0 || fromIndex >= Items.Count || toIndex >= Items.Count)
        {
            return;
        }

        Items.Move(fromIndex, toIndex);
        OrderChanged?.Invoke(this, Items.Select(item => item.Id).ToArray());
    }

    private void Launch(LaunchItem item)
    {
        LaunchResult result = _launcher.Launch(item);
        if (result.Success)
        {
            RequestClose();
        }
        else
        {
            ErrorMessage = result.Message;
        }
    }

    private void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

- [ ] **Step 5: Run tests and verify pass**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter LaunchpadViewModelTests
```

Expected: `Passed!`.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src/LaunchpadWindows/Shell/LauncherService.cs src/LaunchpadWindows/Presentation tests/LaunchpadWindows.Tests/Presentation
git commit -m "feat: add launchpad view model"
```

---

## Task 10: Settings View Model

**Files:**
- Create: `src/LaunchpadWindows/Presentation/SettingsViewModel.cs`
- Create: `tests/LaunchpadWindows.Tests/Presentation/SettingsViewModelTests.cs`

- [ ] **Step 1: Write failing settings view model tests**

Create `tests/LaunchpadWindows.Tests/Presentation/SettingsViewModelTests.cs`:

```csharp
using LaunchpadWindows.Models;
using LaunchpadWindows.Presentation;
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
    public void ToggleAutostart_OnlyUpdatesSettingWhenRegistryWriteSucceeds()
    {
        AppSettings settings = AppSettings.CreateDefault();
        SettingsViewModel vm = new(settings, new FakeAutostart(), @"C:\Users\hp\Desktop", []);

        vm.SetAutostart(true);

        Assert.True(settings.AutostartEnabled);
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

    private sealed class FakeAutostart : IAutostartService
    {
        public AutostartResult SetEnabled(bool enabled) => AutostartResult.Ok();
    }
}
```

- [ ] **Step 2: Modify autostart service to expose an interface**

Modify `src/LaunchpadWindows/SystemIntegration/AutostartService.cs`:

```csharp
public interface IAutostartService
{
    AutostartResult SetEnabled(bool enabled);
}

public sealed class AutostartService : IAutostartService
{
    // keep the existing constructor and SetEnabled method body
}
```

- [ ] **Step 3: Run tests and verify failure**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter SettingsViewModelTests
```

Expected: compile fails because `SettingsViewModel` does not exist.

- [ ] **Step 4: Implement settings view model**

Create `src/LaunchpadWindows/Presentation/SettingsViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using LaunchpadWindows.Models;
using LaunchpadWindows.SystemIntegration;

namespace LaunchpadWindows.Presentation;

public sealed class SettingsViewModel
{
    private readonly AppSettings _settings;
    private readonly IAutostartService _autostartService;
    private readonly List<LaunchItem> _allDesktopItems;

    public SettingsViewModel(AppSettings settings, IAutostartService autostartService, string desktopPath, IReadOnlyList<LaunchItem> desktopItems)
    {
        _settings = settings;
        _autostartService = autostartService;
        _allDesktopItems = desktopItems.ToList();
        DesktopPath = desktopPath;
        foreach (LaunchItem item in settings.ManualItems)
        {
            ManualItems.Add(item);
        }

        foreach (string id in settings.HiddenDesktopItemIds)
        {
            HiddenDesktopItemIds.Add(id);
        }

        foreach (LaunchItem item in _allDesktopItems.Where(item => !settings.HiddenDesktopItemIds.Contains(item.Id, StringComparer.OrdinalIgnoreCase)))
        {
            VisibleDesktopItems.Add(item);
        }
    }

    public string DesktopPath { get; }
    public string HotkeyText => _settings.Hotkey.ToString();
    public ObservableCollection<LaunchItem> ManualItems { get; } = [];
    public ObservableCollection<LaunchItem> VisibleDesktopItems { get; } = [];
    public ObservableCollection<string> HiddenDesktopItemIds { get; } = [];
    public string? ErrorMessage { get; private set; }
    public event EventHandler? SaveRequested;

    public void AddManualItem(string displayName, LaunchItemKind kind, string pathOrUrl)
    {
        LaunchItem item = LaunchItem.CreateManual(displayName, kind, pathOrUrl);
        _settings.ManualItems.Add(item);
        ManualItems.Add(item);
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveManualItem(string itemId)
    {
        _settings.ManualItems.RemoveAll(item => item.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        LaunchItem? item = ManualItems.FirstOrDefault(item => item.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            ManualItems.Remove(item);
        }
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void HideDesktopItem(string itemId)
    {
        if (!_settings.HiddenDesktopItemIds.Contains(itemId, StringComparer.OrdinalIgnoreCase))
        {
            _settings.HiddenDesktopItemIds.Add(itemId);
            HiddenDesktopItemIds.Add(itemId);
            LaunchItem? item = VisibleDesktopItems.FirstOrDefault(item => item.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
            if (item is not null)
            {
                VisibleDesktopItems.Remove(item);
            }
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RestoreHiddenDesktopItem(string itemId)
    {
        _settings.HiddenDesktopItemIds.RemoveAll(id => id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        HiddenDesktopItemIds.Remove(itemId);
        LaunchItem? item = _allDesktopItems.FirstOrDefault(item => item.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        if (item is not null && !VisibleDesktopItems.Any(existing => existing.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase)))
        {
            VisibleDesktopItems.Add(item);
        }
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetAutostart(bool enabled)
    {
        AutostartResult result = _autostartService.SetEnabled(enabled);
        if (result.Success)
        {
            _settings.AutostartEnabled = enabled;
            ErrorMessage = null;
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            ErrorMessage = result.Message;
        }
    }
}
```

- [ ] **Step 5: Run tests and verify pass**

Run:

```powershell
dotnet test tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj --filter SettingsViewModelTests
```

Expected: `Passed!`.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src/LaunchpadWindows/SystemIntegration/AutostartService.cs src/LaunchpadWindows/Presentation/SettingsViewModel.cs tests/LaunchpadWindows.Tests/Presentation/SettingsViewModelTests.cs
git commit -m "feat: add settings view model"
```

---

## Task 11: WPF Launchpad And Settings Windows

**Files:**
- Create: `src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml`
- Create: `src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml.cs`
- Create: `src/LaunchpadWindows/Presentation/SettingsWindow.xaml`
- Create: `src/LaunchpadWindows/Presentation/SettingsWindow.xaml.cs`
- Create: `src/LaunchpadWindows/Presentation/AcrylicWindowHelper.cs`

- [ ] **Step 1: Create acrylic helper**

Create `src/LaunchpadWindows/Presentation/AcrylicWindowHelper.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LaunchpadWindows.Presentation;

public static class AcrylicWindowHelper
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmSystemBackdropTransientWindow = 3;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int attributeValue, int attributeSize);

    public static void Apply(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == nint.Zero)
        {
            return;
        }

        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

        int backdrop = DwmSystemBackdropTransientWindow;
        DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
    }
}
```

- [ ] **Step 2: Create launchpad window XAML**

Create `src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml`:

```xml
<Window x:Class="LaunchpadWindows.Presentation.LaunchpadWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:models="clr-namespace:LaunchpadWindows.Models"
        WindowStyle="None"
        ResizeMode="NoResize"
        ShowInTaskbar="False"
        AllowsTransparency="True"
        Background="#99000000"
        Opacity="0"
        KeyDown="OnKeyDown">
    <Window.Resources>
        <Style TargetType="Button" x:Key="ItemButtonStyle">
            <Setter Property="Width" Value="112" />
            <Setter Property="Height" Value="132" />
            <Setter Property="Margin" Value="8" />
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderBrush" Value="Transparent" />
            <Setter Property="Foreground" Value="White" />
        </Style>
    </Window.Resources>
    <Grid x:Name="BackgroundSurface" Background="Transparent" MouseDown="OnWindowMouseDown">
        <ItemsControl x:Name="ItemsHost" ItemsSource="{Binding Items}" HorizontalAlignment="Center" VerticalAlignment="Center">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <WrapPanel MaxWidth="1040" />
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
                <DataTemplate DataType="{x:Type models:LaunchItem}">
                    <Button Style="{StaticResource ItemButtonStyle}"
                            Command="{Binding DataContext.LaunchCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                            CommandParameter="{Binding}"
                            AllowDrop="True"
                            PreviewMouseMove="OnItemPreviewMouseMove"
                            Drop="OnItemDrop">
                        <StackPanel>
                            <Border Width="72" Height="72" CornerRadius="18" Background="#33FFFFFF" HorizontalAlignment="Center" />
                            <TextBlock Text="{Binding DisplayName}" Margin="0,10,0,0" TextAlignment="Center" TextWrapping="Wrap" MaxHeight="38" />
                        </StackPanel>
                    </Button>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
        <TextBlock Text="{Binding ErrorMessage}" Foreground="White" Background="#CC000000" Padding="12,8" HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="0,0,0,48" />
    </Grid>
</Window>
```

- [ ] **Step 3: Create launchpad window code-behind**

Create `src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using LaunchpadWindows.Models;

namespace LaunchpadWindows.Presentation;

public partial class LaunchpadWindow : Window
{
    private LaunchpadViewModel ViewModel => (LaunchpadViewModel)DataContext;

    public LaunchpadWindow(LaunchpadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            AcrylicWindowHelper.Apply(this);
            FadeTo(1.0);
        };
    }

    public void FadeOutAndClose()
    {
        DoubleAnimation animation = new(0.0, TimeSpan.FromMilliseconds(180));
        animation.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, animation);
    }

    private void FadeTo(double opacity)
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(180)));
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            FadeOutAndClose();
        }
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, BackgroundSurface))
        {
            FadeOutAndClose();
        }
    }

    private void OnItemPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement { DataContext: LaunchItem item } element)
        {
            DragDrop.DoDragDrop(element, item, DragDropEffects.Move);
        }
    }

    private void OnItemDrop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LaunchItem target })
        {
            return;
        }

        if (e.Data.GetData(typeof(LaunchItem)) is not LaunchItem source || source.Id == target.Id)
        {
            return;
        }

        int fromIndex = ViewModel.Items.IndexOf(source);
        int toIndex = ViewModel.Items.IndexOf(target);
        ViewModel.MoveItem(fromIndex, toIndex);
    }
}
```

- [ ] **Step 4: Create settings window**

Create `src/LaunchpadWindows/Presentation/SettingsWindow.xaml`:

```xml
<Window x:Class="LaunchpadWindows.Presentation.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Launchpad Windows Settings"
        Width="720"
        Height="520"
        WindowStartupLocation="CenterScreen">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <StackPanel Orientation="Horizontal">
            <TextBlock Text="Hotkey" FontWeight="SemiBold" Width="120" />
            <TextBlock Text="{Binding HotkeyText}" />
        </StackPanel>
        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,16,0,0">
            <TextBlock Text="Desktop" FontWeight="SemiBold" Width="120" />
            <TextBlock Text="{Binding DesktopPath}" />
        </StackPanel>
        <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,16,0,0">
            <Button Content="Add File Or Shortcut" Width="150" Click="OnAddFileClick" />
            <Button Content="Add Folder" Width="110" Margin="8,0,0,0" Click="OnAddFolderClick" />
            <Button Content="Remove Manual" Width="130" Margin="8,0,0,0" Click="OnRemoveManualClick" />
        </StackPanel>
        <TabControl Grid.Row="3" Margin="0,20,0,0">
            <TabItem Header="Manual Items">
                <ListBox x:Name="ManualItemsList" ItemsSource="{Binding ManualItems}" DisplayMemberPath="DisplayName" />
            </TabItem>
            <TabItem Header="Desktop Items">
                <DockPanel>
                    <Button DockPanel.Dock="Bottom" Content="Hide Selected Desktop Item" Height="32" Margin="0,8,0,0" Click="OnHideDesktopClick" />
                    <ListBox x:Name="DesktopItemsList" ItemsSource="{Binding VisibleDesktopItems}" DisplayMemberPath="DisplayName" />
                </DockPanel>
            </TabItem>
            <TabItem Header="Hidden Desktop Items">
                <DockPanel>
                    <Button DockPanel.Dock="Bottom" Content="Restore Selected" Height="32" Margin="0,8,0,0" Click="OnRestoreHiddenClick" />
                    <ListBox x:Name="HiddenItemsList" ItemsSource="{Binding HiddenDesktopItemIds}" />
                </DockPanel>
            </TabItem>
        </TabControl>
    </Grid>
</Window>
```

Create `src/LaunchpadWindows/Presentation/SettingsWindow.xaml.cs`:

```csharp
using LaunchpadWindows.Models;
using Microsoft.Win32;
using System.Windows;
using Forms = System.Windows.Forms;

namespace LaunchpadWindows.Presentation;

public partial class SettingsWindow : Window
{
    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnAddFileClick(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Add File Or Shortcut",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            string path = dialog.FileName;
            LaunchItemKind kind = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".lnk" => LaunchItemKind.Shortcut,
                ".url" => LaunchItemKind.Url,
                _ => LaunchItemKind.File
            };
            string displayName = kind is LaunchItemKind.Shortcut or LaunchItemKind.Url
                ? Path.GetFileNameWithoutExtension(path)
                : Path.GetFileName(path);
            ViewModel.AddManualItem(displayName, kind, path);
        }
    }

    private void OnAddFolderClick(object sender, RoutedEventArgs e)
    {
        using Forms.FolderBrowserDialog dialog = new()
        {
            Description = "Add Folder"
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            ViewModel.AddManualItem(Path.GetFileName(dialog.SelectedPath), LaunchItemKind.Folder, dialog.SelectedPath);
        }
    }

    private void OnRemoveManualClick(object sender, RoutedEventArgs e)
    {
        if (ManualItemsList.SelectedItem is LaunchItem item)
        {
            ViewModel.RemoveManualItem(item.Id);
        }
    }

    private void OnHideDesktopClick(object sender, RoutedEventArgs e)
    {
        if (DesktopItemsList.SelectedItem is LaunchItem item)
        {
            ViewModel.HideDesktopItem(item.Id);
        }
    }

    private void OnRestoreHiddenClick(object sender, RoutedEventArgs e)
    {
        if (HiddenItemsList.SelectedItem is string itemId)
        {
            ViewModel.RestoreHiddenDesktopItem(itemId);
        }
    }
}
```

- [ ] **Step 5: Build WPF windows**

Run:

```powershell
dotnet build src/LaunchpadWindows/LaunchpadWindows.csproj
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml.cs src/LaunchpadWindows/Presentation/SettingsWindow.xaml src/LaunchpadWindows/Presentation/SettingsWindow.xaml.cs src/LaunchpadWindows/Presentation/AcrylicWindowHelper.cs
git commit -m "feat: add launchpad and settings windows"
```

---

## Task 12: Monitor And Tray Integration

**Files:**
- Create: `src/LaunchpadWindows/SystemIntegration/MonitorService.cs`
- Create: `src/LaunchpadWindows/SystemIntegration/TrayService.cs`
- Modify: `src/LaunchpadWindows/App.xaml.cs`

- [ ] **Step 1: Create monitor service**

Create `src/LaunchpadWindows/SystemIntegration/MonitorService.cs`:

```csharp
using System.Windows;
using System.Windows.Forms;

namespace LaunchpadWindows.SystemIntegration;

public sealed class MonitorService
{
    public Rect GetMouseMonitorBounds()
    {
        Screen screen = Screen.FromPoint(Cursor.Position);
        return new Rect(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height);
    }
}
```

- [ ] **Step 2: Create tray service**

Create `src/LaunchpadWindows/SystemIntegration/TrayService.cs`:

```csharp
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
```

- [ ] **Step 3: Wire composition root**

Replace `src/LaunchpadWindows/App.xaml.cs`:

```csharp
using System.Reflection;
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

public partial class App : Application
{
    private JsonSettingsStore _settingsStore = null!;
    private AppSettings _settings = null!;
    private TrayService _tray = null!;
    private HotkeyService _hotkey = null!;
    private HwndSource _messageSource = null!;
    private LaunchpadWindow? _launchpadWindow;

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

    private void OpenLaunchpad()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        DesktopScanner scanner = new(new FileSystemDesktopReader(), new ShellShortcutResolver());
        IReadOnlyList<LaunchItem> scanned = scanner.Scan(desktopPath, DateTimeOffset.UtcNow);
        IReadOnlyList<LaunchItem> items = ItemMerger.Merge(_settings, scanned);
        _settingsStore.SaveAsync(_settings).GetAwaiter().GetResult();

        LaunchpadViewModel viewModel = new(new LauncherService(new ShellProcessStarter()));
        viewModel.SetItems(items);
        viewModel.CloseRequested += (_, _) => _launchpadWindow?.FadeOutAndClose();
        viewModel.OrderChanged += (_, ids) =>
        {
            _settings.OrderedItemIds = ids.ToList();
            _settingsStore.SaveAsync(_settings).GetAwaiter().GetResult();
        };

        Rect bounds = new MonitorService().GetMouseMonitorBounds();
        _launchpadWindow = new LaunchpadWindow(viewModel)
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height
        };
        _launchpadWindow.Show();
        _launchpadWindow.Activate();
    }

    private void OpenSettings()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string executablePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        DesktopScanner scanner = new(new FileSystemDesktopReader(), new ShellShortcutResolver());
        IReadOnlyList<LaunchItem> scanned = scanner.Scan(desktopPath, DateTimeOffset.UtcNow);
        SettingsViewModel viewModel = new(_settings, new AutostartService(new RegistryRunKey(), () => executablePath), desktopPath, scanned);
        viewModel.SaveRequested += (_, _) => _settingsStore.SaveAsync(_settings).GetAwaiter().GetResult();
        new SettingsWindow(viewModel).Show();
    }
}
```

- [ ] **Step 4: Build the integrated app**

Run:

```powershell
dotnet build LaunchpadWindows.sln
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/LaunchpadWindows/SystemIntegration/MonitorService.cs src/LaunchpadWindows/SystemIntegration/TrayService.cs src/LaunchpadWindows/App.xaml.cs
git commit -m "feat: wire tray hotkey and launchpad shell"
```

---

## Task 13: Icon Extraction And Visual Polish

**Files:**
- Create: `src/LaunchpadWindows/Presentation/IconProvider.cs`
- Create: `src/LaunchpadWindows/Presentation/LaunchItemIconConverter.cs`
- Modify: `src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml`

- [ ] **Step 1: Create icon provider**

Create `src/LaunchpadWindows/Presentation/IconProvider.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LaunchpadWindows.Presentation;

public sealed class IconProvider
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes, out Shfileinfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Shfileinfo
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    public ImageSource? GetIcon(string path)
    {
        nint result = SHGetFileInfo(path, 0, out Shfileinfo info, (uint)Marshal.SizeOf<Shfileinfo>(), ShgfiIcon | ShgfiLargeIcon);
        if (result == nint.Zero || info.hIcon == nint.Zero)
        {
            return null;
        }

        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(64, 64));
        }
        catch
        {
            return null;
        }
        finally
        {
            if (info.hIcon != nint.Zero)
            {
                DestroyIcon(info.hIcon);
            }
        }
    }
}
```

- [ ] **Step 2: Create WPF icon converter**

Create `src/LaunchpadWindows/Presentation/LaunchItemIconConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LaunchpadWindows.Models;

namespace LaunchpadWindows.Presentation;

public sealed class LaunchItemIconConverter : IValueConverter
{
    private static readonly IconProvider IconProvider = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LaunchItem item)
        {
            return null;
        }

        ImageSource? icon = IconProvider.GetIcon(item.PathOrUrl);
        icon?.Freeze();
        return icon;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
```

- [ ] **Step 3: Bind launchpad tiles to system icons**

Modify `src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml` resources:

```xml
<Window.Resources>
    <local:LaunchItemIconConverter x:Key="LaunchItemIconConverter" />
    <Style TargetType="Button" x:Key="ItemButtonStyle">
        <Setter Property="Width" Value="112" />
        <Setter Property="Height" Value="132" />
        <Setter Property="Margin" Value="8" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderBrush" Value="Transparent" />
        <Setter Property="Foreground" Value="White" />
    </Style>
</Window.Resources>
```

Add the local namespace to the `Window` element:

```xml
xmlns:local="clr-namespace:LaunchpadWindows.Presentation"
```

Replace the tile `StackPanel`:

```xml
<StackPanel>
    <Border Width="72" Height="72" CornerRadius="18" Background="#33FFFFFF" HorizontalAlignment="Center">
        <Image Source="{Binding Converter={StaticResource LaunchItemIconConverter}}" Width="54" Height="54" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </Border>
    <TextBlock Text="{Binding DisplayName}" Margin="0,10,0,0" TextAlignment="Center" TextWrapping="Wrap" MaxHeight="38" />
</StackPanel>
```

The fixed `Border` preserves tile layout even when icon extraction returns `null`.

- [ ] **Step 4: Build and run visual smoke test**

Run:

```powershell
dotnet build src/LaunchpadWindows/LaunchpadWindows.csproj
dotnet run --project src/LaunchpadWindows/LaunchpadWindows.csproj
```

Expected: app starts, tray icon appears, and the tray menu can open a full-screen overlay.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/LaunchpadWindows/Presentation/IconProvider.cs src/LaunchpadWindows/Presentation/LaunchItemIconConverter.cs src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml
git commit -m "feat: add icon extraction helper and visual polish"
```

---

## Task 14: Manual Verification And Release Build

**Files:**
- Modify: `docs/superpowers/plans/2026-06-22-launchpad-windows.md`

- [ ] **Step 1: Run the full automated test suite**

Run:

```powershell
dotnet test LaunchpadWindows.sln
```

Expected: all tests pass.

- [ ] **Step 2: Run the app manually**

Run:

```powershell
dotnet run --project src/LaunchpadWindows/LaunchpadWindows.csproj
```

Expected:

- tray icon appears;
- tray menu contains `Open Launchpad`, `Settings`, and `Exit`;
- `Ctrl + Alt + Space` opens the launchpad;
- pressing `Esc` closes the launchpad with fade-out.

- [ ] **Step 3: Verify desktop scan**

Create four temporary entries on the current user's Desktop:

```powershell
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
New-Item -ItemType File -Path (Join-Path $desktop 'LaunchpadTestFile.txt') -Force
New-Item -ItemType Directory -Path (Join-Path $desktop 'LaunchpadTestFolder') -Force
Set-Content -Path (Join-Path $desktop 'LaunchpadTestUrl.url') -Value "[InternetShortcut]`nURL=https://example.com"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $desktop 'LaunchpadTestApp.lnk'))
$shortcut.TargetPath = "$env:WINDIR\System32\notepad.exe"
$shortcut.Save()
```

Expected: reopening the launchpad shows `LaunchpadTestFile.txt`, `LaunchpadTestFolder`, `LaunchpadTestUrl`, and `LaunchpadTestApp`.

- [ ] **Step 4: Verify launch and close behavior**

Actions:

- click `LaunchpadTestFolder`;
- click blank area outside the grid;
- press `Ctrl + Alt + Space` while the overlay is open;
- press `Esc` while the overlay is open.

Expected:

- folder opens in File Explorer;
- overlay closes after clicking a launch item;
- blank area closes the overlay;
- repeated hotkey closes the overlay;
- `Esc` closes the overlay.

- [ ] **Step 5: Verify drag reorder persists**

Actions:

- open the launchpad;
- drag `LaunchpadTestApp` before `LaunchpadTestFile.txt`;
- close the launchpad;
- reopen the launchpad.

Expected: `LaunchpadTestApp` remains before `LaunchpadTestFile.txt` after reopening.

- [ ] **Step 6: Verify settings item management**

Actions:

- open Settings from the tray menu;
- click `Add File Or Shortcut` and select `LaunchpadTestFile.txt`;
- click `Add Folder` and select `LaunchpadTestFolder`;
- select one manual item and click `Remove Manual`;
- open the `Desktop Items` tab, select `LaunchpadTestUrl`, and click `Hide Selected Desktop Item`;
- reopen the launchpad;
- return to Settings, open `Hidden Desktop Items`, select the hidden ID, and click `Restore Selected`;
- reopen the launchpad.

Expected:

- manual file and folder entries appear in the `Manual Items` tab after adding;
- selected manual entry disappears after removal;
- hidden desktop item disappears from the launchpad after hiding;
- restored desktop item appears again after restoring.

- [ ] **Step 7: Verify autostart toggle**

Run the app, open Settings from tray, enable start at login, then check:

```powershell
Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' | Select-Object LaunchpadWindows
```

Expected: output includes a quoted path to `LaunchpadWindows.exe`.

Disable start at login, then run the same command.

Expected: `LaunchpadWindows` value is absent.

- [ ] **Step 8: Clean temporary Desktop entries**

Run:

```powershell
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
Remove-Item -LiteralPath (Join-Path $desktop 'LaunchpadTestFile.txt') -Force
Remove-Item -LiteralPath (Join-Path $desktop 'LaunchpadTestFolder') -Recurse -Force
Remove-Item -LiteralPath (Join-Path $desktop 'LaunchpadTestUrl.url') -Force
Remove-Item -LiteralPath (Join-Path $desktop 'LaunchpadTestApp.lnk') -Force
```

Expected: temporary verification entries are removed from the user Desktop.

- [ ] **Step 9: Publish release build**

Run:

```powershell
dotnet publish src/LaunchpadWindows/LaunchpadWindows.csproj -c Release -r win-x64 --self-contained false
```

Expected: publish succeeds and creates output under `src/LaunchpadWindows/bin/Release/net10.0-windows/win-x64/publish`.

- [ ] **Step 10: Commit verification notes**

Update this task's checklist in `docs/superpowers/plans/2026-06-22-launchpad-windows.md` as executed, then run:

```powershell
git add docs/superpowers/plans/2026-06-22-launchpad-windows.md
git commit -m "test: verify launchpad Windows MVP"
```

---

## Self-Review Notes

Spec coverage:

- WPF / .NET single-process app: Task 1 and Task 12.
- Windows 11 target and frosted overlay: Task 11 and Task 13.
- Current user Desktop scan only: Task 4 and Task 14.
- Manual shortcuts, files, and folders: Task 10, Task 11, and Task 14.
- `Ctrl + Alt + Space` hotkey: Task 2, Task 8, and Task 12.
- Mouse-monitor full-screen behavior: Task 12 and Task 14.
- Click item to launch and auto-close: Task 6, Task 9, and Task 14.
- Click blank area and `Esc` close: Task 11 and Task 14.
- Drag reorder persistence: Task 5, Task 9, Task 11, Task 12, and Task 14.
- Autostart via `HKCU` Run key: Task 7, Task 10, and Task 14.
- Error handling for corrupt settings, hotkey conflict, missing paths, and icon failures: Task 3, Task 6, Task 8, Task 12, and Task 13.

Type consistency:

- `LaunchItem`, `AppSettings`, and `HotkeyGesture` are defined in Task 2 and reused with the same namespace and property names.
- `ILauncher` is added before `LaunchpadViewModel` depends on it.
- `IAutostartService` is added before `SettingsViewModel` depends on it.
- `HotkeyService.DefaultHotkeyId` and `HotkeyService.WmHotkey` are defined before tests use them.
