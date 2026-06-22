using System.IO;
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or InvalidOperationException)
            {
                resolution = new ShortcutResolution(null);
            }
        }

        string displayName = kind is LaunchItemKind.Shortcut or LaunchItemKind.Url
            ? Path.GetFileNameWithoutExtension(entry.Name)
            : entry.Name;

        string normalizedPath = Path.GetFullPath(entry.FullPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        string id = $"desktop:{normalizedPath}";

        return new LaunchItem(id, displayName, LaunchItemSource.DesktopScan, kind, entry.FullPath, resolution?.TargetPath, entry.FullPath, scannedAt);
    }
}
