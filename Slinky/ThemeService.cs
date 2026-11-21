using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.Storage;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Slinky.Services
{
    /// <summary>
    /// A single, app-wide theme source. Every open window/page binds its colors to
    /// this singleton's brush properties.
    ///
    /// The brushes are STABLE instances: Apply never replaces them, it only mutates
    /// their Color. That is what makes theme switching work everywhere — {ThemeResource}
    /// lookups inside control templates resolve a brush reference once (re-evaluation
    /// requires an ActualTheme change, which never happens between themes of the same
    /// Light/Dark group), and a Color mutation repaints every element holding that
    /// brush regardless of theme group or swap order. The actual RGB values live in
    /// ThemePalette.cs (a plain, WinRT-free file).
    /// </summary>
    public class ThemeService : INotifyPropertyChanged
    {
        public static ThemeService Instance { get; } = new ThemeService();

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? ThemeChanged;

        private AppTheme current = AppTheme.LightDefault;
        private bool initialized;
        public AppTheme Current
        {
            get => current;
            private set { current = value; OnPropertyChanged(); }
        }

        private readonly SolidColorBrush background = new(Colors.White);
        public SolidColorBrush Background => background;

        private readonly SolidColorBrush foreground = new(Colors.Black);
        public SolidColorBrush Foreground => foreground;

        /// <summary>A slightly different tone used for chrome (menu bar, toolbars) vs. the writing surface.</summary>
        private readonly SolidColorBrush surface = new(Color.FromArgb(255, 0xF3, 0xF3, 0xF3));
        public SolidColorBrush Surface => surface;

        /// <summary>Used for selected controls and tab chrome.</summary>
        private readonly SolidColorBrush accent = new(Color.FromArgb(255, 0x00, 0x67, 0xC0));
        public SolidColorBrush Accent => accent;

        /// <summary>Whether the *system* chrome (native checkmarks, dialogs, etc.) should run Light or Dark.</summary>
        public ElementTheme ElementTheme => ThemePalette.IsDark(Current) ? ElementTheme.Dark : ElementTheme.Light;

        private const string ThemeSettingsKey = "Theme";

        private ThemeService() => Apply(LoadSavedTheme());

        private static AppTheme LoadSavedTheme()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(ThemeSettingsKey, out var saved)
                && saved is string name
                && Enum.TryParse<AppTheme>(name, out var theme))
            {
                return theme;
            }
            return AppTheme.LightDefault;
        }

        /// <summary>
        /// Re-reads the theme persisted by any process of the same package and applies
        /// it when it changed. Called when the window becomes active — the moment a
        /// stale theme would actually be visible. LocalSettings is package-shared, so
        /// this is the whole cross-instance sync; no IPC is involved.
        /// </summary>
        public void RefreshFromStorage()
        {
            ApplicationData.Current.LocalSettings.Values.TryGetValue(ThemeSettingsKey, out var saved);
            if (ThemeSync.Resolve(saved as string, Current) is AppTheme theme)
            {
                Apply(theme);
            }
        }

        public void Apply(AppTheme theme)
        {
            if (initialized && Current == theme)
            {
                return;
            }

            // Recolor the stable brushes IN PLACE instead of replacing them. Replacing
            // them would orphan every {ThemeResource} reference templates already resolved.
            background.Color = ToColor(ThemePalette.Background(theme));
            foreground.Color = ToColor(ThemePalette.Foreground(theme));
            surface.Color = ToColor(ThemePalette.Surface(theme));
            accent.Color = ToColor(ThemePalette.Accent(theme));

            // Publish the enum only after the brushes carry the new palette.
            Current = theme;
            initialized = true;

            OnPropertyChanged(nameof(ElementTheme));
            ThemeChanged?.Invoke(this, EventArgs.Empty);

            ApplicationData.Current.LocalSettings.Values[ThemeSettingsKey] = theme.ToString();
        }

        private static Color ToColor((byte R, byte G, byte B) rgb) => Color.FromArgb(255, rgb.R, rgb.G, rgb.B);

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
