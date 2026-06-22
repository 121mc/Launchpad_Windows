using System.IO;
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
