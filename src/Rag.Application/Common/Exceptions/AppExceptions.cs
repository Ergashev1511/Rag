namespace Rag.Application.Common.Exceptions;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

public sealed class ValidationException(string message) : Exception(message);

public sealed class UnauthorizedException(string message) : Exception(message);

/// <summary>Tashqi servis (LLM, embedding) vaqtincha ishlamayapti.</summary>
public sealed class ServiceUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
