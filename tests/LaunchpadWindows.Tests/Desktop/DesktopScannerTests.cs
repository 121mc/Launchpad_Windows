using System.IO;
using LaunchpadWindows.Desktop;
using LaunchpadWindows.Models;
using LaunchpadWindows.Shortcuts;

namespace LaunchpadWindows.Tests.Desktop;

public sealed class DesktopScannerTests
{
    [Fact]
    public void Scan_ReturnsTopLevelDesktopEntriesOnly()
    {
        FakeDesktopReader reader = new([
            new DesktopEntry(@"C:\Users\hp\Desktop\App.lnk", "App.lnk", IsDirectory: false),
            new DesktopEntry(@"C:\Users\hp\Desktop\Web.url", "Web.url", IsDirectory: false),
            new DesktopEntry(@"C:\Users\hp\Desktop\Docs", "Docs", IsDirectory: true),
            new DesktopEntry(@"C:\Users\hp\Desktop\note.txt", "note.txt", IsDirectory: false)
        ]);
        DesktopScanner scanner = new(reader, new FakeShortcutResolver());

        IReadOnlyList<LaunchItem> items = scanner.Scan(@"C:\Users\hp\Desktop", DateTimeOffset.Parse("2026-06-22T00:00:00Z"));

        Dictionary<string, LaunchItemKind> kinds = items.ToDictionary(item => item.DisplayName, item => item.Kind);
        Assert.Equal(LaunchItemKind.Shortcut, kinds["App"]);
        Assert.Equal(LaunchItemKind.Url, kinds["Web"]);
        Assert.Equal(LaunchItemKind.Folder, kinds["Docs"]);
        Assert.Equal(LaunchItemKind.File, kinds["note.txt"]);
        Assert.All(items, item => Assert.StartsWith("desktop:", item.Id));
    }

    [Fact]
    public void Scan_UsesFriendlyNamesForShortcutsAndUrls()
    {
        FakeDesktopReader reader = new([
            new DesktopEntry(@"C:\Users\hp\Desktop\App.lnk", "App.lnk", IsDirectory: false),
            new DesktopEntry(@"C:\Users\hp\Desktop\Site.url", "Site.url", IsDirectory: false)
        ]);
        DesktopScanner scanner = new(reader, new FakeShortcutResolver());

        IReadOnlyList<LaunchItem> items = scanner.Scan(@"C:\Users\hp\Desktop", DateTimeOffset.UtcNow);

        Assert.Equal(["App", "Site"], items.Select(item => item.DisplayName).ToArray());
    }

    [Fact]
    public void Scan_KeepsShortcutWhenResolverFails()
    {
        FakeDesktopReader reader = new([
            new DesktopEntry(@"C:\Users\hp\Desktop\Broken.lnk", "Broken.lnk", IsDirectory: false)
        ]);
        DesktopScanner scanner = new(reader, new FakeShortcutResolver(new InvalidOperationException("bad shortcut")));

        LaunchItem item = scanner.Scan(@"C:\Users\hp\Desktop", DateTimeOffset.UtcNow).Single();

        Assert.Equal(LaunchItemKind.Shortcut, item.Kind);
        Assert.Null(item.ResolvedTargetPath);
    }

    [Fact]
    public void FileSystemReader_ThrowsWhenDesktopDirectoryIsMissing()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Desktop");
        FileSystemDesktopReader reader = new();

        Assert.Throws<DirectoryNotFoundException>(() => reader.ReadTopLevelEntries(missingPath));
    }

    [Fact]
    public void FileSystemReader_ReadTopLevelEntries_DoesNotRecurse()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string tempDesktop = Path.Combine(tempRoot, "Desktop");
        string nestedDirectory = Path.Combine(tempDesktop, "Nested");
        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(Path.Combine(tempDesktop, "top.txt"), "top");
        File.WriteAllText(Path.Combine(nestedDirectory, "nested.txt"), "nested");
        FileSystemDesktopReader reader = new();

        try
        {
            IReadOnlyList<DesktopEntry> entries = reader.ReadTopLevelEntries(tempDesktop);

            string[] names = entries.Select(entry => entry.Name).ToArray();
            Assert.Contains("top.txt", names);
            Assert.Contains("Nested", names);
            Assert.DoesNotContain("nested.txt", names);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class FakeDesktopReader(IReadOnlyList<DesktopEntry> entries) : IDesktopReader
    {
        public IReadOnlyList<DesktopEntry> ReadTopLevelEntries(string desktopPath) => entries;
    }

    private sealed class FakeShortcutResolver(Exception? exceptionToThrow = null) : IShortcutResolver
    {
        public ShortcutResolution Resolve(string shortcutPath)
        {
            if (exceptionToThrow is not null)
            {
                throw exceptionToThrow;
            }

            return new ShortcutResolution(shortcutPath + ".target");
        }
    }
}
