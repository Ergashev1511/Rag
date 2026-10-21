using Microsoft.EntityFrameworkCore;
using Rag.Application.Abstractions;
using Rag.Application.Common.Exceptions;

namespace Rag.Application.Documents.Commands;

public sealed record UpdateDocumentInfoCommand(Guid DocumentId, string Title, string? Description);

public sealed class UpdateDocumentInfoHandler(IAppDbContext db)
{
    public async Task HandleAsync(UpdateDocumentInfoCommand command, CancellationToken cancellationToken)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == command.DocumentId && d.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("Hujjat topilmadi.");

        document.SetInfo(command.Title, command.Description);
        await db.SaveChangesAsync(cancellationToken);
    }
}
