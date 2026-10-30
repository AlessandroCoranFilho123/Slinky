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
    /// this singleton's brush properties via x:Bind (Mode=OneWay). Because ThemeService
    /// implements INotifyPropertyChanged, changing the active theme here updates
    /// every bound control immediately, without reloading any page or window.
    ///
    /// The actual RGB values live in ThemePalette.cs (a plain, WinRT-free file), while
    /// ThemeTabState keeps the complete palette transition testable without WinRT.
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

        private SolidColorBrush background = new(Colors.White);
        public SolidColorBrush Background
        {
            get => background;
            private set { background = value; OnPropertyChanged(); }
        }

        private SolidColorBrush foreground = new(Colors.Black);
        public SolidColorBrush Foreground
        {
            get => foreground;
            private set { foreground = value; OnPropertyChanged(); }
        }

        /// <summary>A slightly different tone used for chrome (menu bar, toolbars) vs. the writing surface.</summary>
        private SolidColorBrush surface = new(Color.FromArgb(255, 0xF3, 0xF3, 0xF3));
        public SolidColorBrush Surface
        {
            get => surface;
            private set { surface = value; OnPropertyChanged(); }
        }

        /// <summary>Used for selected controls and tab chrome.</summary>
        private SolidColorBrush accent = new(Color.FromArgb(255, 0x00, 0x67, 0xC0));
        public SolidColorBrush Accent
        {
            get => accent;
            private set { accent = value; OnPropertyChanged(); }
        }

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

        public void Apply(AppTheme theme)
        {
            if (initialized && Current == theme)
            {
                return;
            }

            var palette = ThemeTabState.For(theme);

            Background = new SolidColorBrush(ToColor(palette.Background));
            Foreground = new SolidColorBrush(ToColor(palette.Foreground));
            Surface = new SolidColorBrush(ToColor(palette.Surface));
            Accent = new SolidColorBrush(ToColor(palette.Accent));

            // Publish the enum only after all dependent brushes have their new values,
            // so one-way bindings never observe a mixed old/new palette.
            Current = palette.Theme;
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
