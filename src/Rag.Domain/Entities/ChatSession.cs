using Rag.Domain.Common;

namespace Rag.Domain.Entities;

public sealed class ChatSession : BaseEntity
{
    private const int MaxTitleLength = 100;
    private readonly List<ChatMessage> _messages = [];

    private ChatSession() { }

    public ChatSession(string userId, string firstQuestion)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("Foydalanuvchi aniqlanmagan.");

        UserId = userId;
        Title = firstQuestion.Length <= MaxTitleLength ? firstQuestion : firstQuestion[..MaxTitleLength];
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string UserId { get; private init; } = null!;
    public string Title { get; private init; } = null!;
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ChatMessage> Messages => _messages;

    public bool BelongsTo(string userId) => UserId == userId;

    public ChatMessage AddUserMessage(string content) =>
        AddMessage(ChatMessage.FromUser(Id, content));

    public ChatMessage AddAssistantMessage(string content, string? llmModel, int latencyMs, IEnumerable<MessageSource> sources) =>
        AddMessage(ChatMessage.FromAssistant(Id, content, llmModel, latencyMs, sources));

    private ChatMessage AddMessage(ChatMessage message)
    {
        _messages.Add(message);
        UpdatedAt = DateTimeOffset.UtcNow;
        return message;
    }
}
