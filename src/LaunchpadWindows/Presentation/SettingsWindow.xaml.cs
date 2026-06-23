using System.ComponentModel;
using System.IO;
using System.Windows;
using LaunchpadWindows.Models;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace LaunchpadWindows.Presentation;

public partial class SettingsWindow : Window
{
    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Width = viewModel.SettingsWindowWidth;
        Height = viewModel.SettingsWindowHeight;
        if (viewModel.SettingsWindowLeft.HasValue && viewModel.SettingsWindowTop.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = viewModel.SettingsWindowLeft.Value;
            Top = viewModel.SettingsWindowTop.Value;
        }

        Closing += OnClosing;
    }

    private void OnApplyHotkeyClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SetHotkeyText(HotkeyTextBox.Text);
        HotkeyTextBox.Text = ViewModel.HotkeyText;
    }

    private void OnAutostartClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SetAutostart(AutostartCheckBox.IsChecked == true);
        AutostartCheckBox.IsChecked = ViewModel.AutostartEnabled;
    }

    private void OnAddFileClick(object sender, RoutedEventArgs e)
    {
        Win32OpenFileDialog dialog = new()
        {
            Title = "Add File Or Shortcut",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            string path = dialog.FileName;
            LaunchItemKind kind = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".lnk" => LaunchItemKind.Shortcut,
                ".url" => LaunchItemKind.Url,
                _ => LaunchItemKind.File
            };
            string displayName = kind is LaunchItemKind.Shortcut or LaunchItemKind.Url
                ? Path.GetFileNameWithoutExtension(path)
                : Path.GetFileName(path);
            ViewModel.AddManualItem(displayName, kind, path);
        }
    }

    private void OnAddFolderClick(object sender, RoutedEventArgs e)
    {
        using Forms.FolderBrowserDialog dialog = new()
        {
            Description = "Add Folder"
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            ViewModel.AddManualItem(Path.GetFileName(dialog.SelectedPath), LaunchItemKind.Folder, dialog.SelectedPath);
        }
    }

    private void OnRemoveManualClick(object sender, RoutedEventArgs e)
    {
        if (ManualItemsList.SelectedItem is LaunchItem item)
        {
            ViewModel.RemoveManualItem(item.Id);
        }
    }

    private void OnHideDesktopClick(object sender, RoutedEventArgs e)
    {
        if (DesktopItemsList.SelectedItem is LaunchItem item)
        {
            ViewModel.HideDesktopItem(item.Id);
        }
    }

    private void OnRestoreHiddenClick(object sender, RoutedEventArgs e)
    {
        if (HiddenItemsList.SelectedItem is string itemId)
        {
            ViewModel.RestoreHiddenDesktopItem(itemId);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        ViewModel.SaveWindowBounds(Left, Top, ActualWidth, ActualHeight);
    }
}
