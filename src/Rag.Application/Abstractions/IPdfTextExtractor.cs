namespace Rag.Application.Abstractions;

public sealed record PdfLine(string Text, double FontSize, bool IsBold, bool StartsParagraph);

public sealed record PdfPage(int Number, IReadOnlyList<PdfLine> Lines);

public interface IPdfTextExtractor
{
    IReadOnlyList<PdfPage> Extract(Stream pdf);
}
