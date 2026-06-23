namespace LaunchpadWindows.Models;

public sealed record HotkeyGesture(bool Control, bool Alt, bool Shift, bool Windows, string Key)
{
    public static HotkeyGesture Default { get; } = new(Control: true, Alt: true, Shift: false, Windows: false, Key: "Space");

    public static bool TryParse(string text, out HotkeyGesture? gesture)
    {
        gesture = null;
        string[] parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        bool control = false;
        bool alt = false;
        bool shift = false;
        bool windows = false;
        string? key = null;

        foreach (string part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                control = true;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
            }
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                windows = true;
            }
            else
            {
                if (key is not null)
                {
                    return false;
                }

                key = NormalizeKey(part);
            }
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        gesture = new HotkeyGesture(control, alt, shift, windows, key);
        return true;
    }

    public override string ToString()
    {
        List<string> parts = [];
        if (Control) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Windows) parts.Add("Win");
        parts.Add(Key);
        return string.Join(" + ", parts);
    }

    private static string NormalizeKey(string key)
    {
        string trimmed = key.Trim();
        return trimmed.Length == 1
            ? trimmed.ToUpperInvariant()
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }
}
