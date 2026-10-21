using Rag.Domain.Common;

namespace Rag.Domain.Entities;

/// <summary>Javobda ishlatilgan chunk snapshot i. Chunk ga FK yo'q — tarix hujjat o'zgarsa ham buzilmaydi.</summary>
public sealed class MessageSource : BaseEntity
{
    private MessageSource() { }

    public MessageSource(Guid documentId, string fileName, int pageNumber, string? sectionTitle, string snippet, float score)
    {
        DocumentId = documentId;
        FileName = fileName;
        PageNumber = pageNumber;
        SectionTitle = sectionTitle;
        Snippet = snippet;
        Score = score;
    }

    public Guid MessageId { get; private init; }
    public Guid DocumentId { get; private init; }
    public string FileName { get; private init; } = null!;
    public int PageNumber { get; private init; }
    public string? SectionTitle { get; private init; }
    public string Snippet { get; private init; } = null!;
    public float Score { get; private init; }
}
