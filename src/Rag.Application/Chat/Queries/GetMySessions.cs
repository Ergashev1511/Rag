using Microsoft.EntityFrameworkCore;
using Rag.Application.Abstractions;
using Rag.Application.Common;

namespace Rag.Application.Chat.Queries;

public sealed class GetMySessionsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<ChatSessionDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        return await db.ChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new ChatSessionDto(s.Id, s.Title, s.CreatedAt, s.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}
