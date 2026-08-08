using Slinky.Services;
using Xunit;

namespace Slinky.Tests
{
    public class ContrastCalculatorTests
    {
        [Fact]
        public void BlackOnWhite_IsMaximumContrast()
        {
            var ratio = ContrastCalculator.ContrastRatio(((byte)0, (byte)0, (byte)0), ((byte)255, (byte)255, (byte)255));
            Assert.Equal(21.0, ratio, precision: 1);
        }

        [Fact]
        public void SameColor_HasContrastOfOne()
        {
            var color = ((byte)0x28, (byte)0x2A, (byte)0x36);
            var ratio = ContrastCalculator.ContrastRatio(color, color);
            Assert.Equal(1.0, ratio, precision: 3);
        }

        [Fact]
        public void ContrastRatio_IsSymmetric()
        {
            var a = ((byte)0x10, (byte)0x26, (byte)0x3B);
            var b = ((byte)0xDC, (byte)0xE8, (byte)0xF1);

            var ab = ContrastCalculator.ContrastRatio(a, b);
            var ba = ContrastCalculator.ContrastRatio(b, a);

            Assert.Equal(ab, ba, precision: 6);
        }

        [Theory]
        [InlineData(AppTheme.LightDefault)]
        [InlineData(AppTheme.LightYellowish)]
        [InlineData(AppTheme.DarkDefault)]
        [InlineData(AppTheme.DarkBluish)]
        [InlineData(AppTheme.DarkDracula)]
        public void EveryTheme_MeetsWcagAaForNormalText(AppTheme theme)
        {
            var background = ThemePalette.Background(theme);
            var foreground = ThemePalette.Foreground(theme);

            var ratio = ContrastCalculator.ContrastRatio(background, foreground);

            // WCAG 2.x AA minimum for normal-size text.
            Assert.True(ratio >= 4.5, $"{theme} only reaches {ratio:F2}:1 (needs >= 4.5:1)");
        }
    }
}
