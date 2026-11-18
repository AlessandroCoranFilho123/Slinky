using System;
using System.Collections.Generic;
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
        private const string Key = "PendingExternalOpens";
        private static readonly Mutex QueueMutex = new Mutex(false, "Local\\Slinky-ExternalOpenQueue");

        public static void Enqueue(string path)
        {
            ComLock(() =>
            {
                var settings = ApplicationData.Current.LocalSettings;
                settings.Values[Key] = ExternalOpenQueuePolicy.Merge(settings.Values[Key] as string, path);
            });
        }

        public static IReadOnlyList<string> Drain()
        {
            IReadOnlyList<string> caminhos = new List<string>();
            ComLock(() =>
            {
                var settings = ApplicationData.Current.LocalSettings;
                caminhos = ExternalOpenQueuePolicy.TakeAll(settings.Values[Key] as string);
                settings.Values.Remove(Key);
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
