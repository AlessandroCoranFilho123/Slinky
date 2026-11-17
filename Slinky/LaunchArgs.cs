using System;
using System.Collections.Generic;
using System.Linq;

namespace Slinky
{
    /// <summary>
    /// Extrai o caminho de documento dos argumentos de ativação (associação de
    /// arquivos / "Abrir com"). Função pura com fileExists injetado para cobrir o
    /// parsing headless; múltiplos caminhos entre aspas são aceitos e o primeiro
    /// que existir no disco vence.
    /// </summary>
    public static class LaunchArgs
    {
        public static string? ExtractFilePath(string? arguments, Func<string, bool> fileExists)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return null;
            }

            var candidates = arguments
                .Split('"')
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToList();

            if (candidates.Count == 0)
            {
                candidates.Add(arguments.Trim());
            }

            return ExtractFirstExisting(candidates, fileExists);
        }

        /// <summary>
        /// Caminhos vindos da ativação por arquivo (duplo-clique/"Abrir com"): o shell
        /// entrega-os via FileActivatedEventArgs, não na linha de comando. O primeiro
        /// que existir no disco vence, como no parsing de argumentos.
        /// </summary>
        public static string? ExtractFirstExisting(IEnumerable<string?> paths, Func<string, bool> fileExists)
        {
            return paths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim())
                .FirstOrDefault(fileExists);
        }

        /// <summary>
        /// "Nova janela" lança o exe com este flag para que a instância nova NÃO
        /// redirecione para a janela principal (multi-janela é intencional); ativações
        /// do shell sem o flag concentram-se na janela principal em abas.
        /// </summary>
        public static bool HasNewWindowFlag(string? arguments)
        {
            return arguments is not null
                && arguments.IndexOf("--new-window", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
