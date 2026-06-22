using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LaunchpadWindows.Models;
using LaunchpadWindows.Shortcuts;
using LaunchpadWindows.SystemIntegration;

namespace LaunchpadWindows.Presentation;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly IAutostartService _autostartService;
    private readonly Func<HotkeyGesture, HotkeyRegistrationResult> _hotkeyUpdater;
    private readonly IShortcutResolver? _shortcutResolver;
    private readonly List<LaunchItem> _allDesktopItems;
    private string? _errorMessage;

    public SettingsViewModel(
        AppSettings settings,
        IAutostartService autostartService,
        string desktopPath,
        IReadOnlyList<LaunchItem> desktopItems,
        Func<HotkeyGesture, HotkeyRegistrationResult>? hotkeyUpdater = null,
        IShortcutResolver? shortcutResolver = null)
    {
        _settings = settings;
        _autostartService = autostartService;
        _hotkeyUpdater = hotkeyUpdater ?? (_ => HotkeyRegistrationResult.Ok());
        _shortcutResolver = shortcutResolver;
        _allDesktopItems = desktopItems.ToList();
        DesktopPath = desktopPath;

        foreach (LaunchItem item in settings.ManualItems)
        {
            ManualItems.Add(item);
        }

        foreach (string id in settings.HiddenDesktopItemIds)
        {
            HiddenDesktopItemIds.Add(id);
        }

        foreach (LaunchItem item in _allDesktopItems.Where(item => !settings.HiddenDesktopItemIds.Contains(item.Id, StringComparer.OrdinalIgnoreCase)))
        {
            VisibleDesktopItems.Add(item);
        }
    }

    public string DesktopPath { get; }

    public string HotkeyText => _settings.Hotkey.ToString();

    public bool AutostartEnabled => _settings.AutostartEnabled;

    public double SettingsWindowWidth => _settings.SettingsWindowWidth;

    public double SettingsWindowHeight => _settings.SettingsWindowHeight;

    public double? SettingsWindowLeft => _settings.SettingsWindowLeft;

    public double? SettingsWindowTop => _settings.SettingsWindowTop;

    public ObservableCollection<LaunchItem> ManualItems { get; } = [];

    public ObservableCollection<LaunchItem> VisibleDesktopItems { get; } = [];

    public ObservableCollection<string> HiddenDesktopItemIds { get; } = [];

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public event EventHandler? SaveRequested;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddManualItem(string displayName, LaunchItemKind kind, string pathOrUrl)
    {
        string? resolvedTargetPath = ResolveManualTarget(kind, pathOrUrl);
        LaunchItem item = LaunchItem.CreateManual(displayName, kind, pathOrUrl, resolvedTargetPath);
        _settings.ManualItems.Add(item);
        ManualItems.Add(item);
        RequestSave();
    }

    public void RemoveManualItem(string itemId)
    {
        _settings.ManualItems.RemoveAll(item => item.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        LaunchItem? item = ManualItems.FirstOrDefault(item => item.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            ManualItems.Remove(item);
        }

        RequestSave();
    }

    public void HideDesktopItem(string itemId)
    {
        if (_settings.HiddenDesktopItemIds.Contains(itemId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _settings.HiddenDesktopItemIds.Add(itemId);
        HiddenDesktopItemIds.Add(itemId);

        LaunchItem? item = VisibleDesktopItems.FirstOrDefault(item => item.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            VisibleDesktopItems.Remove(item);
        }

        RequestSave();
    }

    public void RestoreHiddenDesktopItem(string itemId)
    {
        _settings.HiddenDesktopItemIds.RemoveAll(id => id.Equals(itemId, StringComparison.OrdinalIgnoreCase));

        string? hiddenId = HiddenDesktopItemIds.FirstOrDefault(id => id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        if (hiddenId is not null)
        {
            HiddenDesktopItemIds.Remove(hiddenId);
        }

        LaunchItem? item = _allDesktopItems.FirstOrDefault(item => item.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        if (item is not null && !VisibleDesktopItems.Any(existing => existing.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase)))
        {
            VisibleDesktopItems.Add(item);
        }

        RequestSave();
    }

    public void SetHotkeyText(string text)
    {
        if (!HotkeyGesture.TryParse(text, out HotkeyGesture? gesture) || gesture is null || !HasModifier(gesture))
        {
            ErrorMessage = "Enter a hotkey such as Ctrl + Alt + Space.";
            return;
        }

        HotkeyRegistrationResult result = _hotkeyUpdater(gesture);
        if (!result.Success)
        {
            ErrorMessage = result.Message;
            return;
        }

        _settings.Hotkey = gesture;
        ErrorMessage = null;
        OnPropertyChanged(nameof(HotkeyText));
        RequestSave();
    }

    public void SetAutostart(bool enabled)
    {
        AutostartResult result = _autostartService.SetEnabled(enabled);
        if (!result.Success)
        {
            ErrorMessage = result.Message;
            return;
        }

        _settings.AutostartEnabled = enabled;
        ErrorMessage = null;
        OnPropertyChanged(nameof(AutostartEnabled));
        RequestSave();
    }

    public void SaveWindowBounds(double left, double top, double width, double height)
    {
        _settings.SettingsWindowLeft = left;
        _settings.SettingsWindowTop = top;
        _settings.SettingsWindowWidth = width;
        _settings.SettingsWindowHeight = height;
        RequestSave();
    }

    private string? ResolveManualTarget(LaunchItemKind kind, string pathOrUrl)
    {
        if (kind is not (LaunchItemKind.Shortcut or LaunchItemKind.Url) || _shortcutResolver is null)
        {
            return null;
        }

        try
        {
            return _shortcutResolver.Resolve(pathOrUrl).TargetPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            ErrorMessage = $"Unable to resolve shortcut target: {ex.Message}";
            return null;
        }
    }

    private static bool HasModifier(HotkeyGesture gesture) =>
        gesture.Control || gesture.Alt || gesture.Shift || gesture.Windows;

    private void RequestSave() => SaveRequested?.Invoke(this, EventArgs.Empty);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
