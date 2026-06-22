using System.IO;
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
