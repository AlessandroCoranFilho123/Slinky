using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Slinky.Services
{
    public enum MarkdownBlockKind
    {
        Paragraph,
        Header,
        Bullet,
        Numbered,
        Quote,
        Rule,
        Blank
    }

    /// <summary>One logical line of Markdown, already classified — but not yet turned into any UI.</summary>
    public sealed class MarkdownBlock
    {
        public MarkdownBlockKind Kind { get; init; }
        public int HeaderLevel { get; init; }
        public string Text { get; init; } = string.Empty;
        public string Marker { get; init; } = string.Empty; // e.g. "1." for a numbered list item
    }

    public enum MarkdownSpanKind
    {
        Plain,
        Bold,
        Italic,
        Code
    }

    /// <summary>One inline run within a block's text — e.g. the **bold** part of a sentence.</summary>
    public sealed class MarkdownSpan
    {
        public MarkdownSpanKind Kind { get; init; }
        public string Text { get; init; } = string.Empty;
    }

    /// <summary>
    /// Pure Markdown parsing — turns text into <see cref="MarkdownBlock"/>/<see cref="MarkdownSpan"/>
    /// tokens with no WinUI/WinRT dependency at all, so it can be linked into Slinky.Tests and
    /// asserted on directly. MarkdownRenderer.cs is the (WinRT-only) adapter that turns these
    /// tokens into an actual RichTextBlock.
    ///
    /// Supports: # / ## / ### headers, **bold**, *italic* / _italic_, `code`, - / * bullet lists,
    /// 1. numbered lists, > blockquotes and --- rules. Deliberately not the full CommonMark spec.
    /// </summary>
    public static class MarkdownTokenizer
    {
        private static readonly Regex RulePattern = new(@"^(-{3,}|\*{3,}|_{3,})$");
        private static readonly Regex HeaderPattern = new(@"^(#{1,6})\s+(.*)$");
        private static readonly Regex BulletPattern = new(@"^\s*[-*]\s+(.*)$");
        private static readonly Regex NumberedPattern = new(@"^\s*(\d+)[.)]\s+(.*)$");
        private static readonly Regex InlineTokenPattern = new(@"(\*\*.+?\*\*|`.+?`|\*.+?\*|_.+?_)");

        public static List<MarkdownBlock> Tokenize(string markdown)
        {
            var blocks = new List<MarkdownBlock>();

            if (string.IsNullOrEmpty(markdown))
            {
                blocks.Add(new MarkdownBlock { Kind = MarkdownBlockKind.Blank });
                return blocks;
            }

            var lines = markdown.Replace("\r\n", "\n").Split('\n');

            foreach (var line in lines)
            {
                if (RulePattern.IsMatch(line.Trim()))
                {
                    blocks.Add(new MarkdownBlock { Kind = MarkdownBlockKind.Rule });
                    continue;
                }

                var headerMatch = HeaderPattern.Match(line);
                if (headerMatch.Success)
                {
                    blocks.Add(new MarkdownBlock
                    {
                        Kind = MarkdownBlockKind.Header,
                        HeaderLevel = headerMatch.Groups[1].Value.Length,
                        Text = headerMatch.Groups[2].Value
                    });
                    continue;
                }

                if (line.TrimStart().StartsWith(">"))
                {
                    blocks.Add(new MarkdownBlock
                    {
                        Kind = MarkdownBlockKind.Quote,
                        Text = line.TrimStart().TrimStart('>').Trim()
                    });
                    continue;
                }

                var bulletMatch = BulletPattern.Match(line);
                if (bulletMatch.Success)
                {
                    blocks.Add(new MarkdownBlock { Kind = MarkdownBlockKind.Bullet, Text = bulletMatch.Groups[1].Value });
                    continue;
                }

                var numberedMatch = NumberedPattern.Match(line);
                if (numberedMatch.Success)
                {
                    blocks.Add(new MarkdownBlock
                    {
                        Kind = MarkdownBlockKind.Numbered,
                        Text = numberedMatch.Groups[2].Value,
                        Marker = $"{numberedMatch.Groups[1].Value}."
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    blocks.Add(new MarkdownBlock { Kind = MarkdownBlockKind.Blank });
                    continue;
                }

                blocks.Add(new MarkdownBlock { Kind = MarkdownBlockKind.Paragraph, Text = line });
            }

            return blocks;
        }

        public static List<MarkdownSpan> TokenizeInline(string text)
        {
            var spans = new List<MarkdownSpan>();
            var parts = InlineTokenPattern.Split(text);

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("**") && part.EndsWith("**") && part.Length >= 4)
                {
                    spans.Add(new MarkdownSpan { Kind = MarkdownSpanKind.Bold, Text = part[2..^2] });
                }
                else if (part.StartsWith("`") && part.EndsWith("`") && part.Length >= 2)
                {
                    spans.Add(new MarkdownSpan { Kind = MarkdownSpanKind.Code, Text = part[1..^1] });
                }
                else if ((part.StartsWith("*") && part.EndsWith("*") && part.Length >= 2) ||
                         (part.StartsWith("_") && part.EndsWith("_") && part.Length >= 2))
                {
                    spans.Add(new MarkdownSpan { Kind = MarkdownSpanKind.Italic, Text = part[1..^1] });
                }
                else
                {
                    spans.Add(new MarkdownSpan { Kind = MarkdownSpanKind.Plain, Text = part });
                }
            }

            return spans;
        }
    }
}
