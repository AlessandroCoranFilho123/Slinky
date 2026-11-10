using Slinky.Services;
using Xunit;

namespace Slinky.Tests
{
    public class ThemeSyncTests
    {
        [Fact]
        public void Resolve_ReturnsNullWhenStorageNameIsMissingOrInvalid()
        {
            Assert.Null(ThemeSync.Resolve(null, AppTheme.LightDefault));
            Assert.Null(ThemeSync.Resolve("", AppTheme.LightDefault));
            Assert.Null(ThemeSync.Resolve("NoiteInexistente", AppTheme.LightDefault));
        }

        [Fact]
        public void Resolve_ReturnsNullWhenStoredThemeEqualsCurrent()
        {
            Assert.Null(ThemeSync.Resolve(AppTheme.LightDefault.ToString(), AppTheme.LightDefault));
        }

        [Fact]
        public void Resolve_ReturnsStoredThemeWhenItDiffersFromCurrent()
        {
            foreach (AppTheme theme in System.Enum.GetValues(typeof(AppTheme)))
            {
                if (theme == AppTheme.LightDefault)
                {
                    continue;
                }

                Assert.Equal(theme, ThemeSync.Resolve(theme.ToString(), AppTheme.LightDefault));
            }
        }
    }
}
