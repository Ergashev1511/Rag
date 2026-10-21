using Rag.Domain.Common;
using Rag.Domain.Enums;

namespace Rag.Domain.Entities;

public sealed class ChatMessage : BaseEntity
{
    private readonly List<MessageSource> _sources = [];

    private ChatMessage() { }

    private ChatMessage(Guid sessionId, MessageRole role, string content, string? llmModel, int? latencyMs)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Xabar bo'sh bo'lishi mumkin emas.");

        SessionId = sessionId;
        Role = role;
        Content = content;
        LlmModel = llmModel;
        LatencyMs = latencyMs;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid SessionId { get; private init; }
    public MessageRole Role { get; private init; }
    public string Content { get; private init; } = null!;
    public string? LlmModel { get; private init; }
    public int? LatencyMs { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public IReadOnlyCollection<MessageSource> Sources => _sources;

    internal static ChatMessage FromUser(Guid sessionId, string content) =>
        new(sessionId, MessageRole.User, content, null, null);

    internal static ChatMessage FromAssistant(Guid sessionId, string content, string? llmModel, int latencyMs, IEnumerable<MessageSource> sources)
    {
        var message = new ChatMessage(sessionId, MessageRole.Assistant, content, llmModel, latencyMs);
        message._sources.AddRange(sources);
        return message;
    }
}
