using System;

namespace Slinky;

/// <summary>
/// Decides whether a recovered snapshot may be reattached to its original file.
/// Pure function so the headless test suite can cover the rules; the Windows
/// side (FutureAccessList lookup) stays in Note.TryReattachSavedFileAsync.
/// </summary>
public static class RecoveryReattachPolicy
{
    /// <summary>
    /// Reattach requires a captured FAL token and the resolved file must be the
    /// same path the snapshot was taken from (guards against a reused token).
    /// Older snapshots without tokens restore as untitled notes, exactly as before.
    /// </summary>
    public static bool CanReattach(string? fileToken, string? snapshotPath, string resolvedPath)
        => !string.IsNullOrEmpty(fileToken)
           && !string.IsNullOrEmpty(snapshotPath)
           && string.Equals(snapshotPath, resolvedPath, StringComparison.OrdinalIgnoreCase);
}
