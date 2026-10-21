using Rag.Application.Abstractions;
using Rag.Application.Common.Exceptions;

namespace Rag.Application.Documents.Queries;

public sealed class DownloadDocumentHandler(IAppDbContext db, IFileStorage storage)
{
    /// <summary>Faol versiya fayli; hali indekslanmagan bo'lsa oxirgi yuklangan fayl.</summary>
    public async Task<DownloadDto> HandleAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.GetActiveWithVersionsAsync(documentId, cancellationToken);
        var version = document.ActiveVersion ?? document.LatestVersion
            ?? throw new NotFoundException("Hujjat fayli topilmadi.");

        var content = await storage.OpenReadAsync(version.StorageKey, cancellationToken);
        return new DownloadDto(content, version.FileName, PdfFile.ContentType);
    }
}
