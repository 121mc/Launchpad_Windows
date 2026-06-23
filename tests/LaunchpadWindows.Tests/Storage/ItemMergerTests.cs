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

    [Fact]
    public void Merge_PrunesStaleOrderIdsAndPersistsMergedOrder()
    {
        LaunchItem first = Desktop("desktop:A", "A");
        LaunchItem second = Desktop("desktop:B", "B");
        LaunchItem manual = LaunchItem.CreateManual("Manual", LaunchItemKind.File, @"C:\Manual.txt");
        AppSettings settings = AppSettings.CreateDefault();
        settings.ManualItems.Add(manual);
        settings.OrderedItemIds.AddRange(["desktop:stale", manual.Id, "desktop:A"]);

        IReadOnlyList<LaunchItem> merged = ItemMerger.Merge(settings, [first, second]);

        string[] mergedIds = merged.Select(item => item.Id).ToArray();
        Assert.Equal(mergedIds, settings.OrderedItemIds);
        Assert.DoesNotContain("desktop:stale", settings.OrderedItemIds);
    }

    [Fact]
    public void Merge_MatchesHiddenOrderedAndDuplicateIdsIgnoringCase()
    {
        LaunchItem hidden = Desktop("desktop:hidden", "Hidden");
        LaunchItem ordered = Desktop("desktop:ordered", "Ordered");
        LaunchItem duplicateFirst = Desktop("desktop:duplicate", "Duplicate A");
        LaunchItem duplicateSecond = Desktop("DESKTOP:DUPLICATE", "Duplicate B");
        AppSettings settings = AppSettings.CreateDefault();
        settings.HiddenDesktopItemIds.Add("DESKTOP:HIDDEN");
        settings.OrderedItemIds.Add("DESKTOP:ORDERED");

        IReadOnlyList<LaunchItem> merged = ItemMerger.Merge(settings, [hidden, duplicateFirst, duplicateSecond, ordered]);

        Assert.Equal(["Ordered", "Duplicate A"], merged.Select(item => item.DisplayName).ToArray());
        Assert.DoesNotContain(merged, item => item.DisplayName == "Hidden");
        Assert.Single(merged, item => string.Equals(item.Id, "desktop:duplicate", StringComparison.OrdinalIgnoreCase));
    }

    private static LaunchItem Desktop(string id, string name) =>
        new(id, name, LaunchItemSource.DesktopScan, LaunchItemKind.File, @$"C:\Users\hp\Desktop\{name}", null, name, DateTimeOffset.UtcNow);
}
