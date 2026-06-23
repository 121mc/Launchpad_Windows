using System.IO;
using LaunchpadWindows.Models;
using LaunchpadWindows.Storage;

namespace LaunchpadWindows.Tests.Storage;

public sealed class SettingsPersistenceGuardTests
{
    [Fact]
    public async Task LoadOrDefaultsAsync_WhenLoadThrowsPersistenceFailure_ReturnsDefaultsAndError()
    {
        SettingsLoadResult result = await SettingsPersistenceGuard.LoadOrDefaultsAsync(
            _ => Task.FromException<AppSettings>(new IOException("settings locked")));

        Assert.Equal("Ctrl + Alt + Space", result.Settings.Hotkey.ToString());
        Assert.Contains("settings locked", result.ErrorMessage);
    }

    [Fact]
    public async Task TrySaveAsync_WhenSaveThrowsPersistenceFailure_ReportsErrorAndReturnsFalse()
    {
        List<string> errors = [];
        SettingsPersistenceGuard guard = new(
            _ => throw new IOException("disk full"),
            errors.Add);

        bool saved = await guard.TrySaveAsync("Settings");

        Assert.False(saved);
        string error = Assert.Single(errors);
        Assert.Contains("Settings", error);
        Assert.Contains("disk full", error);
    }

    [Fact]
    public async Task TrySaveAsync_SerializesConcurrentSaves()
    {
        int activeSaves = 0;
        int maxActiveSaves = 0;
        SettingsPersistenceGuard guard = new(
            async _ =>
            {
                int active = Interlocked.Increment(ref activeSaves);
                UpdateMax(ref maxActiveSaves, active);
                await Task.Delay(25);
                Interlocked.Decrement(ref activeSaves);
            },
            _ => { });

        bool[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => guard.TrySaveAsync("Settings")));

        Assert.All(results, Assert.True);
        Assert.Equal(1, maxActiveSaves);
    }

    private static void UpdateMax(ref int target, int value)
    {
        int current;
        do
        {
            current = target;
            if (current >= value)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
    }
}
