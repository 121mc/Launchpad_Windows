using System.IO;
using System.Globalization;
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
            AppSettings? settings;
            await using (FileStream stream = File.OpenRead(_settingsPath))
            {
                settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken);
            }

            return IsValid(settings)
                ? settings!
                : await RecoverDefaultsAsync(cancellationToken);
        }
        catch (JsonException)
        {
            return await RecoverDefaultsAsync(cancellationToken);
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

    private async Task<AppSettings> RecoverDefaultsAsync(CancellationToken cancellationToken)
    {
        File.Move(_settingsPath, GetCorruptBackupPath(), overwrite: false);
        AppSettings defaults = AppSettings.CreateDefault();
        await SaveAsync(defaults, cancellationToken);
        return defaults;
    }

    private string GetCorruptBackupPath()
    {
        string ticks = DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture);
        string backupPath = $"{_settingsPath}.corrupt-{ticks}";

        for (int attempt = 1; File.Exists(backupPath); attempt++)
        {
            backupPath = $"{_settingsPath}.corrupt-{ticks}-{attempt.ToString(CultureInfo.InvariantCulture)}";
        }

        return backupPath;
    }

    private static bool IsValid(AppSettings? settings)
    {
        return settings is not null
            && settings.Hotkey is not null
            && settings.OrderedItemIds is not null
            && settings.ManualItems is not null
            && settings.HiddenDesktopItemIds is not null;
    }
}
