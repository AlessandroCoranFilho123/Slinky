using System;
using System.Text;

namespace Slinky;

public enum TextEncodingKind
{
    Utf8,
    Utf16LittleEndian,
    Utf16BigEndian,
    Windows1252
}

public enum LineEndingKind
{
    CrLf,
    Lf,
    Cr,
    Mixed
}

public sealed record DecodedText(
    string Text,
    TextEncodingKind Encoding,
    bool HasBom,
    LineEndingKind LineEnding);

/// <summary>
/// Converts file bytes to the editor representation and back again.
/// The editor always uses LF internally; the original encoding, BOM and
/// preferred line ending are retained as metadata for a subsequent save.
/// </summary>
public static class TextFileCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static TextFileCodec()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static DecodedText Decode(ReadOnlySpan<byte> bytes)
    {
        var encoding = DetectEncoding(bytes, out var offset, out var hasBom);
        var text = encoding.GetString(bytes[offset..]);

        return new DecodedText(
            NormalizeLineEndings(text, out var lineEnding),
            ToKind(encoding),
            hasBom,
            lineEnding);
    }

    public static byte[] Encode(
        string text,
        TextEncodingKind encoding = TextEncodingKind.Utf8,
        bool hasBom = false,
        LineEndingKind lineEnding = LineEndingKind.CrLf)
    {
        var normalized = NormalizeLineEndings(text, out _);
        var output = ApplyLineEnding(normalized, lineEnding == LineEndingKind.Mixed ? LineEndingKind.CrLf : lineEnding);
        var encoder = GetEncoding(encoding, hasBom);
        var payload = encoder.GetBytes(output);
        if (!hasBom)
        {
            return payload;
        }

        var preamble = encoder.GetPreamble();
        var result = new byte[preamble.Length + payload.Length];
        preamble.CopyTo(result, 0);
        payload.CopyTo(result, preamble.Length);
        return result;
    }

    public static string NormalizeLineEndings(string text, out LineEndingKind lineEnding)
    {
        ArgumentNullException.ThrowIfNull(text);

        var crlf = 0;
        var lf = 0;
        var cr = 0;
        var builder = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '\r' when index + 1 < text.Length && text[index + 1] == '\n':
                    crlf++;
                    builder.Append('\n');
                    index++;
                    break;
                case '\r':
                    cr++;
                    builder.Append('\n');
                    break;
                case '\n':
                    lf++;
                    builder.Append('\n');
                    break;
                default:
                    builder.Append(text[index]);
                    break;
            }
        }

        var styles = (crlf > 0 ? 1 : 0) + (lf > 0 ? 1 : 0) + (cr > 0 ? 1 : 0);
        lineEnding = styles > 1
            ? LineEndingKind.Mixed
            : crlf > 0 ? LineEndingKind.CrLf
            : lf > 0 ? LineEndingKind.Lf
            : cr > 0 ? LineEndingKind.Cr
            : LineEndingKind.CrLf;

        return builder.ToString();
    }

    private static string ApplyLineEnding(string text, LineEndingKind lineEnding)
        => lineEnding switch
        {
            LineEndingKind.Lf => text,
            LineEndingKind.Cr => text.Replace("\n", "\r", StringComparison.Ordinal),
            _ => text.Replace("\n", "\r\n", StringComparison.Ordinal)
        };

    private static Encoding DetectEncoding(ReadOnlySpan<byte> bytes, out int offset, out bool hasBom)
    {
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            offset = 3;
            hasBom = true;
            return StrictUtf8;
        }

        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            offset = 2;
            hasBom = true;
            return new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
        }

        if (bytes.StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            offset = 2;
            hasBom = true;
            return new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
        }

        offset = 0;
        hasBom = false;

        try
        {
            StrictUtf8.GetString(bytes);
            return StrictUtf8;
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252);
        }
    }

    private static TextEncodingKind ToKind(Encoding encoding)
        => encoding.CodePage switch
        {
            1200 => TextEncodingKind.Utf16LittleEndian,
            1201 => TextEncodingKind.Utf16BigEndian,
            1252 => TextEncodingKind.Windows1252,
            _ => TextEncodingKind.Utf8
        };

    private static Encoding GetEncoding(TextEncodingKind kind, bool hasBom)
        => kind switch
        {
            TextEncodingKind.Utf16LittleEndian => new UnicodeEncoding(false, hasBom),
            TextEncodingKind.Utf16BigEndian => new UnicodeEncoding(true, hasBom),
            TextEncodingKind.Windows1252 => Encoding.GetEncoding(1252),
            _ => new UTF8Encoding(hasBom)
        };
}
