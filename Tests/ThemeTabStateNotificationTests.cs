using Slinky.Services;
using Xunit;

namespace Slinky.Tests
{
    [Trait("Category", "Unit")]
    public class ThemeTabStateNotificationTests
    {
        [Fact]
        public void ExistingTabsReceiveEveryPaletteTransition()
        {
            var hub = new ThemeTabStateHub(AppTheme.LightDefault);
            var received = new Dictionary<string, ThemeTabState[]>
            {
                ["first"] = Array.Empty<ThemeTabState>(),
                ["second"] = Array.Empty<ThemeTabState>(),
                ["third"] = Array.Empty<ThemeTabState>()
            };

            foreach (var key in received.Keys.ToArray())
            {
                received[key] = new[] { hub.Current };
                var capturedKey = key;
                hub.Register(capturedKey, state => received[capturedKey] = received[capturedKey].Append(state).ToArray());
            }

            var sequence = new[]
            {
                AppTheme.LightYellowish,
                AppTheme.DarkDefault,
                AppTheme.DarkDracula,
                AppTheme.DarkBluish,
                AppTheme.LightDefault
            };

            foreach (var theme in sequence)
            {
                hub.Apply(theme);
            }

            foreach (var states in received.Values)
            {
                Assert.Equal(sequence.Length + 2, states.Length);
                Assert.Equal(AppTheme.LightDefault, states[^1].Theme);
                Assert.Equal(sequence, states.Skip(2).Select(state => state.Theme));
            }
        }
    }
}
