using Rag.Application.Abstractions;
using Rag.Application.Common.Exceptions;

namespace Rag.Application.Documents.Queries;

public sealed class GetDocumentStatusHandler(IAppDbContext db)
{
    public async Task<DocumentStatusDto> HandleAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.GetActiveWithVersionsAsync(documentId, cancellationToken);
        var latest = document.LatestVersion ?? throw new NotFoundException("Hujjat versiyasi topilmadi.");

        return new DocumentStatusDto(
            document.Id, document.CurrentVersion, latest.Version, latest.Status, latest.Attempts, latest.ErrorMessage);
    }
}
