using LaunchpadWindows.Models;
using LaunchpadWindows.Storage;

namespace LaunchpadWindows.Tests.Storage;

public sealed class ItemMergerTests
{
    [Fact]
    public void Merge_KeepsSavedOrderAndAppendsNewItems()
    {
        LaunchItem first = Desktop("desktop:A", "A");
        LaunchItem second = Desktop("desktop:B", "B");
        LaunchItem third = Desktop("desktop:C", "C");
        AppSettings settings = AppSettings.CreateDefault();
        settings.OrderedItemIds.AddRange(["desktop:B", "desktop:A"]);

        IReadOnlyList<LaunchItem> merged = ItemMerger.Merge(settings, [first, second, third]);

        Assert.Equal(["B", "A", "C"], merged.Select(item => item.DisplayName).ToArray());
    }

    [Fact]
    public void Merge_ExcludesHiddenDesktopItemsButKeepsManualItems()
    {
        LaunchItem scanned = Desktop("desktop:A", "A");
        LaunchItem manual = LaunchItem.CreateManual("Manual", LaunchItemKind.File, @"C:\Manual.txt");
        AppSettings settings = AppSettings.CreateDefault();
        settings.HiddenDesktopItemIds.Add("desktop:A");
        settings.ManualItems.Add(manual);

        IReadOnlyList<LaunchItem> merged = ItemMerger.Merge(settings, [scanned]);

        Assert.Single(merged);
        Assert.Equal("Manual", merged[0].DisplayName);
    }

    private static LaunchItem Desktop(string id, string name) =>
        new(id, name, LaunchItemSource.DesktopScan, LaunchItemKind.File, @$"C:\Users\hp\Desktop\{name}", null, name, DateTimeOffset.UtcNow);
}
