namespace Rag.Infrastructure.Pdf;

/// <summary>JSON bloki '{' yoki '[' bilan boshlanib, qavslar balansi nolga tushguncha davom etadi.</summary>
internal static class JsonBlockDetector
{
    private const int MaxBlockLines = 1000;

    public static bool IsStart(string line)
    {
        if (line.StartsWith('{'))
            return true;
        if (!line.StartsWith('['))
            return false;

        // "[1] izoh" kabi matnni JSON massividan ajratish.
        var next = line.AsSpan(1).TrimStart();
        return next.IsEmpty || next[0] is '{' or '[' or '"' or ']';
    }

    /// <summary>Blokning oxirgi qatori indeksi; balans topilmasa null.</summary>
    public static int? FindEnd(IReadOnlyList<string> lines, int start)
    {
        var scanner = new BraceScanner();
        var last = Math.Min(lines.Count, start + MaxBlockLines);

        for (var i = start; i < last; i++)
        {
            scanner.Feed(lines[i]);
            if (scanner.Depth == 0)
                return i;
            if (scanner.Depth < 0)
                return null;
        }

        return null;
    }
}
