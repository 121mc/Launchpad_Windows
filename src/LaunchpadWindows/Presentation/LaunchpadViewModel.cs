using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LaunchpadWindows.Models;
using LaunchpadWindows.Shell;

namespace LaunchpadWindows.Presentation;

public sealed class LaunchpadViewModel : INotifyPropertyChanged
{
    private readonly ILauncher _launcher;
    private string? _errorMessage;

    public LaunchpadViewModel(ILauncher launcher)
    {
        _launcher = launcher;
        LaunchCommand = new RelayCommand(LaunchIfPossible, parameter => parameter is LaunchItem);
        CloseCommand = new RelayCommand(_ => RequestClose());
    }

    public ObservableCollection<LaunchItem> Items { get; } = [];

    public ICommand LaunchCommand { get; }

    public ICommand CloseCommand { get; }

    public event EventHandler? CloseRequested;

    public event EventHandler<string[]>? OrderChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public void SetItems(IEnumerable<LaunchItem> items)
    {
        ErrorMessage = null;
        Items.Clear();
        foreach (LaunchItem item in items)
        {
            Items.Add(item);
        }
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0 || fromIndex >= Items.Count || toIndex >= Items.Count)
        {
            return;
        }

        Items.Move(fromIndex, toIndex);
        OrderChanged?.Invoke(this, Items.Select(item => item.Id).ToArray());
    }

    private void LaunchIfPossible(object? parameter)
    {
        if (parameter is LaunchItem item)
        {
            Launch(item);
        }
    }

    private void Launch(LaunchItem item)
    {
        LaunchResult result = _launcher.Launch(item);
        if (result.Success)
        {
            ErrorMessage = null;
            RequestClose();
        }
        else
        {
            ErrorMessage = result.Message;
        }
    }

    private void RequestClose() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
