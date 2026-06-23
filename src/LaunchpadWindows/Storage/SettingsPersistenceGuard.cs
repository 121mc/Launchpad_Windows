using System.IO;
using LaunchpadWindows.Models;

namespace LaunchpadWindows.Storage;

public sealed record SettingsLoadResult(AppSettings Settings, string? ErrorMessage);

public sealed class SettingsPersistenceGuard
{
    private readonly Func<CancellationToken, Task> _saveAsync;
    private readonly Action<string> _reportError;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public SettingsPersistenceGuard(Func<CancellationToken, Task> saveAsync, Action<string> reportError)
    {
        _saveAsync = saveAsync;
        _reportError = reportError;
    }

    public static async Task<SettingsLoadResult> LoadOrDefaultsAsync(
        Func<CancellationToken, Task<AppSettings>> loadAsync,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return new SettingsLoadResult(await loadAsync(cancellationToken), null);
        }
        catch (Exception ex) when (IsPersistenceFailure(ex))
        {
            return new SettingsLoadResult(
                AppSettings.CreateDefault(),
                $"Settings could not be loaded; defaults are being used: {ex.Message}");
        }
    }

    public async Task<bool> TrySaveAsync(string operationName, CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            await _saveAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (IsPersistenceFailure(ex))
        {
            _reportError($"{operationName} could not be saved: {ex.Message}");
            return false;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private static bool IsPersistenceFailure(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or InvalidOperationException
            or NotSupportedException;
}
