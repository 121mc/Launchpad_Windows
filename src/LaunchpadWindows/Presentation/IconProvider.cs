using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LaunchpadWindows.Presentation;

public sealed class IconProvider
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes, out Shfileinfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Shfileinfo
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    public ImageSource GetIcon(string path)
    {
        nint result = SHGetFileInfo(path, 0, out Shfileinfo info, (uint)Marshal.SizeOf<Shfileinfo>(), ShgfiIcon | ShgfiLargeIcon);
        if (result == nint.Zero || info.hIcon == nint.Zero)
        {
            return GetFallbackIcon();
        }

        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(64, 64));
        }
        catch
        {
            return GetFallbackIcon();
        }
        finally
        {
            if (info.hIcon != nint.Zero)
            {
                DestroyIcon(info.hIcon);
            }
        }
    }

    private static ImageSource GetFallbackIcon()
    {
        return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            System.Drawing.SystemIcons.Application.Handle,
            System.Windows.Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(64, 64));
    }
}
