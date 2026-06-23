using System.Runtime.InteropServices;
using System.Windows.Input;
using LaunchpadWindows.Models;

namespace LaunchpadWindows.SystemIntegration;

[Flags]
public enum HotkeyModifiers
{
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

public sealed record HotkeyRegistrationResult(bool Success, string Message)
{
    public static HotkeyRegistrationResult Ok() => new(true, "");
    public static HotkeyRegistrationResult Conflict() => new(false, "Hotkey is unavailable.");
    public static HotkeyRegistrationResult Invalid(string key) => new(false, $"Unsupported hotkey key: {key}");
}

public interface IHotkeyRegistrar
{
    bool Register(nint windowHandle, int id, HotkeyModifiers modifiers, int virtualKey);
    void Unregister(nint windowHandle, int id);
}

public sealed class NativeHotkeyRegistrar : IHotkeyRegistrar
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    public bool Register(nint windowHandle, int id, HotkeyModifiers modifiers, int virtualKey) =>
        RegisterHotKey(windowHandle, id, (uint)modifiers, (uint)virtualKey);

    public void Unregister(nint windowHandle, int id) => UnregisterHotKey(windowHandle, id);
}

public sealed class HotkeyService
{
    public const int WmHotkey = 0x0312;
    public const int DefaultHotkeyId = 0x4C50;

    private readonly IHotkeyRegistrar _registrar;
    private bool _isRegistered;
    private nint _windowHandle;
    private HotkeyGesture? _registeredGesture;

    public HotkeyService(IHotkeyRegistrar registrar)
    {
        _registrar = registrar;
    }

    public event EventHandler? Activated;

    public HotkeyRegistrationResult Register(nint windowHandle, HotkeyGesture gesture)
    {
        if (!TryBuildRegistration(gesture, out HotkeyModifiers modifiers, out int virtualKey))
        {
            return HotkeyRegistrationResult.Invalid(gesture.Key);
        }

        nint previousHandle = _windowHandle;
        HotkeyGesture? previousGesture = _registeredGesture;
        bool wasRegistered = _isRegistered;
        if (_isRegistered)
        {
            _registrar.Unregister(_windowHandle, DefaultHotkeyId);
            ClearRegistration();
        }

        if (_registrar.Register(windowHandle, DefaultHotkeyId, modifiers, virtualKey))
        {
            _windowHandle = windowHandle;
            _registeredGesture = gesture;
            _isRegistered = true;
            return HotkeyRegistrationResult.Ok();
        }

        RestorePreviousRegistration(wasRegistered, previousHandle, previousGesture);
        return HotkeyRegistrationResult.Conflict();
    }

    public void Unregister()
    {
        if (_isRegistered)
        {
            _registrar.Unregister(_windowHandle, DefaultHotkeyId);
            ClearRegistration();
        }
    }

    public void HandleHotkeyMessage(int message, int id)
    {
        if (_isRegistered && message == WmHotkey && id == DefaultHotkeyId)
        {
            Activated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RestorePreviousRegistration(bool wasRegistered, nint previousHandle, HotkeyGesture? previousGesture)
    {
        if (!wasRegistered || previousGesture is null)
        {
            ClearRegistration();
            return;
        }

        if (TryBuildRegistration(previousGesture, out HotkeyModifiers modifiers, out int virtualKey) &&
            _registrar.Register(previousHandle, DefaultHotkeyId, modifiers, virtualKey))
        {
            _windowHandle = previousHandle;
            _registeredGesture = previousGesture;
            _isRegistered = true;
            return;
        }

        ClearRegistration();
    }

    private void ClearRegistration()
    {
        _isRegistered = false;
        _windowHandle = nint.Zero;
        _registeredGesture = null;
    }

    private static bool TryBuildRegistration(HotkeyGesture gesture, out HotkeyModifiers modifiers, out int virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (gesture.Control) modifiers |= HotkeyModifiers.Control;
        if (gesture.Alt) modifiers |= HotkeyModifiers.Alt;
        if (gesture.Shift) modifiers |= HotkeyModifiers.Shift;
        if (gesture.Windows) modifiers |= HotkeyModifiers.Windows;

        if (modifiers == 0)
        {
            return false;
        }

        if (!TryGetVirtualKey(gesture.Key, out virtualKey))
        {
            return false;
        }

        return virtualKey != 0;
    }

    private static bool TryGetVirtualKey(string keyText, out int virtualKey)
    {
        virtualKey = 0;
        string normalizedKey = keyText.Trim();
        if (normalizedKey.Length == 1)
        {
            char keyChar = normalizedKey[0];
            if (keyChar is >= '0' and <= '9')
            {
                virtualKey = keyChar;
                return true;
            }

            char upperKeyChar = char.ToUpperInvariant(keyChar);
            if (upperKeyChar is >= 'A' and <= 'Z')
            {
                virtualKey = upperKeyChar;
                return true;
            }
        }

        if (normalizedKey.All(char.IsDigit))
        {
            return false;
        }

        if (!Enum.TryParse(normalizedKey, ignoreCase: true, out Key key))
        {
            return false;
        }

        virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return true;
    }
}
