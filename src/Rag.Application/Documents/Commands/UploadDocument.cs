using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rag.Application.Abstractions;
using Rag.Application.Common.Exceptions;
using Rag.Application.Options;
using Rag.Domain.Entities;

namespace Rag.Application.Documents.Commands;

public sealed record UploadDocumentCommand(Stream Content, string FileName, string Title, string? Description);

public sealed class UploadDocumentHandler(
    IAppDbContext db,
    IFileStorage storage,
    IIngestionQueue queue,
    IOptions<RagOptions> options)
{
    public async Task<VersionAcceptedDto> HandleAsync(UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        var file = await PdfFile.ReadAsync(command.Content, command.FileName, options.Value.Ingestion.MaxFileSizeMb, cancellationToken);

        var duplicate = await db.Documents.AnyAsync(
            d => d.DeletedAt == null && d.Versions.Any(v => v.ContentHash == file.Hash), cancellationToken);
        if (duplicate)
            throw new ConflictException("Bu fayl allaqachon yuklangan.");

        var document = new Document(command.Title, command.Description);
        var storageKey = PdfFile.StorageKey(document.Id, document.NextVersionNumber, file.FileName);

        await using (var stream = new MemoryStream(file.Content))
            await storage.SaveAsync(storageKey, stream, PdfFile.ContentType, cancellationToken);

        var version = document.AddVersion(file.FileName, storageKey, file.Hash, file.Size);
        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        await queue.EnqueueAsync(version.Id, cancellationToken);
        return new VersionAcceptedDto(document.Id, version.Version, version.Status);
    }
}
