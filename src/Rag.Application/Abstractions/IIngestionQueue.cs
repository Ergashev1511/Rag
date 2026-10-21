namespace Rag.Application.Abstractions;

public interface IIngestionQueue
{
    ValueTask EnqueueAsync(Guid versionId, CancellationToken cancellationToken = default);
    void EnqueueDelayed(Guid versionId, TimeSpan delay);
}
