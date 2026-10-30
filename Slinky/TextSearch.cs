using System;
using System.Collections.Generic;
using System.Text;

namespace Slinky.Services
{
    public static class TextSearch
    {
        /// <summary>Returns the start index of every non-overlapping match, in document order.</summary>
        public static List<int> FindAll(string text, string term, StringComparison comparison)
        {
            var positions = new List<int>();
            if (string.IsNullOrEmpty(term) || string.IsNullOrEmpty(text))
            {
                return positions;
            }

            int index = 0;
            while ((index = text.IndexOf(term, index, comparison)) != -1)
            {
                positions.Add(index);
                index += term.Length;
            }

            return positions;
        }

        public static int CountOccurrences(string text, string term, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(term))
            {
                return 0;
            }

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(term, index, comparison)) != -1)
            {
                count++;
                index += term.Length;
            }
            return count;
        }

        public static int FindNextIndex(string text, string term, int fromIndex, bool forward, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(term))
            {
                return -1;
            }

            if (forward)
            {
                int start = Math.Clamp(fromIndex, 0, text.Length);
                int index = text.IndexOf(term, start, comparison);
                return index >= 0 ? index : text.IndexOf(term, 0, comparison);
            }

            int backwardStart = Math.Clamp(fromIndex, -1, text.Length - 1);
            int previous = backwardStart >= 0 ? text.LastIndexOf(term, backwardStart, comparison) : -1;
            return previous >= 0 || text.Length == 0
                ? previous
                : text.LastIndexOf(term, text.Length - 1, comparison);
        }

        public static (string Text, int Count) ReplaceAll(string text, string term, string replacement, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(term))
            {
                return (text, 0);
            }

            var builder = new StringBuilder();
            int index = 0;
            int count = 0;

            while (true)
            {
                int found = text.IndexOf(term, index, comparison);
                if (found < 0)
                {
                    builder.Append(text, index, text.Length - index);
                    break;
                }

                builder.Append(text, index, found - index);
                builder.Append(replacement);
                index = found + term.Length;
                count++;
            }

            return count == 0 ? (text, 0) : (builder.ToString(), count);
        }
    }
}
