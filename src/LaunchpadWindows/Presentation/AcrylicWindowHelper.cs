using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LaunchpadWindows.Presentation;

public static class AcrylicWindowHelper
{
    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public nint Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    private const int WcaAccentPolicy = 19;
    private const int AccentEnableBlurBehind = 3;

    /// <summary>
    /// Applies a blur-behind effect to the window.
    /// The window MUST have AllowsTransparency="True" and Background="Transparent".
    /// The visual tint is controlled by the Grid's semi-transparent Background.
    /// </summary>
    public static void Apply(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == nint.Zero)
        {
            return;
        }

        // ACCENT_ENABLE_BLURBEHIND (3) works with WS_EX_LAYERED (AllowsTransparency) windows.
        // GradientColor = 0x01000000 (nearly transparent black in AABBGGRR) - just enough to
        // activate the blur pipeline without adding a visible tint (our Grid provides the tint).
        AccentPolicy accent = new()
        {
            AccentState = AccentEnableBlurBehind,
            AccentFlags = 2,
            GradientColor = 0x01000000
        };

        int accentSize = Marshal.SizeOf<AccentPolicy>();
        nint accentPtr = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            WindowCompositionAttributeData data = new()
            {
                Attribute = WcaAccentPolicy,
                Data = accentPtr,
                SizeOfData = accentSize
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
