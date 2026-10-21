using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Rag.Api.Security;

internal sealed class ApiKeyEndpointFilter(IOptions<SecurityOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = options.Value.AdminApiKey;
        if (string.IsNullOrEmpty(expected))
            return await next(context);

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        var valid = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));

        return valid
            ? await next(context)
            : Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "X-Api-Key noto'g'ri yoki yo'q.");
    }
}
