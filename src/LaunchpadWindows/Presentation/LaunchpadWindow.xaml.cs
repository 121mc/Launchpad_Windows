using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using LaunchpadWindows.Models;
using LaunchpadWindows.SystemIntegration;
using WpfButton = System.Windows.Controls.Button;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace LaunchpadWindows.Presentation;

public partial class LaunchpadWindow : Window
{
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly TimeSpan _fadeDuration;
    private MonitorBounds? _physicalBounds;
    private LaunchpadViewModel ViewModel => (LaunchpadViewModel)DataContext;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    public LaunchpadWindow(LaunchpadViewModel viewModel, TimeSpan fadeDuration)
    {
        _fadeDuration = fadeDuration;
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => ApplyPhysicalBoundsIfAvailable();
        Loaded += (_, _) =>
        {
            AcrylicWindowHelper.Apply(this);
            FadeTo(1.0);
            Keyboard.Focus(this);
            Focus();
        };
    }

    public void SetPhysicalBounds(MonitorBounds bounds)
    {
        _physicalBounds = bounds;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        ApplyPhysicalBoundsIfAvailable();
    }

    public void FadeOutAndClose()
    {
        DoubleAnimation animation = new(0.0, _fadeDuration);
        animation.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, animation);
    }

    private void ApplyPhysicalBoundsIfAvailable()
    {
        if (_physicalBounds is not { } bounds)
        {
            return;
        }

        nint hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == nint.Zero)
        {
            return;
        }

        SetWindowPos(hwnd, nint.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoZOrder | SwpNoActivate | SwpShowWindow);
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

    private System.Windows.Point? _dragStartPoint;

    private void OnItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnItemPreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            _dragStartPoint is not { } startPoint ||
            sender is not FrameworkElement { DataContext: LaunchItem item } element)
        {
            return;
        }

        System.Windows.Point currentPosition = e.GetPosition(null);
        Vector diff = startPoint - currentPosition;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStartPoint = null;
        System.Windows.DragDrop.DoDragDrop(element, item, System.Windows.DragDropEffects.Move);
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
