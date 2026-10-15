using Slinky.Services;
using Xunit;

namespace Slinky.Tests
{
    [Trait("Category", "Unit")]
    public class ThemeTabStateTests
    {
        public static IEnumerable<object[]> AllThemes() =>
            Enum.GetValues<AppTheme>().Select(theme => new object[] { theme });

        [Theory]
        [MemberData(nameof(AllThemes))]
        public void StateUsesTheSinglePaletteSource(AppTheme theme)
        {
            var state = ThemeTabState.For(theme);

            Assert.Equal(theme, state.Theme);
            Assert.Equal(ThemePalette.Background(theme), state.Background);
            Assert.Equal(ThemePalette.Foreground(theme), state.Foreground);
            Assert.Equal(ThemePalette.Surface(theme), state.Surface);
            Assert.Equal(ThemePalette.Accent(theme), state.Accent);
        }

        [Theory]
        [InlineData(AppTheme.LightDefault, false)]
        [InlineData(AppTheme.LightYellowish, false)]
        [InlineData(AppTheme.DarkDefault, true)]
        [InlineData(AppTheme.DarkBluish, true)]
        [InlineData(AppTheme.DarkDracula, true)]
        public void StateUsesTheCorrectSystemTheme(AppTheme theme, bool isDark)
        {
            Assert.Equal(isDark, ThemeTabState.For(theme).IsDark);
        }

        [Theory]
        [InlineData(AppTheme.LightDefault, AppTheme.LightYellowish)]
        [InlineData(AppTheme.LightYellowish, AppTheme.LightDefault)]
        [InlineData(AppTheme.DarkDefault, AppTheme.DarkBluish)]
        [InlineData(AppTheme.DarkBluish, AppTheme.DarkDracula)]
        [InlineData(AppTheme.DarkDracula, AppTheme.DarkDefault)]
        public void SameGroupTransitionProducesANewCompleteState(AppTheme from, AppTheme to)
        {
            var before = ThemeTabState.For(from);
            var after = ThemeTabState.For(to);

            Assert.NotEqual(before, after);
            Assert.Equal(to, after.Theme);
            Assert.Equal(ThemePalette.Background(to), after.Background);
            Assert.Equal(ThemePalette.Foreground(to), after.Foreground);
            Assert.Equal(ThemePalette.Surface(to), after.Surface);
            Assert.Equal(ThemePalette.Accent(to), after.Accent);
        }

        [Fact]
        public void ApplyingThemeUpdatesEveryRegisteredTabIncludingSameGroupChanges()
        {
            var hub = new ThemeTabStateHub(AppTheme.DarkDracula);
            var firstTab = new List<ThemeTabState>();
            var secondTab = new List<ThemeTabState>();

            hub.Register("first", firstTab.Add);
            hub.Register("second", secondTab.Add);
            hub.Apply(AppTheme.DarkBluish);

            Assert.Equal(2, firstTab.Count);
            Assert.Equal(2, secondTab.Count);
            Assert.Equal(AppTheme.DarkBluish, firstTab[^1].Theme);
            Assert.Equal(AppTheme.DarkBluish, secondTab[^1].Theme);
            Assert.Equal(firstTab[^1], secondTab[^1]);
        }

        [Fact]
        public void UnregisterStopsKeepingClosedTabAlive()
        {
            var hub = new ThemeTabStateHub(AppTheme.LightDefault);
            var tab = new object();
            var updates = 0;
            hub.Register(tab, _ => updates++);

            Assert.True(hub.Unregister(tab));
            hub.Apply(AppTheme.LightYellowish);

            Assert.Equal(1, updates);
            Assert.False(hub.Unregister(tab));
        }
    }
}
