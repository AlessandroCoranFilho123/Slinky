using Slinky;
using Xunit;

namespace Slinky.Tests
{
    [Trait("Category", "Unit")]
    public class LaunchArgsTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyArgumentsReturnNull(string? arguments)
        {
            Assert.Null(LaunchArgs.ExtractFilePath(arguments, _ => true));
        }

        [Fact]
        public void QuotedSinglePathIsExtractedWhenItExists()
        {
            Assert.Equal("C:/temp/a.txt", LaunchArgs.ExtractFilePath("\"C:/temp/a.txt\"", _ => true));
        }

        [Fact]
        public void FirstExistingPathWins()
        {
            var args = "\"C:/a.txt\" \"C:/b.txt\"";
            Assert.Equal("C:/b.txt", LaunchArgs.ExtractFilePath(args, p => p.Contains('b')));
        }

        [Fact]
        public void NonExistingPathsReturnNull()
        {
            Assert.Null(LaunchArgs.ExtractFilePath("\"C:/x.txt\"", _ => false));
        }
    }
}
