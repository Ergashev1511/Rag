using Microsoft.EntityFrameworkCore;
using Rag.Application.Abstractions;
using Rag.Application.Common;
using Rag.Application.Common.Exceptions;

namespace Rag.Application.Chat.Queries;

public sealed class GetSessionMessagesHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<ChatMessageDto>> HandleAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var exists = await db.ChatSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Sessiya topilmadi.");

        var messages = await db.ChatMessages
            .AsNoTracking()
            .Include(m => m.Sources)
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return messages
            .Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.CreatedAt, m.Sources.Select(SourceDto.From).ToList()))
            .ToList();
    }
}
