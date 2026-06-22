using System.IO;
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

    [Fact]
    public async Task LoadAsync_BacksUpRootNullJsonAndReturnsDefault()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, "null");
        JsonSettingsStore store = new(path);

        AppSettings settings = await store.LoadAsync();

        AssertDefaultSettings(settings);
        AssertDefaultFileWasWritten(path);
        AssertCorruptBackupExists(directory);
    }

    [Fact]
    public async Task LoadAsync_BacksUpNullCriticalMembersAndReturnsDefault()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, """
            {
              "schemaVersion": 1,
              "hotkey": null,
              "orderedItemIds": null,
              "manualItems": null,
              "hiddenDesktopItemIds": null
            }
            """);
        JsonSettingsStore store = new(path);

        AppSettings settings = await store.LoadAsync();

        AssertDefaultSettings(settings);
        AssertDefaultFileWasWritten(path);
        AssertCorruptBackupExists(directory);
    }

    private static void AssertDefaultSettings(AppSettings settings)
    {
        Assert.Equal("Ctrl + Alt + Space", settings.Hotkey?.ToString());
        Assert.NotNull(settings.OrderedItemIds);
        Assert.Empty(settings.OrderedItemIds);
        Assert.NotNull(settings.ManualItems);
        Assert.Empty(settings.ManualItems);
        Assert.NotNull(settings.HiddenDesktopItemIds);
        Assert.Empty(settings.HiddenDesktopItemIds);
    }

    private static void AssertDefaultFileWasWritten(string path)
    {
        string json = File.ReadAllText(path);

        Assert.Contains("\"hotkey\": {", json, StringComparison.Ordinal);
        Assert.Contains("\"orderedItemIds\": []", json, StringComparison.Ordinal);
        Assert.Contains("\"manualItems\": []", json, StringComparison.Ordinal);
        Assert.Contains("\"hiddenDesktopItemIds\": []", json, StringComparison.Ordinal);
    }

    private static void AssertCorruptBackupExists(string directory)
    {
        Assert.Contains(Directory.EnumerateFiles(directory), file => Path.GetFileName(file).StartsWith("settings.json.corrupt-", StringComparison.Ordinal));
    }
}
