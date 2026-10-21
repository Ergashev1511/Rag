using System.Text;
using System.Text.RegularExpressions;
using Rag.Application.Options;

namespace Rag.Infrastructure.Pdf;

internal sealed record Paragraph(int Page, string Text);

/// <summary>Bo'lim matnini ~TextSize li chunk larga bo'ladi: paragraf → gap → so'z, oxirgi gaplar overlap sifatida.</summary>
internal static partial class TextSplitter
{
    public static IReadOnlyList<Paragraph> Split(IReadOnlyList<Paragraph> paragraphs, ChunkingOptions options)
    {
        var chunks = new List<Paragraph>();
        var current = new StringBuilder();
        var page = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphStart = true;
            foreach (var unit in SplitToFit(paragraph.Text, options.TextSize))
            {
                if (current.Length > 0 && current.Length + unit.Length + 1 > options.TextSize)
                {
                    var text = current.ToString();
                    chunks.Add(new Paragraph(page, text));
                    current.Clear().Append(Overlap(text, options.Overlap));
                    page = paragraph.Page;
                }

                if (current.Length == 0)
                    page = paragraph.Page;
                else
                    current.Append(paragraphStart ? '\n' : ' ');

                current.Append(unit);
                paragraphStart = false;
            }
        }

        if (current.Length > 0)
            chunks.Add(new Paragraph(page, current.ToString()));

        MergeShortTail(chunks, options.MinLength);
        return chunks;
    }

    private static IEnumerable<string> SplitToFit(string text, int maxSize)
    {
        if (text.Length <= maxSize)
        {
            yield return text;
            yield break;
        }

        foreach (var sentence in SentenceBoundary().Split(text))
        {
            if (sentence.Length <= maxSize)
            {
                yield return sentence;
                continue;
            }

            foreach (var piece in SplitByWords(sentence, maxSize))
                yield return piece;
        }
    }

    private static IEnumerable<string> SplitByWords(string text, int maxSize)
    {
        var current = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + word.Length + 1 > maxSize)
            {
                yield return current.ToString();
                current.Clear();
            }

            if (current.Length > 0)
                current.Append(' ');
            current.Append(word);
        }

        if (current.Length > 0)
            yield return current.ToString();
    }

    // Oxirgi 1-2 gap (Overlap belgidan oshmasa).
    private static string Overlap(string text, int maxLength)
    {
        var sentences = SentenceBoundary().Split(text);
        var taken = new List<string>();
        var length = 0;

        for (var i = sentences.Length - 1; i >= 0; i--)
        {
            if (length + sentences[i].Length > maxLength)
                break;
            taken.Insert(0, sentences[i]);
            length += sentences[i].Length + 1;
        }

        return string.Join(' ', taken);
    }

    private static void MergeShortTail(List<Paragraph> chunks, int minLength)
    {
        if (chunks.Count < 2 || chunks[^1].Text.Length >= minLength)
            return;

        chunks[^2] = chunks[^2] with { Text = chunks[^2].Text + "\n" + chunks[^1].Text };
        chunks.RemoveAt(chunks.Count - 1);
    }

    [GeneratedRegex(@"(?<=[.!?…;])\s+")]
    private static partial Regex SentenceBoundary();
}
