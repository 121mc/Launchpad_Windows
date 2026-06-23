namespace LaunchpadWindows.Shortcuts;

public sealed record ShortcutResolution(string? TargetPath);

public interface IShortcutResolver
{
    ShortcutResolution Resolve(string shortcutPath);
}
