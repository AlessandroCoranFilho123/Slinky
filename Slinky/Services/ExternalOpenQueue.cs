using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.Storage;

namespace Slinky.Services
{
    /// <summary>
    /// Fila de caminhos entregues por instâncias que saíram após redirecionar
    /// (single-instancing): o Data do redirect nem sempre sobrevive a ativações por
    /// linha de comando, então os caminhos trafegam via LocalSettings — que é
    /// compartilhado por todos os processos do pacote — e a instância principal os
    /// consome no evento Activated do AppInstance.
    ///
    /// Enqueue/Drain são protegidos por mutex nomeado: dois duplo-cliques quase
    /// simultâneos são processos distintos e a leitura-merge-escrita no
    /// LocalSettings não é atômica sem isso.
    /// </summary>
    public static class ExternalOpenQueue
    {
        private const string LegacyKey = "PendingExternalOpens";
        private const string EntryKeyPrefix = "PendingExternalOpen.";
        private static readonly Mutex QueueMutex = new Mutex(false, "Local\\Slinky-ExternalOpenQueue");

        public static void Enqueue(string path)
        {
            ComLock(() =>
            {
                var settings = ApplicationData.Current.LocalSettings;
                // LocalSettings limita cada valor a cerca de 8 KB. Guardar toda a
                // fila em um JSON único perdia ativações quando muitos caminhos
                // longos chegavam de uma seleção múltipla; uma entrada por caminho
                // mantém cada valor pequeno e o mutex preserva a operação atômica.
                var ticks = DateTime.UtcNow.Ticks;
                var key = EntryKeyPrefix + ticks.ToString("D19");
                while (settings.Values.ContainsKey(key))
                {
                    ticks++;
                    key = EntryKeyPrefix + ticks.ToString("D19");
                }
                settings.Values[key] = path;
            });
        }

        public static IReadOnlyList<string> Drain()
        {
            IReadOnlyList<string> caminhos = new List<string>();
            ComLock(() =>
            {
                var settings = ApplicationData.Current.LocalSettings;
                var drained = ExternalOpenQueuePolicy.TakeAll(settings.Values[LegacyKey] as string);
                settings.Values.Remove(LegacyKey);

                var keys = settings.Values.Keys
                    .Where(key => key.StartsWith(EntryKeyPrefix, StringComparison.Ordinal))
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToList();
                foreach (var key in keys)
                {
                    if (settings.Values[key] is string path && !string.IsNullOrWhiteSpace(path))
                    {
                        drained.Add(path);
                    }
                    settings.Values.Remove(key);
                }
                caminhos = drained;
            });
            return caminhos;
        }

        /// <summary>
        /// Mutex nomeado por sessão. Um processo morto com o mutex preso gera
        /// AbandonedMutexException — MAS o handle já foi adquirido nesse caso, então a
        /// operação segue (os valores do LocalSettings são escritos atomicamente).
        /// </summary>
        private static void ComLock(Action acao)
        {
            bool adquirido = false;
            try
            {
                try
                {
                    QueueMutex.WaitOne();
                    adquirido = true;
                }
                catch (AbandonedMutexException)
                {
                    adquirido = true;
                }

                acao();
            }
            finally
            {
                if (adquirido)
                {
                    try
                    {
                        QueueMutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // thread não era a dona — nada a fazer
                    }
                }
            }
        }
    }
}
