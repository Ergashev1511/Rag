using Rag.Domain.Entities;
using Rag.Domain.Enums;

namespace Rag.Application.Chat;

public sealed record SourceDto(Guid DocumentId, string FileName, int PageNumber, string? SectionTitle, string Snippet, float Score)
{
    public static SourceDto From(MessageSource s) => new(s.DocumentId, s.FileName, s.PageNumber, s.SectionTitle, s.Snippet, s.Score);
}

public sealed record AskQuestionResponse(Guid SessionId, Guid MessageId, string Answer, IReadOnlyList<SourceDto> Sources);

public sealed record ChatSessionDto(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record ChatMessageDto(Guid Id, MessageRole Role, string Content, DateTimeOffset CreatedAt, IReadOnlyList<SourceDto> Sources);
