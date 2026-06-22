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
dotnet new sln -n LaunchpadWindows --format sln
dotnet new wpf -n LaunchpadWindows -o src/LaunchpadWindows -f net10.0
dotnet new xunit -n LaunchpadWindows.Tests -o tests/LaunchpadWindows.Tests -f net10.0
dotnet sln LaunchpadWindows.sln add src/LaunchpadWindows/LaunchpadWindows.csproj
dotnet sln LaunchpadWindows.sln add tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj
```

Expected: `LaunchpadWindows.sln` is created, both projects are created, and both projects are added to the solution. The WPF template emits `net10.0-windows`; the xUnit template emits `net10.0` and is replaced in Step 3 before adding the project reference.

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

- [ ] **Step 3: Replace the project files**

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

Replace `tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

Then add the app project reference after both project files target `net10.0-windows`:

```powershell
dotnet add tests/LaunchpadWindows.Tests/LaunchpadWindows.Tests.csproj reference src/LaunchpadWindows/LaunchpadWindows.csproj
```

Expected: reference is added without target framework incompatibility errors.

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

public partial class App : System.Windows.Application
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

    public static bool TryParse(string text, out HotkeyGesture? gesture)
    {
        gesture = null;
        string[] parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        bool control = false;
        bool alt = false;
        bool shift = false;
        bool windows = false;
        string? key = null;

        foreach (string part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                control = true;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
            }
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                windows = true;
            }
            else
            {
                key = NormalizeKey(part);
            }
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        gesture = new HotkeyGesture(control, alt, shift, windows, key);
        return true;
    }

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

    private static string NormalizeKey(string key)
    {
        string trimmed = key.Trim();
        return trimmed.Length == 1
            ? trimmed.ToUpperInvariant()
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
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
    public static LaunchItem CreateManual(string displayName, LaunchItemKind kind, string pathOrUrl, string? resolvedTargetPath = null)
    {
        string id = $"manual:{Guid.NewGuid():N}";
        return new LaunchItem(id, displayName, LaunchItemSource.Manual, kind, pathOrUrl, resolvedTargetPath, pathOrUrl, null);
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
    public double? SettingsWindowLeft { get; set; }
    public double? SettingsWindowTop { get; set; }

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

        Dictionary<string, LaunchItemKind> kinds = items.ToDictionary(item => item.DisplayName, item => item.Kind);
        Assert.Equal(LaunchItemKind.Shortcut, kinds["App"]);
        Assert.Equal(LaunchItemKind.Url, kinds["Web"]);
        Assert.Equal(LaunchItemKind.Folder, kinds["Docs"]);
        Assert.Equal(LaunchItemKind.File, kinds["note.txt"]);
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
using System.Runtime.InteropServices;
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

        ShortcutResolution? resolution = null;
        if (kind is LaunchItemKind.Shortcut or LaunchItemKind.Url)
        {
            try
            {
                resolution = _shortcutResolver.Resolve(entry.FullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
            {
                resolution = new ShortcutResolution(null);
            }
        }

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
            try
            {
                string? url = File.ReadLines(shortcutPath)
                    .FirstOrDefault(line => line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
                return new ShortcutResolution(url is null ? null : url[4..]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new ShortcutResolution(null);
            }
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
        string launchTarget = item.Kind == LaunchItemKind.Url && !string.IsNullOrWhiteSpace(item.ResolvedTargetPath)
            ? item.ResolvedTargetPath
            : item.PathOrUrl;

        if (item.Kind == LaunchItemKind.Shortcut &&
            !string.IsNullOrWhiteSpace(item.ResolvedTargetPath) &&
            !_fileExists(item.ResolvedTargetPath) &&
            !_directoryExists(item.ResolvedTargetPath))
        {
            return LaunchResult.Fail($"Shortcut target does not exist: {item.ResolvedTargetPath}");
        }

        if (item.Kind != LaunchItemKind.Url && !_fileExists(item.PathOrUrl) && !_directoryExists(item.PathOrUrl))
        {
            return LaunchResult.Fail($"Path does not exist: {item.PathOrUrl}");
        }

        try
        {
            _processStarter.Start(launchTarget);
            return LaunchResult.Ok();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
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

    [Fact]
    public void SetEnabled_ReturnsFailureWhenRunKeyWriteFails()
    {
        FakeRunKey runKey = new(throwOnSet: true);
        AutostartService service = new(runKey, () => @"C:\Apps\LaunchpadWindows.exe");

        AutostartResult result = service.SetEnabled(true);

        Assert.False(result.Success);
        Assert.Contains("registry denied", result.Message);
        Assert.Empty(runKey.Values);
    }

    private sealed class FakeRunKey(bool throwOnSet = false) : IRunKey
    {
        public Dictionary<string, string> Values { get; } = [];
        public void SetValue(string name, string value)
        {
            if (throwOnSet)
            {
                throw new UnauthorizedAccessException("registry denied");
            }

            Values[name] = value;
        }

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
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunPath, writable: true);
        if (key is null)
        {
            throw new InvalidOperationException("Unable to open HKCU Run key.");
        }

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
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException)
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
        FakeHotkeyRegistrar registrar = new(false);
        HotkeyService service = new(registrar);

        HotkeyRegistrationResult result = service.Register(nint.Zero, HotkeyGesture.Default);

        Assert.False(result.Success);
        Assert.Contains("unavailable", result.Message);
    }

    [Fact]
    public void HandleHotkeyMessage_RaisesActivated()
    {
        FakeHotkeyRegistrar registrar = new(true);
        HotkeyService service = new(registrar);
        int activations = 0;
        service.Activated += (_, _) => activations++;
        service.Register(nint.Zero, HotkeyGesture.Default);

        service.HandleHotkeyMessage(HotkeyService.WmHotkey, HotkeyService.DefaultHotkeyId);

        Assert.Equal(1, activations);
    }

    [Fact]
    public void Register_RestoresPreviousHotkeyWhenNewRegistrationFails()
    {
        FakeHotkeyRegistrar registrar = new(true, false, true);
        HotkeyService service = new(registrar);
        service.Register((nint)123, HotkeyGesture.Default);

        HotkeyRegistrationResult result = service.Register((nint)123, new HotkeyGesture(Control: true, Alt: true, Shift: false, Windows: false, Key: "L"));

        Assert.False(result.Success);
        Assert.Equal(3, registrar.RegisterCount);
        Assert.Equal(1, registrar.UnregisterCount);
    }

    private sealed class FakeHotkeyRegistrar(params bool[] registerResults) : IHotkeyRegistrar
    {
        private int _nextResult;
        public int RegisterCount { get; private set; }
        public int UnregisterCount { get; private set; }

        public bool Register(nint windowHandle, int id, HotkeyModifiers modifiers, int virtualKey)
        {
            RegisterCount++;
            bool result = registerResults[Math.Min(_nextResult, registerResults.Length - 1)];
            _nextResult++;
            return result;
        }

        public void Unregister(nint windowHandle, int id)
        {
            UnregisterCount++;
        }
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
    public static HotkeyRegistrationResult Invalid(string key) => new(false, $"Unsupported hotkey key: {key}");
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
    private HotkeyGesture? _registeredGesture;

    public HotkeyService(IHotkeyRegistrar registrar)
    {
        _registrar = registrar;
    }

    public event EventHandler? Activated;

    public HotkeyRegistrationResult Register(nint windowHandle, HotkeyGesture gesture)
    {
        if (!TryBuildRegistration(gesture, out HotkeyModifiers modifiers, out int virtualKey))
        {
            return HotkeyRegistrationResult.Invalid(gesture.Key);
        }

        nint previousHandle = _windowHandle;
        HotkeyGesture? previousGesture = _registeredGesture;
        if (_windowHandle != nint.Zero)
        {
            _registrar.Unregister(_windowHandle, DefaultHotkeyId);
        }

        if (_registrar.Register(windowHandle, DefaultHotkeyId, modifiers, virtualKey))
        {
            _windowHandle = windowHandle;
            _registeredGesture = gesture;
            return HotkeyRegistrationResult.Ok();
        }

        RestorePreviousRegistration(previousHandle, previousGesture);
        return HotkeyRegistrationResult.Conflict();
    }

    public void Unregister()
    {
        if (_windowHandle != nint.Zero)
        {
            _registrar.Unregister(_windowHandle, DefaultHotkeyId);
            _windowHandle = nint.Zero;
            _registeredGesture = null;
        }
    }

    public void HandleHotkeyMessage(int message, int id)
    {
        if (message == WmHotkey && id == DefaultHotkeyId)
        {
            Activated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RestorePreviousRegistration(nint previousHandle, HotkeyGesture? previousGesture)
    {
        if (previousHandle == nint.Zero || previousGesture is null)
        {
            _windowHandle = nint.Zero;
            _registeredGesture = null;
            return;
        }

        if (TryBuildRegistration(previousGesture, out HotkeyModifiers modifiers, out int virtualKey) &&
            _registrar.Register(previousHandle, DefaultHotkeyId, modifiers, virtualKey))
        {
            _windowHandle = previousHandle;
            _registeredGesture = previousGesture;
        }
    }

    private static bool TryBuildRegistration(HotkeyGesture gesture, out HotkeyModifiers modifiers, out int virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (!Enum.TryParse(gesture.Key, ignoreCase: true, out Key key))
        {
            return false;
        }

        if (gesture.Control) modifiers |= HotkeyModifiers.Control;
        if (gesture.Alt) modifiers |= HotkeyModifiers.Alt;
        if (gesture.Shift) modifiers |= HotkeyModifiers.Shift;
        if (gesture.Windows) modifiers |= HotkeyModifiers.Windows;
        virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
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

public interface ILauncher
{
    LaunchResult Launch(LaunchItem item);
}

public sealed class ShellProcessStarter : IProcessStarter
{
    public void Start(string pathOrUrl)
    {
        Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
    }
}

public sealed class LauncherService : ILauncher
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
        string launchTarget = item.Kind == LaunchItemKind.Url && !string.IsNullOrWhiteSpace(item.ResolvedTargetPath)
            ? item.ResolvedTargetPath
            : item.PathOrUrl;

        if (item.Kind == LaunchItemKind.Shortcut &&
            !string.IsNullOrWhiteSpace(item.ResolvedTargetPath) &&
            !_fileExists(item.ResolvedTargetPath) &&
            !_directoryExists(item.ResolvedTargetPath))
        {
            return LaunchResult.Fail($"Shortcut target does not exist: {item.ResolvedTargetPath}");
        }

        if (item.Kind != LaunchItemKind.Url && !_fileExists(item.PathOrUrl) && !_directoryExists(item.PathOrUrl))
        {
            return LaunchResult.Fail($"Path does not exist: {item.PathOrUrl}");
        }

        try
        {
            _processStarter.Start(launchTarget);
            return LaunchResult.Ok();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            return LaunchResult.Fail(ex.Message);
        }
    }
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
}
```

- [ ] **Step 2: Modify autostart service to expose an interface**

Modify `src/LaunchpadWindows/SystemIntegration/AutostartService.cs`:

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

public interface IAutostartService
{
    AutostartResult SetEnabled(bool enabled);
}

public sealed class RegistryRunKey : IRunKey
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public void SetValue(string name, string value)
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunPath, writable: true);
        if (key is null)
        {
            throw new InvalidOperationException("Unable to open HKCU Run key.");
        }

        key.SetValue(name, value);
    }

    public void DeleteValue(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

public sealed class AutostartService : IAutostartService
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
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException)
        {
            return AutostartResult.Fail(ex.Message);
        }
    }

    private static string Quote(string path) => $"\"{path}\"";
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
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LaunchpadWindows.Models;
using LaunchpadWindows.Shortcuts;
using LaunchpadWindows.SystemIntegration;

namespace LaunchpadWindows.Presentation;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly IAutostartService _autostartService;
    private readonly Func<HotkeyGesture, HotkeyRegistrationResult> _hotkeyUpdater;
    private readonly IShortcutResolver? _shortcutResolver;
    private readonly List<LaunchItem> _allDesktopItems;
    private string? _errorMessage;

    public SettingsViewModel(
        AppSettings settings,
        IAutostartService autostartService,
        string desktopPath,
        IReadOnlyList<LaunchItem> desktopItems,
        Func<HotkeyGesture, HotkeyRegistrationResult>? hotkeyUpdater = null,
        IShortcutResolver? shortcutResolver = null)
    {
        _settings = settings;
        _autostartService = autostartService;
        _hotkeyUpdater = hotkeyUpdater ?? (_ => HotkeyRegistrationResult.Ok());
        _shortcutResolver = shortcutResolver;
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
    public bool AutostartEnabled => _settings.AutostartEnabled;
    public double SettingsWindowWidth => _settings.SettingsWindowWidth;
    public double SettingsWindowHeight => _settings.SettingsWindowHeight;
    public double? SettingsWindowLeft => _settings.SettingsWindowLeft;
    public double? SettingsWindowTop => _settings.SettingsWindowTop;
    public ObservableCollection<LaunchItem> ManualItems { get; } = [];
    public ObservableCollection<LaunchItem> VisibleDesktopItems { get; } = [];
    public ObservableCollection<string> HiddenDesktopItemIds { get; } = [];
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public event EventHandler? SaveRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddManualItem(string displayName, LaunchItemKind kind, string pathOrUrl)
    {
        string? resolvedTargetPath = ResolveManualTarget(kind, pathOrUrl);
        LaunchItem item = LaunchItem.CreateManual(displayName, kind, pathOrUrl, resolvedTargetPath);
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

    public void SetHotkeyText(string text)
    {
        if (!HotkeyGesture.TryParse(text, out HotkeyGesture? gesture) ||
            gesture is null ||
            !Enum.TryParse<Key>(gesture.Key, ignoreCase: true, out _))
        {
            ErrorMessage = "Enter a hotkey such as Ctrl + Alt + Space.";
            return;
        }

        HotkeyRegistrationResult result = _hotkeyUpdater(gesture);
        if (!result.Success)
        {
            ErrorMessage = result.Message;
            return;
        }

        _settings.Hotkey = gesture;
        ErrorMessage = null;
        OnPropertyChanged(nameof(HotkeyText));
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetAutostart(bool enabled)
    {
        AutostartResult result = _autostartService.SetEnabled(enabled);
        if (result.Success)
        {
            _settings.AutostartEnabled = enabled;
            ErrorMessage = null;
            OnPropertyChanged(nameof(AutostartEnabled));
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            ErrorMessage = result.Message;
        }
    }

    public void SaveWindowBounds(double left, double top, double width, double height)
    {
        _settings.SettingsWindowLeft = left;
        _settings.SettingsWindowTop = top;
        _settings.SettingsWindowWidth = width;
        _settings.SettingsWindowHeight = height;
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private string? ResolveManualTarget(LaunchItemKind kind, string pathOrUrl)
    {
        if (kind is not (LaunchItemKind.Shortcut or LaunchItemKind.Url) || _shortcutResolver is null)
        {
            return null;
        }

        try
        {
            return _shortcutResolver.Resolve(pathOrUrl).TargetPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            ErrorMessage = $"Unable to resolve shortcut target: {ex.Message}";
            return null;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

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

        Margins margins = new() { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
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
        Topmost="True"
        ShowInTaskbar="False"
        Background="Transparent"
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
    <Grid x:Name="BackgroundSurface" Background="#66000000" MouseDown="OnWindowMouseDown">
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
                            <TextBlock Text="{Binding DisplayName}" Margin="0,10,0,0" TextAlignment="Center" TextWrapping="Wrap" TextTrimming="CharacterEllipsis" LineHeight="18" MaxHeight="36" />
                        </StackPanel>
                    </Button>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
        <TextBlock Text="{Binding ErrorMessage}" Foreground="White" Background="#CC000000" Padding="12,8" HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="0,0,0,48">
            <TextBlock.Style>
                <Style TargetType="TextBlock">
                    <Setter Property="Visibility" Value="Visible" />
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding ErrorMessage}" Value="{x:Null}">
                            <Setter Property="Visibility" Value="Collapsed" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding ErrorMessage}" Value="">
                            <Setter Property="Visibility" Value="Collapsed" />
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </TextBlock.Style>
        </TextBlock>
    </Grid>
</Window>
```

- [ ] **Step 3: Create launchpad window code-behind**

Create `src/LaunchpadWindows/Presentation/LaunchpadWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LaunchpadWindows.Models;

namespace LaunchpadWindows.Presentation;

public partial class LaunchpadWindow : Window
{
    private readonly TimeSpan _fadeDuration;
    private LaunchpadViewModel ViewModel => (LaunchpadViewModel)DataContext;

    public LaunchpadWindow(LaunchpadViewModel viewModel, TimeSpan fadeDuration)
    {
        _fadeDuration = fadeDuration;
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
        DoubleAnimation animation = new(0.0, _fadeDuration);
        animation.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, animation);
    }

    private void FadeTo(double opacity)
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, _fadeDuration));
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
        if (e.ChangedButton == MouseButton.Left &&
            FindAncestor<Button>(e.OriginalSource as DependencyObject) is null)
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

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
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
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <StackPanel Orientation="Horizontal">
            <TextBlock Text="Hotkey" FontWeight="SemiBold" Width="120" />
            <TextBox x:Name="HotkeyTextBox" Text="{Binding HotkeyText, Mode=OneWay}" Width="220" />
            <Button Content="Apply" Width="80" Margin="8,0,0,0" Click="OnApplyHotkeyClick" />
        </StackPanel>
        <CheckBox x:Name="AutostartCheckBox"
                  Grid.Row="1"
                  Content="Start at login"
                  IsChecked="{Binding AutostartEnabled, Mode=OneWay}"
                  Margin="120,16,0,0"
                  Click="OnAutostartClick" />
        <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,16,0,0">
            <TextBlock Text="Desktop" FontWeight="SemiBold" Width="120" />
            <TextBlock Text="{Binding DesktopPath}" TextTrimming="CharacterEllipsis" />
        </StackPanel>
        <TextBlock Grid.Row="3" Text="{Binding ErrorMessage}" Foreground="#B00020" Margin="120,12,0,0" />
        <StackPanel Grid.Row="4" Orientation="Horizontal" Margin="0,16,0,0">
            <Button Content="Add File Or Shortcut" Width="150" Click="OnAddFileClick" />
            <Button Content="Add Folder" Width="110" Margin="8,0,0,0" Click="OnAddFolderClick" />
            <Button Content="Remove Manual" Width="130" Margin="8,0,0,0" Click="OnRemoveManualClick" />
        </StackPanel>
        <TabControl Grid.Row="5" Margin="0,20,0,0">
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
using System.ComponentModel;
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
        Width = viewModel.SettingsWindowWidth;
        Height = viewModel.SettingsWindowHeight;
        if (viewModel.SettingsWindowLeft.HasValue && viewModel.SettingsWindowTop.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = viewModel.SettingsWindowLeft.Value;
            Top = viewModel.SettingsWindowTop.Value;
        }

        Closing += OnClosing;
    }

    private void OnApplyHotkeyClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SetHotkeyText(HotkeyTextBox.Text);
        HotkeyTextBox.Text = ViewModel.HotkeyText;
    }

    private void OnAutostartClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SetAutostart(AutostartCheckBox.IsChecked == true);
        AutostartCheckBox.IsChecked = ViewModel.AutostartEnabled;
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

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        ViewModel.SaveWindowBounds(Left, Top, ActualWidth, ActualHeight);
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
        IReadOnlyList<LaunchItem> scanned = ScanDesktopOrReport(desktopPath);
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
        _launchpadWindow = new LaunchpadWindow(viewModel, _settings.FadeDuration)
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
        IReadOnlyList<LaunchItem> scanned = ScanDesktopOrReport(desktopPath);
        SettingsViewModel viewModel = new(
            _settings,
            new AutostartService(new RegistryRunKey(), () => executablePath),
            desktopPath,
            scanned,
            RegisterHotkeyFromSettings,
            new ShellShortcutResolver());
        viewModel.SaveRequested += (_, _) => _settingsStore.SaveAsync(_settings).GetAwaiter().GetResult();
        new SettingsWindow(viewModel).Show();
    }

    private IReadOnlyList<LaunchItem> ScanDesktopOrReport(string desktopPath)
    {
        try
        {
            DesktopScanner scanner = new(new FileSystemDesktopReader(), new ShellShortcutResolver());
            return scanner.Scan(desktopPath, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            _tray.ShowMessage("Launchpad Windows", $"Desktop scan failed: {ex.Message}");
            return [];
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

    public ImageSource GetIcon(string path)
    {
        nint result = SHGetFileInfo(path, 0, out Shfileinfo info, (uint)Marshal.SizeOf<Shfileinfo>(), ShgfiIcon | ShgfiLargeIcon);
        if (result == nint.Zero || info.hIcon == nint.Zero)
        {
            return GetFallbackIcon();
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
            return GetFallbackIcon();
        }
        finally
        {
            if (info.hIcon != nint.Zero)
            {
                DestroyIcon(info.hIcon);
            }
        }
    }

    private static ImageSource GetFallbackIcon()
    {
        return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            System.Drawing.SystemIcons.Application.Handle,
            System.Windows.Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(64, 64));
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

        ImageSource icon = IconProvider.GetIcon(item.PathOrUrl);
        icon.Freeze();
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
    <TextBlock Text="{Binding DisplayName}" Margin="0,10,0,0" TextAlignment="Center" TextWrapping="Wrap" TextTrimming="CharacterEllipsis" LineHeight="18" MaxHeight="36" />
</StackPanel>
```

The fixed `Border` preserves tile layout, and `IconProvider` returns the standard application icon when shell icon extraction fails.

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

- [ ] **Step 3: Verify current-user Desktop scan**

Create temporary entries on the current user's Desktop:

```powershell
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
New-Item -ItemType File -Path (Join-Path $desktop 'LaunchpadTestFile.txt') -Force
New-Item -ItemType Directory -Path (Join-Path $desktop 'LaunchpadTestFolder') -Force
New-Item -ItemType File -Path (Join-Path $desktop 'LaunchpadTestVeryLongFileNameThatShouldClampToTwoLinesAndNotOverlapNeighboringItems.txt') -Force
Set-Content -Path (Join-Path $desktop 'LaunchpadTestUrl.url') -Value "[InternetShortcut]`nURL=https://example.com"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $desktop 'LaunchpadTestApp.lnk'))
$shortcut.TargetPath = "$env:WINDIR\System32\notepad.exe"
$shortcut.Save()
$brokenShortcut = $shell.CreateShortcut((Join-Path $desktop 'LaunchpadBrokenShortcut.lnk'))
$brokenShortcut.TargetPath = (Join-Path $desktop 'LaunchpadMissingTarget.exe')
$brokenShortcut.Save()
```

Expected: reopening the launchpad shows `LaunchpadTestFile.txt`, `LaunchpadTestFolder`, `LaunchpadTestUrl`, `LaunchpadTestApp`, `LaunchpadBrokenShortcut`, and the long-name file. The long-name label stays within its tile, clamps to two lines, and does not overlap neighboring tiles.

Verify the public Desktop is excluded:

```powershell
$publicDesktop = [Environment]::GetFolderPath('CommonDesktopDirectory')
$publicTestPath = Join-Path $publicDesktop 'LaunchpadPublicShouldNotAppear.txt'
try {
    New-Item -ItemType File -Path $publicTestPath -Force -ErrorAction Stop
    "created"
} catch {
    "skipped: $($_.Exception.Message)"
}
```

Expected: if the public Desktop file was created, reopening the launchpad does not show `LaunchpadPublicShouldNotAppear.txt`. If creation is denied by Windows permissions, inspect the launchpad and confirm existing public Desktop entries are not included.

- [ ] **Step 4: Verify launch and close behavior**

Actions:

- click `LaunchpadTestFolder`;
- click `LaunchpadBrokenShortcut`;
- click blank area outside the grid;
- move the mouse to another monitor, then press `Ctrl + Alt + Space`;
- press `Ctrl + Alt + Space` while the overlay is open;
- press `Esc` while the overlay is open.

Expected:

- folder opens in File Explorer;
- overlay closes after clicking a launch item;
- broken shortcut shows a lightweight error message and remains visible;
- blank area closes the overlay;
- overlay opens on the monitor containing the mouse pointer;
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
- click `Add File Or Shortcut` and select `LaunchpadTestApp.lnk`;
- click `Add File Or Shortcut` and select `LaunchpadTestFile.txt`;
- click `Add Folder` and select `LaunchpadTestFolder`;
- select one manual item and click `Remove Manual`;
- open the `Desktop Items` tab, select `LaunchpadTestUrl`, and click `Hide Selected Desktop Item`;
- reopen the launchpad;
- return to Settings, open `Hidden Desktop Items`, select the hidden ID, and click `Restore Selected`;
- resize and move the Settings window, close it, then reopen Settings;
- reopen the launchpad.

Expected:

- manual shortcut, file, and folder entries appear in the `Manual Items` tab after adding;
- clicking the manual `LaunchpadTestApp` entry launches the shortcut target through shell behavior and closes the overlay;
- selected manual entry disappears after removal;
- hidden desktop item disappears from the launchpad after hiding;
- restored desktop item appears again after restoring;
- Settings reopens at the last saved size and position.

- [ ] **Step 7: Verify hotkey editing and autostart toggle**

Run the app, open Settings from the tray menu, replace the hotkey text with `Ctrl + Shift + L`, and click `Apply`.

Expected: `Ctrl + Shift + L` opens and closes the launchpad. Change the hotkey text back to `Ctrl + Alt + Space`, click `Apply`, and confirm the default hotkey works again.

Run the app, open Settings from tray, enable start at login, then check:

```powershell
Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' | Select-Object LaunchpadWindows
```

Expected: output includes a quoted path to `LaunchpadWindows.exe`.

Disable start at login, then run the same command.

Expected: `LaunchpadWindows` value is absent.

- [ ] **Step 8: Verify visual layout and icon fallback**

Actions:

- open the launchpad at the current display scaling;
- if Windows Settings exposes multiple scaling values for the display, repeat at `100%`, `125%`, and `150%`;
- resize or move to common desktop resolutions available on the machine, including the smallest attached monitor;
- inspect `LaunchpadBrokenShortcut` and the long-name file.

Expected:

- frosted/acrylic background is visible on Windows 11;
- fade-in and fade-out remain smooth;
- icon tiles stay fixed size during hover and drag;
- the broken shortcut shows a system or fallback icon instead of an empty tile;
- long labels stay inside their tiles and do not overlap neighboring entries.

- [ ] **Step 9: Clean temporary Desktop entries**

Run:

```powershell
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
Remove-Item -LiteralPath (Join-Path $desktop 'LaunchpadTestFile.txt') -Force
Remove-Item -LiteralPath (Join-Path $desktop 'LaunchpadTestFolder') -Recurse -Force
Remove-Item -LiteralPath (Join-Path $desktop 'LaunchpadTestVeryLongFileNameThatShouldClampToTwoLinesAndNotOverlapNeighboringItems.txt') -Force
Remove-Item -LiteralPath (Join-Path $desktop 'LaunchpadTestUrl.url') -Force
Remove-Item -LiteralPath (Join-Path $desktop 'LaunchpadTestApp.lnk') -Force
Remove-Item -LiteralPath (Join-Path $desktop 'LaunchpadBrokenShortcut.lnk') -Force
$publicDesktop = [Environment]::GetFolderPath('CommonDesktopDirectory')
$publicTestPath = Join-Path $publicDesktop 'LaunchpadPublicShouldNotAppear.txt'
if (Test-Path -LiteralPath $publicTestPath) {
    Remove-Item -LiteralPath $publicTestPath -Force
}
```

Expected: temporary verification entries are removed from the user Desktop.

- [ ] **Step 10: Publish release build**

Run:

```powershell
dotnet publish src/LaunchpadWindows/LaunchpadWindows.csproj -c Release -r win-x64 --self-contained false
```

Expected: publish succeeds and creates output under `src/LaunchpadWindows/bin/Release/net10.0-windows/win-x64/publish`.

- [ ] **Step 11: Commit verification notes**

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
- Current user Desktop scan only and public Desktop exclusion: Task 4 and Task 14.
- Manual shortcuts, files, and folders, including manual shortcut target resolution: Task 2, Task 10, Task 11, and Task 14.
- `Ctrl + Alt + Space` hotkey and settings hotkey editing: Task 2, Task 8, Task 10, Task 11, Task 12, and Task 14.
- Mouse-monitor full-screen behavior: Task 12 and Task 14.
- Click item to launch, broken shortcut handling, URL launch target selection, launch failure reporting, and auto-close: Task 6, Task 9, and Task 14.
- Click blank area, item-grid whitespace, and `Esc` close: Task 11 and Task 14.
- Drag reorder persistence: Task 5, Task 9, Task 11, Task 12, and Task 14.
- Autostart via `HKCU` Run key: Task 7, Task 10, and Task 14.
- Error handling for corrupt settings, hotkey conflict, missing paths, desktop scan failures, shortcut failures, and icon failures: Task 3, Task 4, Task 6, Task 8, Task 12, and Task 13.
- Settings window size and position persistence: Task 2, Task 10, Task 11, and Task 14.
- Visual checks for long labels, fallback icons, common resolutions, and display scaling: Task 11, Task 13, and Task 14.

Type consistency:

- `LaunchItem`, `AppSettings`, and `HotkeyGesture` are defined in Task 2 and reused with the same namespace and property names.
- `ILauncher` is added before `LaunchpadViewModel` depends on it.
- `IAutostartService` is added before `SettingsViewModel` depends on it.
- `HotkeyService.DefaultHotkeyId` and `HotkeyService.WmHotkey` are defined before tests use them.
- `SettingsViewModel` receives `HotkeyRegistrationResult` from Task 8 and only persists a hotkey after the runtime registration succeeds.
