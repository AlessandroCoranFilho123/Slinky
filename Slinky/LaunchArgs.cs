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

            foreach (var candidate in candidates)
            {
                if (fileExists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
