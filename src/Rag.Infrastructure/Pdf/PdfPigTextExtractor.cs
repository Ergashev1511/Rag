using Rag.Application.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rag.Infrastructure.Pdf;

/// <summary>Sahifa so'zlarini qatorlarga yig'adi, har qator uchun shrift o'lchami, qalinligi va paragraf boshi aniqlanadi.</summary>
internal sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    private const double SameRowTolerance = 0.5;
    private const double ParagraphGapFactor = 0.8;

    public IReadOnlyList<PdfPage> Extract(Stream pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.GetPages().Select(page => new PdfPage(page.Number, ReadLines(page))).ToList();
    }

    private static List<PdfLine> ReadLines(Page page)
    {
        var rows = GroupIntoRows(page.GetWords());
        var lines = new List<PdfLine>(rows.Count);
        double? previousBottom = null;

        foreach (var row in rows)
        {
            var letters = row.SelectMany(w => w.Letters).ToList();
            var top = row.Max(w => w.BoundingBox.Top);
            var bottom = row.Min(w => w.BoundingBox.Bottom);
            var startsParagraph = previousBottom is null || previousBottom.Value - top > (top - bottom) * ParagraphGapFactor;

            lines.Add(new PdfLine(
                string.Join(' ', row.Select(w => w.Text)),
                letters.Count == 0 ? 0 : letters.Average(l => l.PointSize),
                letters.Count(IsBold) * 2 > letters.Count,
                startsParagraph));

            previousBottom = bottom;
        }

        return lines;
    }

    private static List<List<Word>> GroupIntoRows(IEnumerable<Word> words)
    {
        var rows = new List<List<Word>>();
        foreach (var word in words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).OrderByDescending(w => w.BoundingBox.Bottom))
        {
            var tolerance = Math.Max(word.BoundingBox.Height, 1) * SameRowTolerance;
            if (rows.Count > 0 && Math.Abs(rows[^1][0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= tolerance)
                rows[^1].Add(word);
            else
                rows.Add([word]);
        }

        foreach (var row in rows)
            row.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));

        return rows;
    }

    private static bool IsBold(Letter letter) =>
        letter.Font?.IsBold == true || letter.Font?.Name?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true;
}
