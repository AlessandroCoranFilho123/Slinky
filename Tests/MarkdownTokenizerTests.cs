using System.Linq;
using Slinky.Services;
using Xunit;

namespace Slinky.Tests
{
    public class MarkdownTokenizerTests
    {
        [Theory]
        [InlineData("# Título", 1, "Título")]
        [InlineData("## Subtítulo", 2, "Subtítulo")]
        [InlineData("###### Nível seis", 6, "Nível seis")]
        public void Tokenize_RecognizesHeaders(string line, int expectedLevel, string expectedText)
        {
            var block = Assert.Single(MarkdownTokenizer.Tokenize(line));

            Assert.Equal(MarkdownBlockKind.Header, block.Kind);
            Assert.Equal(expectedLevel, block.HeaderLevel);
            Assert.Equal(expectedText, block.Text);
        }

        [Fact]
        public void Tokenize_HashWithoutSpace_IsNotAHeader()
        {
            var block = Assert.Single(MarkdownTokenizer.Tokenize("#semespaço"));
            Assert.Equal(MarkdownBlockKind.Paragraph, block.Kind);
        }

        [Theory]
        [InlineData("- item")]
        [InlineData("* item")]
        [InlineData("  - item")]
        public void Tokenize_RecognizesBullets(string line)
        {
            var block = Assert.Single(MarkdownTokenizer.Tokenize(line));
            Assert.Equal(MarkdownBlockKind.Bullet, block.Kind);
            Assert.Equal("item", block.Text);
        }

        [Theory]
        [InlineData("1. primeiro", "1.")]
        [InlineData("2) segundo", "2.")]
        public void Tokenize_RecognizesNumberedItems(string line, string expectedMarker)
        {
            var block = Assert.Single(MarkdownTokenizer.Tokenize(line));
            Assert.Equal(MarkdownBlockKind.Numbered, block.Kind);
            Assert.Equal(expectedMarker, block.Marker);
        }

        [Fact]
        public void Tokenize_RecognizesBlockquote()
        {
            var block = Assert.Single(MarkdownTokenizer.Tokenize("> uma citação"));
            Assert.Equal(MarkdownBlockKind.Quote, block.Kind);
            Assert.Equal("uma citação", block.Text);
        }

        [Theory]
        [InlineData("---")]
        [InlineData("***")]
        [InlineData("____")]
        public void Tokenize_RecognizesRules(string line)
        {
            var block = Assert.Single(MarkdownTokenizer.Tokenize(line));
            Assert.Equal(MarkdownBlockKind.Rule, block.Kind);
        }

        [Fact]
        public void Tokenize_BlankLine_IsItsOwnBlock()
        {
            var block = Assert.Single(MarkdownTokenizer.Tokenize("   "));
            Assert.Equal(MarkdownBlockKind.Blank, block.Kind);
        }

        [Fact]
        public void Tokenize_PlainLine_IsAParagraph()
        {
            var block = Assert.Single(MarkdownTokenizer.Tokenize("um texto qualquer"));
            Assert.Equal(MarkdownBlockKind.Paragraph, block.Kind);
            Assert.Equal("um texto qualquer", block.Text);
        }

        [Fact]
        public void Tokenize_MultipleLines_ProducesOneBlockPerLine()
        {
            var blocks = MarkdownTokenizer.Tokenize("# Título\ntexto\n- item");

            Assert.Equal(3, blocks.Count);
            Assert.Equal(MarkdownBlockKind.Header, blocks[0].Kind);
            Assert.Equal(MarkdownBlockKind.Paragraph, blocks[1].Kind);
            Assert.Equal(MarkdownBlockKind.Bullet, blocks[2].Kind);
        }

        [Fact]
        public void TokenizeInline_Bold_IsRecognized()
        {
            var spans = MarkdownTokenizer.TokenizeInline("isto é **negrito** no meio");

            Assert.Contains(spans, s => s.Kind == MarkdownSpanKind.Bold && s.Text == "negrito");
            Assert.Contains(spans, s => s.Kind == MarkdownSpanKind.Plain && s.Text.Contains("isto"));
        }

        [Theory]
        [InlineData("texto *itálico* aqui")]
        [InlineData("texto _itálico_ aqui")]
        public void TokenizeInline_Italic_IsRecognized(string text)
        {
            var spans = MarkdownTokenizer.TokenizeInline(text);
            Assert.Contains(spans, s => s.Kind == MarkdownSpanKind.Italic && s.Text == "itálico");
        }

        [Fact]
        public void TokenizeInline_Code_IsRecognized()
        {
            var spans = MarkdownTokenizer.TokenizeInline("use `var x = 1;` aqui");
            Assert.Contains(spans, s => s.Kind == MarkdownSpanKind.Code && s.Text == "var x = 1;");
        }

        [Fact]
        public void TokenizeInline_PlainText_HasNoSpecialSpans()
        {
            var spans = MarkdownTokenizer.TokenizeInline("nada de especial aqui");
            Assert.All(spans, s => Assert.Equal(MarkdownSpanKind.Plain, s.Kind));
        }

        [Fact]
        public void TokenizeInline_BoldTakesPriorityOverItalicAsterisks()
        {
            var spans = MarkdownTokenizer.TokenizeInline("**negrito**");
            var single = Assert.Single(spans);
            Assert.Equal(MarkdownSpanKind.Bold, single.Kind);
            Assert.Equal("negrito", single.Text);
        }
    }
}
