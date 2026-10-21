using Rag.Domain.Enums;

namespace Rag.Application.Abstractions;

public sealed record TextChunk(int PageNumber, string? SectionTitle, ChunkType Type, string Content);

public interface IChunker
{
    IReadOnlyList<TextChunk> Chunk(IReadOnlyList<PdfPage> pages);
}
