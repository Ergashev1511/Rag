using Rag.Application.Abstractions;
using Rag.Application.Common.Exceptions;

namespace Rag.Application.Common;

public static class CurrentUserExtensions
{
    public static string RequireUserId(this ICurrentUser currentUser) =>
        string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new UnauthorizedException("Foydalanuvchi aniqlanmadi (X-User-Id header).")
            : currentUser.UserId;
}
