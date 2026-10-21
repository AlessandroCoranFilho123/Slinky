using Slinky;
using Xunit;

namespace Slinky.Tests;

public sealed class PrintPaginationTests
{
    [Fact]
    public void WrapLines_PreservesBlankLinesAndBreaksLongWords()
    {
        var lines = PrintPagination.WrapLines("abc\r\n\r\n0123456789", 4);

        Assert.Equal(new[] { "abc", "", "0123", "4567", "89" }, lines);
    }

    [Fact]
    public void Paginate_AlwaysProducesOnePageForEmptyDocument()
    {
        var pages = PrintPagination.Paginate(string.Empty, 800, 1000, 1, 100);

        var page = Assert.Single(pages);
        Assert.Equal(new[] { string.Empty }, page.Lines);
        Assert.True(page.ContentWidth > 0);
        Assert.True(page.ContentHeight > 0);
    }

    [Fact]
    public void Paginate_ClampsInvalidSettingsAndKeepsMarginsWithinPage()
    {
        var pages = PrintPagination.Paginate("texto", 100, 100, -1, double.NaN);

        var page = Assert.Single(pages);
        Assert.Equal(100, page.ContentWidth);
        Assert.Equal(100, page.ContentHeight);
        Assert.False(double.IsNaN(page.FontSize));
    }
}
