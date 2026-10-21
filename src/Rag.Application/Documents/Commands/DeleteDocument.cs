using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rag.Application.Abstractions;

namespace Rag.Application.Documents.Commands;

public sealed class DeleteDocumentHandler(IAppDbContext db, IFileStorage storage, ILogger<DeleteDocumentHandler> logger)
{
    public async Task HandleAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.GetActiveWithVersionsAsync(documentId, cancellationToken);

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.DocumentChunks.Where(c => c.DocumentId == documentId).ExecuteDeleteAsync(cancellationToken);
            document.Delete();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // Fayl o'chmasa DB bekor qilinmaydi: yetim fayl keyin tozalanadi.
        foreach (var key in document.Versions.Select(v => v.StorageKey).Distinct())
        {
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Fayl o'chirilmadi: {StorageKey}", key);
            }
        }
    }
}
