using Rag.Application.Abstractions;

namespace Rag.Application.Documents.Queries;

public sealed class GetDocumentHandler(IAppDbContext db)
{
    public async Task<DocumentDetailsDto> HandleAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var d = await db.GetActiveWithVersionsAsync(documentId, cancellationToken);

        return new DocumentDetailsDto(
            d.Id,
            d.Title,
            d.Description,
            d.CurrentVersion,
            d.CreatedAt,
            d.UpdatedAt,
            d.Versions.OrderByDescending(v => v.Version).Select(DocumentVersionDto.From).ToList());
    }
}
