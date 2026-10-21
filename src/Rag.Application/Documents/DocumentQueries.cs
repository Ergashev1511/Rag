using Microsoft.EntityFrameworkCore;
using Rag.Application.Abstractions;
using Rag.Application.Common.Exceptions;
using Rag.Domain.Entities;

namespace Rag.Application.Documents;

internal static class DocumentQueries
{
    public static async Task<Document> GetActiveWithVersionsAsync(this IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        await db.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, cancellationToken)
        ?? throw new NotFoundException("Hujjat topilmadi.");
}
