using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Slinky.Services
{
    /// <summary>
    /// WinUI permite apenas um ContentDialog por XamlRoot. A guarda é por processo
    /// porque cada processo do Slinky possui uma única janela.
    /// </summary>
    internal static class DialogCoordinator
    {
        private static int open;

        public static bool IsOpen => Volatile.Read(ref open) != 0;

        public static async Task<ContentDialogResult?> ShowAsync(ContentDialog dialog, ElementTheme theme)
        {
            if (Interlocked.CompareExchange(ref open, 1, 0) != 0)
            {
                return null;
            }

            try
            {
                dialog.RequestedTheme = theme;
                return await dialog.ShowAsync();
            }
            finally
            {
                Volatile.Write(ref open, 0);
            }
        }
    }
}
