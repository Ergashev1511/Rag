using Microsoft.EntityFrameworkCore;
using Rag.Application.Abstractions;

namespace Rag.Application.Documents.Commands;

/// <summary>
/// Embedding modeli yoki chunking o'zgarganda: har faol hujjat uchun joriy fayldan yangi versiya yaratiladi.
/// Yangi versiya Ready bo'lguncha eski versiya qidiruvda ishlayveradi.
/// </summary>
public sealed class ReindexAllHandler(IAppDbContext db, IIngestionQueue queue)
{
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var documents = await db.Documents
            .Include(d => d.Versions)
            .Where(d => d.DeletedAt == null && d.CurrentVersion > 0)
            .ToListAsync(cancellationToken);

        var versionIds = new List<Guid>(documents.Count);
        foreach (var document in documents)
        {
            var active = document.ActiveVersion!;
            versionIds.Add(document.AddVersion(active.FileName, active.StorageKey, active.ContentHash, active.FileSize).Id);
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var id in versionIds)
            await queue.EnqueueAsync(id, cancellationToken);

        return versionIds.Count;
    }
}
