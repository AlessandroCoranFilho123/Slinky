using System;
using System.Text.Json;

namespace Slinky;

public sealed class RecoverySnapshot
{
    // Zero means an older snapshot without ownership metadata and is treated as stale.
    public int OwnerProcessId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? FilePath { get; set; }

    /// <summary>
    /// FutureAccessList token captured for FilePath while the note was open, so the
    /// restore flow can reattach the recovered text to its original file without
    /// broad filesystem access. Null on snapshots written by older versions.
    /// </summary>
    public string? FileToken { get; set; }

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
