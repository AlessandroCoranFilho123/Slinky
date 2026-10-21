using System;
using System.Collections.Generic;
using System.Linq;

namespace Slinky;

public sealed record RecentFileListEntry(string Token, string DisplayName, string Path);

public static class RecentFileListPolicy
{
    public static List<RecentFileListEntry> Insert(
        IEnumerable<RecentFileListEntry> current,
        RecentFileListEntry entry,
        int maxEntries = 10)
    {
        var result = current
            .Where(item => !string.Equals(item.Path, entry.Path, StringComparison.OrdinalIgnoreCase))
            .ToList();
        result.Insert(0, entry);

        if (maxEntries < 1)
        {
            return new List<RecentFileListEntry>();
        }

        return result.Take(maxEntries).ToList();
    }
}
