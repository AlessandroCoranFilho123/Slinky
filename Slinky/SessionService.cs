using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Slinky;
using System;

namespace Slinky.Services
{
    /// <summary>
    /// Remembers which real files are open right now, so the next launch can reopen them —
    /// separate from RecoveryService, which is specifically about unsaved *text* surviving a
    /// crash. This is a plain convenience (like a browser reopening your tabs), not a safety
    /// net, so it only ever tracks notes that already have a file; a blank/never-saved note
    /// has nothing stable to reopen anyway.
    ///
    /// Backed by FutureAccessList (as opposed to RecentFilesService's MostRecentlyUsedList) so
    /// the two features don't compete for the same WinRT list's capacity.
    /// </summary>
    public static class SessionService
    {
        private const string SessionFileName = "session.json";
        private const string TemporarySessionFileName = "session.json.tmp";
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static readonly List<string> CurrentTokens = new();

        /// <summary>Call whenever the set of open, file-backed tabs changes (a tab opens or closes).</summary>
        public static async Task SaveOpenFilesAsync(IEnumerable<StorageFile> openFiles)
        {
            await Gate.WaitAsync();
            try
            {
                var tokens = new List<string>();

                foreach (var oldToken in CurrentTokens)
                {
                    try { StorageApplicationPermissions.FutureAccessList.Remove(oldToken); } catch { }
                }
                CurrentTokens.Clear();

                foreach (var file in openFiles)
                {
                    try
                    {
                        var token = StorageApplicationPermissions.FutureAccessList.Add(file, file.Path);
                        tokens.Add(token);
                        CurrentTokens.Add(token);
                    }
                    catch
                    {
                        // list may be full — just skip this one, session restore is best-effort
                    }
                }

                var folder = ApplicationData.Current.LocalFolder;
                var temporaryFile = await folder.CreateFileAsync(TemporarySessionFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(temporaryFile, SessionStateCodec.Serialize(tokens));
                await temporaryFile.MoveAsync(folder, SessionFileName, NameCollisionOption.ReplaceExisting);
            }
            catch
            {
                // best-effort; never let this interrupt normal tab open/close
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>
        /// Reads back whatever was open last time and consumes the session file — a fresh one
        /// gets written as soon as tabs open again, so this never doubles up on relaunch.
        /// </summary>
        public static async Task<List<StorageFile>> LoadAndClearAsync()
        {
            var result = new List<StorageFile>();

            await Gate.WaitAsync();

            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                if (await folder.TryGetItemAsync(SessionFileName) is not StorageFile sessionFile)
                {
                    return result;
                }

                var json = await FileIO.ReadTextAsync(sessionFile);
                if (!SessionStateCodec.TryDeserialize(json, out var tokens))
                {
                    await sessionFile.DeleteAsync();
                    return result;
                }

                foreach (var token in tokens)
                {
                    try
                    {
                        result.Add(await StorageApplicationPermissions.FutureAccessList.GetFileAsync(token));
                    }
                    catch
                    {
                        // moved/deleted since — just skip it
                    }
                    finally
                    {
                        try { StorageApplicationPermissions.FutureAccessList.Remove(token); } catch { }
                    }
                }

                await sessionFile.DeleteAsync();
                CurrentTokens.Clear();
            }
            catch
            {
                // best-effort
            }
            finally
            {
                Gate.Release();
            }

            return result;
        }
    }
}
