using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using Rag.Application.Abstractions;
using Rag.Infrastructure.Background;
using Rag.Infrastructure.Embedding;
using Rag.Infrastructure.Llm;
using Rag.Infrastructure.Options;
using Rag.Infrastructure.Pdf;
using Rag.Infrastructure.Persistence;
using Rag.Infrastructure.Storage;
using Rag.Infrastructure.VectorStore;

namespace Rag.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Db")
            ?? throw new InvalidOperationException("ConnectionStrings:Db sozlanmagan.");

        services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.SectionName));
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<MinioOptions>(configuration.GetSection(MinioOptions.SectionName));

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IVectorStore, PgVectorStore>();

        services.AddSingleton<IMinioClient>(sp =>
        {
            var minio = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            return new MinioClient()
                .WithEndpoint(minio.Endpoint)
                .WithCredentials(minio.AccessKey, minio.SecretKey)
                .WithSSL(minio.UseSsl)
                .Build();
        });
        services.AddScoped<IFileStorage, MinioFileStorage>();

        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddSingleton<IChunker, StructureAwareChunker>();

        services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(ConfigureOllama);
        services.AddHttpClient<OllamaHealthCheck>(ConfigureOllama);
        services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>((sp, http) =>
        {
            var llm = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            http.BaseAddress = new Uri(llm.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(llm.TimeoutSeconds);
            if (!string.IsNullOrWhiteSpace(llm.ApiKey))
                http.DefaultRequestHeaders.Authorization = new("Bearer", llm.ApiKey);
        });

        services.AddSingleton<ChannelIngestionQueue>();
        services.AddSingleton<IIngestionQueue>(sp => sp.GetRequiredService<ChannelIngestionQueue>());
        services.AddHostedService<IngestionWorker>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres")
            .AddCheck<MinioHealthCheck>("minio")
            .AddCheck<OllamaHealthCheck>("ollama");

        return services;
    }

    private static void ConfigureOllama(IServiceProvider sp, HttpClient http)
    {
        var embedding = sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
        http.BaseAddress = new Uri(embedding.BaseUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(embedding.TimeoutSeconds);
    }
}
