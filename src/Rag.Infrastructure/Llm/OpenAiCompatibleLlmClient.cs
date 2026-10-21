using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rag.Application.Abstractions;
using Rag.Application.Common.Exceptions;
using Rag.Infrastructure.Options;

namespace Rag.Infrastructure.Llm;

/// <summary>OpenAI-compatible /chat/completions: OpenRouter, Ollama (/v1), vLLM va h.k.</summary>
internal sealed class OpenAiCompatibleLlmClient(
    HttpClient http,
    IOptions<LlmOptions> options,
    ILogger<OpenAiCompatibleLlmClient> logger) : ILlmClient
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private sealed record RequestMessage(string Role, string Content);

    private sealed record CompletionRequest(string Model, IReadOnlyList<RequestMessage> Messages, double Temperature);

    private sealed record CompletionResponse(IReadOnlyList<Choice>? Choices, ErrorInfo? Error);

    private sealed record Choice(RequestMessage? Message);

    private sealed record ErrorInfo(string? Message);

    public string ModelName => options.Value.Model;

    public async Task<string> CompleteAsync(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken)
    {
        var request = new CompletionRequest(
            ModelName,
            messages.Select(m => new RequestMessage(m.Role.ToString().ToLowerInvariant(), m.Content)).ToList(),
            options.Value.Temperature);

        var maxRetries = options.Value.MaxRetries;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var response = await http.PostAsJsonAsync("chat/completions", request, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return await ReadAnswerAsync(response, cancellationToken);

                // OpenRouter sababini body da qaytaradi (noto'g'ri kalit, model topilmadi, limit va h.k.).
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("LLM {Status} qaytardi (model: {Model}, urinish {Attempt}): {Error}",
                    (int)response.StatusCode, ModelName, attempt + 1, error);

                if (!IsTransient(response.StatusCode) || attempt >= maxRetries)
                    throw new ServiceUnavailableException($"LLM xato qaytardi: {(int)response.StatusCode}.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                if (attempt >= maxRetries)
                    throw new ServiceUnavailableException("LLM servisiga ulanib bo'lmadi. Keyinroq urinib ko'ring.", ex);

                logger.LogWarning(ex, "LLM so'rovi xato, qayta urinish {Attempt}", attempt + 1);
            }

            await Task.Delay(RetryDelay * (attempt + 1), cancellationToken);
        }
    }

    private async Task<string> ReadAnswerAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var completion = await response.Content.ReadFromJsonAsync<CompletionResponse>(cancellationToken);
        var answer = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (!string.IsNullOrWhiteSpace(answer))
            return answer.Trim();

        // OpenRouter ba'zan 200 bilan "error" obyektini qaytaradi (provayder xatosi).
        logger.LogWarning("LLM bo'sh javob qaytardi (model: {Model}): {Error}", ModelName, completion?.Error?.Message);
        throw new ServiceUnavailableException("LLM bo'sh javob qaytardi.");
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)status >= 500;
}
