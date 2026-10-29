using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Printing;
using Microsoft.UI.Windowing;
using Slinky.Models;
using Slinky.Services;
using Slinky.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Printing;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Microsoft.UI;

namespace Slinky
{
    public sealed partial class MainWindow : Window
    {
        public ThemeService Theme => ThemeService.Instance;
        private readonly ThemeTabStateHub tabThemeStates = new(ThemeService.Instance.Current);

        // ----- Printing state -----------------------------------------------------------
        private PrintDocument? printDocument;
        private IPrintDocumentSource? printDocumentSource;
        private readonly List<UIElement> printPreviewPages = new();
        private string printContentText = string.Empty;
        private string printContentTitle = "Slinky";
        private PrintSettings currentPrintSettings = new();
        private bool closeConfirmed;
        private bool closePromptOpen;
        // Only one ContentDialog may be open at a time (XAML rule); every ShowAsync
        // site below sets this so shortcuts and menu handlers can wait instead of
        // throwing COMException 0x80000019.
        private bool contentDialogOpen;
        private bool startupRestorationInProgress = true;

        private sealed class PrintSettings
        {
            public bool Landscape;
            public double MarginInches = 1.0;
            public double ScalePercent = 100.0;
        }

        public MainWindow()
        {
            this.InitializeComponent();
            AddMainKeyboardAccelerators();
            WindowIconService.Apply(this);
            AppWindow.Closing += MainAppWindow_Closing;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            ThemeService.Instance.ThemeChanged += ThemeService_ThemeChanged;
            Closed += MainWindow_Closed;
            ApplyElementTheme();

            try
            {
                RegisterForPrinting();
            }
            catch
            {
                // Printing just won't be available in this environment; the rest of the app still works.
            }

            // The initial blank note is created after session/recovery restoration.
            // Creating it here would leave an extra empty tab on every restored session.
        }

        private void AddMainKeyboardAccelerators()
        {
            // Windows.System.VirtualKey does not expose names for the main keyboard
            // '+'/'-' OEM scan codes in the SDK used by the XAML compiler. Their
            // documented virtual-key values are 0xBB and 0xBD, so add them at runtime
            // while retaining Add/Subtract for numeric-keypad users.
            var plus = new KeyboardAccelerator
            {
                Key = (VirtualKey)0xBB,
                Modifiers = VirtualKeyModifiers.Control
            };
            plus.Invoked += ZoomInAccelerator_Invoked;

            var minus = new KeyboardAccelerator
            {
                Key = (VirtualKey)0xBD,
                Modifiers = VirtualKeyModifiers.Control
            };
            minus.Invoked += ZoomOutAccelerator_Invoked;

            RootGrid.KeyboardAccelerators.Add(plus);
            RootGrid.KeyboardAccelerators.Add(minus);
        }

        private void ApplyElementTheme()
        {
            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = ThemeService.Instance.ElementTheme;
            }

            if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            {
                var buttonColor = ThemeService.Instance.ElementTheme == ElementTheme.Light ? Colors.Black : Colors.White;
                AppWindow.TitleBar.ButtonForegroundColor = buttonColor;
                AppWindow.TitleBar.ButtonHoverForegroundColor = buttonColor;
                AppWindow.TitleBar.ButtonPressedForegroundColor = buttonColor;
            }

            UpdateTabViewThemeResources();
        }

        private void ThemeService_ThemeChanged(object? sender, EventArgs e) => ApplyElementTheme();

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            ThemeService.Instance.ThemeChanged -= ThemeService_ThemeChanged;
            Closed -= MainWindow_Closed;
        }

        private void UpdateTabViewThemeResources()
        {
            var theme = ThemeService.Instance;

            // Keep defaults for containers created by TabView and also write the same
            // resources into every existing item. TabView caches the visual tree of an
            // existing TabViewItem, so changing only TabView.Resources leaves old tabs
            // stale when both themes share the same Light/Dark ElementTheme.
            NotesTabView.Resources["TabViewItemHeaderBackground"] = theme.Surface;
            NotesTabView.Resources["TabViewItemHeaderBackgroundSelected"] = theme.Background;
            NotesTabView.Resources["TabViewItemHeaderBackgroundPointerOver"] = theme.Background;
            NotesTabView.Resources["TabViewItemHeaderBackgroundSelectedPointerOver"] = theme.Background;
            NotesTabView.Resources["TabViewItemHeaderForeground"] = theme.Foreground;
            NotesTabView.Resources["TabViewItemHeaderForegroundSelected"] = theme.Foreground;
            NotesTabView.Resources["TabViewItemHeaderForegroundPointerOver"] = theme.Foreground;
            NotesTabView.Resources["TabViewItemHeaderForegroundSelectedPointerOver"] = theme.Foreground;

            tabThemeStates.Apply(theme.Current);
        }

        // ----- Startup: crash recovery, session restore, recent files ----------------

        private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            RootGrid.Loaded -= RootGrid_Loaded; // only run this once, at startup

            try
            {
                var recoveredPaths = await CheckForRecoverableNotesAsync();
                await RestoreSessionAsync(recoveredPaths);
                await RefreshRecentFilesMenuAsync();

            }
            finally
            {
                if (NotesTabView.TabItems.Count == 0)
                {
                    AddNoteTab(new Note());
                }

                startupRestorationInProgress = false;
                await RefreshSessionAsync();
            }
        }

        /// <summary>
        /// The TabView can measure a single-line note to its content height. In that state,
        /// the blank area below the first line belongs to the window rather than to NotePage,
        /// so the page-level tap handler cannot receive it. Preserve the editor's valid caret
        /// when that window-level surface is clicked, without inventing a document line.
        /// </summary>
        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            PreserveEditorFocusForWindowSurface(e.OriginalSource as DependencyObject);
        }

        private void RootGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            PreserveEditorFocusForWindowSurface(e.OriginalSource as DependencyObject);
        }

        private void PreserveEditorFocusForWindowSurface(DependencyObject? source)
        {
            var activePage = GetActiveNotePage();

            if (source is null || activePage is null ||
                IsDescendantOf(source, NotesTabView) ||
                IsDescendantOf(source, MainMenuBar) ||
                IsDescendantOf(source, AppTitleBar) ||
                IsDescendantOf(source, activePage))
            {
                return;
            }

            activePage.FocusEditorWithoutMovingCaret();
        }

        private static bool IsDescendantOf(DependencyObject element, DependencyObject ancestor)
        {
            var current = element;
            while (current is not null)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        /// <summary>Returns the file paths (if any) that were just restored, so session-restore can skip re-opening the same files stale.</summary>
        private async Task<HashSet<string>> CheckForRecoverableNotesAsync()
        {
            var recoveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var leftovers = await RecoveryService.FindLeftoverSnapshotsAsync();
            if (leftovers.Count == 0)
            {
                return recoveredPaths;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Title = "Notas não salvas encontradas",
                Content = $"O Slinky parece ter sido fechado sem salvar {leftovers.Count} nota(s) da última vez. Restaurar o conteúdo?",
                PrimaryButtonText = "Restaurar",
                CloseButtonText = "Descartar",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                foreach (var (_, snapshot) in leftovers)
                {
                    var note = new Note();
                    note.RestoreFromSnapshot(snapshot);
                    await note.TryReattachSavedFileAsync(snapshot);
                    AddNoteTab(note);

                    if (!string.IsNullOrEmpty(snapshot.FilePath))
                    {
                        recoveredPaths.Add(snapshot.FilePath);
                    }
                }
            }

            // Whether restored or discarded, clean up the leftover files so they don't keep reappearing.
            foreach (var (snapshotFile, _) in leftovers)
            {
                try { await snapshotFile.DeleteAsync(); } catch { }
            }

            return recoveredPaths;
        }

        /// <summary>Reopens whatever real files were open last time — a plain convenience, unrelated to crash recovery.</summary>
        private async Task RestoreSessionAsync(HashSet<string> excludePaths)
        {
            var files = await SessionService.LoadAndClearAsync();

            foreach (var file in SessionRestorePolicy.Filter(files, file => file.Path, excludePaths))
            {
                var note = new Note();
                if (await note.OpenAsync(file))
                {
                    AddNoteTab(note);
                }
            }
        }

        private async Task RefreshSessionAsync()
        {
            var openFiles = NotesTabView.TabItems.OfType<TabViewItem>()
                .Select(t => t.Tag as Note)
                .Where(n => n?.File is not null)
                .Select(n => n!.File!);

            await SessionService.SaveOpenFilesAsync(openFiles);
        }

        private async Task RefreshRecentFilesMenuAsync()
        {
            var entries = await RecentFilesService.LoadAsync();

            RecentFilesMenuItem.Items.Clear();

            if (entries.Count == 0)
            {
                RecentFilesMenuItem.Items.Add(new MenuFlyoutItem { Text = "(nenhum arquivo recente)", IsEnabled = false });
                return;
            }

            foreach (var entry in entries)
            {
                var item = new MenuFlyoutItem { Text = entry.DisplayName };
                item.Click += async (s, e) => await OpenRecentFileAsync(entry);
                RecentFilesMenuItem.Items.Add(item);
            }
        }

        private async Task OpenRecentFileAsync(RecentFileEntry entry)
        {
            var file = await RecentFilesService.TryOpenAsync(entry);
            if (file is null)
            {
                await ShowInfoDialogAsync("Não foi possível abrir", $"\"{entry.DisplayName}\" não foi encontrado — pode ter sido movido ou excluído.");
                await RecentFilesService.RemoveAsync(entry);
                await RefreshRecentFilesMenuAsync();
                return;
            }

            var note = new Note();
            if (!await note.OpenAsync(file))
            {
                await ShowInfoDialogAsync("Não foi possível abrir o arquivo", note.LastError ?? "Erro desconhecido.");
                return;
            }

            AddNoteTab(note);
            await RefreshRecentFilesMenuAsync();
        }

        // ----- Tab management -----------------------------------------------------

        private void AddNoteTab(Note note, int? insertAtIndex = null)
        {
            var page = new NotePage(note);
            page.RequestClose += (s, e) => RemoveTab(note);

            var tabItem = new TabViewItem
            {
                Header = note.DisplayName,
                Content = page,
                Tag = note,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };

            note.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Note.DisplayName))
                {
                    tabItem.Header = note.DisplayName;
                }
                if (e.PropertyName == nameof(Note.File))
                {
                    if (!startupRestorationInProgress)
                    {
                        _ = RefreshSessionAsync();
                    }
                    _ = RefreshRecentFilesMenuAsync();
                }
            };

            if (insertAtIndex is int index && index >= 0 && index <= NotesTabView.TabItems.Count)
            {
                NotesTabView.TabItems.Insert(index, tabItem);
            }
            else
            {
                NotesTabView.TabItems.Add(tabItem);
            }

            tabThemeStates.Register(tabItem, _ => ApplyTabItemTheme(tabItem));
            NotesTabView.SelectedItem = tabItem;
            if (!startupRestorationInProgress)
            {
                _ = RefreshSessionAsync();
            }
        }

        private void ApplyTabItemTheme(TabViewItem tabItem)
        {
            var theme = ThemeService.Instance;
            tabItem.Resources["TabViewItemHeaderBackground"] = theme.Surface;
            tabItem.Resources["TabViewItemHeaderBackgroundSelected"] = theme.Background;
            tabItem.Resources["TabViewItemHeaderBackgroundPointerOver"] = theme.Background;
            tabItem.Resources["TabViewItemHeaderBackgroundSelectedPointerOver"] = theme.Background;
            tabItem.Resources["TabViewItemHeaderForeground"] = theme.Foreground;
            tabItem.Resources["TabViewItemHeaderForegroundSelected"] = theme.Foreground;
            tabItem.Resources["TabViewItemHeaderForegroundPointerOver"] = theme.Foreground;
            tabItem.Resources["TabViewItemHeaderForegroundSelectedPointerOver"] = theme.Foreground;
            tabItem.Background = theme.Surface;
            tabItem.Foreground = theme.Foreground;
            (tabItem.Content as NotePage)?.ApplyTheme();
            // Force the existing template's visual states to reevaluate even when both
            // palettes are Dark (or both are Light), where RequestedTheme alone is unchanged.
            tabItem.RequestedTheme = ElementTheme.Default;
            tabItem.RequestedTheme = theme.ElementTheme;
        }

        /// <summary>Removes a note's tab (if it has one) and clears its recovery snapshot — the one place all tab-closing paths should go through.</summary>
        private void RemoveTab(Note note)
        {
            var tabItem = NotesTabView.TabItems
                .OfType<TabViewItem>()
                .FirstOrDefault(t => ReferenceEquals(t.Tag, note));

            if (tabItem is not null)
            {
                tabThemeStates.Unregister(tabItem);
                NotesTabView.TabItems.Remove(tabItem);
            }

            _ = note.ClearRecoveryAsync();
            _ = RefreshSessionAsync();
        }

        private NotePage? GetActiveNotePage() => (NotesTabView.SelectedItem as TabViewItem)?.Content as NotePage;

        private List<Note> GetOpenNotes() => NotesTabView.TabItems.OfType<TabViewItem>()
            .Select(t => t.Tag as Note)
            .Where(n => n is not null)
            .Cast<Note>()
            .ToList();

        private async Task<bool> ConfirmCloseNoteAsync(Note note)
        {
            if (!note.IsDirty)
            {
                return true;
            }

            if (contentDialogOpen)
            {
                return false;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Title = "Alterações não salvas",
                Content = $"Deseja salvar as alterações em \"{note.DisplayName.TrimEnd('•', ' ')}\" antes de continuar?",
                PrimaryButtonText = "Salvar",
                SecondaryButtonText = "Não salvar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary
            };

            contentDialogOpen = true;
            ContentDialogResult result;
            try
            {
                result = await dialog.ShowAsync();
            }
            finally
            {
                contentDialogOpen = false;
            }

            if (result == ContentDialogResult.None)
            {
                return false;
            }

            if (result == ContentDialogResult.Primary)
            {
                var saved = await note.SaveAsync();
                if (!saved && note.LastError is string error)
                {
                    await ShowInfoDialogAsync("Não foi possível salvar", error);
                }
                return saved;
            }

            return true;
        }

        private async void MainAppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            if (closeConfirmed)
            {
                return;
            }

            args.Cancel = true;
            if (closePromptOpen)
            {
                return;
            }

            closePromptOpen = true;
            try
            {
                await ConfirmAndExitAsync();
            }
            finally
            {
                closePromptOpen = false;
            }
        }

        private async Task ConfirmAndExitAsync()
        {
            var notes = GetOpenNotes();

            foreach (var note in notes)
            {
                if (!await ConfirmCloseNoteAsync(note))
                {
                    return;
                }
            }

            foreach (var note in notes)
            {
                await note.ClearRecoveryAsync();
            }

            closeConfirmed = true;
            Application.Current.Exit();
        }

        private void NotesTabView_AddTabButtonClick(TabView sender, object args) => AddNoteTab(new Note());

        private async void NotesTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            var tabItem = args.Tab;

            if (tabItem.Tag is not Note note)
            {
                sender.TabItems.Remove(tabItem);
                return;
            }

            if (await ConfirmCloseNoteAsync(note))
            {
                RemoveTab(note);
            }
        }

        // ----- Menu: Arquivo --------------------------------------------------------

        private async void NovoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (NotesTabView.SelectedItem is not TabViewItem currentTab || currentTab.Tag is not Note currentNote)
            {
                return;
            }

            if (!await ConfirmCloseNoteAsync(currentNote))
            {
                return;
            }

            int index = NotesTabView.TabItems.IndexOf(currentTab);
            RemoveTab(currentNote);
            AddNoteTab(new Note(), index);
        }

        private void NovaAbaMenuItem_Click(object sender, RoutedEventArgs e) => AddNoteTab(new Note());

        private async void NovaJanelaMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    await ShowInfoDialogAsync("Nova janela", "O caminho do aplicativo não está disponível.");
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await ShowInfoDialogAsync("Não foi possível abrir uma nova janela", ex.Message);
            }
        }

        private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var note = new Note();
            if (!await note.OpenAsync(file))
            {
                await ShowInfoDialogAsync("Não foi possível abrir o arquivo", note.LastError ?? "Erro desconhecido.");
                return;
            }
            AddNoteTab(note);
            await RefreshRecentFilesMenuAsync();
        }

        private async void SalvarMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (GetActiveNotePage() is NotePage page)
            {
                await page.SaveAsync();
                await RefreshRecentFilesMenuAsync();
            }
        }

        private async void SalvarComoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (GetActiveNotePage() is NotePage page)
            {
                await page.SaveAsAsync();
                await RefreshRecentFilesMenuAsync();
            }
        }

        private async void ExcluirArquivoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (GetActiveNotePage() is NotePage page)
            {
                await page.DeleteFileAsync();
            }
        }

        private void ConfigurarMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var themeWindow = new ThemeWindow();
            themeWindow.Activate();
        }

        private async void SairMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await ConfirmAndExitAsync();
        }

        // ----- Menu: Editar ----------------------------------------------------------

        private void UndoMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.Undo();
        private void CutMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.Cut();
        private void CopyMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.Copy();
        private void PasteMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.Paste();
        private void DeleteSelectionMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.DeleteSelection();
        private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.SelectAllText();
        private void ZoomInMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.ZoomIn();
        private void ZoomOutMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.ZoomOut();
        private void ZoomResetMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.ResetZoom();

        private void FindMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.OpenFindBar(withReplace: false);

        private void FindReplaceMenuItem_Click(object sender, RoutedEventArgs e) => GetActiveNotePage()?.OpenFindBar(withReplace: true);

        // ----- Atalhos de teclado -------------------------------------------------------
        // Only for actions with no native default handler. Ctrl+Z/X/C/V/A already work as
        // soon as NoteEditor has focus (TextBox handles those on its own) — adding a global
        // accelerator for them too would be redundant and risks fighting with that default
        // behavior, so they're intentionally left out here.

        private void NovoAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            NovoMenuItem_Click(this, new RoutedEventArgs());
        }

        private void NovaAbaAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            NovaAbaMenuItem_Click(this, new RoutedEventArgs());
        }

        private void AbrirAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            OpenFileButton_Click(this, new RoutedEventArgs());
        }

        private void SalvarAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            if (closePromptOpen || contentDialogOpen)
            {
                return;
            }
            SalvarMenuItem_Click(this, new RoutedEventArgs());
        }

        private void SalvarComoAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            if (closePromptOpen || contentDialogOpen)
            {
                return;
            }
            SalvarComoMenuItem_Click(this, new RoutedEventArgs());
        }

        private async void FecharAbaAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            if (closePromptOpen || contentDialogOpen || NotesTabView.SelectedItem is not TabViewItem { Tag: Note note })
            {
                return;
            }

            if (await ConfirmCloseNoteAsync(note))
            {
                RemoveTab(note);
                if (NotesTabView.TabItems.Count == 0)
                {
                    AddNoteTab(new Note());
                }
            }
        }

        private void FindAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            FindMenuItem_Click(this, new RoutedEventArgs());
        }

        private void FindReplaceAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            FindReplaceMenuItem_Click(this, new RoutedEventArgs());
        }

        private void ImprimirAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ImprimirMenuItem_Click(this, new RoutedEventArgs());
        }

        private void ZoomInAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ZoomInMenuItem_Click(this, new RoutedEventArgs());
        }

        private void ZoomOutAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ZoomOutMenuItem_Click(this, new RoutedEventArgs());
        }

        private void ZoomResetAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ZoomResetMenuItem_Click(this, new RoutedEventArgs());
        }

        // ----- Imprimir ---------------------------------------------------------------

        private void RegisterForPrinting()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var printManager = PrintManagerInterop.GetForWindow(hwnd);
            printManager.PrintTaskRequested += PrintTask_Requested;

            printDocument = new PrintDocument();
            printDocumentSource = printDocument.DocumentSource;
            printDocument.Paginate += PrintDocument_Paginate;
            printDocument.GetPreviewPage += PrintDocument_GetPreviewPage;
            printDocument.AddPages += PrintDocument_AddPages;
        }

        private async void ImprimirMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var page = GetActiveNotePage();
            if (page is null)
            {
                return;
            }

            if (printDocument is null || !PrintManager.IsSupported())
            {
                await ShowInfoDialogAsync("Impressão", "A impressão não está disponível neste dispositivo.");
                return;
            }

            var settings = await ShowPrintSettingsDialogAsync();
            if (settings is null)
            {
                return; // Cancelar
            }

            currentPrintSettings = settings;
            printContentText = page.NoteText;
            printContentTitle = page.NoteTitle;

            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
            }
            catch
            {
                await ShowInfoDialogAsync("Impressão", "Não foi possível iniciar a impressão agora.");
            }
        }

        private async Task<PrintSettings?> ShowPrintSettingsDialogAsync()
        {
            var portraitRadio = new RadioButton { Content = "Retrato", GroupName = "Orientation", IsChecked = !currentPrintSettings.Landscape };
            var landscapeRadio = new RadioButton { Content = "Paisagem", GroupName = "Orientation", IsChecked = currentPrintSettings.Landscape };

            var orientationPanel = new StackPanel { Spacing = 4 };
            orientationPanel.Children.Add(new TextBlock { Text = "Orientação", Opacity = 0.7 });
            orientationPanel.Children.Add(portraitRadio);
            orientationPanel.Children.Add(landscapeRadio);

            var marginBox = new NumberBox
            {
                Header = "Margens (polegadas)",
                Value = currentPrintSettings.MarginInches,
                Minimum = 0,
                Maximum = 3,
                SmallChange = 0.25,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };

            var scaleSlider = new Slider
            {
                Header = "Escala (%)",
                Minimum = 25,
                Maximum = 200,
                StepFrequency = 5,
                Value = currentPrintSettings.ScalePercent
            };

            var content = new StackPanel { Spacing = 16, Width = 320 };
            content.Children.Add(orientationPanel);
            content.Children.Add(marginBox);
            content.Children.Add(scaleSlider);

            var dialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Title = "Configurações de impressão",
                Content = content,
                PrimaryButtonText = "Imprimir",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return null;
            }

            return new PrintSettings
            {
                Landscape = landscapeRadio.IsChecked == true,
                MarginInches = double.IsNaN(marginBox.Value) ? currentPrintSettings.MarginInches : marginBox.Value,
                ScalePercent = double.IsNaN(scaleSlider.Value) ? currentPrintSettings.ScalePercent : scaleSlider.Value
            };
        }

        private void PrintTask_Requested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            var printTask = args.Request.CreatePrintTask(
                string.IsNullOrEmpty(printContentTitle) ? "Slinky" : printContentTitle,
                PrintTaskSourceRequested);

            printTask.Completed += PrintTask_Completed;
        }

        private void PrintTaskSourceRequested(PrintTaskSourceRequestedArgs args) => args.SetSource(printDocumentSource);

        private void PrintTask_Completed(PrintTask sender, PrintTaskCompletedEventArgs args)
        {
            if (args.Completion == PrintTaskCompletion.Failed)
            {
                DispatcherQueue.TryEnqueue(() => _ = ShowInfoDialogAsync("Impressão", "A impressão falhou."));
            }
        }

        private void PrintDocument_Paginate(object sender, PaginateEventArgs e)
        {
            printPreviewPages.Clear();

            var options = (PrintTaskOptions)e.PrintTaskOptions;
            options.Orientation = currentPrintSettings.Landscape ? PrintOrientation.Landscape : PrintOrientation.Portrait;
            var pageDescription = options.GetPageDescription(0);

            double pageWidth = pageDescription.PageSize.Width;
            double pageHeight = pageDescription.PageSize.Height;
            var pages = PrintPagination.Paginate(
                printContentText,
                pageWidth,
                pageHeight,
                currentPrintSettings.MarginInches,
                currentPrintSettings.ScalePercent);

            foreach (var page in pages)
            {
                var block = new TextBlock
                {
                    Text = string.Join("\n", page.Lines),
                    FontSize = page.FontSize,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                    Width = page.ContentWidth,
                    Height = page.ContentHeight,
                    Foreground = new SolidColorBrush(Colors.Black),
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Top
                };

                var pageGrid = new Grid
                {
                    Width = pageWidth,
                    Height = pageHeight,
                    Background = new SolidColorBrush(Colors.White)
                };
                pageGrid.Padding = new Thickness(
                    Math.Max(0, currentPrintSettings.MarginInches) * 96.0);
                pageGrid.Children.Add(block);

                printPreviewPages.Add(pageGrid);
            }

            printDocument!.SetPreviewPageCount(printPreviewPages.Count, PreviewPageCountType.Intermediate);
        }

        private void PrintDocument_GetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            printDocument!.SetPreviewPage(e.PageNumber, printPreviewPages[e.PageNumber - 1]);
        }

        private void PrintDocument_AddPages(object sender, AddPagesEventArgs e)
        {
            foreach (var page in printPreviewPages)
            {
                printDocument!.AddPage(page);
            }
            printDocument!.AddPagesComplete();
        }

        // ----- Small helpers -----------------------------------------------------------

        private async Task ShowInfoDialogAsync(string title, string message)
        {
            if (contentDialogOpen)
            {
                return;
            }
            var dialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            };
            contentDialogOpen = true;
            try
            {
                await dialog.ShowAsync();
            }
            finally
            {
                contentDialogOpen = false;
            }
        }
    }
}
