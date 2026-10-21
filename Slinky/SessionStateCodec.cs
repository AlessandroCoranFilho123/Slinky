using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Slinky;

public static class SessionStateCodec
{
    public static string Serialize(IEnumerable<string> tokens)
        => JsonSerializer.Serialize(tokens.Where(token => !string.IsNullOrWhiteSpace(token)).Distinct(StringComparer.Ordinal));

    public static bool TryDeserialize(string json, out List<string> tokens)
    {
        try
        {
            tokens = (JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return true;
        }
        catch (JsonException)
        {
            tokens = new List<string>();
            return false;
        }
    }
}
