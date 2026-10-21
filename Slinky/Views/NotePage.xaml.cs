using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Slinky.Models;
using Slinky.Services;
using Slinky.Diagnostics;
using Windows.ApplicationModel.DataTransfer;

namespace Slinky.Views
{
    public sealed partial class NotePage : Page
    {
        private const double DefaultFontSize = 14;
        private const double MinFontSize = 10;
        private const double MaxFontSize = 40;

        private readonly Note noteModel;
        private bool suppressEditorChange;

        public ThemeService Theme => ThemeService.Instance;

        /// <summary>Raised after this note's file has been deleted, asking the parent to close this tab.</summary>
        public event EventHandler? RequestClose;

        /// <summary>Used by "Imprimir" in MainWindow.</summary>
        public string NoteText => noteModel.Text;
        public string NoteTitle => noteModel.File?.Name ?? "Sem título";

        public NotePage() : this(new Note()) { }

        public NotePage(Note note)
        {
            noteModel = note;
            this.InitializeComponent();

            // NoteEditor.Text is set here (not x:Bind) and kept in sync via NoteEditor_TextChanged
            // below — a two-way x:Bind on a live-typing TextBox can fight with the caret position
            // on every keystroke, which is exactly the "can only type on one line" symptom.
            suppressEditorChange = true;
            NoteEditor.Text = noteModel.Text;
            suppressEditorChange = false;
            ApplyTheme();
            EditorDiag.Attach(this, NoteEditor);

        }
        private Visibility PathRowVisibility(string? path) => string.IsNullOrEmpty(path) ? Visibility.Collapsed : Visibility.Visible;

        private void NoteEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressEditorChange)
            {
                return;
            }

            noteModel.Text = NoteEditor.Text;
        }

        /// <summary>
        /// Restores a visible focus indicator for keyboard (Tab) navigation only — the
        /// TextControlBorderBrushFocused override above intentionally silences it for clicks
        /// (see NotePageRoot_Tapped), but a Tab-only user still needs to see where focus is.
        /// </summary>
        private void NoteEditor_GotFocus(object sender, RoutedEventArgs e)
        {
            if (NoteEditor.FocusState == FocusState.Keyboard)
            {
                NoteEditor.BorderThickness = new Thickness(0, 0, 0, 2);
                NoteEditor.BorderBrush = Theme.Accent;
            }
        }

        private void NoteEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            NoteEditor.BorderThickness = new Thickness(0);
        }

        /// <summary>
        /// The caret being rendered is not proof that the TextBox owns keyboard focus. Request
        /// focus after the page enters the visual tree so a new empty note can be edited
        /// immediately, without a compensating click.
        /// </summary>
        private void NotePage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= NotePage_Loaded;
            if (FindBar.Visibility != Visibility.Visible)
            {
                NoteEditor.Focus(FocusState.Programmatic);
            }
        }

        // ----- Fallback focus for taps outside the editor -----------------

        /// <summary>
        /// The TextBox owns every tap inside its bounds. This is important both for existing
        /// newline-created lines and for the visual area below the only line, which is not a
        /// real document line. Only a tap outside the editor gets the page-level fallback.
        /// </summary>
        private void NotePageRoot_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            bool tappedInsideEditor = source is not null && IsDescendantOf(source, NoteEditor);
            bool tappedInsideFindBar = source is not null && IsDescendantOf(source, FindBar);
            var position = e.GetPosition(NoteEditor);
            bool withinEditorBounds = EditorTapRouting.IsWithinEditorBounds(
                position.X,
                position.Y,
                NoteEditor.ActualWidth,
                NoteEditor.ActualHeight);
            EditorDiag.Routing("enter", tappedInsideEditor, tappedInsideFindBar, withinEditorBounds, NoteEditor);

            if (EditorTapRouting.ShouldPreserveNativeHandling(tappedInsideEditor, tappedInsideFindBar))
            {
                EditorDiag.Routing("preserve-native", tappedInsideEditor, tappedInsideFindBar, withinEditorBounds, NoteEditor);
                return;
            }

            if (withinEditorBounds)
            {
                // The point is inside the editor surface but did not resolve to a text
                // descendant (for example, visual space below the last logical line). Keep
                // the existing valid caret and restore focus without inventing a line or
                // moving the caret to the end of the document.
                NoteEditor.Focus(FocusState.Pointer);
                EditorDiag.Routing("focus-without-selection-change", tappedInsideEditor, tappedInsideFindBar, withinEditorBounds, NoteEditor);
                return;
            }

            NoteEditor.Focus(FocusState.Pointer);
            int end = NoteEditor.Text.Length;
            NoteEditor.Select(end, 0);
            EditorDiag.Routing("fallback-select-end", tappedInsideEditor, tappedInsideFindBar, withinEditorBounds, NoteEditor);
        }

        /// <summary>
        /// Keeps the current valid caret when the click lands in visual space that is
        /// outside the collapsed native TextBox surface. It intentionally does not select
        /// or create a line; the document remains unchanged.
        /// </summary>
        public void FocusEditorWithoutMovingCaret()
        {
            NoteEditor.Focus(FocusState.Pointer);
        }

        /// <summary>
        /// Applies the complete palette to the editor and its native TextBox template.
        /// The template has separate PointerOver/Focused brushes, so setting only
        /// TextBox.Background would still allow a system-colored hover surface.
        /// </summary>
        public void ApplyTheme()
        {
            var background = Theme.Background;
            var foreground = Theme.Foreground;

            EditorSurface.Background = background;
            NoteEditor.Background = background;
            NoteEditor.Foreground = foreground;
            NoteEditor.Resources["TextControlBackground"] = background;
            NoteEditor.Resources["TextControlBackgroundPointerOver"] = background;
            NoteEditor.Resources["TextControlBackgroundFocused"] = background;
            NoteEditor.Resources["TextControlBackgroundDisabled"] = background;
            NoteEditor.Resources["TextControlForegroundPointerOver"] = foreground;
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

        // ----- Save / Delete (called from the "Arquivo" menu) ------------------------------

        public async Task SaveAsync()
        {
            if (!await noteModel.SaveAsync() && noteModel.LastError is string error)
            {
                await ShowErrorAsync("Não foi possível salvar", error);
            }
        }

        public async Task SaveAsAsync()
        {
            if (!await noteModel.SaveAsAsync() && noteModel.LastError is string error)
            {
                await ShowErrorAsync("Não foi possível salvar", error);
            }
        }

        public async Task DeleteFileAsync()
        {
            var file = noteModel.File;

            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Excluir arquivo",
                Content = file is null
                    ? "Esta nota ainda não foi salva em disco. Fechar mesmo assim?"
                    : $"Isso vai apagar \"{file.Name}\" do disco definitivamente. Continuar?",
                PrimaryButtonText = "Excluir",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            if (!await noteModel.DeleteAsync())
            {
                await ShowErrorAsync("Não foi possível excluir", noteModel.LastError ?? "Erro desconhecido.");
                return;
            }

            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private async Task ShowErrorAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }

        // ----- Edit menu operations (called from MainWindow's "Editar" menu) ------------------------------

        public void Undo()
        {
            if (NoteEditor.CanUndo)
            {
                NoteEditor.Undo();
            }
        }

        public void Copy()
        {
            if (string.IsNullOrEmpty(NoteEditor.SelectedText))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(NoteEditor.SelectedText);
            Clipboard.SetContent(dataPackage);
        }

        public void Cut()
        {
            if (string.IsNullOrEmpty(NoteEditor.SelectedText))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(NoteEditor.SelectedText);
            Clipboard.SetContent(dataPackage);
            NoteEditor.SelectedText = string.Empty;
        }

        public async void Paste()
        {
            var dataPackageView = Clipboard.GetContent();
            if (!dataPackageView.Contains(StandardDataFormats.Text))
            {
                return;
            }

            var text = await dataPackageView.GetTextAsync();
            NoteEditor.SelectedText = text;
        }

        public void DeleteSelection() => NoteEditor.SelectedText = string.Empty;

        public void SelectAllText() => NoteEditor.SelectAll();

        // ----- Zoom ------------------------------

        public void ZoomIn() => ApplyFontSize(NoteEditor.FontSize + 2);

        public void ZoomOut() => ApplyFontSize(NoteEditor.FontSize - 2);

        public void ResetZoom() => ApplyFontSize(DefaultFontSize);

        private void ApplyFontSize(double size)
        {
            size = Math.Clamp(size, MinFontSize, MaxFontSize);
            NoteEditor.FontSize = size;
        }

        // ----- Localizar / Localizar e substituir ------------------------------

        /// <summary>Opens the find bar (and the replace row, if requested).</summary>
        public void OpenFindBar(bool withReplace)
        {
            FindBar.Visibility = Visibility.Visible;
            ReplaceRow.Visibility = withReplace ? Visibility.Visible : Visibility.Collapsed;

            if (!string.IsNullOrEmpty(NoteEditor.SelectedText))
            {
                FindTextBox.Text = NoteEditor.SelectedText;
            }

            FindTextBox.Focus(FocusState.Programmatic);
            FindTextBox.SelectAll();
            UpdateFindStatus();
        }

        private void CloseFindBar_Click(object sender, RoutedEventArgs e) => CloseFindBar();

        private void CloseFindBar()
        {
            FindBar.Visibility = Visibility.Collapsed;
            NoteEditor.Focus(FocusState.Programmatic);
        }

        private void FindBar_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                FindNextFrom(NoteEditor.SelectionStart + NoteEditor.SelectionLength, forward: true);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                CloseFindBar();
                e.Handled = true;
            }
        }

        private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e) => FindNextFrom(0, forward: true, focusEditor: false);

        private void FindOption_Changed(object sender, RoutedEventArgs e) => FindNextFrom(0, forward: true, focusEditor: false);

        private void FindNext_Click(object sender, RoutedEventArgs e) =>
            FindNextFrom(NoteEditor.SelectionStart + NoteEditor.SelectionLength, forward: true);

        private void FindPrevious_Click(object sender, RoutedEventArgs e) =>
            FindNextFrom(NoteEditor.SelectionStart - 1, forward: false);

        private void FindNextFrom(int fromIndex, bool forward, bool focusEditor = true)
        {
            var term = FindTextBox.Text;
            if (string.IsNullOrEmpty(term))
            {
                FindStatusText.Text = string.Empty;
                return;
            }

            var comparison = MatchCaseCheckBox.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var text = NoteEditor.Text;
            int index;

            index = TextSearch.FindNextIndex(text, term, fromIndex, forward, comparison);

            if (index < 0)
            {
                FindStatusText.Text = "Não encontrado";
                return;
            }

            if (focusEditor)
            {
                NoteEditor.Focus(FocusState.Programmatic);
            }
            NoteEditor.Select(index, term.Length);
            UpdateFindStatus();
        }

        private void UpdateFindStatus()
        {
            var term = FindTextBox.Text;
            if (string.IsNullOrEmpty(term))
            {
                FindStatusText.Text = string.Empty;
                return;
            }

            var comparison = MatchCaseCheckBox.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int count = TextSearch.CountOccurrences(NoteEditor.Text, term, comparison);
            FindStatusText.Text = count == 0 ? "Não encontrado" : $"{count} ocorrência(s)";
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            var term = FindTextBox.Text;
            if (string.IsNullOrEmpty(term)) return;

            var comparison = MatchCaseCheckBox.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (NoteEditor.SelectionLength == term.Length &&
                string.Equals(NoteEditor.SelectedText, term, comparison))
            {
                int start = NoteEditor.SelectionStart;
                var replacement = ReplaceTextBox.Text;
                NoteEditor.SelectedText = replacement;
                FindNextFrom(start + replacement.Length, forward: true);
            }
            else
            {
                FindNextFrom(NoteEditor.SelectionStart, forward: true);
            }
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            var term = FindTextBox.Text;
            if (string.IsNullOrEmpty(term)) return;

            var comparison = MatchCaseCheckBox.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var replacement = ReplaceTextBox.Text;
            var original = NoteEditor.Text;

            var (result, count) = TextSearch.ReplaceAll(original, term, replacement, comparison);

            if (count > 0)
            {
                NoteEditor.Text = result; // NoteEditor_TextChanged mirrors this into noteModel.Text
            }

            FindStatusText.Text = count == 0 ? "Não encontrado" : $"{count} substituição(ões) feita(s)";
        }
    }
}
