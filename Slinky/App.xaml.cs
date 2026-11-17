// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using Slinky.Services;
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

            // Janela única para arquivos abertos pelo shell (duplo clique/"Abrir com"):
            // a primeira instância registra chave "principal-{pid}" e toda nova ativação
            // redireciona para a principal VIVA em vez de criar outra janela. "Nova
            // janela" (menu) lança o exe com --new-window e cria uma instância própria,
            // preservando o multi-janela intencional.
            //
            // argv: args.Arguments (WinUI) vem VAZIO em lançamentos por Process.Start
            // do exe empacotado — a fonte confiável de argv é GetCommandLineArgs.
            var argv = string.Join(' ', Environment.GetCommandLineArgs().Skip(1));
            var isNewWindow = LaunchArgs.HasNewWindowFlag(argv);

            if (!isNewWindow)
            {
                var alvo = EncontrarInstanciaPrincipal();
                if (alvo is not null)
                {
                    // Instância extra: encaminha o documento pela fila compartilhada (o
                    // Data do redirect não sobrevive a ativações por linha de comando),
                    // redireciona e sai sem criar janela. O redirect tem que COMPLETAR
                    // antes do exit ("must wait for the method to complete, otherwise
                    // the redirection will fail") — por isso roda em Task.Run com
                    // Semaphore, sem bloquear a STA diretamente (padrão da doc oficial
                    // de single-instancing).
                    if (ExtractActivatedPath(args, argv) is string caminho)
                    {
                        Slinky.Services.ExternalOpenQueue.Enqueue(caminho);
                    }

                    var redirectArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
                    var redirectConcluido = new System.Threading.Semaphore(0, 1);
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        alvo.RedirectActivationToAsync(redirectArgs).AsTask().Wait();
                        redirectConcluido.Release();
                    });
                    redirectConcluido.WaitOne();

                    Application.Current.Exit();
                    return;
                }
            }

            // Nenhuma principal viva (ou lançamento "Nova janela"): esta instância
            // assume a janela. Chave com PID — chaves órfãs de processos mortos nunca
            // colidem com a própria (FindOrRegisterForKey de chave fixa retornaria uma
            // instância morta e o app sairia sem abrir).
            var instancia = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey(
                isNewWindow ? $"janela-{Environment.ProcessId}" : $"principal-{Environment.ProcessId}");
            instancia.Activated += PrincipalInstance_Activated;

            Slinky.Services.ExternalOpenQueue.Clear();
            _window = new MainWindow();
            Window = _window;
            if (ExtractActivatedPath(args, argv) is string filePath)
            {
                ((MainWindow)_window).PendingLaunchPath = filePath;
            }
            _window.Activate();
        }

        /// <summary>
        /// Instância "principal-{pid}" viva para receber redirects — procurada por
        /// GetInstances (só instâncias usando a API) com verificação de vida do
        /// processo, imune a chaves órfãs de terminações abruptas (race documentada).
        /// </summary>
        private static Microsoft.Windows.AppLifecycle.AppInstance? EncontrarInstanciaPrincipal()
        {
            foreach (var inst in Microsoft.Windows.AppLifecycle.AppInstance.GetInstances())
            {
                if (inst.Key is not null
                    && inst.Key.StartsWith("principal-", StringComparison.Ordinal)
                    && inst.ProcessId != Environment.ProcessId
                    && ProcessoVivo((int)inst.ProcessId))
                {
                    return inst;
                }
            }

            return null;
        }

        private static bool ProcessoVivo(int pid)
        {
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById(pid);
                return !p.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Caminho do documento desta ativação: três canais de entrega.
        /// (1) Linha de comando: args.Arguments OU GetCommandLineArgs (a WinUI devolve
        /// Arguments vazio em lançamentos por Process.Start do exe empacotado).
        /// (2) Duplo-clique no Explorer: a ativação empacotada entrega o arquivo via
        /// FileActivatedEventArgs (AppLifecycle). Sem comparação de Kind: a projeção
        /// 19041 não traz ExtendedActivationKind, e o cast do Data já caracteriza a
        /// ativação por arquivo.
        /// </summary>
        private static string? ExtractActivatedPath(Microsoft.UI.Xaml.LaunchActivatedEventArgs args, string argv)
        {
            if (LaunchArgs.ExtractFilePath(args.Arguments, File.Exists) is string direto)
            {
                return direto;
            }

            if (LaunchArgs.ExtractFilePath(argv, File.Exists) is string doArgv)
            {
                return doArgv;
            }

            return Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs()?.Data
                is Windows.ApplicationModel.Activation.FileActivatedEventArgs fileArgs
                && LaunchArgs.ExtractFirstExisting(
                    fileArgs.Files.Select(f => f is Windows.Storage.StorageFile sf ? sf.Path : null),
                    File.Exists) is string activatedPath
                ? activatedPath
                : null;
        }

        /// <summary>
        /// Caminho do documento desta ativação: dois canais de entrega.
        /// (1) Lançamento explícito: caminho na linha de comando do processo.
        /// (2) Duplo-clique no Explorer: a ativação empacotada entrega o arquivo
        /// via FileActivatedEventArgs (AppLifecycle), não via linha de comando.
        /// Sem comparação de Kind: a projeção 19041 não traz ExtendedActivationKind,
        /// e o cast do Data já caracteriza a ativação por arquivo.
        /// </summary>
        private static string? ExtractActivatedPath(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            if (LaunchArgs.ExtractFilePath(args.Arguments, File.Exists) is string filePath)
            {
                return filePath;
            }

            return Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs()?.Data
                is Windows.ApplicationModel.Activation.FileActivatedEventArgs fileArgs
                && LaunchArgs.ExtractFirstExisting(
                    fileArgs.Files.Select(f => f is Windows.Storage.StorageFile sf ? sf.Path : null),
                    File.Exists) is string activatedPath
                ? activatedPath
                : null;
        }

        private void PrincipalInstance_Activated(object? sender, Microsoft.Windows.AppLifecycle.AppActivationArguments args)
        {
            // O redirect chega em thread de fundo; os caminhos foram enfileirados pela
            // instância que saiu (LocalSettings é compartilhado pelo pacote).
            _window?.DispatcherQueue.TryEnqueue(() =>
            {
                if (_window is not MainWindow mainWindow)
                {
                    return;
                }

                foreach (var caminho in Slinky.Services.ExternalOpenQueue.Drain())
                {
                    mainWindow.EnqueueOpenPath(caminho);
                }
            });
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
                // Rotacao simples: acima de 512 KB, a geracao anterior vai para
                // crash-prev.log e um novo arquivo comeca (evita crescimento sem fim).
                if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024)
                {
                    File.Copy(path, Path.Combine(folder.Path, "crash-prev.log"), true);
                    File.Delete(path);
                }
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
                    CloseButtonText = "OK",
                    RequestedTheme = ThemeService.Instance.ElementTheme
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
