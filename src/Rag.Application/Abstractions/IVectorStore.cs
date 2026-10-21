using Rag.Domain.Enums;

namespace Rag.Application.Abstractions;

public sealed record SearchQuery(float[] Embedding, string Text, int TopK, double MinScore, bool Hybrid);

/// <summary>Score — cosine o'xshashlik (0..1). Faqat kalit so'z bo'yicha topilganda 0.</summary>
public sealed record SearchHit(
    Guid DocumentId,
    string DocumentTitle,
    string FileName,
    int PageNumber,
    string? SectionTitle,
    ChunkType ChunkType,
    string Content,
    float Score);

public interface IVectorStore
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken);
}
