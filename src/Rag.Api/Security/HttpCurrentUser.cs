using Rag.Application.Abstractions;

namespace Rag.Api.Security;

/// <summary>Foydalanuvchi dashboard backend idan X-User-Id header orqali keladi. Keyin JWT ga almashtiriladi.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string HeaderName = "X-User-Id";

    public string? UserId
    {
        get
        {
            var value = accessor.HttpContext?.Request.Headers[HeaderName].ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
