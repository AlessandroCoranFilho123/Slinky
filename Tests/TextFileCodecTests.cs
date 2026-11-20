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

    [Fact]
    public void Decode_EmptyBytesYieldsEmptyUtf8Document()
    {
        // arquivo de 0 bytes e legitimo (Notepad abre); abrir nao pode falhar
        var result = TextFileCodec.Decode(ReadOnlySpan<byte>.Empty);

        Assert.Equal(string.Empty, result.Text);
        Assert.Equal(TextEncodingKind.Utf8, result.Encoding);
        Assert.False(result.HasBom);
        Assert.Equal(LineEndingKind.CrLf, result.LineEnding);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LooksLikeBinary_AcceptsUtf16TextWithBom(bool bigEndian)
    {
        var encoding = new UnicodeEncoding(bigEndian, byteOrderMark: true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes("texto ASCII")).ToArray();

        Assert.False(TextFileCodec.LooksLikeBinary(bytes));
    }

    [Fact]
    public void LooksLikeBinary_RejectsNulInUtf8Text()
    {
        Assert.True(TextFileCodec.LooksLikeBinary(Encoding.UTF8.GetBytes("texto\0corrompido")));
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 })]
    public void LooksLikeBinary_RejectsKnownBinaryHeaders(byte[] bytes)
    {
        Assert.True(TextFileCodec.LooksLikeBinary(bytes));
    }

    [Fact]
    public void Encode_Windows1252WithEmoji_ReportsEncodingFailure()
    {
        Assert.Throws<EncoderFallbackException>(() =>
            TextFileCodec.Encode("texto 🙂", TextEncodingKind.Windows1252, false, LineEndingKind.CrLf));
    }

    [Fact]
    public void RecoverySnapshotCodec_RoundTripsRealDiskJson()
    {
        // payload REAL capturado da pasta Recovery durante o smoke-e2e de 17/11
        var json = "{\"OwnerProcessId\":2628,\"Text\":\"e2e recovery marcador 30-10\",\"FilePath\":null,\"FileToken\":null,\"SavedAt\":\"2026-11-18T14:21:47.092156-03:00\",\"Encoding\":0,\"HasBom\":false,\"LineEnding\":0}";

        Assert.True(RecoverySnapshotCodec.TryDeserialize(json, out var snapshot));
        Assert.NotNull(snapshot);
        Assert.Equal("e2e recovery marcador 30-10", snapshot!.Text);
        Assert.Equal(2628, snapshot.OwnerProcessId);
        Assert.Null(snapshot.FilePath);
    }

    [Fact]
    public void EncodingAndLineEndingLabelsCoverAllKinds()
    {
        Assert.Equal("UTF-8", TextFileCodec.EncodingLabel(TextEncodingKind.Utf8));
        Assert.Equal("UTF-16 LE", TextFileCodec.EncodingLabel(TextEncodingKind.Utf16LittleEndian));
        Assert.Equal("UTF-16 BE", TextFileCodec.EncodingLabel(TextEncodingKind.Utf16BigEndian));
        Assert.Equal("Windows-1252", TextFileCodec.EncodingLabel(TextEncodingKind.Windows1252));
        Assert.Equal("CRLF", TextFileCodec.LineEndingLabel(LineEndingKind.CrLf));
        Assert.Equal("LF", TextFileCodec.LineEndingLabel(LineEndingKind.Lf));
        Assert.Equal("CR", TextFileCodec.LineEndingLabel(LineEndingKind.Cr));
        Assert.Equal("Misto", TextFileCodec.LineEndingLabel(LineEndingKind.Mixed));
    }
}
