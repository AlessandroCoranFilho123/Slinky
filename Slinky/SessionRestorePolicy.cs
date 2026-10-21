using System;
using System.Collections.Generic;
using System.Linq;

namespace Slinky;

public static class SessionRestorePolicy
{
    public static IEnumerable<T> Filter<T>(
        IEnumerable<T> sessionItems,
        Func<T, string?> pathSelector,
        IEnumerable<string> excludedPaths)
    {
        var excluded = new HashSet<string>(excludedPaths, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in sessionItems)
        {
            var path = pathSelector(item);
            if (string.IsNullOrWhiteSpace(path) || excluded.Contains(path) || !seen.Add(path))
            {
                continue;
            }

            yield return item;
        }
    }
}
