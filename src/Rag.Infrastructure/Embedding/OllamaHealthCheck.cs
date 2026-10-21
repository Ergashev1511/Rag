using System.Net.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Rag.Infrastructure.Options;

namespace Rag.Infrastructure.Embedding;

/// <summary>Ollama ishlayapti va embedding modeli yuklangan (pull qilingan).</summary>
internal sealed class OllamaHealthCheck(HttpClient http, IOptions<EmbeddingOptions> options) : IHealthCheck
{
    private sealed record TagsResponse(IReadOnlyList<ModelInfo>? Models);

    private sealed record ModelInfo(string Name);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync("api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return HealthCheckResult.Unhealthy($"Ollama javobi: {(int)response.StatusCode}.");

            var tags = await response.Content.ReadFromJsonAsync<TagsResponse>(cancellationToken);
            var model = options.Value.Model;
            return tags?.Models?.Any(m => IsSameModel(m.Name, model)) == true
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Embedding modeli '{model}' Ollama da yo'q: ollama pull {model}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Ollama ga ulanib bo'lmadi.", ex);
        }
    }

    // "bge-m3" va "bge-m3:latest" — bitta model.
    private static bool IsSameModel(string name, string model) =>
        string.Equals(name, model, StringComparison.OrdinalIgnoreCase)
        || (!model.Contains(':') && string.Equals(name, model + ":latest", StringComparison.OrdinalIgnoreCase));
}
