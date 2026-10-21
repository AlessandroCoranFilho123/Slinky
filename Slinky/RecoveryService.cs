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
            await ApplicationData.Current.LocalFolder.CreateFolderAsync(FolderName, CreationCollisionOption.OpenIfExists);

        public static async Task SaveSnapshotAsync(Guid recoveryId, RecoverySnapshot snapshot)
        {
            await Gate.WaitAsync();
            StorageFile? temporaryFile = null;

            try
            {
                var folder = await GetRecoveryFolderAsync();
                var fileName = $"{recoveryId}.json";
                var temporaryName = $"{fileName}.{Guid.NewGuid():N}.tmp";
                temporaryFile = await folder.CreateFileAsync(temporaryName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(temporaryFile, RecoverySnapshotCodec.Serialize(snapshot));
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

        public static async Task DeleteSnapshotAsync(Guid recoveryId)
        {
            await Gate.WaitAsync();

            try
            {
                var folder = await GetRecoveryFolderAsync();
                if (await folder.TryGetItemAsync($"{recoveryId}.json") is StorageFile snapshotFile)
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
                    if (RecoverySnapshotCodec.TryDeserialize(json, out var snapshot) && snapshot is not null)
                    {
                        results.Add((file, snapshot));
                    }
                }
                catch
                {
                    // Skip anything unreadable — recovery is best-effort, not a hard requirement.
                }
            }

            return results;
        }
    }
}
