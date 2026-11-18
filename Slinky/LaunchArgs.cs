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

            return ExtractAllExisting(candidates, fileExists).FirstOrDefault();
        }

        /// <summary>
        /// Caminhos vindos da ativação por arquivo (duplo-clique/"Abrir com"): o shell
        /// entrega-os via FileActivatedEventArgs, não na linha de comando. Devolve TODOS
        /// os que existirem no disco, na ordem de chegada (seleção múltipla no Explorer).
        /// </summary>
        public static List<string> ExtractAllExisting(IEnumerable<string?> paths, Func<string, bool> fileExists)
        {
            return paths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim())
                .Where(fileExists)
                .ToList();
        }

        /// <summary>
        /// "Nova janela" lança o exe com este flag para que a instância nova NÃO
        /// redirecione para a janela principal (multi-janela é intencional); ativações
        /// do shell sem o flag concentram-se na janela principal em abas.
        /// Comparação por TOKEN EXATO: um caminho de arquivo que contenha
        /// "--new-window" no nome não pode disparar o flag.
        /// </summary>
        public static bool HasNewWindowFlag(string? arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return false;
            }

            return HasNewWindowFlag(arguments.Split(' '));
        }

        public static bool HasNewWindowFlag(IEnumerable<string?> tokens)
        {
            return tokens.Any(t => string.Equals(t, "--new-window", StringComparison.OrdinalIgnoreCase));
        }
    }
}
