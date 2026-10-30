using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Windows.Storage;
using Slinky;

namespace Slinky.Services
{
    /// <summary>
    /// Every open Note periodically writes a small JSON snapshot of its text to this folder
    /// (see Note.ScheduleAutosave). A cleanly-closed tab deletes its own snapshot; anything
    /// still here at the next startup means the app didn't shut down cleanly last time.
    /// </summary>
    public static class RecoveryService
    {
        private const string FolderName = "Recovery";
        private static readonly SemaphoreSlim Gate = new(1, 1);

        public static async Task<StorageFolder> GetRecoveryFolderAsync() =>
            await (await StateFolders.LocalFolderProvider()).CreateFolderAsync(FolderName, CreationCollisionOption.OpenIfExists);

        // Snapshot generations invalidated by ClearRecoveryAsync, per snapshot file.
        // Guarded by Gate (every read/write of this dictionary holds it). A tick that
        // captured an older generation must not resurrect the snapshot after a save,
        // close or delete already removed it (race R1/R2 of RACE-AUDIT-2026-10-30).
        private static readonly Dictionary<string, int> InvalidatedGenerations = new();

        public static async Task SaveSnapshotAsync(Guid recoveryId, RecoverySnapshot snapshot, int generation)
        {
            await Gate.WaitAsync();
            StorageFile? temporaryFile = null;
            var fileName = $"{recoveryId}.json";

            try
            {
                var folder = await GetRecoveryFolderAsync();
                if (InvalidatedGenerations.TryGetValue(fileName, out var invalidated))
                {
                    if (generation < invalidated)
                    {
                        // The tick was scheduled before a save/close/delete that already
                        // cleared this snapshot: refuse the stale write and clean up.
                        // (A tick with the SAME generation as the clear is a fresh,
                        // post-clear edit and must be written.)
                        InvalidatedGenerations.Remove(fileName);
                        if (await folder.TryGetItemAsync(fileName) is StorageFile stale)
                        {
                            await stale.DeleteAsync();
                        }
                        return;
                    }

                    InvalidatedGenerations.Remove(fileName);
                }

                var temporaryName = $"{fileName}.{Guid.NewGuid():N}.tmp";
                temporaryFile = await folder.CreateFileAsync(temporaryName, CreationCollisionOption.ReplaceExisting);
                // JsonSerializer walks the whole document text (megabytes on large
                // notes); off the UI thread it must not freeze typing or saving.
                var json = await Task.Run(() => RecoverySnapshotCodec.Serialize(snapshot));
                await FileIO.WriteTextAsync(temporaryFile, json);
                await temporaryFile.MoveAsync(folder, fileName, NameCollisionOption.ReplaceExisting);
                temporaryFile = null;
            }
            finally
            {
                if (temporaryFile is not null)
                {
                    try { await temporaryFile.DeleteAsync(); } catch { }
                }

                Gate.Release();
            }
        }

        public static async Task DeleteSnapshotAsync(Guid recoveryId, int generation)
        {
            await Gate.WaitAsync();

            try
            {
                var fileName = $"{recoveryId}.json";
                InvalidatedGenerations[fileName] = Math.Max(
                    InvalidatedGenerations.TryGetValue(fileName, out var current) ? current : 0,
                    generation);

                var folder = await GetRecoveryFolderAsync();
                if (await folder.TryGetItemAsync(fileName) is StorageFile snapshotFile)
                {
                    await snapshotFile.DeleteAsync();
                }
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>Reads every leftover snapshot file. Corrupt/partial files are skipped rather than blocking startup.</summary>
        public static async Task<List<(StorageFile File, RecoverySnapshot Snapshot)>> FindLeftoverSnapshotsAsync()
        {
            var results = new List<(StorageFile, RecoverySnapshot)>();
            var folder = await GetRecoveryFolderAsync();
            var files = await folder.GetFilesAsync();

            foreach (var file in files)
            {
                try
                {
                    var json = await FileIO.ReadTextAsync(file);
                    RecoverySnapshot? snapshot = null;
                    // Startup path: deserializing a multi-megabyte snapshot is CPU
                    // work that must not run on the UI thread.
                    var deserialized = await Task.Run(() => RecoverySnapshotCodec.TryDeserialize(json, out snapshot));
                    if (deserialized && snapshot is not null)
                    {
                        if (!IsOwnedByLiveProcess(snapshot.OwnerProcessId))
                        {
                            results.Add((file, snapshot));
                        }
                    }
                }
                catch
                {
                    // Skip anything unreadable — recovery is best-effort, not a hard requirement.
                }
            }

            return results;
        }

        private static bool IsOwnedByLiveProcess(int processId)
        {
            if (processId <= 0)
            {
                return false;
            }

            if (processId == Environment.ProcessId)
            {
                // Startup runs before this process has produced a new snapshot; treat
                // a reused PID as stale rather than hiding recoverable content.
                return false;
            }

            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }
}
