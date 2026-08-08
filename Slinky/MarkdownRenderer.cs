using Windows.UI;
using Microsoft.UI;
using Windows.UI.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Slinky.Services
{
    /// <summary>
    /// Turns MarkdownTokenizer's output into an actual RichTextBlock. All the parsing rules
    /// themselves live in MarkdownTokenizer.cs (WinRT-free, unit tested) — this file is just
    /// the thin, WinUI-only adapter, so it stays untested but has as little logic as possible.
    /// </summary>
    public static class MarkdownRenderer
    {
        public static void Render(string markdown, RichTextBlock target)
        {
            target.Blocks.Clear();

            foreach (var block in MarkdownTokenizer.Tokenize(markdown))
            {
                target.Blocks.Add(BuildParagraph(block));
            }
        }

        private static Paragraph BuildParagraph(MarkdownBlock block)
        {
            switch (block.Kind)
            {
                case MarkdownBlockKind.Rule:
                {
                    var rule = new Paragraph { Margin = new Thickness(0, 8, 0, 8) };
                    rule.Inlines.Add(new Run
                    {
                        Text = new string('—', 24),
                        Foreground = new SolidColorBrush(Colors.Gray)
                    });
                    return rule;
                }

                case MarkdownBlockKind.Header:
                {
                    var headerParagraph = new Paragraph
                    {
                        Margin = new Thickness(0, block.HeaderLevel == 1 ? 4 : 8, 0, 4)
                    };
                    headerParagraph.Inlines.Add(new Run
                    {
                        Text = block.Text,
                        FontSize = block.HeaderLevel switch
                        {
                            1 => 28,
                            2 => 24,
                            3 => 20,
                            4 => 18,
                            5 => 16,
                            _ => 15
                        },
                        FontWeight = FontWeights.SemiBold
                    });
                    return headerParagraph;
                }

                case MarkdownBlockKind.Quote:
                {
                    var quoteParagraph = new Paragraph { Margin = new Thickness(12, 2, 0, 2) };
                    AddInlines(quoteParagraph.Inlines, block.Text, italic: true, dim: true);
                    return quoteParagraph;
                }

                case MarkdownBlockKind.Bullet:
                {
                    var bulletParagraph = new Paragraph { Margin = new Thickness(16, 1, 0, 1) };
                    bulletParagraph.Inlines.Add(new Run { Text = "•  " });
                    AddInlines(bulletParagraph.Inlines, block.Text);
                    return bulletParagraph;
                }

                case MarkdownBlockKind.Numbered:
                {
                    var numberedParagraph = new Paragraph { Margin = new Thickness(16, 1, 0, 1) };
                    numberedParagraph.Inlines.Add(new Run { Text = $"{block.Marker}  " });
                    AddInlines(numberedParagraph.Inlines, block.Text);
                    return numberedParagraph;
                }

                case MarkdownBlockKind.Blank:
                    return new Paragraph { Margin = new Thickness(0, 2, 0, 2) };

                default: // Paragraph
                {
                    var paragraph = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
                    AddInlines(paragraph.Inlines, block.Text);
                    return paragraph;
                }
            }
        }

        private static void AddInlines(InlineCollection inlines, string text, bool italic = false, bool dim = false)
        {
            foreach (var span in MarkdownTokenizer.TokenizeInline(text))
            {
                var run = new Run { Text = span.Text };

                switch (span.Kind)
                {
                    case MarkdownSpanKind.Bold:
                        run.FontWeight = FontWeights.Bold;
                        break;
                    case MarkdownSpanKind.Code:
                        run.FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace");
                        break;
                    case MarkdownSpanKind.Italic:
                        run.FontStyle = FontStyle.Italic;
                        break;
                }

                if (italic) run.FontStyle = FontStyle.Italic;
                if (dim) run.Foreground = new SolidColorBrush(Colors.Gray);

                inlines.Add(run);
            }
        }
    }
}
