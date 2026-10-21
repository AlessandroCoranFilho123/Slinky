using System;
using System.Collections.Generic;
using System.Linq;

namespace Slinky;

public sealed record PrintPageLayout(
    IReadOnlyList<string> Lines,
    double ContentWidth,
    double ContentHeight,
    double FontSize);

public static class PrintPagination
{
    public static IReadOnlyList<PrintPageLayout> Paginate(
        string text,
        double pageWidth,
        double pageHeight,
        double marginInches,
        double scalePercent)
    {
        var safeMargin = double.IsFinite(marginInches) ? Math.Max(0, marginInches) : 1.0;
        var safeScale = double.IsFinite(scalePercent) ? scalePercent : 100.0;
        var marginPixels = safeMargin * 96.0;
        var contentWidth = Math.Max(pageWidth - marginPixels * 2, 50);
        var contentHeight = Math.Max(pageHeight - marginPixels * 2, 50);
        var fontSize = 12 * Math.Clamp(safeScale, 25, 200) / 100.0;
        var charsPerLine = Math.Max(10, (int)(contentWidth / (fontSize * 0.6)));
        var linesPerPage = Math.Max(5, (int)(contentHeight / (fontSize * 1.3)));
        var wrappedLines = WrapLines(text, charsPerLine);

        return Chunk(wrappedLines, linesPerPage)
            .Select(lines => new PrintPageLayout(lines, contentWidth, contentHeight, fontSize))
            .ToList();
    }

    public static IReadOnlyList<string> WrapLines(string text, int width)
    {
        width = Math.Max(1, width);
        var normalized = TextFileCodec.NormalizeLineEndings(text ?? string.Empty, out _);
        var result = new List<string>();

        foreach (var rawLine in normalized.Split('\n'))
        {
            if (rawLine.Length == 0)
            {
                result.Add(string.Empty);
                continue;
            }

            var remaining = rawLine;
            while (remaining.Length > width)
            {
                var splitAt = remaining.LastIndexOf(' ', width - 1, width);
                if (splitAt <= 0)
                {
                    splitAt = width;
                }

                result.Add(remaining[..splitAt].TrimEnd());
                remaining = remaining[splitAt..].TrimStart();
            }

            result.Add(remaining);
        }

        return result;
    }

    private static IReadOnlyList<IReadOnlyList<string>> Chunk(IReadOnlyList<string> lines, int size)
    {
        var pages = new List<IReadOnlyList<string>>();
        for (var index = 0; index < lines.Count; index += size)
        {
            pages.Add(lines.Skip(index).Take(size).ToList());
        }

        return pages.Count == 0
            ? new[] { (IReadOnlyList<string>)new List<string> { string.Empty } }
            : pages;
    }
}
