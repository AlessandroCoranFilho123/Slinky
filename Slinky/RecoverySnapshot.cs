using System;
using System.Text.Json;

namespace Slinky;

public sealed class RecoverySnapshot
{
    public string Text { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public DateTime SavedAt { get; set; }
    public TextEncodingKind Encoding { get; set; } = TextEncodingKind.Utf8;
    public bool HasBom { get; set; }
    public LineEndingKind LineEnding { get; set; } = LineEndingKind.CrLf;
}

public static class RecoverySnapshotCodec
{
    public static string Serialize(RecoverySnapshot snapshot)
        => JsonSerializer.Serialize(snapshot);

    public static bool TryDeserialize(string json, out RecoverySnapshot? snapshot)
    {
        try
        {
            snapshot = JsonSerializer.Deserialize<RecoverySnapshot>(json);
            return snapshot is not null;
        }
        catch (JsonException)
        {
            snapshot = null;
            return false;
        }
    }
}
