namespace Slinky.Services
{
    /// <summary>
    /// Raw RGB values for each theme. Kept separate from ThemeService (which depends on
    /// Microsoft.UI.Xaml.Media.SolidColorBrush and is therefore WinRT-only) specifically so
    /// this table — and the WCAG contrast it produces — can be covered by a plain unit test
    /// that runs without a Windows/WinUI runtime. ThemeService.Apply reads these same values
    /// to build its brushes, so there's a single source of truth, not a second copy to drift.
    /// </summary>
    public static class ThemePalette
    {
        public static (byte R, byte G, byte B) Background(AppTheme theme) => theme switch
        {
            AppTheme.LightDefault => (0xFF, 0xFF, 0xFF),
            AppTheme.LightYellowish => (0xF4, 0xEC, 0xD8),
            AppTheme.DarkDefault => (0x1E, 0x1E, 0x1E),
            AppTheme.DarkBluish => (0x10, 0x26, 0x3B),
            AppTheme.DarkDracula => (0x28, 0x2A, 0x36),
            _ => (0xFF, 0xFF, 0xFF)
        };

        public static (byte R, byte G, byte B) Foreground(AppTheme theme) => theme switch
        {
            AppTheme.LightDefault => (0x1A, 0x1A, 0x1A),
            AppTheme.LightYellowish => (0x4B, 0x36, 0x21),
            AppTheme.DarkDefault => (0xD4, 0xD4, 0xD4),
            AppTheme.DarkBluish => (0xDC, 0xE8, 0xF1),
            AppTheme.DarkDracula => (0xF8, 0xF8, 0xF2),
            _ => (0x00, 0x00, 0x00)
        };

        /// <summary>Slightly different tone used for chrome (menu bar, toolbars) vs. the writing surface.</summary>
        public static (byte R, byte G, byte B) Surface(AppTheme theme) => theme switch
        {
            AppTheme.LightDefault => (0xF3, 0xF3, 0xF3),
            AppTheme.LightYellowish => (0xEA, 0xDF, 0xC3),
            AppTheme.DarkDefault => (0x28, 0x28, 0x28),
            AppTheme.DarkBluish => (0x16, 0x30, 0x48),
            AppTheme.DarkDracula => (0x44, 0x47, 0x5A),
            _ => (0xF3, 0xF3, 0xF3)
        };

        /// <summary>Used for selected controls and tab chrome.</summary>
        public static (byte R, byte G, byte B) Accent(AppTheme theme) => theme switch
        {
            AppTheme.LightDefault => (0x00, 0x67, 0xC0),
            AppTheme.LightYellowish => (0x8A, 0x5A, 0x1E),
            AppTheme.DarkDefault => (0x4F, 0xC1, 0xFF),
            AppTheme.DarkBluish => (0x64, 0xD2, 0xFF),
            AppTheme.DarkDracula => (0xBD, 0x93, 0xF9),
            _ => (0x00, 0x67, 0xC0)
        };

        /// <summary>Single source of truth for which themes are dark; ThemeService and ThemeTabState both read this.</summary>
        public static bool IsDark(AppTheme theme) => theme is not (AppTheme.LightDefault or AppTheme.LightYellowish);
    }
}
