// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using Windows.Storage;

namespace Slinky
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    
    public partial class App : Application
    {

        private Window? _window;
        private bool unhandledExceptionReported;

        /// <summary>
        /// Lets other classes (like Note, when it needs to show an "Abrir"/"Salvar como"
        /// file picker) reach the app's single Window. Desktop file pickers need a window
        /// handle so the system knows which window to appear over.
        /// </summary>
        public static Window? Window { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            this.UnhandledException += App_UnhandledException;

            _window = new MainWindow();
            Window = _window;
            _window.Activate();
        }

        /// <summary>
        /// Last-resort safety net: logs whatever wasn't caught closer to where it happened,
        /// then keeps the app alive instead of letting it crash outright. This is not a
        /// substitute for handling specific, expected failures near their source (see
        /// Note.cs) — it only exists so an unanticipated bug doesn't also take down every
        /// other open tab with it.
        /// </summary>
        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var path = Path.Combine(folder.Path, "crash.log");
                File.AppendAllText(path, $"{DateTime.Now:u} — {e.Exception}\n\n");
            }
            catch
            {
                // Logging must never itself throw from inside an exception handler.
            }

            e.Handled = true;

            if (unhandledExceptionReported || _window?.Content is not FrameworkElement root)
            {
                return;
            }

            unhandledExceptionReported = true;
            _window.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var dialog = new ContentDialog
                    {
                        XamlRoot = root.XamlRoot,
                        Title = "Ocorreu um erro inesperado",
                        Content = "O erro foi registrado em crash.log. Algumas operações podem não estar disponíveis; salve seu trabalho e reinicie o Slinky.",
                        CloseButtonText = "OK"
                    };
                    await dialog.ShowAsync();
                }
                catch
                {
                    // The UI may already be inconsistent; logging above is the fallback.
                }
            });
        }
    }
}
