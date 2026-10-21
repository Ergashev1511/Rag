using Rag.Application.Chat.Commands;
using Rag.Application.Chat.Queries;

namespace Rag.Api.Endpoints;

internal static class ChatEndpoints
{
    public const string RateLimitPolicy = "chat";

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat").WithTags("Chat");

        group.MapPost("/", async (AskQuestionCommand command, AskQuestionHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(command, ct)))
            .RequireRateLimiting(RateLimitPolicy);

        group.MapGet("/sessions", async (GetMySessionsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)));

        group.MapGet("/sessions/{id:guid}/messages", async (Guid id, GetSessionMessagesHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, ct)));

        group.MapDelete("/sessions/{id:guid}", async (Guid id, DeleteSessionHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
