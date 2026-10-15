using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace Slinky.Services
{
    internal static class WindowIconService
    {
        private const string IconRelativePath = @"Assets\AppIcon.ico";

        public static void Apply(Window window)
        {
            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, IconRelativePath);
                if (File.Exists(iconPath))
                {
                    window.AppWindow.SetIcon(iconPath);
                }
            }
            catch
            {
                // A missing icon must never block the window from opening.
            }
        }
    }
}
