using System.Text;
using Slinky;
using Xunit;

namespace Slinky.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public void DocumentRoundTrip_UsesRealTemporaryFileAndPreservesMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), $"slinky-{Guid.NewGuid():N}.txt");
        try
        {
            var bytes = TextFileCodec.Encode("ação\nsegunda linha", TextEncodingKind.Utf8, true, LineEndingKind.CrLf);
            File.WriteAllBytes(path, bytes);

            var decoded = TextFileCodec.Decode(File.ReadAllBytes(path));

            Assert.Equal("ação\nsegunda linha", decoded.Text);
            Assert.Equal(TextEncodingKind.Utf8, decoded.Encoding);
            Assert.True(decoded.HasBom);
            Assert.Equal(LineEndingKind.CrLf, decoded.LineEnding);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SessionStateCodec_DeduplicatesTokensAndRejectsCorruptJson()
    {
        var json = SessionStateCodec.Serialize(new[] { "a", "a", "b", "" });

        Assert.True(SessionStateCodec.TryDeserialize(json, out var tokens));
        Assert.Equal(new[] { "a", "b" }, tokens);
        Assert.False(SessionStateCodec.TryDeserialize("{broken", out _));
    }

    [Fact]
    public void RecoverySnapshotCodec_RoundTripsAndRejectsCorruptJson()
    {
        var snapshot = new RecoverySnapshot
        {
            OwnerProcessId = 1234,
            Text = "linha 1\nlinha 2",
            FilePath = "C:\\temp\\arquivo.txt",
            FileToken = "token-da-fal",
            SavedAt = DateTime.UtcNow,
            Encoding = TextEncodingKind.Windows1252,
            HasBom = false,
            LineEnding = LineEndingKind.Lf
        };

        var json = RecoverySnapshotCodec.Serialize(snapshot);

        Assert.True(RecoverySnapshotCodec.TryDeserialize(json, out var restored));
        Assert.NotNull(restored);
        Assert.Equal(snapshot.Text, restored!.Text);
        Assert.Equal(snapshot.OwnerProcessId, restored.OwnerProcessId);
        Assert.Equal(snapshot.FilePath, restored.FilePath);
        Assert.Equal(snapshot.FileToken, restored.FileToken);
        Assert.Equal(snapshot.Encoding, restored.Encoding);
        Assert.Equal(snapshot.LineEnding, restored.LineEnding);
        Assert.False(RecoverySnapshotCodec.TryDeserialize("not-json", out _));

        // Snapshots de versões antigas (sem token) desserializam com null — o restore
        // precisa tratar isso como nota sem título, como sempre fez.
        var legado = @"{""Text"":""x"",""FilePath"":""C:\\x.txt""}";
        Assert.True(RecoverySnapshotCodec.TryDeserialize(legado, out var antigo));
        Assert.Null(antigo!.FileToken);
    }

    [Fact]
    public void RecoveryReattachPolicy_RequiresTokenAndMatchingPath()
    {
        // Sem token (snapshot legado): nunca reconecta.
        Assert.False(RecoveryReattachPolicy.CanReattach(null, "C:\\x.txt", "C:\\x.txt"));
        Assert.False(RecoveryReattachPolicy.CanReattach("", "C:\\x.txt", "C:\\x.txt"));

        // Token presente exige que o arquivo resolvido seja o mesmo caminho do snapshot.
        Assert.True(RecoveryReattachPolicy.CanReattach("tok", "C:\\x.txt", "C:\\x.txt"));
        Assert.True(RecoveryReattachPolicy.CanReattach("tok", "C:\\X.TXT", "c:\\x.txt"));
        Assert.False(RecoveryReattachPolicy.CanReattach("tok", "C:\\x.txt", "C:\\y.txt"));
        Assert.False(RecoveryReattachPolicy.CanReattach("tok", null, "C:\\x.txt"));
    }

    [Fact]
    public void RecoveryLifecycle_AllowsWritesUntilCloseAndThenBlocksThem()
    {
        var lifecycle = new RecoveryLifecycle();

        Assert.True(lifecycle.CanSchedule);
        Assert.True(lifecycle.TryBeginWrite());

        lifecycle.Close();

        Assert.True(lifecycle.IsClosed);
        Assert.False(lifecycle.CanSchedule);
        Assert.False(lifecycle.TryBeginWrite());
    }

    [Fact]
    public void RecentFileListPolicy_DeduplicatesBumpsAndCapsEntries()
    {
        var current = Enumerable.Range(1, 10)
            .Select(index => new RecentFileListEntry($"token-{index}", $"Arquivo {index}", $"C:\\arquivo-{index}.txt"));

        var updated = RecentFileListPolicy.Insert(
            current,
            new RecentFileListEntry("token-new", "Arquivo 5 atualizado", "c:\\ARQUIVO-5.TXT"));

        Assert.Equal(10, updated.Count);
        Assert.Equal("token-new", updated[0].Token);
        Assert.DoesNotContain(updated, entry => entry.Token == "token-5");
        Assert.Contains(updated, entry => entry.Token == "token-10");
    }

    [Fact]
    public void SessionRestorePolicy_PreservesOrderAndSkipsRecoveredOrDuplicatedPaths()
    {
        var session = new[] { "C:\\one.txt", "C:\\two.txt", "c:\\ONE.TXT", "C:\\three.txt" };

        var result = SessionRestorePolicy.Filter(session, path => path, new[] { "c:\\two.TXT" }).ToArray();

        Assert.Equal(new[] { "C:\\one.txt", "C:\\three.txt" }, result);
    }
}
