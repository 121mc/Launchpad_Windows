using LaunchpadWindows.Models;

namespace LaunchpadWindows.Storage;

public static class ItemMerger
{
    public static IReadOnlyList<LaunchItem> Merge(AppSettings settings, IReadOnlyList<LaunchItem> scannedItems)
    {
        Dictionary<string, LaunchItem> available = scannedItems
            .Where(item => !settings.HiddenDesktopItemIds.Contains(item.Id, StringComparer.OrdinalIgnoreCase))
            .Concat(settings.ManualItems)
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<LaunchItem> ordered = [];
        foreach (string id in settings.OrderedItemIds)
        {
            if (available.Remove(id, out LaunchItem? item))
            {
                ordered.Add(item);
            }
        }

        ordered.AddRange(available.Values.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase));

        settings.OrderedItemIds = ordered.Select(item => item.Id).ToList();
        return ordered;
    }
}
