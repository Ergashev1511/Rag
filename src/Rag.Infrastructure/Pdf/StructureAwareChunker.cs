using Microsoft.Extensions.Options;
using Rag.Application.Abstractions;
using Rag.Application.Options;

namespace Rag.Infrastructure.Pdf;

/// <summary>
/// Sahifalar → normalizatsiya → kolontitul tozalash → sarlavha / matn / JSON bloklari → chunk lar.
/// Chunk sarlavha chegarasidan oshib o'tmaydi; har chunk sarlavhalar zanjirini (section_title) oladi.
/// </summary>
internal sealed class StructureAwareChunker(IOptions<RagOptions> options) : IChunker
{
    private const double HeadingSizeDelta = 1.0;
    private const int MaxHeadingLength = 150;

    private sealed record PageLine(int Page, PdfLine Line);

    public IReadOnlyList<TextChunk> Chunk(IReadOnlyList<PdfPage> pages)
    {
        var lines = HeaderFooterFilter.Apply(Normalize(pages))
            .SelectMany(page => page.Lines.Select(line => new PageLine(page.Number, line)))
            .ToList();
        if (lines.Count == 0)
            return [];

        var headings = new HeadingClassifier(lines.Select(l => l.Line).ToList());
        var texts = lines.Select(l => l.Line.Text).ToList();
        var builder = new SectionChunkBuilder(options.Value.Chunking);

        for (var i = 0; i < lines.Count; i++)
        {
            var (page, line) = lines[i];

            if (JsonBlockDetector.IsStart(line.Text) && JsonBlockDetector.FindEnd(texts, i) is { } end)
            {
                builder.AddJson(page, string.Join('\n', texts.GetRange(i, end - i + 1)));
                i = end;
            }
            else if (headings.GetLevel(line) is { } level)
            {
                builder.AddHeading(level, line.Text);
            }
            else
            {
                builder.AddLine(page, line);
            }
        }

        return builder.Complete();
    }

    private static List<PdfPage> Normalize(IReadOnlyList<PdfPage> pages) =>
        pages
            .Select(page => page with
            {
                Lines = page.Lines
                    .Select(line => line with { Text = TextNormalizer.NormalizeLine(line.Text) })
                    .Where(line => line.Text.Length > 0)
                    .ToList()
            })
            .ToList();

    /// <summary>Asosiy matndan katta shrift — sarlavha (o'lcham bo'yicha daraja); bir xil o'lchamdagi qalin qisqa qator — eng quyi daraja.</summary>
    private sealed class HeadingClassifier
    {
        private readonly double _bodySize;
        private readonly Dictionary<double, int> _levels;

        public HeadingClassifier(IReadOnlyList<PdfLine> lines)
        {
            _bodySize = lines
                .GroupBy(l => Round(l.FontSize))
                .MaxBy(g => g.Sum(l => l.Text.Length))!
                .Key;

            _levels = lines
                .Select(l => Round(l.FontSize))
                .Where(size => size >= _bodySize + HeadingSizeDelta)
                .Distinct()
                .OrderDescending()
                .Select((size, index) => (size, level: index + 1))
                .ToDictionary(x => x.size, x => x.level);
        }

        public int? GetLevel(PdfLine line)
        {
            if (line.Text.Length > MaxHeadingLength)
                return null;

            if (_levels.TryGetValue(Round(line.FontSize), out var level))
                return level;

            var looksLikeHeading = line.IsBold && line.StartsParagraph && !line.Text.EndsWith('.') && !line.Text.EndsWith(',');
            return looksLikeHeading ? _levels.Count + 1 : null;
        }

        private static double Round(double size) => Math.Round(size * 2, MidpointRounding.AwayFromZero) / 2;
    }
}
