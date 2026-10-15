using Slinky.Services;
using Xunit;

namespace Slinky.Tests
{
    /// <summary>
    /// Headless end-to-end workflow for the theme boundary. The WinUI smoke run covers
    /// the actual window; this test covers the same user sequence without requiring a
    /// registered Windows App SDK runtime in CI.
    /// </summary>
    [Trait("Category", "EndToEnd")]
    public class ThemeSwitchingEndToEndTests
    {
        [Fact]
        public void OpenTabsFollowTheUsersCompleteThemeSelectionWorkflow()
        {
            var hub = new ThemeTabStateHub(AppTheme.DarkDracula);
            var tabStates = new[]
            {
                new List<ThemeTabState>(),
                new List<ThemeTabState>()
            };

            for (var index = 0; index < tabStates.Length; index++)
            {
                var states = tabStates[index];
                hub.Register(index, states.Add);
            }

            foreach (var selectedTheme in Enum.GetValues<AppTheme>())
            {
                hub.Apply(selectedTheme);
            }

            Assert.All(tabStates, states =>
            {
                Assert.Equal(Enum.GetValues<AppTheme>().Length + 1, states.Count);
                Assert.Equal(Enum.GetValues<AppTheme>(), states.Skip(1).Select(state => state.Theme));
                Assert.Equal(ThemeTabState.For(AppTheme.DarkDracula), states[0]);
                Assert.Equal(ThemeTabState.For(AppTheme.DarkDracula), states[^1]);
            });
        }
    }
}
