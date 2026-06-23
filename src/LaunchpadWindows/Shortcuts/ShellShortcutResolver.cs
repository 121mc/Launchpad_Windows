using System.IO;
using System.Runtime.InteropServices;

namespace LaunchpadWindows.Shortcuts;

public sealed class ShellShortcutResolver : IShortcutResolver
{
    public ShortcutResolution Resolve(string shortcutPath)
    {
        if (Path.GetExtension(shortcutPath).Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string? url = File.ReadLines(shortcutPath)
                    .FirstOrDefault(line => line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
                return new ShortcutResolution(url is null ? null : url[4..]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new ShortcutResolution(null);
            }
        }

        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return new ShortcutResolution(null);
        }

        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcutPath]);
            string? targetPath = shortcut?.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            return new ShortcutResolution(string.IsNullOrWhiteSpace(targetPath) ? null : targetPath);
        }
        catch (Exception ex) when (IsResolverFailure(ex))
        {
            return new ShortcutResolution(null);
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static bool IsResolverFailure(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or COMException
            or InvalidOperationException
            or NotSupportedException
            or System.Reflection.TargetInvocationException
            or MemberAccessException;

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}
