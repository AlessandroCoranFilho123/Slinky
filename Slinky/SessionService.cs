using System.Collections.Generic;
using System.Linq;
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
        private const string LegacySessionFileName = "session.json";
        private static string SessionFileName => $"session-{Environment.ProcessId}.json";
        private static string TemporarySessionFileName => $"session-{Environment.ProcessId}.json.tmp";
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static readonly Dictionary<string, string> PathToToken = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The FutureAccessList token currently held for this path, or null. Used by the
        /// recovery snapshot writer so a restored note can be reattached to its file.
        /// </summary>
        public static string? TokenForPath(string? path)
            => path is not null && PathToToken.TryGetValue(path, out var token) ? token : null;

        /// <summary>Call whenever the set of open, file-backed tabs changes (a tab opens or closes).</summary>
        public static async Task SaveOpenFilesAsync(IEnumerable<StorageFile> openFiles)
        {
            await Gate.WaitAsync();
            try
            {
                var tokens = new List<string>();

                foreach (var oldToken in PathToToken.Values)
                {
                    try { StorageApplicationPermissions.FutureAccessList.Remove(oldToken); } catch { }
                }
                PathToToken.Clear();

                foreach (var file in openFiles)
                {
                    try
                    {
                        var token = StorageApplicationPermissions.FutureAccessList.Add(file, file.Path);
                        tokens.Add(token);
                        PathToToken[file.Path] = token;
                    }
                    catch
                    {
                        // list may be full — just skip this one, session restore is best-effort
                    }
                }

                var folder = await StateFolders.LocalFolderProvider();
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
                var folder = await StateFolders.LocalFolderProvider();
                var files = await folder.GetFilesAsync();

                // Startup-only path: leftover .tmp files mean a process died between
                // the write and the atomic replace — never usable, just cleanup.
                foreach (var file in files.Where(f => f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)))
                {
                    try { await file.DeleteAsync(); } catch { }
                }

                var candidates = files
                    .Where(file => IsSessionFile(file.Name) && !IsOwnedByLiveProcess(file.Name))
                    .OrderByDescending(file => file.DateCreated);

                foreach (var sessionFile in candidates)
                {
                    if (!SessionStateCodec.TryDeserialize(await FileIO.ReadTextAsync(sessionFile), out var tokens))
                    {
                        await sessionFile.DeleteAsync();
                        continue;
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
                }
                PathToToken.Clear();
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

        private static bool IsSessionFile(string fileName)
            => fileName.Equals(LegacySessionFileName, StringComparison.OrdinalIgnoreCase)
               || (fileName.StartsWith("session-", StringComparison.OrdinalIgnoreCase)
                   && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        private static bool IsOwnedByLiveProcess(string fileName)
        {
            if (!fileName.StartsWith("session-", StringComparison.OrdinalIgnoreCase))
            {
                // The legacy file has no owner metadata and is safe to consume once.
                return false;
            }

            var pidText = fileName["session-".Length..^".json".Length];
            if (!int.TryParse(pidText, out var processId))
            {
                return false;
            }

            if (processId == Environment.ProcessId)
            {
                // This process has not written its session yet during startup. A reused
                // PID therefore represents stale state, not a live owner.
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
