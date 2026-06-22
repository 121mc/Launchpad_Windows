using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using LaunchpadWindows.Shortcuts;

namespace LaunchpadWindows.Tests.Shortcuts;

public sealed class ShellShortcutResolverTests
{
    [Fact]
    public void Resolve_UrlFile_ReturnsUrlTarget()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string shortcutPath = Path.Combine(tempRoot, "site.url");
        File.WriteAllText(shortcutPath, """
            [InternetShortcut]
            URL=https://example.com
            """);
        ShellShortcutResolver resolver = new();

        try
        {
            ShortcutResolution resolution = resolver.Resolve(shortcutPath);

            Assert.Equal("https://example.com", resolution.TargetPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Resolve_MissingUrlFile_ReturnsNullTarget()
    {
        string shortcutPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.url");
        ShellShortcutResolver resolver = new();

        ShortcutResolution resolution = resolver.Resolve(shortcutPath);

        Assert.Null(resolution.TargetPath);
    }

    [Fact]
    public void Resolve_LnkFile_ReturnsShellShortcutTarget()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string shortcutPath = Path.Combine(tempRoot, "target.lnk");
        string targetPath = GetExistingShortcutTarget(tempRoot);
        CreateShellShortcut(shortcutPath, targetPath);
        ShellShortcutResolver resolver = new();

        try
        {
            ShortcutResolution resolution = resolver.Resolve(shortcutPath);

            Assert.Equal(targetPath, resolution.TargetPath, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string GetExistingShortcutTarget(string tempRoot)
    {
        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string notepadPath = Path.Combine(windowsDirectory, "System32", "notepad.exe");
        if (File.Exists(notepadPath))
        {
            return notepadPath;
        }

        string targetPath = Path.Combine(tempRoot, "target.txt");
        File.WriteAllText(targetPath, "shortcut target");
        return targetPath;
    }

    private static void CreateShellShortcut(string shortcutPath, string targetPath)
    {
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        Assert.NotNull(shellType);

        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [shortcutPath]);
            Type? shortcutType = shortcut?.GetType();
            Assert.NotNull(shortcutType);

            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [targetPath]);
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}
