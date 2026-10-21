namespace Rag.Application.Options;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public ChunkingOptions Chunking { get; init; } = new();
    public RetrievalOptions Retrieval { get; init; } = new();
    public IngestionOptions Ingestion { get; init; } = new();
}

public sealed class ChunkingOptions
{
    public int TextSize { get; init; } = 1000;
    public int Overlap { get; init; } = 150;
    public int MinLength { get; init; } = 50;
    public int JsonMaxSize { get; init; } = 3000;
}

public sealed class RetrievalOptions
{
    public int TopK { get; init; } = 8;
    public double MinScore { get; init; } = 0.35;
    public int HistoryMessages { get; init; } = 6;
    public bool Hybrid { get; init; }
}

public sealed class IngestionOptions
{
    public int EmbeddingBatchSize { get; init; } = 16;
    public int MaxRetries { get; init; } = 3;
    public int MaxFileSizeMb { get; init; } = 50;
}
