using System.IO;

namespace LaunchpadWindows.Desktop;

public sealed class FileSystemDesktopReader : IDesktopReader
{
    public IReadOnlyList<DesktopEntry> ReadTopLevelEntries(string desktopPath)
    {
        if (!Directory.Exists(desktopPath))
        {
            throw new DirectoryNotFoundException($"Desktop directory was not found: {desktopPath}");
        }

        return Directory.EnumerateFileSystemEntries(desktopPath, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DesktopEntry(path, Path.GetFileName(path), Directory.Exists(path)))
            .ToList();
    }
}
