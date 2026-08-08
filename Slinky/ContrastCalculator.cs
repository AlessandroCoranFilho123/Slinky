using System;

namespace Slinky.Services
{
    /// <summary>
    /// WCAG 2.x relative luminance / contrast ratio math. Pure — takes raw RGB bytes in,
    /// returns a double out — with no WinRT/XAML dependency, so it can be linked directly
    /// into the unit test project and actually asserted on, instead of only checked by hand
    /// with a one-off script.
    /// </summary>
    public static class ContrastCalculator
    {
        public static double RelativeLuminance(byte r, byte g, byte b)
        {
            double rl = Linearize(r / 255.0);
            double gl = Linearize(g / 255.0);
            double bl = Linearize(b / 255.0);
            return 0.2126 * rl + 0.7152 * gl + 0.0722 * bl;
        }

        private static double Linearize(double channel) =>
            channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

        /// <summary>WCAG contrast ratio between two colors. 4.5:1 is the AA minimum for normal-size text.</summary>
        public static double ContrastRatio((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
        {
            double la = RelativeLuminance(a.R, a.G, a.B);
            double lb = RelativeLuminance(b.R, b.G, b.B);
            double lighter = Math.Max(la, lb);
            double darker = Math.Min(la, lb);
            return (lighter + 0.05) / (darker + 0.05);
        }
    }
}
