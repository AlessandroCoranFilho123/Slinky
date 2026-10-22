using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Slinky;

namespace Slinky.Services
{
    public sealed class RecentFileEntry
    {
        public string Token { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    /// <summary>
    /// "Abrir recente" — backed by StorageApplicationPermissions.MostRecentlyUsedList, which is
    /// what lets Slinky reopen a file the user picked in a *previous* session without needing
    /// the broad filesystem-access capability (and without re-prompting via a picker).
    /// The actual ordered list of entries is kept in a small JSON file since the WinRT list
    /// itself doesn't expose "most recent first" ordering — Slinky owns that ordering itself.
    /// </summary>
    public static class RecentFilesService
    {
        private const string SettingsFileName = "recent-files.json";
        private const string TemporarySettingsFileName = "recent-files.json.tmp";
        private const int MaxEntries = 10;
        // The lock covers the complete read-modify-write transaction. Locking only
        // SaveAsync still allows two AddAsync calls to read the same old list and then
        // overwrite one another with different results.
        private static readonly SemaphoreSlim Gate = new(1, 1);

        public static async Task<List<RecentFileEntry>> LoadAsync()
        {
            await Gate.WaitAsync();
            try
            {
                return await LoadCoreAsync();
            }
            catch
            {
                return new List<RecentFileEntry>();
            }
            finally
            {
                Gate.Release();
            }
        }

        private static async Task<List<RecentFileEntry>> LoadCoreAsync()
        {
            var folder = ApplicationData.Current.LocalFolder;
            if (await folder.TryGetItemAsync(SettingsFileName) is not StorageFile file)
            {
                return new List<RecentFileEntry>();
            }

            var json = await FileIO.ReadTextAsync(file);
            return System.Text.Json.JsonSerializer.Deserialize<List<RecentFileEntry>>(json) ?? new List<RecentFileEntry>();
        }

        private static async Task SaveCoreAsync(List<RecentFileEntry> entries)
        {
            var folder = ApplicationData.Current.LocalFolder;
            var temporaryFile = await folder.CreateFileAsync(TemporarySettingsFileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(temporaryFile, System.Text.Json.JsonSerializer.Serialize(entries));
            await temporaryFile.MoveAsync(folder, SettingsFileName, NameCollisionOption.ReplaceExisting);
        }

        /// <summary>Records (or bumps to the top) a file that was just opened or saved.</summary>
        public static async Task AddAsync(StorageFile file)
        {
            await Gate.WaitAsync();
            try
            {
                var entries = await LoadCoreAsync();
                string token;
                try
                {
                    token = StorageApplicationPermissions.MostRecentlyUsedList.Add(file, file.Path);
                }
                catch
                {
                    return; // the WinRT list may be full or unavailable — MRU just won't track this one
                }

                var previousTokens = entries.Select(entry => entry.Token).ToHashSet(StringComparer.Ordinal);
                var updated = RecentFileListPolicy.Insert(
                    entries.Select(entry => new RecentFileListEntry(entry.Token, entry.DisplayName, entry.Path)),
                    new RecentFileListEntry(token, file.Name, file.Path),
                    MaxEntries);

                foreach (var removedToken in previousTokens.Except(updated.Select(entry => entry.Token), StringComparer.Ordinal))
                {
                    TryRemoveFromList(removedToken);
                }

                entries = updated
                    .Select(entry => new RecentFileEntry { Token = entry.Token, DisplayName = entry.DisplayName, Path = entry.Path })
                    .ToList();

                await SaveCoreAsync(entries);
            }
            catch
            {
                // MRU is a convenience, not critical data — never let this interrupt normal use.
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>Re-resolves a StorageFile from a stored entry. Returns null if the file moved/was deleted.</summary>
        public static async Task<StorageFile?> TryOpenAsync(RecentFileEntry entry)
        {
            try
            {
                return await StorageApplicationPermissions.MostRecentlyUsedList.GetFileAsync(entry.Token);
            }
            catch
            {
                return null;
            }
        }

        public static async Task RemoveAsync(RecentFileEntry entry)
        {
            await Gate.WaitAsync();
            try
            {
                var entries = await LoadCoreAsync();
                entries.RemoveAll(e => e.Token == entry.Token);
                TryRemoveFromList(entry.Token);
                await SaveCoreAsync(entries);
            }
            catch
            {
                // MRU is a convenience, not critical data — never let this interrupt normal use.
            }
            finally
            {
                Gate.Release();
            }
        }

        private static void TryRemoveFromList(string token)
        {
            try
            {
                StorageApplicationPermissions.MostRecentlyUsedList.Remove(token);
            }
            catch
            {
                // token already gone — fine
            }
        }
    }
}
