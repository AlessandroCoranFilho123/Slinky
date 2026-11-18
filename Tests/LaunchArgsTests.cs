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
        public void FileActivationPreservesOrderAndReturnsAllExisting()
        {
            var paths = new[] { "C:/x.txt", "C:/b.txt", "C:/a.txt" };
            Assert.Equal(new[] { "C:/b.txt", "C:/a.txt" }, LaunchArgs.ExtractAllExisting(paths, p => p.Contains('b') || p.Contains('a')));
        }

        [Fact]
        public void FileActivationIgnoresNullAndBlankAndMissing()
        {
            var paths = new string?[] { null, "   ", "C:/a.txt", "C:/sumiu.txt" };
            Assert.Equal(new[] { "C:/a.txt" }, LaunchArgs.ExtractAllExisting(paths, p => p.EndsWith("a.txt")));
            Assert.Empty(LaunchArgs.ExtractAllExisting(new string?[] { null, "" }, _ => true));
        }

        [Theory]
        [InlineData(new string?[] { "--new-window" }, true)]
        [InlineData(new string?[] { "C:/a.txt", "--new-window" }, true)]
        [InlineData(new string?[] { "--NEW-WINDOW" }, true)]
        [InlineData(new string?[] { null, "C:/a.txt" }, false)]
        [InlineData(new string?[] { "--new window" }, false)]
        // token exato: um caminho que CONTENHA o texto não pode disparar o flag
        [InlineData(new string?[] { "C:/pasta com --new-window no nome.txt" }, false)]
        [InlineData(new string?[] { "C:/a.txt" }, false)]
        public void HasNewWindowFlag_DetectaTokenExato(string?[] tokens, bool esperado)
        {
            Assert.Equal(esperado, LaunchArgs.HasNewWindowFlag(tokens));
        }

        [Theory]
        [InlineData("--new-window", true)]
        [InlineData("C:/a.txt --new-window", true)]
        [InlineData("--NEW-WINDOW", true)]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("C:/a.txt", false)]
        [InlineData("--new window", false)]
        public void HasNewWindowFlag_DetectaOFlagDeNovaJanela(string? arguments, bool esperado)
        {
            Assert.Equal(esperado, LaunchArgs.HasNewWindowFlag(arguments));
        }
    }
}
