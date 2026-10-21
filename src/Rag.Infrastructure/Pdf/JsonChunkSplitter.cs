using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rag.Infrastructure.Pdf;

/// <summary>
/// Kichik JSON — bitta chunk. Katta JSON — yuqori daraja kalitlari / massiv elementlari bo'yicha bo'linadi,
/// har bo'lakka kontekst sarlavhasi qo'shiladi. Parse bo'lmasa — faqat balansli joyda kesiladi.
/// </summary>
internal static class JsonChunkSplitter
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static IReadOnlyList<string> Split(string raw, string? leadIn, string? sectionTitle, int maxSize)
    {
        var node = TryParse(raw);
        var json = node?.ToJsonString(Pretty) ?? raw;
        var single = leadIn is null ? json : $"{leadIn}\n{json}";
        if (single.Length <= maxSize)
            return [single];

        var parts = node switch
        {
            JsonObject obj => Group(obj.Select(p => $"  {JsonSerializer.Serialize(p.Key)}: {Serialize(p.Value)}"), maxSize)
                .Select(items => "{\n" + string.Join(",\n", items) + "\n}"),
            JsonArray array => Group(array.Select(e => "  " + Serialize(e)), maxSize)
                .Select(items => "[\n" + string.Join(",\n", items) + "\n]"),
            _ => SplitBalanced(raw, maxSize)
        };
        var list = parts.ToList();
        if (list.Count <= 1)
            return [single];

        var context = leadIn ?? sectionTitle ?? "JSON";
        return list.Select((part, i) => $"{context} ({i + 1}/{list.Count} qism)\n{part}").ToList();
    }

    private static JsonNode? TryParse(string raw)
    {
        try
        {
            return JsonNode.Parse(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Serialize(JsonNode? node) => node?.ToJsonString(Pretty) ?? "null";

    private static IEnumerable<List<string>> Group(IEnumerable<string> items, int maxSize)
    {
        var current = new List<string>();
        var length = 0;

        foreach (var item in items)
        {
            if (current.Count > 0 && length + item.Length > maxSize)
            {
                yield return current;
                current = [];
                length = 0;
            }

            current.Add(item);
            length += item.Length + 2;
        }

        if (current.Count > 0)
            yield return current;
    }

    // Faqat yuqori daraja (depth <= 1) va string tashqarisida kesiladi.
    private static IEnumerable<string> SplitBalanced(string raw, int maxSize)
    {
        var scanner = new BraceScanner();
        var current = new StringBuilder();

        foreach (var line in raw.Split('\n'))
        {
            if (current.Length > 0 && current.Length + line.Length > maxSize && scanner.Depth <= 1 && !scanner.InString)
            {
                yield return current.ToString();
                current.Clear();
            }

            scanner.Feed(line);
            if (current.Length > 0)
                current.Append('\n');
            current.Append(line);
        }

        if (current.Length > 0)
            yield return current.ToString();
    }
}
