using System.Diagnostics;
using System.IO;
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
