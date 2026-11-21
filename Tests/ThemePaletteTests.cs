using Slinky.Services;
using Xunit;

namespace Slinky.Tests
{
    /// <summary>
    /// ThemePalette.IsDark is the single source of truth for the Light/Dark split —
    /// it decides root RequestedTheme, title bar tints and dialog theming. Palette
    /// contrast itself is covered by ContrastCalculatorTests.
    /// </summary>
    public class ThemePaletteTests
    {
        [Theory]
        [InlineData(AppTheme.LightDefault, false)]
        [InlineData(AppTheme.LightYellowish, false)]
        [InlineData(AppTheme.DarkDefault, true)]
        [InlineData(AppTheme.DarkBluish, true)]
        [InlineData(AppTheme.DarkDracula, true)]
        public void IsDark_ClassifiesByLightDarkGroup(AppTheme theme, bool expected)
            => Assert.Equal(expected, ThemePalette.IsDark(theme));
    }
}
