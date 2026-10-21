namespace Rag.Application.Abstractions;

public interface IEmbeddingService
{
    string ModelName { get; }
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
