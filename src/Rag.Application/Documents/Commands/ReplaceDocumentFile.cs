using Microsoft.Extensions.Options;
using Rag.Application.Abstractions;
using Rag.Application.Options;

namespace Rag.Application.Documents.Commands;

public sealed record ReplaceDocumentFileCommand(Guid DocumentId, Stream Content, string FileName);

/// <summary>Accepted = null bo'lsa fayl o'zgarmagan.</summary>
public sealed record ReplaceDocumentFileResult(VersionAcceptedDto? Accepted);

public sealed class ReplaceDocumentFileHandler(
    IAppDbContext db,
    IFileStorage storage,
    IIngestionQueue queue,
    IOptions<RagOptions> options)
{
    public async Task<ReplaceDocumentFileResult> HandleAsync(ReplaceDocumentFileCommand command, CancellationToken cancellationToken)
    {
        var document = await db.GetActiveWithVersionsAsync(command.DocumentId, cancellationToken);
        var file = await PdfFile.ReadAsync(command.Content, command.FileName, options.Value.Ingestion.MaxFileSizeMb, cancellationToken);

        if (document.LatestVersion?.ContentHash == file.Hash)
            return new ReplaceDocumentFileResult(null);

        var storageKey = PdfFile.StorageKey(document.Id, document.NextVersionNumber, file.FileName);
        await using (var stream = new MemoryStream(file.Content))
            await storage.SaveAsync(storageKey, stream, PdfFile.ContentType, cancellationToken);

        var version = document.AddVersion(file.FileName, storageKey, file.Hash, file.Size);
        await db.SaveChangesAsync(cancellationToken);

        await queue.EnqueueAsync(version.Id, cancellationToken);
        return new ReplaceDocumentFileResult(new VersionAcceptedDto(document.Id, version.Version, version.Status));
    }
}
