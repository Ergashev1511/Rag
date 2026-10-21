using Rag.Application.Abstractions;
using Rag.Application.Common.Exceptions;
using Rag.Domain.Enums;

namespace Rag.Application.Documents.Commands;

public sealed class RetryIndexingHandler(IAppDbContext db, IIngestionQueue queue)
{
    public async Task<VersionAcceptedDto> HandleAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.GetActiveWithVersionsAsync(documentId, cancellationToken);
        var version = document.LatestVersion;

        if (version?.Status != VersionStatus.Failed)
            throw new ConflictException("Qayta urinish faqat 'Failed' holatidagi oxirgi versiya uchun mumkin.");

        version.Retry();
        await db.SaveChangesAsync(cancellationToken);

        await queue.EnqueueAsync(version.Id, cancellationToken);
        return new VersionAcceptedDto(document.Id, version.Version, version.Status);
    }
}
