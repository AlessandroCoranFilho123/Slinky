using System.Text;
using Slinky;
using Xunit;

namespace Slinky.Tests;

public sealed class TextFileCodecTests
{
    [Theory]
    [InlineData("a\r\nb", LineEndingKind.CrLf)]
    [InlineData("a\nb", LineEndingKind.Lf)]
    [InlineData("a\rb", LineEndingKind.Cr)]
    public void Decode_NormalizesLineEndingsAndRetainsStyle(string source, LineEndingKind expected)
    {
        var result = TextFileCodec.Decode(Encoding.UTF8.GetBytes(source));

        Assert.Equal("a\nb", result.Text);
        Assert.Equal(expected, result.LineEnding);
        Assert.Equal(TextEncodingKind.Utf8, result.Encoding);
        Assert.False(result.HasBom);
    }

    [Fact]
    public void Decode_ReportsMixedLineEndings()
    {
        var result = TextFileCodec.Decode(Encoding.UTF8.GetBytes("a\r\nb\nc\rd"));

        Assert.Equal("a\nb\nc\nd", result.Text);
        Assert.Equal(LineEndingKind.Mixed, result.LineEnding);
    }

    [Fact]
    public void Decode_DetectsUtf8Bom()
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes("ação\r\n")).ToArray();

        var result = TextFileCodec.Decode(bytes);

        Assert.Equal("ação\n", result.Text);
        Assert.Equal(TextEncodingKind.Utf8, result.Encoding);
        Assert.True(result.HasBom);
    }

    [Fact]
    public void Decode_DetectsUtf16LittleEndianBom()
    {
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes("texto\r\n")).ToArray();

        var result = TextFileCodec.Decode(bytes);

        Assert.Equal("texto\n", result.Text);
        Assert.Equal(TextEncodingKind.Utf16LittleEndian, result.Encoding);
        Assert.True(result.HasBom);
    }

    [Fact]
    public void Decode_FallsBackToWindows1252ForInvalidUtf8()
    {
        var bytes = new byte[] { 0xA3, 0x20, 0xE7 };

        var result = TextFileCodec.Decode(bytes);

        Assert.Equal("£ ç", result.Text);
        Assert.Equal(TextEncodingKind.Windows1252, result.Encoding);
    }

    [Fact]
    public void Encode_RoundTripsUtf8WithOriginalLineEnding()
    {
        var bytes = TextFileCodec.Encode("a\nb", TextEncodingKind.Utf8, false, LineEndingKind.CrLf);

        Assert.Equal(new byte[] { (byte)'a', 0x0D, 0x0A, (byte)'b' }, bytes);
    }

    [Fact]
    public void Encode_PreservesUtf16Bom()
    {
        var bytes = TextFileCodec.Encode("ação", TextEncodingKind.Utf16BigEndian, true, LineEndingKind.Lf);

        Assert.Equal(new byte[] { 0xFE, 0xFF }, bytes[..2]);
        Assert.Equal("ação", new UnicodeEncoding(bigEndian: true, byteOrderMark: false).GetString(bytes[2..]));
    }

    [Fact]
    public void Encode_MixedLineEndingUsesSafeDefaultCrLf()
    {
        var bytes = TextFileCodec.Encode("a\nb", TextEncodingKind.Utf8, false, LineEndingKind.Mixed);

        Assert.Equal("a\r\nb", Encoding.UTF8.GetString(bytes));
    }
}
