using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Slinky; // for App.Window, used by the save picker to know which window to appear over
using Slinky.Services;

namespace Slinky.Models
{
    /// <summary>
    /// A single open document: its text, the file it's backed by (if any),
    /// and whether it currently has unsaved changes. Notifies the UI
    /// (tab header, toolbar) whenever any of that changes.
    ///
    /// Every disk operation (Open/Save/SaveAs/Delete) catches its own exceptions
    /// (locked files, permission errors, missing paths, etc.) and reports them
    /// through LastError instead of throwing — callers running inside an
    /// "async void" event handler would otherwise crash the whole app on a
    /// simple file hiccup.
    ///
    /// While dirty, this also writes a periodic recovery snapshot (see
    /// RecoveryService) a couple of seconds after the user stops typing, so a
    /// crash or unexpected close doesn't lose everything. ClearRecoveryAsync
    /// removes that snapshot once the note is closed cleanly.
    /// </summary>
    public class Note : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Set after any operation below returns false due to a genuine error (not a user cancellation).</summary>
        public string? LastError { get; private set; }

        private readonly Guid recoveryId = Guid.NewGuid();
        private DispatcherTimer? autosaveTimer;
        private readonly RecoveryLifecycle recoveryLifecycle = new();
        private DateTime? autosavePendingSince;
        // Incremented whenever a snapshot must be invalidated (save, close, delete);
        // autosave writes carry the generation they were created with.
        private int snapshotGeneration;
        private static readonly TimeSpan AutosaveDebounce = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan AutosaveMaximumDelay = TimeSpan.FromSeconds(30);

        private StorageFile? file;
        public StorageFile? File
        {
            get => file;
            private set
            {
                file = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(PathDisplay));

            }
        }

        private string text = string.Empty;
        public TextEncodingKind EncodingKind { get; private set; } = TextEncodingKind.Utf8;
        public bool HasBom { get; private set; }
        public LineEndingKind LineEnding { get; private set; } = LineEndingKind.CrLf;

        public string Text
        {
            get => text;
            set
            {
                // WinUI TextBox can report CR-only line breaks after editing. Keep the
                // document invariant independent of the control implementation; the
                // original on-disk style remains in LineEnding for the next save.
                value = TextFileCodec.NormalizeLineEndings(value ?? string.Empty, out _);
                if (text == value) return;
                text = value;
                IsDirty = true;
                OnPropertyChanged();
                ScheduleAutosave();
            }
        }

        private bool isDirty;
        public bool IsDirty
        {
            get => isDirty;
            private set
            {
                if (isDirty == value) return;
                isDirty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        /// <summary>What to show as the tab's title: the filename, plus a dot if there are unsaved changes.</summary>
        public string DisplayName => (File?.Name ?? "Sem título") + (IsDirty ? " •" : string.Empty);

        /// <summary>Full path for display in the toolbar. Empty for a note that was never saved.</summary>
        public string PathDisplay => File?.Path ?? string.Empty;

        public DateTime CreatedAt { get; } = DateTime.Now;

        /// <summary>Loads an existing text file into this note. Returns false (with LastError set) on failure.</summary>
        public async Task<bool> OpenAsync(StorageFile fileToOpen)
        {
            try
            {
                var buffer = await FileIO.ReadBufferAsync(fileToOpen);
                CryptographicBuffer.CopyToByteArray(buffer, out var bytes);
                var decoded = TextFileCodec.Decode(bytes);
                text = decoded.Text;
                EncodingKind = decoded.Encoding;
                HasBom = decoded.HasBom;
                LineEnding = decoded.LineEnding;
                OnPropertyChanged(nameof(Text));
                File = fileToOpen;
                IsDirty = false;
                LastError = null;
                await RecentFilesService.AddAsync(fileToOpen);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Não foi possível abrir o arquivo: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Saves to the existing file, or falls back to "Salvar como" if this note has none yet.
        /// Returns false both when the user cancels a picker and when saving genuinely fails —
        /// check LastError to tell the two apart (it's null on cancellation).
        /// </summary>
        public async Task<bool> SaveAsync()
        {
            if (File is null)
            {
                return await SaveAsAsync();
            }

            try
            {
                await WriteBytesSafeAsync(File, TextFileCodec.Encode(Text, EncodingKind, HasBom, LineEnding));
                IsDirty = false;
                LastError = null;
                await RecentFilesService.AddAsync(File);
                await ClearRecoveryAsync(closeNote: false);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Não foi possível salvar: {ex.Message}";
                return false;
            }
        }

        /// <summary>Always prompts for a location, even if the note already has a file.</summary>
        public async Task<bool> SaveAsAsync()
        {
            try
            {
                LastError = null;

                var savePicker = new FileSavePicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Window!);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("Documento de texto", new List<string>
                {
                    ".txt", ".log", ".ini", ".json", ".csv", ".md"
                });
                savePicker.SuggestedFileName = File is not null
                    ? System.IO.Path.GetFileNameWithoutExtension(File.Name)
                    : "Nova nota";

                var chosenFile = await savePicker.PickSaveFileAsync();
                if (chosenFile is null)
                {
                    return false; // user cancelled — not an error, LastError stays null
                }

                await WriteBytesSafeAsync(chosenFile, TextFileCodec.Encode(Text, EncodingKind, HasBom, LineEnding));
                await RecentFilesService.AddAsync(chosenFile);
                File = chosenFile;
                IsDirty = false;
                LastError = null;
                await ClearRecoveryAsync(closeNote: false);
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Não foi possível salvar: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Writes bytes without risking a torn file if the process dies mid-write:
        /// the payload goes to a temporary file in the same folder and replaces the
        /// target with a rename. Falls back to a direct write when the folder cannot
        /// host the temporary file (permissions, path length) — saving must not fail
        /// because of the safety net.
        /// </summary>
        private static async Task WriteBytesSafeAsync(StorageFile target, byte[] bytes)
        {
            StorageFile? temporary = null;
            try
            {
                var folder = await target.GetParentAsync();
                if (folder is null)
                {
                    await FileIO.WriteBytesAsync(target, bytes);
                    return;
                }

                temporary = await folder.CreateFileAsync($"{target.Name}.{Guid.NewGuid():N}.tmp", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteBytesAsync(temporary, bytes);
                await temporary.MoveAndReplaceAsync(target);
                temporary = null;
            }
            catch
            {
                try { if (temporary is not null) { await temporary.DeleteAsync(); } } catch { }
                await FileIO.WriteBytesAsync(target, bytes);
            }
        }

        /// <summary>Deletes the backing file from disk, if there is one. Returns false (with LastError set) on failure.</summary>
        public async Task<bool> DeleteAsync()
        {
            try
            {
                if (File is not null)
                {
                    await File.DeleteAsync();
                }
                // The user explicitly confirmed discarding this note's content, so a
                // later close must not prompt "unsaved changes" over a deleted file.
                IsDirty = false;
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Não foi possível excluir: {ex.Message}";
                return false;
            }
        }

        // ----- Crash recovery ------------------------------

        /// <summary>Restores text recovered from a previous, uncleanly-closed session.</summary>
        public void RestoreFromSnapshot(RecoverySnapshot snapshot)
        {
            Text = snapshot.Text;
            EncodingKind = snapshot.Encoding;
            HasBom = snapshot.HasBom;
            LineEnding = snapshot.LineEnding;
            IsDirty = true;
        }

        /// <summary>
        /// Reattaches recovered text to its original file via the FutureAccessList token
        /// captured in the snapshot, so a later Ctrl+S writes to the same document instead
        /// of forcing "Salvar como" — matching the modern Notepad recovery model. Returns
        /// false when there is no token, the token no longer resolves, or the resolved
        /// file is not the snapshot's path; the note then stays untitled.
        /// </summary>
        public async Task<bool> TryReattachSavedFileAsync(RecoverySnapshot snapshot)
        {
            if (!RecoveryReattachPolicy.CanReattach(snapshot.FileToken, snapshot.FilePath, snapshot.FilePath ?? string.Empty))
            {
                return false;
            }

            try
            {
                var reattached = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(snapshot.FileToken!);
                if (!RecoveryReattachPolicy.CanReattach(snapshot.FileToken, snapshot.FilePath, reattached.Path))
                {
                    return false;
                }

                File = reattached;
                EncodingKind = snapshot.Encoding;
                HasBom = snapshot.HasBom;
                LineEnding = snapshot.LineEnding;
                IsDirty = true;
                await RecentFilesService.AddAsync(reattached);
                return true;
            }
            catch
            {
                // Token expired, removed by a session rewrite, or file moved/deleted since:
                // the note stays untitled with the recovered text, as in previous versions.
                return false;
            }
        }

        /// <summary>Restarts (debounced) after every edit; only the last edit in a ~2s burst actually hits disk.</summary>
        private void ScheduleAutosave()
        {
            try
            {
                if (!recoveryLifecycle.CanSchedule)
                {
                    return;
                }

                autosavePendingSince ??= DateTime.UtcNow;

                if (autosaveTimer is null)
                {
                    autosaveTimer = new DispatcherTimer();
                    autosaveTimer.Tick += AutosaveTimer_Tick;
                }

                var remaining = AutosaveMaximumDelay - (DateTime.UtcNow - autosavePendingSince.Value);
                autosaveTimer.Interval = remaining <= TimeSpan.Zero
                    ? TimeSpan.FromMilliseconds(1)
                    : remaining < AutosaveDebounce ? remaining : AutosaveDebounce;
                autosaveTimer.Stop();
                autosaveTimer.Start();
            }
            catch
            {
                // No DispatcherQueue on this thread (e.g. a unit test host, or some other
                // non-UI context) — autosave just won't schedule there. Never let this block
                // normal editing, which is the only thing that actually matters here.
            }
        }

        private async void AutosaveTimer_Tick(object? sender, object e)
        {
            autosaveTimer?.Stop();

            if (!recoveryLifecycle.CanSchedule || !IsDirty)
            {
                return;
            }

            try
            {
                if (!recoveryLifecycle.TryBeginWrite() || !IsDirty)
                {
                    return;
                }

                // The generation captured here is checked after the write: if the user
                // saved/closed/deleted while the snapshot was in flight (generation moved),
                // RecoveryService refuses the stale write instead of resurrecting it.
                var generation = snapshotGeneration;

                var snapshot = new RecoverySnapshot
                {
                    OwnerProcessId = Environment.ProcessId,
                    Text = Text,
                    FilePath = File?.Path,
                    FileToken = File is null ? null : SessionService.TokenForPath(File.Path),
                    SavedAt = DateTime.Now,
                    Encoding = EncodingKind,
                    HasBom = HasBom,
                    LineEnding = LineEnding
                };
                await RecoveryService.SaveSnapshotAsync(recoveryId, snapshot, generation);
                autosavePendingSince = null;
            }
            catch
            {
                // Recovery is a best-effort safety net; a failure here shouldn't interrupt editing.
            }
        }

        /// <summary>Call when this note is closed cleanly (saved, discarded on purpose, or deleted) so its recovery snapshot doesn't linger.</summary>
        public async Task ClearRecoveryAsync(bool closeNote = true)
        {
            if (closeNote)
            {
                recoveryLifecycle.Close();
            }

            autosaveTimer?.Stop();
            autosavePendingSince = null;
            // Invalidates any autosave already in flight: its write carries the older
            // generation and RecoveryService will refuse it (race R1/R2).
            snapshotGeneration++;

            try
            {
                await RecoveryService.DeleteSnapshotAsync(recoveryId, snapshotGeneration);
            }
            catch
            {
                // Non-critical cleanup — a leftover file just means one extra (harmless) restore prompt later.
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
