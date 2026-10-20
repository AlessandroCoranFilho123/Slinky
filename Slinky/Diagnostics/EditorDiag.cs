using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Slinky.Views;
using Windows.Foundation;

namespace Slinky.Diagnostics
{
    /// <summary>
    /// Opt-in editor diagnostics. It records routed input, focus, hit testing and layout
    /// without handling or changing any event. Release builds are silent unless
    /// SLINKY_DEBUG=1 is explicitly set.
    /// </summary>
    internal static class EditorDiag
    {
        private static readonly object Sync = new();
        private static readonly bool Enabled = IsEnabled();
        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "slinky-debug.log");

        public static void Attach(NotePage page, TextBox editor)
        {
            if (!Enabled)
            {
                return;
            }

            page.Loaded += Page_Loaded;
            page.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(Page_PointerPressed), true);
            page.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(Page_PointerReleased), true);
            page.AddHandler(UIElement.TappedEvent, new TappedEventHandler(Page_Tapped), true);
            editor.GotFocus += Editor_GotFocus;
            editor.LostFocus += Editor_LostFocus;
            editor.KeyDown += Editor_KeyDown;
            editor.TextChanged += Editor_TextChanged;

            Log("EditorDiag", $"attach page={Describe(page)} editor={Describe(editor)}");
        }

        public static void Routing(string phase, bool tappedInsideEditor, bool tappedInsideFindBar, bool withinEditorBounds, TextBox editor)
        {
            if (Enabled)
            {
                Log("EditorTapRouting", $"{phase} insideEditor={tappedInsideEditor} insideFindBar={tappedInsideFindBar} withinBounds={withinEditorBounds} {Snapshot(editor)}");
            }
        }

        private static bool IsEnabled()
        {
#if DEBUG
            return true;
#else
            return string.Equals(Environment.GetEnvironmentVariable("SLINKY_DEBUG"), "1", StringComparison.Ordinal);
#endif
        }

        private static void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not NotePage page)
            {
                return;
            }

            Log("Layout", $"page loaded {Describe(page)}");
            LogVisualTree(page);
            LogFocus(page, "loaded");
        }

        private static void Page_PointerPressed(object sender, PointerRoutedEventArgs e) => LogPointer(sender, "PointerPressed", e);

        private static void Page_PointerReleased(object sender, PointerRoutedEventArgs e) => LogPointer(sender, "PointerReleased", e);

        private static void Page_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is not NotePage page)
            {
                return;
            }

            var editor = FindEditor(page);
            Log("Input", $"Tapped handled={e.Handled} original={Describe(e.OriginalSource as DependencyObject)}");
            LogHitTest(page, e.GetPosition(page));
            LogFocus(page, "after Tapped");
            if (editor is not null)
            {
                Log("Editor", Snapshot(editor));
            }
        }

        private static void Editor_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox editor)
            {
                Log("Focus", $"GotFocus original={Describe(e.OriginalSource as DependencyObject)} focusState={editor.FocusState} {Snapshot(editor)}");
            }
        }

        private static void Editor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox editor)
            {
                Log("Focus", $"LostFocus original={Describe(e.OriginalSource as DependencyObject)} focusState={editor.FocusState} {Snapshot(editor)}");
            }
        }

        private static void Editor_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (sender is TextBox editor)
            {
                Log("Keyboard", $"KeyDown key={e.Key} handled={e.Handled} {Snapshot(editor)}");
            }
        }

        private static void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox editor)
            {
                Log("Keyboard", $"TextChanged {Snapshot(editor)}");
            }
        }

        private static void LogPointer(object sender, string eventName, PointerRoutedEventArgs e)
        {
            if (sender is not NotePage page)
            {
                return;
            }

            var point = e.GetCurrentPoint(page).Position;
            Log("Input", $"{eventName} point=({point.X:0.##},{point.Y:0.##}) handled={e.Handled} original={Describe(e.OriginalSource as DependencyObject)}");
            LogHitTest(page, point);
            LogFocus(page, $"during {eventName}");

            var editor = FindEditor(page);
            if (editor is not null)
            {
                Log("Editor", Snapshot(editor));
            }
        }

        private static TextBox? FindEditor(NotePage page)
        {
            return FindDescendants(page).OfType<TextBox>().FirstOrDefault();
        }

        private static void LogHitTest(NotePage page, Point point)
        {
            try
            {
                var root = page.XamlRoot?.Content as UIElement ?? page;
                var rootPoint = page.TransformToVisual(root).TransformPoint(point);
                var hit = VisualTreeHelper.FindElementsInHostCoordinates(rootPoint, root)
                    .Select(Describe)
                    .ToArray();
                Log("HitTest", $"point=({rootPoint.X:0.##},{rootPoint.Y:0.##}) elements=[{string.Join(" > ", hit)}]");
            }
            catch (Exception ex)
            {
                Log("HitTest", $"failed {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void LogFocus(NotePage page, string phase)
        {
            try
            {
                var focused = page.XamlRoot is null ? null : FocusManager.GetFocusedElement(page.XamlRoot);
                Log("Focus", $"{phase} focused={Describe(focused as DependencyObject)}");
            }
            catch (Exception ex)
            {
                Log("Focus", $"{phase} failed {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void LogVisualTree(NotePage page)
        {
            try
            {
                var root = page.XamlRoot?.Content as UIElement;
                if (root is not null)
                {
                    Log("Layout", "ancestor chain: " + string.Join(" <- ", Ancestors(page).Select(Describe)));
                    Walk(root, 0);
                }
                else
                {
                    Walk(page, 0);
                }
            }
            catch (Exception ex)
            {
                Log("Layout", $"visual tree failed {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void Walk(DependencyObject element, int depth)
        {
            if (depth > 12)
            {
                return;
            }

            Log("Layout", new string(' ', depth * 2) + Describe(element));
            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                Walk(VisualTreeHelper.GetChild(element, i), depth + 1);
            }
        }

        private static IEnumerable<DependencyObject> Ancestors(DependencyObject element)
        {
            var current = element;
            while (current is not null)
            {
                yield return current;
                current = VisualTreeHelper.GetParent(current);
            }
        }

        private static IEnumerable<DependencyObject> FindDescendants(DependencyObject root)
        {
            yield return root;
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                foreach (var child in FindDescendants(VisualTreeHelper.GetChild(root, i)))
                {
                    yield return child;
                }
            }
        }

        private static string Snapshot(TextBox editor)
        {
            string text = editor.Text ?? string.Empty;
            string tail = text.Length <= 24 ? text : text[^24..];
            return $"instance={RuntimeHelpers.GetHashCode(editor)} enabled={editor.IsEnabled} readOnly={editor.IsReadOnly} textLength={text.Length} tail={tail.Replace("\r", "\\r").Replace("\n", "\\n")} selection=({editor.SelectionStart},{editor.SelectionLength}) focusState={editor.FocusState}";
        }

        private static string Describe(DependencyObject? element)
        {
            if (element is null)
            {
                return "<null>";
            }

            string name = element is FrameworkElement frameworkElement && !string.IsNullOrEmpty(frameworkElement.Name)
                ? $" name={frameworkElement.Name}"
                : string.Empty;
            string size = element is FrameworkElement sized
                ? $" size=({sized.ActualWidth:0.##}x{sized.ActualHeight:0.##})"
                : string.Empty;
            string background = element switch
            {
                Control control => $" background={BrushDescription(control.Background)}",
                Panel panel => $" background={BrushDescription(panel.Background)}",
                Border border => $" background={BrushDescription(border.Background)}",
                _ => string.Empty
            };

            string bounds = string.Empty;
            if (element is FrameworkElement positioned && positioned.XamlRoot?.Content is UIElement root)
            {
                try
                {
                    var rect = positioned.TransformToVisual(root).TransformBounds(new Rect(0, 0, positioned.ActualWidth, positioned.ActualHeight));
                    bounds = $" bounds=({rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##})";
                }
                catch
                {
                    bounds = " bounds=<unavailable>";
                }
            }

            return $"{element.GetType().Name}{name}{size}{background}{bounds} id={RuntimeHelpers.GetHashCode(element)}";
        }

        private static string BrushDescription(Brush? brush)
        {
            if (brush is null)
            {
                return "<null>";
            }

            return brush is SolidColorBrush solid
                ? solid.Color.ToString()
                : brush.GetType().Name;
        }

        private static void Log(string component, string message)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                string line = $"{DateTimeOffset.UtcNow:O} [{component}] {message}{Environment.NewLine}";
                lock (Sync)
                {
                    File.AppendAllText(LogPath, line);
                }
            }
            catch
            {
                // Diagnostics must never affect editor behavior.
            }
        }
    }
}
