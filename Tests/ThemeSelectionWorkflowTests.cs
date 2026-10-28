using Slinky.Services;
using Xunit;

namespace Slinky.Tests
{
    /// <summary>
    /// Exercises theme selection against the headless tab-state model. This does not
    /// instantiate WinUI windows or controls; visual behavior belongs to the UI smoke run.
    /// </summary>
    [Trait("Category", "Unit")]
    public class ThemeSelectionWorkflowTests
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
