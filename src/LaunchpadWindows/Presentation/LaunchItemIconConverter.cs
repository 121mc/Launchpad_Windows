using System.Globalization;
using System.Windows.Media;
using LaunchpadWindows.Models;

namespace LaunchpadWindows.Presentation;

public sealed class LaunchItemIconConverter : System.Windows.Data.IValueConverter
{
    private static readonly IconProvider IconProvider = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LaunchItem item)
        {
            return null;
        }

        ImageSource icon = IconProvider.GetIcon(item.PathOrUrl);
        icon.Freeze();
        return icon;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
