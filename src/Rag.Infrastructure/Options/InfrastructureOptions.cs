namespace Rag.Infrastructure.Options;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "bge-m3";
    public int Dimensions { get; init; } = 1024;
    public int TimeoutSeconds { get; init; } = 300;
}

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string BaseUrl { get; init; } = "https://openrouter.ai/api/v1";
    public string? ApiKey { get; init; }
    public string Model { get; init; } = string.Empty;
    public double Temperature { get; init; } = 0.2;
    public int MaxRetries { get; init; } = 2;
    public int TimeoutSeconds { get; init; } = 120;
}

public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; init; } = "localhost:9000";
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string Bucket { get; init; } = "documents";
    public bool UseSsl { get; init; }
}
