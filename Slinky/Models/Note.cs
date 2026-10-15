using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.Storage;
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

                if (value is not null)
                {
                    _ = RecentFilesService.AddAsync(value);
                }
            }
        }

        private string text = string.Empty;
        public string Text
        {
            get => text;
            set
            {
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
                Text = await FileIO.ReadTextAsync(fileToOpen);
                File = fileToOpen;
                IsDirty = false;
                LastError = null;
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
                await FileIO.WriteTextAsync(File, Text);
                IsDirty = false;
                LastError = null;
                await ClearRecoveryAsync();
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
                savePicker.FileTypeChoices.Add("Documento de texto", new List<string> { ".txt" });
                savePicker.SuggestedFileName = File is not null
                    ? System.IO.Path.GetFileNameWithoutExtension(File.Name)
                    : "Nova nota";

                var chosenFile = await savePicker.PickSaveFileAsync();
                if (chosenFile is null)
                {
                    return false; // user cancelled — not an error, LastError stays null
                }

                await FileIO.WriteTextAsync(chosenFile, Text);
                File = chosenFile;
                IsDirty = false;
                LastError = null;
                await ClearRecoveryAsync();
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Não foi possível salvar: {ex.Message}";
                return false;
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
            // We only recover the text, not the file link: reconnecting to snapshot.FilePath would
            // need StorageFile.GetFileFromPathAsync, which needs broad filesystem access this app
            // doesn't request. "Salvar como" reconnects it to a real file if the user wants that.
            Text = snapshot.Text;
            IsDirty = true;
        }

        /// <summary>Restarts (debounced) after every edit; only the last edit in a ~2s burst actually hits disk.</summary>
        private void ScheduleAutosave()
        {
            try
            {
                if (autosaveTimer is null)
                {
                    autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    autosaveTimer.Tick += AutosaveTimer_Tick;
                }

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

            if (!IsDirty)
            {
                return;
            }

            try
            {
                var folder = await RecoveryService.GetRecoveryFolderAsync();
                var snapshotFile = await folder.CreateFileAsync($"{recoveryId}.json", CreationCollisionOption.ReplaceExisting);
                var snapshot = new RecoverySnapshot { Text = Text, FilePath = File?.Path, SavedAt = DateTime.Now };
                await FileIO.WriteTextAsync(snapshotFile, JsonSerializer.Serialize(snapshot));
            }
            catch
            {
                // Recovery is a best-effort safety net; a failure here shouldn't interrupt editing.
            }
        }

        /// <summary>Call when this note is closed cleanly (saved, discarded on purpose, or deleted) so its recovery snapshot doesn't linger.</summary>
        public async Task ClearRecoveryAsync()
        {
            autosaveTimer?.Stop();

            try
            {
                var folder = await RecoveryService.GetRecoveryFolderAsync();
                if (await folder.TryGetItemAsync($"{recoveryId}.json") is StorageFile snapshotFile)
                {
                    await snapshotFile.DeleteAsync();
                }
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
