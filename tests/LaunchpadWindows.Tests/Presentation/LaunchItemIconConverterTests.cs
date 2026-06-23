using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LaunchpadWindows.Models;
using LaunchpadWindows.Presentation;

namespace LaunchpadWindows.Tests.Presentation;

public sealed class LaunchItemIconConverterTests
{
    [Fact]
    public void Convert_WhenValueIsNotLaunchItem_ReturnsNull()
    {
        LaunchItemIconConverter converter = new();

        object? result = converter.Convert("not an item", typeof(ImageSource), null, CultureInfo.InvariantCulture);

        Assert.Null(result);
    }

    [Fact]
    public void Convert_WhenShellIconCannotBeResolved_ReturnsFrozenFallbackIcon()
    {
        LaunchItemIconConverter converter = new();
        LaunchItem item = LaunchItem.CreateManual("Missing", LaunchItemKind.File, @"Z:\this-path-should-not-exist\missing.exe");

        object? result = converter.Convert(item, typeof(ImageSource), null, CultureInfo.InvariantCulture);

        ImageSource image = Assert.IsAssignableFrom<ImageSource>(result);
        Assert.True(image.IsFrozen);
    }

    [Fact]
    public void ConvertBack_ReturnsBindingDoNothing()
    {
        LaunchItemIconConverter converter = new();

        object result = converter.ConvertBack(new object(), typeof(LaunchItem), null, CultureInfo.InvariantCulture);

        Assert.Same(Binding.DoNothing, result);
    }
}
