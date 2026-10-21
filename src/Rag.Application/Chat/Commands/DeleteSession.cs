using Microsoft.EntityFrameworkCore;
using Rag.Application.Abstractions;
using Rag.Application.Common;
using Rag.Application.Common.Exceptions;

namespace Rag.Application.Chat.Commands;

public sealed class DeleteSessionHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task HandleAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var deleted = await db.ChatSessions
            .Where(s => s.Id == sessionId && s.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
            throw new NotFoundException("Sessiya topilmadi.");
    }
}
