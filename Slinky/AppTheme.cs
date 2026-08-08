namespace Slinky.Services
{
    /// <summary>The five color modes offered in "Configurar". Deliberately its own file with
    /// zero WinRT/XAML dependency, so it (and anything that only needs the enum, like
    /// ThemePalette) can be linked into the unit test project without pulling in WinUI.</summary>
    public enum AppTheme
    {
        LightDefault,
        LightYellowish,
        DarkDefault,
        DarkBluish,
        DarkDracula
    }
}
