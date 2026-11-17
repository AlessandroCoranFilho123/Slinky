using System.Collections.Generic;
using Windows.Storage;

namespace Slinky.Services
{
    /// <summary>
    /// Fila de caminhos entregues por instâncias que saíram após redirecionar
    /// (single-instancing): o Data do redirect nem sempre sobrevive a ativações por
    /// linha de comando, então os caminhos trafegam via LocalSettings — que é
    /// compartilhado por todos os processos do pacote — e a instância principal os
    /// consome no evento Activated do AppInstance.
    /// </summary>
    public static class ExternalOpenQueue
    {
        private const string Key = "PendingExternalOpens";

        public static void Enqueue(string path)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[Key] = ExternalOpenQueuePolicy.Merge(settings.Values[Key] as string, path);
        }

        public static IReadOnlyList<string> Drain()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var caminhos = ExternalOpenQueuePolicy.TakeAll(settings.Values[Key] as string);
            settings.Values.Remove(Key);
            return caminhos;
        }

        public static void Clear()
        {
            ApplicationData.Current.LocalSettings.Values.Remove(Key);
        }
    }
}
