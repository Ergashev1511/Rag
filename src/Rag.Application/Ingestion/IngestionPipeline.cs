using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rag.Application.Abstractions;
using Rag.Application.Options;
using Rag.Domain.Entities;
using Rag.Domain.Enums;

namespace Rag.Application.Ingestion;

/// <summary>Versiyani indekslaydi: PDF → sahifalar → chunk → embedding → DB (atomik almashtirish).</summary>
public sealed class IngestionPipeline(
    IAppDbContext db,
    IFileStorage storage,
    IPdfTextExtractor extractor,
    IChunker chunker,
    IEmbeddingService embeddingService,
    IIngestionQueue queue,
    IOptions<RagOptions> options,
    ILogger<IngestionPipeline> logger)
{
    private static readonly TimeSpan RetryDelayStep = TimeSpan.FromSeconds(30);

    private sealed record EmbeddedChunk(TextChunk Chunk, float[] Embedding);

    public async Task ProcessAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var (document, version) = await LoadAsync(versionId, cancellationToken);
        if (document is null || version is null || document.IsDeleted
            || version.Status is not (VersionStatus.Pending or VersionStatus.Processing))
        {
            logger.LogInformation("Versiya {VersionId} indekslanmaydi (topilmadi, o'chirilgan yoki tayyor)", versionId);
            return;
        }

        version.StartProcessing();
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var (pageCount, chunks) = await BuildChunksAsync(version.StorageKey, cancellationToken);
            await SaveAsync(versionId, pageCount, chunks, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Processing holatida qoladi, server qayta ishga tushganda navbatga qo'yiladi.
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(versionId, ex, cancellationToken);
        }
    }

    private async Task<(int PageCount, List<EmbeddedChunk> Chunks)> BuildChunksAsync(string storageKey, CancellationToken cancellationToken)
    {
        IReadOnlyList<PdfPage> pages;
        await using (var pdf = await storage.OpenReadAsync(storageKey, cancellationToken))
            pages = extractor.Extract(pdf);

        if (pages.All(p => p.Lines.Count == 0))
            throw new IngestionException("PDF dan matn chiqmadi (skaner PDF bo'lishi mumkin). OCR kerak.");

        var chunks = chunker.Chunk(pages);
        if (chunks.Count == 0)
            throw new IngestionException("PDF dan chunk hosil bo'lmadi.");

        var batchSize = Math.Max(1, options.Value.Ingestion.EmbeddingBatchSize);
        var result = new List<EmbeddedChunk>(chunks.Count);

        foreach (var batch in chunks.Chunk(batchSize))
        {
            // Sarlavha embedding kontekstiga qo'shiladi: "Response" chunk i qaysi API ga tegishli ekanini o'zida tashiydi.
            var texts = batch.Select(c => string.IsNullOrEmpty(c.SectionTitle) ? c.Content : $"{c.SectionTitle}\n{c.Content}").ToList();
            var embeddings = await embeddingService.EmbedAsync(texts, cancellationToken);
            result.AddRange(batch.Zip(embeddings, (chunk, embedding) => new EmbeddedChunk(chunk, embedding)));
        }

        return (pages.Count, result);
    }

    private async Task SaveAsync(Guid versionId, int pageCount, List<EmbeddedChunk> chunks, CancellationToken cancellationToken)
    {
        // Indekslash davomida hujjat o'chirilgan yoki yangilangan bo'lishi mumkin — yangidan o'qiymiz.
        db.ChangeTracker.Clear();
        var (document, version) = await LoadAsync(versionId, cancellationToken);
        if (document is null || version is null)
            return;

        if (document.IsDeleted)
        {
            version.MarkFailed("Indekslash davomida hujjat o'chirildi.");
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (version.Version <= document.CurrentVersion)
        {
            version.MarkFailed("Yangiroq versiya allaqachon faol.");
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            db.DocumentChunks.AddRange(chunks.Select((c, index) => new DocumentChunk(
                document.Id,
                version.Version,
                index,
                c.Chunk.PageNumber,
                c.Chunk.SectionTitle,
                c.Chunk.Type,
                c.Chunk.Content,
                c.Embedding,
                embeddingService.ModelName)));

            document.Activate(version.Version);
            version.MarkReady(pageCount, chunks.Count);
            await db.SaveChangesAsync(cancellationToken);

            await db.DocumentChunks
                .Where(c => c.DocumentId == document.Id && c.DocumentVersion < version.Version)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        logger.LogInformation("Hujjat {DocumentId} v{Version} indekslandi: {Pages} sahifa, {Chunks} chunk",
            document.Id, version.Version, pageCount, chunks.Count);

        await DeleteObsoleteFilesAsync(document, version, cancellationToken);
    }

    private async Task HandleFailureAsync(Guid versionId, Exception exception, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var version = await db.DocumentVersions.FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (version is null)
            return;

        var canRetry = exception is not IngestionException && version.Attempts < options.Value.Ingestion.MaxRetries;
        if (canRetry)
            version.ScheduleRetry(exception.Message);
        else
            version.MarkFailed(exception.Message);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogError(exception, "Versiya {VersionId} indekslashda xato (urinish {Attempt}, qayta urinish: {Retry})",
            versionId, version.Attempts, canRetry);

        if (canRetry)
            queue.EnqueueDelayed(versionId, RetryDelayStep * version.Attempts);
    }

    private async Task DeleteObsoleteFilesAsync(Document document, DocumentVersion active, CancellationToken cancellationToken)
    {
        var obsoleteKeys = document.Versions
            .Where(v => v.Version < active.Version && v.StorageKey != active.StorageKey)
            .Select(v => v.StorageKey)
            .Distinct();

        foreach (var key in obsoleteKeys)
        {
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Eski versiya fayli o'chirilmadi: {StorageKey}", key);
            }
        }
    }

    private async Task<(Document? Document, DocumentVersion? Version)> LoadAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Versions.Any(v => v.Id == versionId), cancellationToken);

        return (document, document?.Versions.First(v => v.Id == versionId));
    }
}
