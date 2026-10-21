using Rag.Application.Abstractions;
using Rag.Application.Options;
using Rag.Domain.Enums;

namespace Rag.Infrastructure.Pdf;

/// <summary>Sarlavhalar zanjiri, paragraflar va JSON bloklarini kuzatib chunk lar ro'yxatini yig'adi.</summary>
internal sealed class SectionChunkBuilder(ChunkingOptions options)
{
    private const int MaxLeadInLength = 200;
    private const string SectionSeparator = " > ";

    private readonly List<(int Level, string Title)> _headings = [];
    private readonly List<Paragraph> _paragraphs = [];
    private readonly List<TextChunk> _chunks = [];
    private string _paragraph = string.Empty;
    private int _paragraphPage;
    private bool _lastWasHeading;

    private string? Section => _headings.Count == 0 ? null : string.Join(SectionSeparator, _headings.Select(h => h.Title));

    public void AddHeading(int level, string text)
    {
        FlushText();

        // Ko'p qatorli sarlavha: bir xil darajadagi ketma-ket qatorlar birlashtiriladi.
        if (_lastWasHeading && _headings.Count > 0 && _headings[^1].Level == level)
        {
            _headings[^1] = (level, $"{_headings[^1].Title} {text}");
            return;
        }

        _headings.RemoveAll(h => h.Level >= level);
        _headings.Add((level, text));
        _lastWasHeading = true;
    }

    public void AddLine(int page, PdfLine line)
    {
        if (line.StartsParagraph)
            EndParagraph();

        if (_paragraph.Length == 0)
            _paragraphPage = page;

        _paragraph = TextNormalizer.AppendLine(_paragraph, line.Text);
        _lastWasHeading = false;
    }

    public void AddJson(int page, string json)
    {
        EndParagraph();

        // JSON dan oldingi qisqa tushuntirish ("Request namunasi:") JSON bilan birga qoladi.
        string? leadIn = null;
        if (_paragraphs.Count > 0 && _paragraphs[^1].Text.Length <= MaxLeadInLength)
        {
            leadIn = _paragraphs[^1].Text;
            _paragraphs.RemoveAt(_paragraphs.Count - 1);
        }

        FlushText();

        foreach (var part in JsonChunkSplitter.Split(json, leadIn, Section, options.JsonMaxSize))
            _chunks.Add(new TextChunk(page, Section, ChunkType.Json, part));

        _lastWasHeading = false;
    }

    public IReadOnlyList<TextChunk> Complete()
    {
        FlushText();
        return _chunks;
    }

    private void EndParagraph()
    {
        if (_paragraph.Length == 0)
            return;

        _paragraphs.Add(new Paragraph(_paragraphPage, _paragraph));
        _paragraph = string.Empty;
    }

    private void FlushText()
    {
        EndParagraph();
        if (_paragraphs.Count == 0)
            return;

        foreach (var piece in TextSplitter.Split(_paragraphs, options))
            _chunks.Add(new TextChunk(piece.Page, Section, ChunkType.Text, piece.Text));

        _paragraphs.Clear();
    }
}
