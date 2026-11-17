using System.Collections.Generic;
using System.Text.Json;

namespace Slinky
{
    /// <summary>
    /// Helpers puros da fila entre processos de arquivos abertos externamente
    /// (single-instancing): a instância que sai faz merge do caminho na lista JSON
    /// guardada em LocalSettings; a instância principal faz parse e drena. JSON
    /// inválido/obsoleto é tratado como lista vazia, nunca quebra a abertura.
    /// </summary>
    public static class ExternalOpenQueuePolicy
    {
        public static string Merge(string? existingJson, string path)
        {
            var lista = TakeAll(existingJson);
            lista.Add(path);
            return JsonSerializer.Serialize(lista);
        }

        public static List<string> TakeAll(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }
    }
}
