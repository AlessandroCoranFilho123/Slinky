using Slinky.Services;
using Xunit;

namespace Slinky.Tests
{
    [Trait("Category", "Unit")]
    public class TextSearchTests
    {
        [Fact]
        public void FindNextWrapsForward()
        {
            Assert.Equal(0, TextSearch.FindNextIndex("one two one", "one", 9, true, StringComparison.Ordinal));
        }

        [Fact]
        public void FindNextWrapsBackward()
        {
            Assert.Equal(8, TextSearch.FindNextIndex("one two one", "one", -1, false, StringComparison.Ordinal));
        }

        [Fact]
        public void FindNextHonorsCaseSensitivity()
        {
            Assert.Equal(-1, TextSearch.FindNextIndex("Alpha", "alpha", 0, true, StringComparison.Ordinal));
            Assert.Equal(0, TextSearch.FindNextIndex("Alpha", "alpha", 0, true, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("aaaa", "aa", 2)]
        [InlineData("Sem texto", "z", 0)]
        [InlineData("", "", 0)]
        public void CountOccurrencesDoesNotOverlap(string text, string term, int expected)
        {
            Assert.Equal(expected, TextSearch.CountOccurrences(text, term, StringComparison.Ordinal));
        }

        [Fact]
        public void ReplaceAllPreservesOriginalWhenThereAreNoMatches()
        {
            var result = TextSearch.ReplaceAll("abc", "x", "y", StringComparison.Ordinal);

            Assert.Equal("abc", result.Text);
            Assert.Equal(0, result.Count);
        }

        [Fact]
        public void ReplaceAllSupportsEmptyReplacementAndCaseInsensitiveSearch()
        {
            var result = TextSearch.ReplaceAll("A a A", "a", string.Empty, StringComparison.OrdinalIgnoreCase);

            Assert.Equal("  ", result.Text);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void FindAllReturnsNonOverlappingPositionsInDocumentOrder()
        {
            Assert.Equal(new[] { 0, 7 }, TextSearch.FindAll("aba ba aba", "aba", StringComparison.Ordinal));
        }

        [Fact]
        public void FindAllRespectsCaseComparisonAndReturnsEmptyWhenAbsent()
        {
            Assert.Equal(new[] { 1 }, TextSearch.FindAll("xXx", "Xx", StringComparison.Ordinal));
            Assert.Empty(TextSearch.FindAll("xXx", "zz", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(TextSearch.FindAll("qualquer", string.Empty, StringComparison.Ordinal));
        }
    }
}
