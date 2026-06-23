using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LaunchpadWindows.Presentation;

public sealed class IconProvider
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiSysIconIndex = 0x000004000;

    private const int ShilJumbo = 4;      // 256x256
    private const int ShilExtraLarge = 2;  // 48x48

    private static readonly Guid IidIImageList = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes, out Shfileinfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppvObj);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern nint ImageList_GetIcon(nint himl, int i, int flags);

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

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(nint hbmImage, nint hbmMask, out int pi);
        [PreserveSig] int ReplaceIcon(int i, nint hicon, out int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, nint hbmImage, nint hbmMask);
        [PreserveSig] int AddMasked(nint hbmImage, int crMask, out int pi);
        [PreserveSig] int Draw(ref ImageListDrawParams pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out nint picon);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ImageListDrawParams
    {
        public int cbSize;
    }

    public ImageSource GetIcon(string path)
    {
        // Try to get jumbo (256x256) icon first for crisp display.
        ImageSource? jumbo = TryGetHighResIcon(path, ShilJumbo);
        if (jumbo is not null) return jumbo;

        // Fall back to extra-large (48x48).
        ImageSource? extraLarge = TryGetHighResIcon(path, ShilExtraLarge);
        if (extraLarge is not null) return extraLarge;

        // Last resort: SHGetFileInfo large icon (32x32).
        return GetLargeIcon(path);
    }

    private static ImageSource? TryGetHighResIcon(string path, int imageListId)
    {
        try
        {
            // Get the system icon index for this file.
            nint result = SHGetFileInfo(path, 0, out Shfileinfo info,
                (uint)Marshal.SizeOf<Shfileinfo>(), ShgfiSysIconIndex);
            if (result == nint.Zero) return null;

            int iconIndex = info.iIcon;

            // Clean up any icon handle from SHGetFileInfo when using SHGFI_SYSICONINDEX.
            if (info.hIcon != nint.Zero) DestroyIcon(info.hIcon);

            // Get the system image list at the requested size.
            Guid iid = IidIImageList;
            int hr = SHGetImageList(imageListId, ref iid, out IImageList imgList);
            if (hr != 0 || imgList is null) return null;

            // Extract the icon from the image list.
            hr = imgList.GetIcon(iconIndex, 0, out nint hIcon);
            if (hr != 0 || hIcon == nint.Zero) return null;

            try
            {
                BitmapSource bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource GetLargeIcon(string path)
    {
        nint result = SHGetFileInfo(path, 0, out Shfileinfo info,
            (uint)Marshal.SizeOf<Shfileinfo>(), ShgfiIcon | ShgfiLargeIcon);
        if (result == nint.Zero || info.hIcon == nint.Zero)
        {
            return GetFallbackIcon();
        }

        try
        {
            BitmapSource bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bmp.Freeze();
            return bmp;
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
        BitmapSource bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            System.Drawing.SystemIcons.Application.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        bmp.Freeze();
        return bmp;
    }
}
