using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rag.Application.Abstractions;
using Rag.Application.Common;
using Rag.Application.Common.Exceptions;
using Rag.Application.Options;
using Rag.Domain.Entities;

namespace Rag.Application.Chat.Commands;

public sealed record AskQuestionCommand(Guid? SessionId, string Question);

public sealed class AskQuestionHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IEmbeddingService embeddingService,
    IVectorStore vectorStore,
    ILlmClient llmClient,
    IOptions<RagOptions> options)
{
    private const int MaxQuestionLength = 2000;
    private const int SnippetLength = 300;

    public async Task<AskQuestionResponse> HandleAsync(AskQuestionCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var question = command.Question?.Trim();
        if (string.IsNullOrEmpty(question))
            throw new ValidationException("Savol bo'sh bo'lishi mumkin emas.");
        if (question.Length > MaxQuestionLength)
            throw new ValidationException($"Savol {MaxQuestionLength} belgidan oshmasligi kerak.");

        var retrieval = options.Value.Retrieval;
        var session = await GetOrCreateSessionAsync(command.SessionId, userId, question, cancellationToken);
        var history = await GetHistoryAsync(session.Id, retrieval.HistoryMessages, cancellationToken);

        // Savol LLM xato bersa ham saqlanib qoladi.
        session.AddUserMessage(question);
        await db.SaveChangesAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var embedding = (await embeddingService.EmbedAsync([question], cancellationToken))[0];
        var hits = await vectorStore.SearchAsync(
            new SearchQuery(embedding, question, retrieval.TopK, retrieval.MinScore, retrieval.Hybrid), cancellationToken);

        string answer;
        string? model = null;
        if (hits.Count == 0)
        {
            answer = PromptBuilder.NotFoundMessage(question);
        }
        else
        {
            answer = await llmClient.CompleteAsync(PromptBuilder.Build(hits, history, question), cancellationToken);
            model = llmClient.ModelName;
        }

        var sources = hits
            .Select(h => new MessageSource(h.DocumentId, h.FileName, h.PageNumber, h.SectionTitle, Snippet(h.Content), h.Score))
            .ToList();
        var message = session.AddAssistantMessage(answer, model, (int)stopwatch.ElapsedMilliseconds, sources);
        await db.SaveChangesAsync(cancellationToken);

        return new AskQuestionResponse(session.Id, message.Id, answer, sources.Select(SourceDto.From).ToList());
    }

    private async Task<ChatSession> GetOrCreateSessionAsync(Guid? sessionId, string userId, string question, CancellationToken cancellationToken)
    {
        if (sessionId is { } id)
        {
            return await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken)
                ?? throw new NotFoundException("Sessiya topilmadi.");
        }

        var session = new ChatSession(userId, question);
        db.ChatSessions.Add(session);
        return session;
    }

    private async Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(Guid sessionId, int count, CancellationToken cancellationToken)
    {
        var messages = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    private static string Snippet(string content) =>
        content.Length <= SnippetLength ? content : content[..SnippetLength] + "…";
}
