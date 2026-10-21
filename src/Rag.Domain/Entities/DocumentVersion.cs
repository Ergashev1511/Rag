using Rag.Domain.Common;
using Rag.Domain.Enums;

namespace Rag.Domain.Entities;

public sealed class DocumentVersion : BaseEntity
{
    private DocumentVersion() { }

    internal DocumentVersion(Guid documentId, int version, string fileName, string storageKey, string contentHash, long fileSize)
    {
        DocumentId = documentId;
        Version = version;
        FileName = fileName;
        StorageKey = storageKey;
        ContentHash = contentHash;
        FileSize = fileSize;
        Status = VersionStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid DocumentId { get; private init; }
    public int Version { get; private init; }
    public string FileName { get; private init; } = null!;
    public string StorageKey { get; private init; } = null!;
    public string ContentHash { get; private init; } = null!;
    public long FileSize { get; private init; }
    public int PageCount { get; private set; }
    public int ChunkCount { get; private set; }
    public VersionStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? IndexedAt { get; private set; }

    public void StartProcessing()
    {
        if (Status is not (VersionStatus.Pending or VersionStatus.Processing))
            throw new DomainException($"'{Status}' holatidagi versiyani qayta ishlab bo'lmaydi.");

        Status = VersionStatus.Processing;
        Attempts++;
        ErrorMessage = null;
    }

    public void MarkReady(int pageCount, int chunkCount)
    {
        if (Status != VersionStatus.Processing)
            throw new DomainException("Faqat 'Processing' holatidagi versiya 'Ready' bo'lishi mumkin.");

        Status = VersionStatus.Ready;
        PageCount = pageCount;
        ChunkCount = chunkCount;
        IndexedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Vaqtinchalik xato: versiya qayta navbatga qo'yiladi.</summary>
    public void ScheduleRetry(string error)
    {
        ErrorMessage = error;
        Status = VersionStatus.Pending;
    }

    public void MarkFailed(string error)
    {
        ErrorMessage = error;
        Status = VersionStatus.Failed;
    }

    public void Retry()
    {
        if (Status != VersionStatus.Failed)
            throw new DomainException("Faqat 'Failed' holatidagi versiyani qayta urinish mumkin.");

        Status = VersionStatus.Pending;
        Attempts = 0;
        ErrorMessage = null;
    }
}
