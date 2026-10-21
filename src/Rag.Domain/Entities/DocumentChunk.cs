using Rag.Domain.Common;
using Rag.Domain.Enums;

namespace Rag.Domain.Entities;

public sealed class DocumentChunk : BaseEntity
{
    private DocumentChunk() { }

    public DocumentChunk(
        Guid documentId,
        int documentVersion,
        int chunkIndex,
        int pageNumber,
        string? sectionTitle,
        ChunkType chunkType,
        string content,
        float[] embedding,
        string embeddingModel)
    {
        DocumentId = documentId;
        DocumentVersion = documentVersion;
        ChunkIndex = chunkIndex;
        PageNumber = pageNumber;
        SectionTitle = sectionTitle;
        ChunkType = chunkType;
        Content = content;
        CharCount = content.Length;
        Embedding = embedding;
        EmbeddingModel = embeddingModel;
    }

    public Guid DocumentId { get; private init; }
    public int DocumentVersion { get; private init; }
    public int ChunkIndex { get; private init; }
    public int PageNumber { get; private init; }
    public string? SectionTitle { get; private init; }
    public ChunkType ChunkType { get; private init; }
    public string Content { get; private init; } = null!;
    public int CharCount { get; private init; }
    public float[] Embedding { get; private init; } = null!;
    public string EmbeddingModel { get; private init; } = null!;
}
