namespace Rag.Application.Abstractions;

public enum LlmRole
{
    System,
    User,
    Assistant
}

public sealed record LlmMessage(LlmRole Role, string Content);

public interface ILlmClient
{
    string ModelName { get; }
    Task<string> CompleteAsync(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken);
}
