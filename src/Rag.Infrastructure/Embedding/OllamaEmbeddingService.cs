using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Rag.Application.Abstractions;
using Rag.Infrastructure.Options;

namespace Rag.Infrastructure.Embedding;

/// <summary>Ollama /api/embed (batch).</summary>
internal sealed class OllamaEmbeddingService(HttpClient http, IOptions<EmbeddingOptions> options) : IEmbeddingService
{
    private sealed record EmbedRequest(string Model, IReadOnlyList<string> Input);

    private sealed record EmbedResponse(float[][] Embeddings);

    public string ModelName => options.Value.Model;

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        var request = new EmbedRequest(ModelName, texts);
        using var response = await http.PostAsJsonAsync("api/embed", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Ollama xato matnini qaytaradi, masalan: {"error":"model \"bge-m3\" not found, try pulling it first"}.
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Ollama embedding xatosi {(int)response.StatusCode} (model: {ModelName}): {error}", null, response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken);
        var embeddings = result?.Embeddings ?? [];

        if (embeddings.Length != texts.Count)
            throw new InvalidOperationException($"Embedding soni mos emas: {embeddings.Length} != {texts.Count}.");

        var dimensions = options.Value.Dimensions;
        if (embeddings.Any(e => e.Length != dimensions))
            throw new InvalidOperationException($"Embedding o'lchami {dimensions} emas. Model: {ModelName}.");

        return embeddings;
    }
}
