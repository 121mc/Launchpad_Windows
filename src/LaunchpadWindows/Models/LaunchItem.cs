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
