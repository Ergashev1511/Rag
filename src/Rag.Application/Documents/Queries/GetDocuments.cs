using Microsoft.EntityFrameworkCore;
using Rag.Application.Abstractions;

namespace Rag.Application.Documents.Queries;

public sealed class GetDocumentsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<DocumentSummaryDto>> HandleAsync(CancellationToken cancellationToken) =>
        await db.Documents
            .AsNoTracking()
            .Where(d => d.DeletedAt == null)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new DocumentSummaryDto(
                d.Id,
                d.Title,
                d.Description,
                d.CurrentVersion,
                d.Versions.OrderByDescending(v => v.Version).Select(v => (Domain.Enums.VersionStatus?)v.Status).FirstOrDefault(),
                d.UpdatedAt))
            .ToListAsync(cancellationToken);
}
