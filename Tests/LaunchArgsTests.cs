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

        [Fact]
        public void FileActivationFirstExistingPathWins()
        {
            var paths = new[] { "C:/x.txt", "C:/b.txt", "C:/a.txt" };
            Assert.Equal("C:/b.txt", LaunchArgs.ExtractFirstExisting(paths, p => p.Contains('b')));
        }

        [Fact]
        public void FileActivationIgnoresNullAndBlankEntries()
        {
            var paths = new string?[] { null, "   ", "C:/a.txt" };
            Assert.Equal("C:/a.txt", LaunchArgs.ExtractFirstExisting(paths, _ => true));
            Assert.Null(LaunchArgs.ExtractFirstExisting(new string?[] { null, "" }, _ => true));
        }
    }
}
