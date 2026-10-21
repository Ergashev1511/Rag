using Microsoft.AspNetCore.Mvc;
using Rag.Api.Security;
using Rag.Application.Documents.Commands;
using Rag.Application.Documents.Queries;

namespace Rag.Api.Endpoints;

internal static class DocumentsEndpoints
{
    internal sealed record UpdateDocumentInfoRequest(string Title, string? Description);

    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents");

        group.MapGet("/", async (GetDocumentsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)));

        group.MapGet("/{id:guid}", async (Guid id, GetDocumentHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, ct)));

        group.MapGet("/{id:guid}/status", async (Guid id, GetDocumentStatusHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(id, ct)));

        group.MapGet("/{id:guid}/download", async (Guid id, DownloadDocumentHandler handler, CancellationToken ct) =>
        {
            var file = await handler.HandleAsync(id, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });

        var admin = group.MapGroup(string.Empty).AddEndpointFilter<ApiKeyEndpointFilter>();

        admin.MapPost("/", async (
            IFormFile file,
            [FromForm] string title,
            [FromForm] string? description,
            UploadDocumentHandler handler,
            CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await handler.HandleAsync(new UploadDocumentCommand(stream, file.FileName, title, description), ct);
            return Results.Accepted($"/api/documents/{result.DocumentId}/status", result);
        }).DisableAntiforgery();

        admin.MapPut("/{id:guid}", async (Guid id, UpdateDocumentInfoRequest request, UpdateDocumentInfoHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new UpdateDocumentInfoCommand(id, request.Title, request.Description), ct);
            return Results.Ok();
        });

        admin.MapPut("/{id:guid}/file", async (Guid id, IFormFile file, ReplaceDocumentFileHandler handler, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await handler.HandleAsync(new ReplaceDocumentFileCommand(id, stream, file.FileName), ct);
            return result.Accepted is null
                ? Results.Ok(new { changed = false })
                : Results.Accepted($"/api/documents/{id}/status", result.Accepted);
        }).DisableAntiforgery();

        admin.MapPost("/{id:guid}/retry", async (Guid id, RetryIndexingHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return Results.Accepted($"/api/documents/{id}/status", result);
        });

        admin.MapDelete("/{id:guid}", async (Guid id, DeleteDocumentHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(id, ct);
            return Results.NoContent();
        });

        app.MapPost("/api/admin/reindex", async (ReindexAllHandler handler, CancellationToken ct) =>
                Results.Accepted(value: new { queued = await handler.HandleAsync(ct) }))
            .AddEndpointFilter<ApiKeyEndpointFilter>()
            .WithTags("Admin");

        return app;
    }
}
