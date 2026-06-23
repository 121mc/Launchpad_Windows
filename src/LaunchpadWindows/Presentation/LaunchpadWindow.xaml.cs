using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LaunchpadWindows.Models;
using WpfButton = System.Windows.Controls.Button;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace LaunchpadWindows.Presentation;

public partial class LaunchpadWindow : Window
{
    private readonly TimeSpan _fadeDuration;
    private LaunchpadViewModel ViewModel => (LaunchpadViewModel)DataContext;

    public LaunchpadWindow(LaunchpadViewModel viewModel, TimeSpan fadeDuration)
    {
        _fadeDuration = fadeDuration;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            AcrylicWindowHelper.Apply(this);
            FadeTo(1.0);
        };
    }

    public void FadeOutAndClose()
    {
        DoubleAnimation animation = new(0.0, _fadeDuration);
        animation.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, animation);
    }

    private void FadeTo(double opacity)
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, _fadeDuration));
    }

    private void OnKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            FadeOutAndClose();
        }
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left &&
            FindAncestor<WpfButton>(e.OriginalSource as DependencyObject) is null)
        {
            FadeOutAndClose();
        }
    }

    private void OnErrorToastMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void OnItemPreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement { DataContext: LaunchItem item } element)
        {
            System.Windows.DragDrop.DoDragDrop(element, item, System.Windows.DragDropEffects.Move);
        }
    }

    private void OnItemDrop(object sender, WpfDragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LaunchItem target })
        {
            return;
        }

        if (e.Data.GetData(typeof(LaunchItem)) is not LaunchItem source || source.Id == target.Id)
        {
            return;
        }

        int fromIndex = ViewModel.Items.IndexOf(source);
        int toIndex = ViewModel.Items.IndexOf(target);
        ViewModel.MoveItem(fromIndex, toIndex);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
